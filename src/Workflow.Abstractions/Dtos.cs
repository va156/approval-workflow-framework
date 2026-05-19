namespace Workflow.Abstractions;

/// <summary>
/// Request payload for workflow preview operation.
/// </summary>
public sealed class PreviewRequest
{
    /// <summary>
    /// Stable process key used to select published definition.
    /// </summary>
    public required string ProcessKey { get; init; }

    /// <summary>
    /// Unsaved form payload used for conditions and assignee resolution.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> DraftData { get; init; }
}

/// <summary>
/// Preview response describing projected route.
/// </summary>
public sealed class WorkflowPreviewResult
{
    /// <summary>
    /// Stable process key.
    /// </summary>
    public required string ProcessKey { get; init; }

    /// <summary>
    /// Published definition version used for preview.
    /// </summary>
    public required int DefinitionVersion { get; init; }

    /// <summary>
    /// Calculated steps for current draft payload.
    /// </summary>
    public required List<PreviewStep> Steps { get; init; }
}

/// <summary>
/// Projected step returned by preview mode.
/// </summary>
public sealed class PreviewStep
{
    /// <summary>
    /// Definition node identifier.
    /// </summary>
    public required Guid NodeId { get; init; }

    /// <summary>
    /// Display name of step.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Indicates whether step belongs to block.
    /// </summary>
    public required bool IsBlockStep { get; init; }

    /// <summary>
    /// Parent block node identifier for block child steps.
    /// </summary>
    public Guid? ParentBlockNodeId { get; init; }

    /// <summary>
    /// Users resolved as responsible for this step.
    /// </summary>
    public required List<string> ResponsibleUsers { get; init; }

    /// <summary>
    /// Custom status key mapped for pending semantic.
    /// </summary>
    public required string CustomStatusKey { get; init; }

    /// <summary>
    /// Custom status display name mapped for pending semantic.
    /// </summary>
    public required string CustomStatusName { get; init; }

    /// <summary>
    /// Calculated deadline timestamp in UTC.
    /// </summary>
    public required DateTimeOffset? DeadlineAtUtc { get; init; }
}

/// <summary>
/// Request payload for starting runtime process instance.
/// </summary>
public sealed class StartProcessRequest
{
    /// <summary>
    /// Stable process key.
    /// </summary>
    public required string ProcessKey { get; init; }

    /// <summary>
    /// External business request identifier.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Persisted request payload snapshot.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> RequestData { get; init; }
}

/// <summary>
/// Result of process start operation.
/// </summary>
public sealed class StartProcessResult
{
    /// <summary>
    /// Created runtime process instance identifier.
    /// </summary>
    public required Guid ProcessInstanceId { get; init; }

    /// <summary>
    /// Definition version used to create runtime instance.
    /// </summary>
    public required int DefinitionVersion { get; init; }

    /// <summary>
    /// Initially active step projections.
    /// </summary>
    public required IReadOnlyList<StepActionProjection> ActiveSteps { get; init; }
}

/// <summary>
/// Request payload for step action execution.
/// </summary>
public sealed class StepActionRequest
{
    /// <summary>
    /// Runtime step identifier.
    /// </summary>
    public required Guid StepId { get; init; }

    /// <summary>
    /// Action to execute.
    /// </summary>
    public required StepActionType Action { get; init; }

    /// <summary>
    /// User identifier of actor.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Optional user comment.
    /// </summary>
    public string? Comment { get; init; }
}

/// <summary>
/// Result of step action execution.
/// </summary>
public sealed class StepActionResult
{
    /// <summary>
    /// Runtime process identifier.
    /// </summary>
    public required Guid ProcessInstanceId { get; init; }

    /// <summary>
    /// Target step identifier.
    /// </summary>
    public required Guid StepId { get; init; }

    /// <summary>
    /// Technical step status.
    /// </summary>
    public required StepStatus StepStatus { get; init; }

    /// <summary>
    /// Custom status key bound to technical status semantic.
    /// </summary>
    public required string CustomStatusKey { get; init; }

    /// <summary>
    /// Custom status display name bound to technical status semantic.
    /// </summary>
    public required string CustomStatusName { get; init; }

    /// <summary>
    /// Current process aggregate state.
    /// </summary>
    public required string ProcessState { get; init; }
}

/// <summary>
/// Runtime step projection returned after start operation.
/// </summary>
public sealed class StepActionProjection
{
    /// <summary>
    /// Runtime step identifier.
    /// </summary>
    public required Guid StepId { get; init; }

    /// <summary>
    /// Step display name.
    /// </summary>
    public required string StepName { get; init; }

    /// <summary>
    /// Technical step status.
    /// </summary>
    public required StepStatus Status { get; init; }

    /// <summary>
    /// Custom status key.
    /// </summary>
    public required string CustomStatusKey { get; init; }

    /// <summary>
    /// Custom status display name.
    /// </summary>
    public required string CustomStatusName { get; init; }

    /// <summary>
    /// Users currently responsible for the step.
    /// </summary>
    public required IReadOnlyList<string> ResponsibleUsers { get; init; }

    /// <summary>
    /// Step deadline in UTC.
    /// </summary>
    public DateTimeOffset? DeadlineAtUtc { get; init; }
}

/// <summary>
/// User-configurable notification template.
/// </summary>
public sealed class NotificationTemplate
{
    /// <summary>
    /// Unique template key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Subject template text.
    /// </summary>
    public required string SubjectTemplate { get; init; }

    /// <summary>
    /// Body template text.
    /// </summary>
    public required string BodyTemplate { get; init; }
}

/// <summary>
/// Outgoing notification message.
/// </summary>
public sealed class NotificationMessage
{
    /// <summary>
    /// Template key used to create message.
    /// </summary>
    public required string TemplateKey { get; init; }

    /// <summary>
    /// Destination user identifier.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Notification subject.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Notification body.
    /// </summary>
    public required string Body { get; init; }
}
