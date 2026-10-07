using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.AiCenter;

public static class AiTechnicalExportReceiptContract
{
    public const string Route = "/api/ai/operations/technical-export-receipts";
    public const int MaxRecords = 1_000;
    public const int ReadSeconds = 10;
    public const string CapacityMessage = "导出凭据超过读取上限，请缩短凭据时间范围。";
}

public class AiTechnicalExportReceiptWindow
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed class AiTechnicalExportReceiptQuery : AiTechnicalExportReceiptWindow
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record AiTechnicalExportReceiptSummary(
    Guid ExportId, DateTimeOffset FirstRecordedAt, DateTimeOffset LastRecordedAt,
    int RequestedCount, int PreparedCount, int FailedCount, bool CanVerify, string VerificationReason);

public sealed record AiTechnicalExportReceipt(
    Guid ReceiptId, DateTimeOffset RecordedAt, int SchemaVersion, string Outcome,
    DateTimeOffset From, DateTimeOffset To, DateTimeOffset ObservedFrom, DateTimeOffset? ObservedTo,
    int? RunCount, int? UsageCount, int? Bytes, string? FileSha256, string? FailureCode);

public sealed record AiTechnicalExportReceiptPage(
    Guid TenantId, DateTimeOffset ReceiptFrom, DateTimeOffset ReceiptTo,
    DateTimeOffset ObservedFrom, DateTimeOffset ObservedTo, int MatchedRecordCount, int UnreadableRecordCount,
    PagedResult<AiTechnicalExportReceiptSummary> Exports)
{
    public string Scope => "CurrentCaller";
    public bool WindowInterpretable => UnreadableRecordCount == 0;
}

public sealed record AiTechnicalExportReceiptDetail(
    Guid TenantId, DateTimeOffset ReceiptFrom, DateTimeOffset ReceiptTo,
    DateTimeOffset ObservedFrom, DateTimeOffset ObservedTo, int MatchedRecordCount, int UnreadableRecordCount,
    AiTechnicalExportReceiptSummary Export, IReadOnlyList<AiTechnicalExportReceipt> Receipts)
{
    public string Scope => "CurrentCaller";
    public bool WindowInterpretable => UnreadableRecordCount == 0;
}

public interface IAiTechnicalExportReceiptService
{
    Task<AiTechnicalExportReceiptPage> QueryAsync(AiTechnicalExportReceiptQuery request, CancellationToken ct = default);
    Task<AiTechnicalExportReceiptDetail> GetAsync(Guid exportId, AiTechnicalExportReceiptWindow request, CancellationToken ct = default);
}
