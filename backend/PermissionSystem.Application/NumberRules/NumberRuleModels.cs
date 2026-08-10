using PermissionSystem.Shared.Pagination;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.NumberRules;

public sealed class NumberRuleQueryRequest : PaginationRequest
{
    public string? Keyword { get; init; }

    public string? BusinessType { get; init; }

    public bool? IsEnabled { get; init; }
}

public sealed class CreateOrUpdateNumberRuleRequest
{
    public byte[]? ConcurrencyToken { get; init; }

    public string RuleCode { get; init; } = string.Empty;

    public string RuleName { get; init; } = string.Empty;

    public string BusinessType { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public string DateFormat { get; init; } = "yyyyMMdd";

    public int SequenceLength { get; init; } = 4;

    public string ResetCycle { get; init; } = "Daily";

    public string Separator { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;

    public string? Remark { get; init; }
}

public sealed class NumberRuleResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string RuleCode { get; init; } = string.Empty;

    public string RuleName { get; init; } = string.Empty;

    public string BusinessType { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public string DateFormat { get; init; } = string.Empty;

    public int SequenceLength { get; init; }

    public string ResetCycle { get; init; } = string.Empty;

    public string Separator { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public string? Remark { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public byte[] ConcurrencyToken { get; init; } = [];
}

public sealed class NumberRulePreviewResponse
{
    public string Number { get; init; } = string.Empty;

    public string Pattern { get; init; } = string.Empty;
}

public sealed class NumberGenerateResponse
{
    public string RuleCode { get; init; } = string.Empty;

    public string Number { get; init; } = string.Empty;
}

public interface INumberRuleService
{
    Task<PagedResult<NumberRuleResponse>> GetPagedAsync(
        NumberRuleQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<NumberRuleResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<NumberRuleResponse> CreateAsync(
        CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<NumberRuleResponse> UpdateAsync(
        Guid id,
        CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task EnableAsync(Guid id, CancellationToken cancellationToken = default);

    Task DisableAsync(Guid id, CancellationToken cancellationToken = default);

    Task<NumberRulePreviewResponse> PreviewAsync(
        CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken = default);

    Task ResetSequenceAsync(string ruleCode, CancellationToken cancellationToken = default);
}

public interface INumberGenerator
{
    Task<string> GenerateAsync(string ruleCode, CancellationToken cancellationToken = default);

    Task<string> GenerateAsync(
        string ruleCode,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default);
}
