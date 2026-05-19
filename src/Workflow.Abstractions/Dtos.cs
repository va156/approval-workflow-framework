namespace Workflow.Abstractions;

public sealed class PreviewRequest
{
    public required string ProcessKey { get; init; }
    public required IReadOnlyDictionary<string, object?> DraftData { get; init; }
}

public sealed class WorkflowPreviewResult
{
    public required string ProcessKey { get; init; }
    public required int DefinitionVersion { get; init; }
    public required List<PreviewStep> Steps { get; init; }
}

public sealed class PreviewStep
{
    public required Guid NodeId { get; init; }
    public required string Name { get; init; }
    public required bool IsBlockStep { get; init; }
    public Guid? ParentBlockNodeId { get; init; }
    public required List<string> ResponsibleUsers { get; init; }
    public required string CustomStatusKey { get; init; }
    public required string CustomStatusName { get; init; }
    public required DateTimeOffset? DeadlineAtUtc { get; init; }
}

public sealed class StartProcessRequest
{
    public required string ProcessKey { get; init; }
    public required string RequestId { get; init; }
    public required IReadOnlyDictionary<string, object?> RequestData { get; init; }
}

public sealed class StartProcessResult
{
    public required Guid ProcessInstanceId { get; init; }
    public required int DefinitionVersion { get; init; }
    public required IReadOnlyList<StepActionProjection> ActiveSteps { get; init; }
}

public sealed class StepActionRequest
{
    public required Guid StepId { get; init; }
    public required StepActionType Action { get; init; }
    public required string UserId { get; init; }
    public string? Comment { get; init; }
}

public sealed class StepActionResult
{
    public required Guid ProcessInstanceId { get; init; }
    public required Guid StepId { get; init; }
    public required StepStatus StepStatus { get; init; }
    public required string CustomStatusKey { get; init; }
    public required string CustomStatusName { get; init; }
    public required string ProcessState { get; init; }
}

public sealed class StepActionProjection
{
    public required Guid StepId { get; init; }
    public required string StepName { get; init; }
    public required StepStatus Status { get; init; }
    public required string CustomStatusKey { get; init; }
    public required string CustomStatusName { get; init; }
    public required IReadOnlyList<string> ResponsibleUsers { get; init; }
    public DateTimeOffset? DeadlineAtUtc { get; init; }
}

public sealed class NotificationTemplate
{
    public required string Key { get; init; }
    public required string SubjectTemplate { get; init; }
    public required string BodyTemplate { get; init; }
}

public sealed class NotificationMessage
{
    public required string TemplateKey { get; init; }
    public required string UserId { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
}
