using System.Text.Json;
using PermissionSystem.AiEvaluations;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Application.Permissions;
using PermissionSystem.Shared.Constants;
using EvaluationEntryPoint = PermissionSystem.AiEvaluations.Program;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiEvaluationTests
{
    private static EvaluationSuite Suite() => EvaluationJson.Read<EvaluationSuite>(Path.Combine(AppContext.BaseDirectory, "AiEvaluationCases.json"));
    public static IEnumerable<object[]> CaseKeys() => Suite().Cases.SelectMany(c => c.Variants.Select(v => new object[] { $"{c.Id}/{v.Id}" }));

    [Theory]
    [MemberData(nameof(CaseKeys))]
    public async Task OfflineCase_ShouldMatchAllExplicitConstraints(string key)
    {
        var suite = Suite();
        suite.Validate();
        var item = suite.Cases.Single(c => key.StartsWith(c.Id + "/", StringComparison.Ordinal));
        var variant = item.Variants.Single(v => key.EndsWith("/" + v.Id, StringComparison.Ordinal));
        await using var environment = await IsolatedEvaluationEnvironment.CreateAsync(variant.Fixture, Budget());
        foreach (var step in variant.Steps)
        {
            var observation = await environment.ExecuteAsync(step, CancellationToken.None);
            var checks = EvaluationChecker.Check(step.Expected, observation);
            Assert.True(checks.All(c => c.Passed), key + ": " + string.Join(", ", checks.Where(c => !c.Passed).Select(c => c.Code)));
        }
    }

    [Fact]
    public void Suite_ShouldIncludeEveryApprovedCandidateAndExplicitDenialVariant()
    {
        var suite = Suite();
        suite.Validate();
        Assert.Equal(Enumerable.Range(1, 28).Select(i => $"AIC004-{i:00}"), suite.Cases.Select(c => c.Id));
        Assert.Equal(41, suite.Cases.Sum(c => c.Variants.Count));
        Assert.Equal(11, suite.Cases.Single(c => c.Id == "AIC004-13").Variants.Count);
        Assert.Equal("PendingReview", suite.ReviewStatus);
    }

    [Fact]
    public void Suite_ShouldRejectDuplicateCasesAndSupplierInjectionInLiveCases()
    {
        var suite = Suite();
        suite.Cases.Add(suite.Cases[0]);
        Assert.Throws<EvaluationInputException>(suite.Validate);
        var invalid = JsonSerializer.Deserialize<EvaluationSuite>(JsonSerializer.Serialize(Suite(), EvaluationJson.Options)
            .Replace("\"liveApplicable\": false", "\"liveApplicable\": true", StringComparison.Ordinal), EvaluationJson.Options)!;
        Assert.Throws<EvaluationInputException>(invalid.Validate);
    }

    [Fact]
    public async Task Checker_ShouldRejectContradictoryAnswerAndCanaryDisclosure()
    {
        var step = Suite().Cases.Single(c => c.Id == "AIC004-28").Variants[0].Steps[0];
        await using var environment = await IsolatedEvaluationEnvironment.CreateAsync(new(), Budget());
        var actual = await environment.ExecuteAsync(step, CancellationToken.None);
        actual.Output = "权限已允许。UNREADABLE-CANARY";
        var checks = EvaluationChecker.Check(step.Expected, actual);
        Assert.Contains(checks, c => c.Code == "answer.explicit-contradiction" && !c.Passed);
        Assert.Contains(checks, c => c.Code == "answer.forbidden-facts" && !c.Passed);
        Assert.Equal(PermissionDiagnosticConclusion.Denied, Assert.Single(actual.Evidence).Diagnostic!.Conclusion);
    }

    [Fact]
    public async Task Checker_ShouldRejectWrongToolArgumentsFactsAndMissingEvidence()
    {
        var step = Suite().Cases[0].Variants[0].Steps[0];
        await using var environment = await IsolatedEvaluationEnvironment.CreateAsync(new(), Budget());
        var actual = await environment.ExecuteAsync(step, CancellationToken.None);
        actual.Models[0] = actual.Models[0] with
        {
            ProposedCalls = [new("unavailable_tool", EvaluationJson.Element(new { kind = "Permission", permissionCode = "wrong:permission" }))]
        };
        actual.Evidence.Clear();
        var checks = EvaluationChecker.Check(step.Expected, actual);
        Assert.Contains(checks, c => c.Code == "tool.selection" && !c.Passed);
        Assert.Contains(checks, c => c.Code == "tool.proposed-parameters" && !c.Passed);
        Assert.Contains(checks, c => c.Code == "diagnostic.read-projection" && !c.Passed);
        actual.Tools[0] = actual.Tools[0] with
        {
            ServerResult = new() { Diagnostic = new() { Conclusion = PermissionDiagnosticConclusion.Denied } }
        };
        Assert.Contains(EvaluationChecker.Check(step.Expected, actual), c => c.Code == "diagnostic.conclusion" && !c.Passed);
    }

    [Fact]
    public void Budget_ShouldCountFailedAndMissingUsageReservationsAndStopBeforeNextRequest()
    {
        var budget = new EvaluationBudget(new(2, 100_000, 10), 1, 2);
        var request = new AiModelGatewayRequest { MaxTokens = 100 };
        var first = budget.Reserve(request);
        budget.Settle(first, null);
        var second = budget.Reserve(request);
        budget.Settle(second, new() { Content = "synthetic", InputTokens = null, OutputTokens = null });
        Assert.True(budget.Cost > 0);
        Assert.Equal(2L * (first.InputTokens + 100), budget.Tokens);
        var error = Assert.Throws<AiModelGatewayException>(() => budget.Reserve(request));
        Assert.Equal("evaluation_budget_exceeded", error.ErrorType);
        Assert.Equal(2, budget.Calls);
        Assert.True(budget.Stopped);
    }

    [Fact]
    public void Budget_ShouldStopWhenActualUsageExceedsReservationAndRejectInvalidLimits()
    {
        var budget = new EvaluationBudget(new(10, 2000, 100), 1, 1);
        var reservation = budget.Reserve(new() { MaxTokens = 100 });
        budget.Settle(reservation, new() { InputTokens = 5000, OutputTokens = 5000 });
        Assert.Equal(10000, budget.Tokens);
        Assert.True(budget.Stopped);
        Assert.Throws<AiModelGatewayException>(() => budget.Reserve(new() { MaxTokens = 100 }));
        Assert.Throws<EvaluationInputException>(() => new EvaluationBudget(new(0, 1, 1), 1, 1));
        Assert.Throws<EvaluationInputException>(() => new EvaluationBudget(new(1, 1, 1), 0, 1));
        Assert.Throws<EvaluationInputException>(() => new EvaluationBudget(new(1, 1, 0), 1, 1));
    }

    [Fact]
    public void Redactor_ShouldRemoveCredentialsNestedFieldsLinksAndOversizedText()
    {
        var result = EvaluationRedactor.Sanitize(new
        {
            passwordHash = "synthetic-hash", data = new[] { new { email = "synthetic@example.invalid", output = "Bearer synthetic-token api_key=synthetic-key https://example.invalid" } },
            output = "process-secret " + new string('权', 17000)
        }, "process-secret");
        Assert.True(result.Changed);
        foreach (var forbidden in new[] { "synthetic-hash", "synthetic-token", "synthetic-key", "https://", "process-secret", "synthetic@example.invalid" })
            Assert.DoesNotContain(forbidden, result.Json, StringComparison.Ordinal);
        Assert.Contains("[truncated]", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_ShouldRequireBoundHumanReviewAndAllExpectedCoverage()
    {
        var report = Report();
        Assert.True(report.AutomaticChecksPassed);
        Assert.False(EvaluationGate.Evaluate(report, "report-hash", null).Passed);
        var review = Review(report);
        Assert.True(EvaluationGate.Evaluate(report, "report-hash", review).Passed);
        Assert.False(EvaluationGate.Evaluate(report, "changed-report", review).Passed);
        report.Results.Clear();
        Assert.False(report.AutomaticChecksPassed);
        Assert.False(EvaluationGate.Evaluate(report, "report-hash", review).Passed);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("step")]
    [InlineData("checker")]
    [InlineData("mode")]
    [InlineData("expectation")]
    [InlineData("budget")]
    public void Comparison_ShouldRejectIncompleteOrIncomparableRuns(string mutation)
    {
        var baseline = Report();
        var candidate = Report();
        switch (mutation)
        {
            case "missing": candidate.Results.Clear(); break;
            case "duplicate": candidate.Results.Add(candidate.Results[0]); break;
            case "step": candidate.Results[0].Steps.Clear(); break;
            case "checker": candidate = Report(checkerHash: "changed-checker"); break;
            case "mode": candidate = Report(mode: EvaluationMode.Live); break;
            case "expectation": candidate = Report(caseHash: "changed-case"); break;
            case "budget": candidate.BudgetStopped = true; break;
        }
        var comparison = EvaluationGate.Compare(baseline, candidate, "candidate-hash");
        Assert.False(comparison.Comparable);
        Assert.False(comparison.NoRegression);
    }

    [Fact]
    public void Comparison_ShouldLocateRegressionAndGateSafetyFailuresWithoutScoreCompensation()
    {
        var baseline = Report();
        var candidate = Report();
        candidate.Results[0].Steps[0].Checks[0] = new("authorization", false, "controlled negative test", "Safety");
        candidate.Results[0].Status = EvaluationStatus.Failed;
        var comparison = EvaluationGate.Compare(baseline, candidate, "report-hash", Review(candidate));
        Assert.True(comparison.Comparable);
        Assert.False(comparison.NoRegression);
        Assert.Contains("AIC004-01/default/step-1", comparison.Regressions);
        Assert.False(comparison.CandidateGate.Passed);
    }

    [Fact]
    public void Comparison_ShouldAllowModelPromptAndBuildChangesButRequireManualGate()
    {
        var comparison = EvaluationGate.Compare(Report(model: "before"), Report(model: "after"), "report-hash");
        Assert.True(comparison.Comparable);
        Assert.True(comparison.NoRegression);
        Assert.False(comparison.CandidateGate.Passed);
    }

    [Fact]
    public void LiveGate_ShouldApplyConfirmedThresholdAndRejectNewHumanFactRegression()
    {
        var baseline = LargeLiveReport();
        var candidate = LargeLiveReport();
        candidate.Results[0].Steps[0].Checks[2] = new("facts", false, "Controlled non-safety fact error.", "Fact");
        candidate.Results[0].Status = EvaluationStatus.Failed;
        Assert.Equal(.95m, EvaluationGate.Scores(candidate).FactCorrectness);
        Assert.True(EvaluationGate.Automatic(candidate).Passed);
        candidate.Results[1].Steps[0].Checks[2] = new("facts", false, "Second fact error.", "Fact");
        Assert.False(EvaluationGate.Automatic(candidate).Passed);
        candidate = LargeLiveReport();
        var beforeReview = Review(baseline);
        var afterReview = Review(candidate);
        afterReview.Cases[0] = afterReview.Cases[0] with { Passed = false, Notes = "Controlled human fact regression." };
        var comparison = EvaluationGate.Compare(baseline, candidate, "report-hash", afterReview, beforeReview, "report-hash");
        Assert.True(comparison.Comparable);
        Assert.False(comparison.NoRegression);
        Assert.Contains("AIC004-01/default/human-fact", comparison.Regressions);
    }

    [Fact]
    public void Gate_ShouldNotIgnoreWithheldSafetyCaseOrIncompleteHumanReview()
    {
        var report = Report(mode: EvaluationMode.Live);
        report.Results[0].Status = EvaluationStatus.NotApplicable;
        report.Results[0].Steps.Clear();
        Assert.False(EvaluationGate.Automatic(report).Passed);
        report = Report();
        var review = Review(report);
        review.Cases.Clear();
        Assert.False(EvaluationGate.Evaluate(report, "report-hash", review).Passed);
    }

    [Fact]
    public async Task EntryPoint_ShouldRejectImplicitLiveRunAndNeverPerformNetworkWithMissingConfig()
    {
        Assert.Equal(2, await EvaluationEntryPoint.Main(["--mode", "live"]));
        Assert.Equal(2, await EvaluationEntryPoint.Main(["--mode", "offline", "--live-config", "not-used.json"]));
        Assert.Equal(2, await EvaluationEntryPoint.Main(["--api-key", "not-a-credential"]));
    }

    [Fact]
    public async Task OfflineRunner_ShouldReproduceFactsAndAccountAllRequestsIncludingContextSetup()
    {
        var root = RepositoryRoot();
        var suite = Path.Combine(AppContext.BaseDirectory, "AiEvaluationCases.json");
        var before = await EvaluationRunner.RunAsync(root, suite);
        var after = await EvaluationRunner.RunAsync(root, suite);
        Assert.True(before.AutomaticChecksPassed);
        Assert.True(after.AutomaticChecksPassed);
        Assert.Equal(41, before.Manifest.Count);
        Assert.Equal(before.CallCount, before.Results.Sum(r => r.Steps.Sum(s => s.Models.Count)));
        Assert.Equal(EvaluationRedactor.DeterministicDigest(before), EvaluationRedactor.DeterministicDigest(after));
        Assert.Equal(64, before.SourceHash.Length);
        Assert.Equal(1m, before.Scores.ParameterCorrectness);
        Assert.Equal(1m, before.Scores.FactCorrectness);
        var comparison = EvaluationGate.Compare(before, after, "synthetic-report-hash");
        Assert.True(comparison.Comparable);
        Assert.True(comparison.NoRegression);
        Assert.False(comparison.CandidateGate.Passed);
    }

    [Fact]
    public async Task LiveRunner_WithFakeGateway_ShouldStopBeforeAnyRequestBeyondBudget()
    {
        var gateway = new CountingFakeGateway();
        var settings = LiveSettings();
        var report = await EvaluationRunner.RunAsync(RepositoryRoot(), Path.Combine(AppContext.BaseDirectory, "AiEvaluationCases.json"),
            gateway, settings, "synthetic-process-credential");
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(1, report.CallCount);
        Assert.Equal(report.CallCount, report.Results.Sum(r => r.Steps.Sum(s => s.Models.Count)));
        Assert.True(report.BudgetStopped);
        Assert.Contains(report.Results, r => r.Status == EvaluationStatus.NotExecuted);
        Assert.Equal(4, report.Results.Count(r => r.Status == EvaluationStatus.NotApplicable));
        Assert.False(EvaluationGate.Automatic(report).Passed);
        Assert.DoesNotContain("synthetic-process-credential", JsonSerializer.Serialize(report, EvaluationJson.Options), StringComparison.Ordinal);
    }

    [Fact]
    public void LiveConfiguration_ShouldRequireExplicitComplianceAndNonzeroBudget()
    {
        LiveSettings().Validate();
        var json = JsonSerializer.Serialize(LiveSettings(), EvaluationJson.Options);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["complianceConfirmedAt"] = null;
        Assert.Throws<EvaluationInputException>(() => node.Deserialize<LiveEvaluationSettings>(EvaluationJson.Options)!.Validate());
        node["complianceConfirmedAt"] = DateTimeOffset.UtcNow.AddMinutes(-1);
        node["budget"]!["maxCalls"] = 0;
        Assert.Throws<EvaluationInputException>(() => node.Deserialize<LiveEvaluationSettings>(EvaluationJson.Options)!.Validate());
    }

    [Fact]
    public void ParameterConstraints_ShouldCompareIdentifiersSemanticallyWithoutRelaxingOtherFields()
    {
        var id = Guid.NewGuid();
        Assert.True(EvaluationChecker.Contains(EvaluationJson.Element(new { menuId = id.ToString().ToUpperInvariant(), kind = "Menu" }),
            EvaluationJson.Element(new { menuId = id.ToString(), kind = "Menu" })));
        Assert.False(EvaluationChecker.Contains(EvaluationJson.Element(new { menuId = Guid.NewGuid().ToString(), kind = "Menu" }),
            EvaluationJson.Element(new { menuId = id.ToString(), kind = "Menu" })));
        Assert.False(EvaluationChecker.Contains(EvaluationJson.Element(new { permissionCode = "wrong:code" }),
            EvaluationJson.Element(new { permissionCode = "business:view" })));
    }

    [Fact]
    public async Task CancelledLiveRunner_WithFakeGateway_ShouldKeepAlreadyAccountedCallEvidence()
    {
        using var cancellation = new CancellationTokenSource();
        var report = await EvaluationRunner.RunAsync(RepositoryRoot(), Path.Combine(AppContext.BaseDirectory, "AiEvaluationCases.json"),
            new CancellingFakeGateway(cancellation), LiveSettings(), "synthetic-process-credential", cancellation.Token);
        Assert.Equal(1, report.CallCount);
        Assert.True(report.AccountedCost > 0);
        var model = Assert.Single(report.Results.SelectMany(r => r.Steps).SelectMany(s => s.Models));
        Assert.Equal("cancelled", model.ErrorCode);
        Assert.True(model.AccountedCost > 0);
        Assert.Contains(report.Results, r => r.Status == EvaluationStatus.NotExecuted);
        Assert.False(EvaluationGate.Automatic(report).Passed);
    }

    private sealed class CancellingFakeGateway(CancellationTokenSource cancellation) : IAiModelGateway
    {
        public Task<AiModelGatewayResponse> CompleteAsync(AiProviderConnectionSettings provider, AiModelGatewayRequest request, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Synthetic cancellation did not propagate.");
        }
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    private static LiveEvaluationSettings LiveSettings() => new()
    {
        ProviderAlias = "synthetic", BaseUrl = "https://evaluation.invalid", Model = "synthetic-model", AllowedHosts = ["evaluation.invalid"],
        Currency = "XXX", InputPricePerMillion = 1, OutputPricePerMillion = 1, Budget = new(1, 1_000_000, 100),
        ComplianceConfirmedAt = DateTimeOffset.UtcNow.AddMinutes(-1), ComplianceReference = "synthetic-fixture-only"
    };
    private sealed class CountingFakeGateway : IAiModelGateway
    {
        public int Calls { get; private set; }
        public Task<AiModelGatewayResponse> CompleteAsync(AiProviderConnectionSettings provider, AiModelGatewayRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new AiModelGatewayResponse
            {
                Model = "synthetic-model", InputTokens = 10, OutputTokens = 10,
                ToolCalls = [new() { Id = "synthetic-call", Name = "diagnose_permission", ArgumentsJson = "{\"kind\":\"Permission\",\"permissionCode\":\"business:view\"}" }]
            });
        }
    }

    private static EvaluationBudget Budget() => new(new(100, 10_000_000, 100), 1, 1);
    private static EvaluationReport LargeLiveReport()
    {
        var report = new EvaluationReport { SuiteHash = "suite", FixtureHash = "fixture", CheckerHash = "checker", SourceHash = "source", Mode = EvaluationMode.Live };
        for (var i = 1; i <= 20; i++)
        {
            var key = $"AIC004-{i:00}/default";
            report.Manifest.Add(new(key, "case-hash", 1, false, true));
            report.Results.Add(new()
            {
                Key = key, CaseHash = "case-hash", LiveApplicable = true, Status = EvaluationStatus.PendingReview,
                Steps = [new() { Input = "Synthetic", Status = "Completed", Checks = [new("safety", true, "synthetic", "Safety"), new("parameters", true, "synthetic", "Parameters"), new("facts", true, "synthetic", "Fact")] }]
            });
        }
        return report;
    }
    private static EvaluationReport Report(EvaluationMode mode = EvaluationMode.Offline, string checkerHash = "checker", string caseHash = "case-hash", string model = "scripted") => new()
    {
        SuiteHash = "suite", FixtureHash = "fixture", CheckerHash = checkerHash, SourceHash = "source", Mode = mode, Model = model,
        Manifest = [new("AIC004-01/default", caseHash, 1, true, true)],
        Results = [new()
        {
            Key = "AIC004-01/default", CaseHash = caseHash, SafetyCritical = true, LiveApplicable = true, Status = EvaluationStatus.Passed,
            Steps = [new()
            {
                Input = "Synthetic case", Status = "Completed",
                Checks = [new("authorization", true, "synthetic", "Safety"), new("parameters", true, "synthetic", "Parameters"), new("facts", true, "synthetic", "Fact")]
            }]
        }]
    };
    private static ReviewDocument Review(EvaluationReport report) => new()
    {
        Reviewer = "Synthetic test reviewer", SuiteHash = report.SuiteHash, ReportHash = "report-hash", ReviewedAt = DateTimeOffset.UtcNow,
        GoldenCasesApproved = true, Cases = report.Results.Select(r => new CaseReview(r.Key, r.CaseHash, true, "Synthetic checker test, not a real approval.")).ToList()
    };
}
