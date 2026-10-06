using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.AiTools;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.AiEvaluations;

public sealed record BudgetLimits(int MaxCalls, long MaxTokens, decimal MaxEstimatedCost);
public sealed record BudgetReservation(int InputTokens, int OutputTokens, decimal Cost);

public sealed class EvaluationBudget
{
    private readonly BudgetLimits _limits;
    private readonly decimal _inputPrice;
    private readonly decimal _outputPrice;
    public int Calls { get; private set; }
    public long Tokens { get; private set; }
    public decimal Cost { get; private set; }
    public bool Stopped { get; private set; }

    public EvaluationBudget(BudgetLimits limits, decimal inputPrice, decimal outputPrice)
    {
        if (limits.MaxCalls is < 1 or > 10000 || limits.MaxTokens is < 1 or > 100_000_000 ||
            limits.MaxEstimatedCost is <= 0 or > 1_000_000 || inputPrice is <= 0 or > 1_000_000 || outputPrice is <= 0 or > 1_000_000)
            throw new EvaluationInputException("Explicit positive call, token, cost limits and token prices are required.");
        _limits = limits;
        _inputPrice = inputPrice;
        _outputPrice = outputPrice;
    }

    public BudgetReservation Reserve(AiModelGatewayRequest request)
    {
        // Byte-based input estimate includes tool schemas and framing; it is not a billing upper bound.
        var input = checked(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new { request.Messages, request.Tools })) + 512);
        var output = request.MaxTokens ?? throw new EvaluationInputException("maxTokens is required.");
        if (output is < 1 or > 32768) throw new EvaluationInputException("maxTokens is out of range.");
        var cost = input * _inputPrice / 1_000_000m + output * _outputPrice / 1_000_000m;
        if (Stopped || Calls + 1 > _limits.MaxCalls || Tokens + input + output > _limits.MaxTokens || Cost + cost > _limits.MaxEstimatedCost)
        {
            Stopped = true;
            throw new AiModelGatewayException("evaluation_budget_exceeded", ErrorCode.TooManyRequests,
                "Evaluation budget exhausted.", false);
        }
        Calls++;
        Tokens += input + output;
        Cost += cost;
        return new(input, output, cost);
    }

    public decimal Settle(BudgetReservation reservation, AiModelGatewayResponse? response)
    {
        var input = Math.Max((long)reservation.InputTokens, response?.InputTokens is >= 0 ? response.InputTokens.Value : 0);
        var output = Math.Max((long)reservation.OutputTokens, response?.OutputTokens is >= 0 ? response.OutputTokens.Value : 0);
        var cost = Math.Max(reservation.Cost, input * _inputPrice / 1_000_000m + output * _outputPrice / 1_000_000m);
        Tokens += input + output - reservation.InputTokens - reservation.OutputTokens;
        Cost += cost - reservation.Cost;
        Stopped |= Tokens > _limits.MaxTokens || Cost > _limits.MaxEstimatedCost;
        return cost;
    }
}

public sealed class LiveEvaluationSettings
{
    public required string ProviderAlias { get; init; }
    public required string BaseUrl { get; init; }
    public string ChatCompletionsPath { get; init; } = "v1/chat/completions";
    public required string Model { get; init; }
    public decimal Temperature { get; init; }
    public int MaxTokens { get; init; } = 2048;
    public int TimeoutSeconds { get; init; } = 30;
    public bool AllowPrivateNetwork { get; init; }
    public string[] AllowedHosts { get; init; } = [];
    public required string Currency { get; init; }
    public decimal InputPricePerMillion { get; init; }
    public decimal OutputPricePerMillion { get; init; }
    public required BudgetLimits Budget { get; init; }
    public DateTimeOffset? ComplianceConfirmedAt { get; init; }
    public string ComplianceReference { get; init; } = "";

    public void Validate()
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(ProviderAlias, "^[a-z0-9-]{1,50}$") ||
            string.IsNullOrWhiteSpace(Model) || Model.Length > 100 || MaxTokens is < 1 or > 32768 ||
            TimeoutSeconds is < 1 or > 90 || Temperature is < 0 or > 2 ||
            !System.Text.RegularExpressions.Regex.IsMatch(Currency, "^[A-Z]{3}$") || AllowedHosts.Length == 0 ||
            !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            ComplianceConfirmedAt is null || ComplianceConfirmedAt <= DateTimeOffset.MinValue || ComplianceConfirmedAt > DateTimeOffset.UtcNow ||
            !System.Text.RegularExpressions.Regex.IsMatch(ComplianceReference, "^[a-z0-9-]{1,100}$"))
            throw new EvaluationInputException("Invalid live model, currency or HTTPS host policy.");
        _ = new EvaluationBudget(Budget, InputPricePerMillion, OutputPricePerMillion);
    }
}

