namespace Workflow.Abstractions;

/// <summary>
/// Lifecycle state of a process definition version.
/// </summary>
public enum ProcessDefinitionStatus
{
    /// <summary>Definition can be edited.</summary>
    Draft = 0,
    /// <summary>Definition is active for new instances.</summary>
    Published = 1,
    /// <summary>Definition is retained as historical version.</summary>
    Archived = 2
}

/// <summary>
/// Node kind in a process graph.
/// </summary>
public enum NodeType
{
    /// <summary>Atomic approval step.</summary>
    Step = 0,
    /// <summary>Container of child steps.</summary>
    Block = 1
}

/// <summary>
/// Technical runtime step status.
/// </summary>
public enum StepStatus
{
    /// <summary>Step is waiting for processing.</summary>
    Pending = 0,
    /// <summary>Step is assigned to group and not yet claimed.</summary>
    Unclaimed = 1,
    /// <summary>Step is actively processed by specific user.</summary>
    InProgress = 2,
    /// <summary>Step was approved.</summary>
    Approved = 3,
    /// <summary>Step was rejected.</summary>
    Rejected = 4,
    /// <summary>Step requested rework from initiator.</summary>
    ReworkRequested = 5,
    /// <summary>Step was cancelled by workflow logic.</summary>
    Cancelled = 6,
    /// <summary>Step exceeded deadline.</summary>
    Expired = 7
}

/// <summary>
/// Source type for resolving step assignees.
/// </summary>
public enum AssigneeType
{
    /// <summary>Direct user assignment.</summary>
    User = 0,
    /// <summary>Group-based assignment.</summary>
    Group = 1,
    /// <summary>Dynamic assignment from request field.</summary>
    DynamicField = 2
}

/// <summary>
/// Rule for determining when a block is considered complete.
/// </summary>
public enum BlockCompletionPolicy
{
    /// <summary>All child steps must complete positively.</summary>
    All = 0,
    /// <summary>Any single child step can complete the block.</summary>
    Any = 1,
    /// <summary>Majority of child steps is required.</summary>
    Majority = 2
}

/// <summary>
/// Rule for handling rework within a block.
/// </summary>
public enum ReworkPolicy
{
    /// <summary>Return for rework immediately on first rework action.</summary>
    Immediate = 0,
    /// <summary>Wait until block finishes before returning for rework.</summary>
    Deferred = 1
}

/// <summary>
/// Notification event type used by template bindings.
/// </summary>
public enum NotificationEventType
{
    /// <summary>Step was assigned.</summary>
    OnAssigned = 0,
    /// <summary>Deadline warning event.</summary>
    OnDeadlineSoon = 1,
    /// <summary>Step approved event.</summary>
    OnApproved = 2,
    /// <summary>Step rejected event.</summary>
    OnRejected = 3,
    /// <summary>Rework requested event.</summary>
    OnRework = 4
}

/// <summary>
/// User action supported on step instance endpoints.
/// </summary>
public enum StepActionType
{
    /// <summary>Claim step from group.</summary>
    Claim = 0,
    /// <summary>Approve step.</summary>
    Approve = 1,
    /// <summary>Reject step.</summary>
    Reject = 2,
    /// <summary>Request rework.</summary>
    Rework = 3
}

/// <summary>
/// Semantic status layer used for custom status mapping.
/// </summary>
public enum WorkflowStatusSemantic
{
    /// <summary>Pending semantic.</summary>
    Pending = 0,
    /// <summary>Unclaimed semantic.</summary>
    Unclaimed = 1,
    /// <summary>In progress semantic.</summary>
    InProgress = 2,
    /// <summary>Approved semantic.</summary>
    Approved = 3,
    /// <summary>Rejected semantic.</summary>
    Rejected = 4,
    /// <summary>Rework requested semantic.</summary>
    ReworkRequested = 5,
    /// <summary>Cancelled semantic.</summary>
    Cancelled = 6,
    /// <summary>Expired semantic.</summary>
    Expired = 7
}

