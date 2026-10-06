using System.Text.Json.Serialization;

namespace PermissionSystem.Application.Permissions;

[JsonConverter(typeof(JsonStringEnumConverter<PermissionDiagnosticKind>))]
public enum PermissionDiagnosticKind { Menu, Permission, DataScope }

[JsonConverter(typeof(JsonStringEnumConverter<PermissionDiagnosticConclusion>))]
public enum PermissionDiagnosticConclusion { Allowed, Denied, Limited, InsufficientEvidence }

[JsonConverter(typeof(JsonStringEnumConverter<PermissionDiagnosticCheckStatus>))]
public enum PermissionDiagnosticCheckStatus { Passed, Failed, NotEvaluated }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PermissionDiagnosticRequest
{
    public PermissionDiagnosticKind? Kind { get; init; }
    public Guid? TargetUserId { get; init; }
    public Guid? MenuId { get; init; }
    public string? PermissionCode { get; init; }
}

public sealed class PermissionDiagnosticTarget
{
    public Guid UserId { get; init; }
    public PermissionDiagnosticKind Kind { get; init; }
    public Guid? MenuId { get; init; }
    public string? PermissionCode { get; init; }
}

public sealed record PermissionDiagnosticCheck(
    string Code, PermissionDiagnosticCheckStatus Status, string Description, string Source);

public sealed record PermissionDiagnosticEntry(string Code, string Label);

public sealed class PermissionDiagnosticResponse
{
    public const int CurrentVersion = 1;
    public const int MaxChecks = 20;
    public const int MaxSerializedLength = 16_384;

    public int Version { get; init; } = CurrentVersion;
    public PermissionDiagnosticTarget Target { get; init; } = new();
    public string EvaluationBasis { get; init; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; init; }
    public PermissionDiagnosticConclusion Conclusion { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<PermissionDiagnosticCheck> Checks { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public IReadOnlyList<PermissionDiagnosticEntry> SuggestedEntries { get; init; } = [];
    public bool IsTruncated { get; init; }
}

public interface IPermissionDiagnosticService
{
    Task<PermissionDiagnosticResponse> DiagnoseAsync(
        PermissionDiagnosticRequest request, CancellationToken cancellationToken = default);

    Task<bool> CanReadAsync(PermissionDiagnosticResponse result, CancellationToken cancellationToken = default);
}