internal sealed class ScriptedGateway : IAiModelGateway
{
    private readonly Queue<AiModelGatewayResponse> _responses = new();
    private string? _error;

    public void Prepare(EvaluationStep step)
    {
        _responses.Clear();
        _error = step.GatewayError;
        if (step.Calls.Count > 0)
            _responses.Enqueue(new()
            {
                ToolCalls = step.Calls.Select((call, index) => new AiModelToolCall
                {
                    Id = $"eval-call-{index + 1}", Name = call.Name, ArgumentsJson = call.Arguments.GetRawText()
                }).ToArray(), Model = "scripted", InputTokens = step.MissingUsage ? null : 100, OutputTokens = step.MissingUsage ? null : 30
            });
        _responses.Enqueue(new()
        {
            Content = step.Answer, Model = "scripted", InputTokens = step.MissingUsage ? null : 100, OutputTokens = step.MissingUsage ? null : 30
        });
    }

    public Task<AiModelGatewayResponse> CompleteAsync(AiProviderConnectionSettings provider, AiModelGatewayRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_error is not null)
            throw new AiModelGatewayException(_error, ErrorCode.InternalServerError, "Injected supplier failure.", true);
        if (_responses.Count == 0)
            throw new AiModelGatewayException("evaluation_script_exhausted", ErrorCode.InternalServerError, "Offline script exhausted.", false);
        return Task.FromResult(_responses.Dequeue());
    }
}

internal sealed class RecordingGateway(IAiModelGateway inner, EvaluationBudget budget,
    Func<IReadOnlyList<AiToolDefinition>> catalog) : IAiModelGateway
{
    public List<ModelObservation> Observations { get; } = [];

    public async Task<AiModelGatewayResponse> CompleteAsync(AiProviderConnectionSettings provider, AiModelGatewayRequest request, CancellationToken cancellationToken = default)
    {
        var reservation = budget.Reserve(request);
        var watch = Stopwatch.StartNew();
        AiModelGatewayResponse? response = null;
        string? error = null;
        try
        {
            response = await inner.CompleteAsync(provider, request, cancellationToken);
            return response;
        }
        catch (AiModelGatewayException exception) { error = exception.ErrorType; throw; }
        catch (OperationCanceledException) { error = "cancelled"; throw; }
        catch (Exception) { error = "evaluation_gateway_failed"; throw; }
        finally
        {
            var cost = budget.Settle(reservation, response);
            var definitions = catalog().ToDictionary(item => item.FunctionName, StringComparer.Ordinal);
            var snapshots = request.Tools.Select(tool =>
            {
                definitions.TryGetValue(tool.Name, out var definition);
                return new ToolSnapshot(tool.Name, definition?.ToolCode ?? "unregistered", definition?.Version ?? "unknown",
                    EvaluationJson.Digest(tool.Description), EvaluationJson.Digest(tool.ParametersJson),
                    EvaluationJson.Digest(definition?.OutputSchemaJson ?? ""));
            }).ToArray();
            Observations.Add(new(provider.ModelName, response?.Model,
                EvaluationJson.Digest(string.Join("\n", request.Messages.Where(m => m.Role == "system").Select(m => m.Content))),
                snapshots, request.Temperature, request.MaxTokens, provider.TimeoutSeconds,
                response?.ToolCalls.Select(c => new ProposedCall(c.Name, JsonSerializer.Deserialize<JsonElement>(c.ArgumentsJson))).ToArray() ?? [],
                response?.Content, error, response?.InputTokens, response?.OutputTokens,
                reservation.InputTokens + reservation.OutputTokens, cost,
                response?.InputTokens is not >= 0 || response?.OutputTokens is not >= 0, watch.ElapsedMilliseconds));
        }
    }
}

internal sealed class RecordingToolRegistry(IAiReadOnlyToolRegistry inner) : IAiReadOnlyToolRegistry
{
    public List<ToolObservation> Observations { get; } = [];
    public IReadOnlyList<AiToolDefinition> GetAvailableTools() => inner.GetAvailableTools();

    public async Task<AiToolExecutionResult> ExecuteAsync(string toolCode, string argumentsJson, CancellationToken cancellationToken = default)
    {
        var arguments = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        try
        {
            var result = await inner.ExecuteAsync(toolCode, argumentsJson, cancellationToken);
            Observations.Add(new(toolCode, arguments, "Completed",
                result.StructuredResult is null ? null : EvaluationJson.Element(result.StructuredResult.Context.Parameters), result.StructuredResult));
            return result;
        }
        catch (Exception)
        {
            Observations.Add(new(toolCode, arguments, "Failed", null));
            throw;
        }
    }
}