/// <summary>
/// Configurable process definition that can be versioned and published.
/// </summary>
public sealed class ProcessDefinition
{
    /// <summary>
    /// Definition identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// Stable process key shared by all versions.
    /// </summary>
    public string ProcessKey { get; set; } = string.Empty;
    /// <summary>
    /// Definition version number within process key.
    /// </summary>
    public int Version { get; set; }
    /// <summary>
    /// Human-friendly process name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Definition lifecycle status.
    /// </summary>
    public ProcessDefinitionStatus Status { get; set; } = ProcessDefinitionStatus.Draft;
    /// <summary>
    /// Definition-level custom status catalog.
    /// </summary>
    public List<WorkflowStatusDefinition> Statuses { get; set; } = [];
    /// <summary>
    /// Top-level process nodes.
    /// </summary>
    public List<NodeDefinition> Nodes { get; set; } = [];
}

/// <summary>
/// Definition node representing either step or block.
/// </summary>
public sealed class NodeDefinition
{
    /// <summary>
    /// Node identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// Node kind.
    /// </summary>
    public NodeType Type { get; set; }
    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Ordering index in parent scope.
    /// </summary>
    public int SortOrder { get; set; }
    /// <summary>
    /// Indicates parallel behavior in parent scope.
    /// </summary>
    public bool Parallel { get; set; }
    /// <summary>
    /// Optional condition expression for activation.
    /// </summary>
    public string? ConditionExpression { get; set; }
    /// <summary>
    /// Deadline in working days.
    /// </summary>
    public int DeadlineWorkingDays { get; set; }
    /// <summary>
    /// Indicates whether rework action is enabled.
    /// </summary>
    public bool NeedRework { get; set; }
    /// <summary>
    /// Block completion policy when node type is block.
    /// </summary>
    public BlockCompletionPolicy? BlockCompletionPolicy { get; set; }
    /// <summary>
    /// Rework behavior for block nodes.
    /// </summary>
    public ReworkPolicy? ReworkPolicy { get; set; }
    /// <summary>
    /// Rule for resolving step assignees.
    /// </summary>
    public AssigneeRule? AssigneeRule { get; set; }
    /// <summary>
    /// Optional semantic-to-custom-status mappings for steps.
    /// </summary>
    public List<StepStatusBinding> StatusBindings { get; set; } = [];
    /// <summary>
    /// Child nodes (typically block steps).
    /// </summary>
    public List<NodeDefinition> Children { get; set; } = [];
    /// <summary>
    /// Notification bindings for node events.
    /// </summary>
    public List<NotificationBinding> NotificationBindings { get; set; } = [];
}

/// <summary>
/// Custom status catalog entry owned by process definition.
/// </summary>
public sealed class WorkflowStatusDefinition
{
    /// <summary>
    /// Unique status key within definition.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Display text shown in UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Optional status description.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Technical semantic mapped by engine transitions.
    /// </summary>
    public WorkflowStatusSemantic Semantic { get; set; }
    /// <summary>
    /// Indicates fallback default for semantic.
    /// </summary>
    public bool IsDefault { get; set; }
}

/// <summary>
/// Mapping from technical semantic to custom status key for a step.
/// </summary>
public sealed class StepStatusBinding
{
    /// <summary>
    /// Technical semantic to map.
    /// </summary>
    public WorkflowStatusSemantic Semantic { get; set; }
    /// <summary>
    /// Target custom status key.
    /// </summary>
    public string StatusKey { get; set; } = string.Empty;
}

