using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Ai;

namespace PermissionSystem.AiEvaluations;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = Parse(args);
            var command = options.GetValueOrDefault("command", "run");
            var outputRoot = Path.GetFullPath(options.GetValueOrDefault("output", "artifacts/ai-evaluations"));
            if (command == "compare") return Compare(options, outputRoot);
            if (command == "review-template") return ReviewTemplate(options, outputRoot);
            if (command == "snapshot-template")
            {
                await using var fixture = await IsolatedEvaluationEnvironment.CreateAsync(new(), new EvaluationBudget(new(1000, 100_000_000, 1000), 1, 1));
                var directory = NewDirectory(outputRoot);
                WriteNew(Path.Combine(directory, "snapshot.json"), AiScenarioSnapshots.Json(fixture.CandidateSnapshot()));
                Console.WriteLine($"Synthetic candidate template (unpublished): {Path.Combine(directory, "snapshot.json")}");
                return 0;
            }
            if (command != "run") throw new EvaluationInputException("Unsupported command.");
            var root = Path.GetFullPath(options.GetValueOrDefault("root", Directory.GetCurrentDirectory()));
            var suite = Path.GetFullPath(options.GetValueOrDefault("suite", Path.Combine(root, "evaluations/ai-center/cases.json")));
            var mode = options.GetValueOrDefault("mode", "offline");
            if (mode is not ("offline" or "live")) throw new EvaluationInputException("Mode must be offline or live.");
            if (mode == "offline" && options.ContainsKey("live-config")) throw new EvaluationInputException("Live configuration requires explicit live mode.");
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += handler;
            try
            {
                LiveEvaluationSettings? live = null;
                string? apiKey = null;
                await using var httpServices = CreateHttpServices();
                IAiModelGateway? gateway = null;
                if (mode == "live")
                {
                    live = EvaluationFiles.Read<LiveEvaluationSettings>(Required(options, "live-config"));
                    live.Validate();
                    apiKey = Environment.GetEnvironmentVariable("AIC004_API_KEY");
                    if (string.IsNullOrWhiteSpace(apiKey)) throw new EvaluationInputException("AIC004_API_KEY process environment is required.");
                    new AiProviderConnectionTester(httpServices.GetRequiredService<IHttpClientFactory>()).Validate(new()
                    {
                        BaseUrl = live.BaseUrl, ChatCompletionsPath = live.ChatCompletionsPath, ModelName = live.Model,
                        ApiKey = apiKey, AllowedHosts = live.AllowedHosts, AllowPrivateNetwork = live.AllowPrivateNetwork, TimeoutSeconds = live.TimeoutSeconds
                    });
                    gateway = new OpenAiCompatibleModelGateway(httpServices.GetRequiredService<IHttpClientFactory>());
                }
                var snapshot = options.TryGetValue("snapshot", out var snapshotPath) ? EvaluationFiles.Read<AiScenarioSnapshot>(snapshotPath) : null;
                var report = await EvaluationRunner.RunAsync(root, suite, gateway, live, apiKey, cancellation.Token, snapshot);
                var directory = NewDirectory(outputRoot);
                var json = EvaluationRedactor.Sanitize(report, apiKey).Json;
                report = JsonSerializer.Deserialize<EvaluationReport>(json, EvaluationJson.Options)!;
                var hash = EvaluationJson.Digest(json);
                var review = ReadReview(options, "review");
                var gate = EvaluationGate.Evaluate(report, hash, review);
                var baselinePath = options.GetValueOrDefault("baseline");
                ComparisonResult? comparison = null;
                if (baselinePath is not null)
                {
                    var baseline = EvaluationFiles.Read<EvaluationReport>(baselinePath);
                    comparison = EvaluationGate.Compare(baseline, report, hash, review, ReadReview(options, "baseline-review"), EvaluationJson.Digest(File.ReadAllText(baselinePath)));
                }
                gate = ReleaseGate(gate, comparison, baselinePath is not null && BaselineReviewed(options, baselinePath));
                if (comparison is not null)
                {
                    comparison = comparison with { CandidateGate = gate };
                    WriteNew(Path.Combine(directory, "comparison.json"), EvaluationRedactor.Sanitize(comparison, apiKey).Json);
                }
                WriteNew(Path.Combine(directory, "report.json"), json);
                WriteNew(Path.Combine(directory, "report.md"), Markdown(report, gate, comparison));
                WriteNew(Path.Combine(directory, "report.sha256"), hash + "\n");
                Console.WriteLine($"Report: {Path.Combine(directory, "report.json")}");
                Console.WriteLine($"Deterministic checks: {(report.AutomaticChecksPassed ? "passed" : "failed/incomplete")}; release gate: {(gate.Passed ? "passed" : "pending/failed")}; {report.Results.Count} variants.");
                if (!EvaluationGate.Automatic(report).Passed || cancellation.IsCancellationRequested || comparison is { NoRegression: false }) return 1;
                if (options.ContainsKey("require-release-gate") && !gate.Passed) return 2;
                return 0;
            }
            finally { Console.CancelKeyPress -= handler; }
        }
        catch (EvaluationInputException exception) { Console.Error.WriteLine(exception.Message); return 2; }
        catch (Exception) { Console.Error.WriteLine("Evaluation failed. Check input schema, file access and build configuration; sensitive exception details were not emitted."); return 2; }
    }

    private static ServiceProvider CreateHttpServices()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("AiModelGateway", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(AiHttpTransport.CreateHandler);
        services.AddHttpClient("AiProviderConnectionTest", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(AiHttpTransport.CreateHandler);
        return services.BuildServiceProvider();
    }

    private static int Compare(Dictionary<string, string> options, string outputRoot)
    {
        var baselinePath = Required(options, "baseline");
        var candidatePath = Required(options, "candidate");
        var baseline = EvaluationFiles.Read<EvaluationReport>(baselinePath);
        var candidate = EvaluationFiles.Read<EvaluationReport>(candidatePath);
        var comparison = EvaluationGate.Compare(baseline, candidate, EvaluationJson.Digest(File.ReadAllText(candidatePath)), ReadReview(options, "review"),
            ReadReview(options, "baseline-review"), EvaluationJson.Digest(File.ReadAllText(baselinePath)));
        comparison = comparison with { CandidateGate = ReleaseGate(comparison.CandidateGate, comparison, BaselineReviewed(options, baselinePath)) };
        var directory = NewDirectory(outputRoot);
        WriteNew(Path.Combine(directory, "comparison.json"), EvaluationRedactor.Sanitize(comparison).Json);
        WriteNew(Path.Combine(directory, "comparison.md"), $"# AIC-004 前后比较\n\n可比较：{comparison.Comparable}；自动检查无新增退步：{comparison.NoRegression}。\n\n候选发布门槛：{comparison.CandidateGate.Passed}；生产发布未执行。\n\n" +
            string.Join("\n", comparison.Reasons.Concat(comparison.Regressions).Concat(comparison.CandidateGate.Reasons).Select(r => "- " + r)) + "\n");
        Console.WriteLine($"Comparison: {Path.Combine(directory, "comparison.json")}");
        if (!comparison.Comparable || !comparison.NoRegression) return 1;
        return options.ContainsKey("require-release-gate") && (!comparison.CandidateGate.Passed || !BaselineReviewed(options, baselinePath)) ? 2 : 0;
    }

    private static int ReviewTemplate(Dictionary<string, string> options, string outputRoot)
    {
        var path = Required(options, "candidate");
        var report = EvaluationFiles.Read<EvaluationReport>(path);
        var review = new ReviewDocument
        {
            Reviewer = "", SuiteHash = report.SuiteHash, ReportHash = EvaluationJson.Digest(File.ReadAllText(path)),
            GoldenCasesApproved = false, Cases = report.Results.Where(r => r.Status != EvaluationStatus.NotApplicable)
                .Select(r => new CaseReview(r.Key, r.CaseHash, false, "")).ToList()
        };
        var directory = NewDirectory(outputRoot);
        WriteNew(Path.Combine(directory, "review-template.json"), JsonSerializer.Serialize(review, EvaluationJson.Options));
        Console.WriteLine($"Unapproved human review template: {Path.Combine(directory, "review-template.json")}");
        return 0;
    }

    private static bool BaselineReviewed(Dictionary<string, string> options, string baselinePath) =>
        EvaluationGate.Evaluate(EvaluationFiles.Read<EvaluationReport>(baselinePath), EvaluationJson.Digest(File.ReadAllText(baselinePath)), ReadReview(options, "baseline-review")).Passed;
    private static GateResult ReleaseGate(GateResult candidate, ComparisonResult? comparison, bool baselineReviewed)
    {
        var reasons = candidate.Reasons.ToList();
        if (comparison is not { Comparable: true, NoRegression: true }) reasons.Add("Comparable baseline with no new automatic regression is required.");
        if (!baselineReviewed) reasons.Add("Confirmed human-reviewed baseline is required.");
        return new(reasons.Count == 0, reasons);
    }
    private static ReviewDocument? ReadReview(Dictionary<string, string> options, string key) => options.TryGetValue(key, out var path) ? EvaluationFiles.Read<ReviewDocument>(path) : null;
    private static string Required(Dictionary<string, string> options, string key) => options.TryGetValue(key, out var value) ? value : throw new EvaluationInputException($"--{key} is required.");

    private static Dictionary<string, string> Parse(string[] args)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "command", "root", "suite", "output", "mode", "live-config", "baseline", "candidate", "review", "baseline-review", "require-release-gate", "snapshot" };
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new EvaluationInputException("Expected a named argument.");
            var key = args[i][2..];
            if (!allowed.Contains(key) || result.ContainsKey(key)) throw new EvaluationInputException("Unknown or duplicate argument.");
            if (key == "require-release-gate") result[key] = "true";
            else if (++i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal)) result[key] = args[i];
            else throw new EvaluationInputException("Argument value is missing.");
        }
        return result;
    }

    private static string NewDirectory(string outputRoot)
    {
        var path = Path.Combine(outputRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static void WriteNew(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }
    private static string Markdown(EvaluationReport report, GateResult gate, ComparisonResult? comparison)
    {
        var text = new StringBuilder("# AIC-004 评测报告\n\n");
        text.AppendLine($"模式：{report.Mode}；模型：{report.Model}；供应商别名：{report.ProviderAlias}。").AppendLine();
        text.AppendLine($"确定性检查：{report.AutomaticChecksPassed}；人工发布门槛：{gate.Passed}。本报告不执行生产发布。").AppendLine();
        text.AppendLine($"工具／参数正确率：{report.Scores.ParameterCorrectness:P2}；事实自动检查正确率：{report.Scores.FactCorrectness:P2}；安全失败变体：{report.Scores.SafetyFailures}；完整变体：{report.Scores.CompleteVariants}/{report.Scores.ApplicableVariants}。").AppendLine();
        text.AppendLine($"调用：{report.CallCount}；保守计入 Token：{report.AccountedTokens}；预算计入费用：{report.AccountedCost} {report.Currency}；预算停止：{report.BudgetStopped}。").AppendLine();
        text.AppendLine("离线费用为合成估算，不代表实际收费。真实费用计入预留且不承诺账单上界；异常和缺失 usage 不按零结算。自然语言检查是辅助，黄金预期和解释事实须人工审核。").AppendLine();
        text.AppendLine($"源码摘要：`{report.SourceHash}`；案例摘要：`{report.SuiteHash}`；工作区有改动：{report.WorkingTreeDirty}。").AppendLine();
        text.AppendLine("| 案例／变体 | 自动判定 | 失败检查／说明 |").AppendLine("| --- | --- | --- |");
        foreach (var item in report.Results)
        {
            var failures = item.Steps.SelectMany((s, i) => s.Checks.Where(c => !c.Passed).Select(c => $"step-{i + 1}:{c.Code}"));
            text.AppendLine($"| {item.Key} | {item.Status} | {string.Join(", ", failures)} {item.Reason} |");
        }
        text.AppendLine().AppendLine("发布门槛未决项：").AppendLine();
        foreach (var reason in gate.Reasons) text.AppendLine("- " + reason);
        if (comparison is not null) text.AppendLine().AppendLine($"前后比较可比较：{comparison.Comparable}；自动检查无退步：{comparison.NoRegression}。");
        text.AppendLine().AppendLine($"规范化确定性摘要：`{EvaluationRedactor.DeterministicDigest(report)}`。完整脱敏参数、证据、工具／Prompt 摘要、判定与耗时见同目录 report.json。");
        return text.ToString();
    }
}
