using PermissionSystem.Domain.Common;

namespace PermissionSystem.Domain.Entities;

public sealed class AiScenario : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public Guid? CurrentVersionId { get; set; }
    public int Revision { get; set; }
}

public sealed class AiScenarioDraft : BaseEntity
{
    public Guid ScenarioId { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public int Revision { get; set; }
}

public interface IAiImmutableRecord;

public sealed class AiScenarioVersion : BaseEntity, IAiImmutableRecord
{
    public Guid ScenarioId { get; init; }
    public int VersionNumber { get; init; }
    public string SnapshotJson { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;
    public string BuildIdentity { get; init; } = string.Empty;
}

public sealed class AiScenarioEvaluation : BaseEntity, IAiImmutableRecord
{
    public Guid ScenarioId { get; init; }
    public Guid VersionId { get; init; }
    public string ReportHash { get; init; } = string.Empty;
    public string ReportJson { get; init; } = string.Empty;
    public string ModelFingerprint { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public bool AutomaticPassed { get; init; }
}

public enum AiScenarioEventType { Reviewed, Published, Stopped, RolledBack, EvaluationRevoked }

public sealed class AiScenarioReleaseEvent : BaseEntity, IAiImmutableRecord
{
    public int Sequence { get; init; }
    public Guid ScenarioId { get; init; }
    public Guid VersionId { get; init; }
    public Guid? PreviousVersionId { get; init; }
    public Guid? EvaluationId { get; init; }
    public AiScenarioEventType Type { get; init; }
    public Guid ActorUserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ReviewJson { get; init; }
    public string? QualificationJson { get; init; }
}
