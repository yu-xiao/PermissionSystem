namespace PermissionSystem.Application.AiCenter;

public static class AiTechnicalExportContract
{
    public const int SchemaVersion = 1;
    public const int MaxRuns = 10_000;
    public const int MaxUsages = 50_000;
    public const int MaxBytes = 16 * 1024 * 1024;
    public const int PreparationSeconds = 10;
    public const int LockSeconds = 30;
    public const string Route = "/api/ai/operations/technical-export";
    public const string CapacityMessage = "技术元数据超过导出上限，请缩短时间范围后重试。";
    public static string FileName(Guid exportId) => $"ai-technical-{exportId:N}.json";
}

public sealed class AiTechnicalExportRequest
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record AiTechnicalExportFile(Guid ExportId, byte[] Content)
{
    public string FileName => AiTechnicalExportContract.FileName(ExportId);
}

public interface IAiTechnicalExportService
{
    Task<AiTechnicalExportFile> ExportAsync(AiTechnicalExportRequest request, CancellationToken cancellationToken = default);
}

public sealed record AiTechnicalExportManifest(
    int SchemaVersion, Guid ExportId, Guid TenantId, DateTimeOffset From, DateTimeOffset To,
    DateTimeOffset ObservedFrom, DateTimeOffset ObservedTo, int RunCount, int UsageCount, string PayloadSha256)
{
    public string Purpose => "TechnicalReview";
    public string Recipient => "CurrentCaller";
    public int MaxRuns => AiTechnicalExportContract.MaxRuns;
    public int MaxUsages => AiTechnicalExportContract.MaxUsages;
    public int MaxBytes => AiTechnicalExportContract.MaxBytes;
    public string Consistency => "ObservationWindowNotTransactionSnapshot";
    public string CostBasis => "EstimatedNotSupplierInvoice";
    public string IntegrityBasis => "SHA256OfCompactUtf8PayloadNotSignature";
}

public sealed record AiTechnicalRunExport(
    Guid Id, Guid? ScenarioId, Guid? ScenarioVersionId, DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string Status, long? DurationMilliseconds,
    int? FallbackCount, bool InvalidDuration, bool InvalidFallbackCount);

public sealed record AiTechnicalUsageExport(
    Guid Id, Guid RunId, DateTimeOffset CreatedAt, int? Sequence, int? Round, int? Attempt,
    string RouteRole, string Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    long? DurationMilliseconds, int? InputTokens, int? OutputTokens, int? EstimatedInputTokens,
    int? EstimatedOutputTokens, decimal? EstimatedCost, string? PricingCurrency,
    bool UsageIncomplete, bool CostUnknown, bool Unsettled, bool InvalidDuration, bool InvalidCounters,
    bool InvalidActualTokens, bool InvalidEstimatedTokens);