/// <summary>
/// Assignment resolution rule configured per step.
/// </summary>
public sealed class AssigneeRule
{
    /// <summary>
    /// Assignee source type.
    /// </summary>
    public AssigneeType Type { get; set; }
    /// <summary>
    /// Assignee value (user id, group id, or field key).
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Binding to notification template for a specific event.
/// </summary>
public sealed class NotificationBinding
{
    /// <summary>
    /// Event type that triggers notification.
    /// </summary>
    public NotificationEventType EventType { get; set; }
    /// <summary>
    /// Template key to use for notification.
    /// </summary>
    public string TemplateKey { get; set; } = string.Empty;
}

/// <summary>
/// Runtime process instance created from published definition.
/// </summary>
public sealed class ProcessInstance
{
    /// <summary>
    /// Runtime process identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// Stable process key.
    /// </summary>
    public string ProcessKey { get; set; } = string.Empty;
    /// <summary>
    /// Definition identifier used for runtime.
    /// </summary>
    public Guid DefinitionId { get; set; }
    /// <summary>
    /// Definition version used for runtime.
    /// </summary>
    public int DefinitionVersion { get; set; }
    /// <summary>
    /// External request identifier.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
    /// <summary>
    /// Aggregate runtime process state.
    /// </summary>
    public string State { get; set; } = "InProgress";
    /// <summary>
    /// Runtime step snapshots.
    /// </summary>
    public List<StepInstance> Steps { get; set; } = [];
}

/// <summary>
/// Runtime step snapshot with current status, assignments, and history.
/// </summary>
public sealed class StepInstance
{
    /// <summary>
    /// Runtime step identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// Source definition node identifier.
    /// </summary>
    public Guid NodeId { get; set; }
    /// <summary>
    /// Step display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Technical step status.
    /// </summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;
    /// <summary>
    /// Custom status key resolved from semantic mapping.
    /// </summary>
    public string CustomStatusKey { get; set; } = "pending";
    /// <summary>
    /// Custom status display name resolved from semantic mapping.
    /// </summary>
    public string CustomStatusName { get; set; } = "Pending";
    /// <summary>
    /// Creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Deadline timestamp in UTC.
    /// </summary>
    public DateTimeOffset? DeadlineAtUtc { get; set; }
    /// <summary>
    /// Indicates whether step belongs to a block.
    /// </summary>
    public bool IsBlockStep { get; set; }
    /// <summary>
    /// Parent block node identifier for block child steps.
    /// </summary>
    public Guid? ParentBlockNodeId { get; set; }
    /// <summary>
    /// Current assignments for this step.
    /// </summary>
    public List<Assignment> Assignments { get; set; } = [];
    /// <summary>
    /// Historical status transitions.
    /// </summary>
    public List<StepStatusHistoryItem> History { get; set; } = [];
}

/// <summary>
/// Runtime assignment record for a user or group.
/// </summary>
public sealed class Assignment
{
    /// <summary>
    /// Assignment identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// Assignment principal type.
    /// </summary>
    public AssigneeType Type { get; set; }
    /// <summary>
    /// Assigned principal identifier.
    /// </summary>
    public string PrincipalId { get; set; } = string.Empty;
    /// <summary>
    /// Indicates whether assignment has been claimed.
    /// </summary>
    public bool Claimed { get; set; }
    /// <summary>
    /// User identifier that claimed assignment.
    /// </summary>
    public string? ClaimedByUserId { get; set; }
}

/// <summary>
/// Immutable status transition record for audit trail.
/// </summary>
public sealed class StepStatusHistoryItem
{
    /// <summary>
    /// Timestamp of transition in UTC.
    /// </summary>
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// New technical status.
    /// </summary>
    public StepStatus NewStatus { get; set; }
    /// <summary>
    /// New custom status key.
    /// </summary>
    public string CustomStatusKey { get; set; } = string.Empty;
    /// <summary>
    /// New custom status display name.
    /// </summary>
    public string CustomStatusName { get; set; } = string.Empty;
    /// <summary>
    /// User identifier that triggered transition.
    /// </summary>
    public string ChangedBy { get; set; } = string.Empty;
    /// <summary>
    /// Optional transition comment.
    /// </summary>
    public string? Comment { get; set; }
}
