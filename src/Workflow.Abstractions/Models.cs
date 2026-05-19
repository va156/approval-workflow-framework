namespace Workflow.Abstractions;

public enum ProcessDefinitionStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public enum NodeType
{
    Step = 0,
    Block = 1
}

public enum StepStatus
{
    Pending = 0,
    Unclaimed = 1,
    InProgress = 2,
    Approved = 3,
    Rejected = 4,
    ReworkRequested = 5,
    Cancelled = 6,
    Expired = 7
}

public enum AssigneeType
{
    User = 0,
    Group = 1,
    DynamicField = 2
}

public enum BlockCompletionPolicy
{
    All = 0,
    Any = 1,
    Majority = 2
}

public enum ReworkPolicy
{
    Immediate = 0,
    Deferred = 1
}

public enum NotificationEventType
{
    OnAssigned = 0,
    OnDeadlineSoon = 1,
    OnApproved = 2,
    OnRejected = 3,
    OnRework = 4
}

public enum StepActionType
{
    Claim = 0,
    Approve = 1,
    Reject = 2,
    Rework = 3
}

public enum WorkflowStatusSemantic
{
    Pending = 0,
    Unclaimed = 1,
    InProgress = 2,
    Approved = 3,
    Rejected = 4,
    ReworkRequested = 5,
    Cancelled = 6,
    Expired = 7
}

public sealed class ProcessDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProcessKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProcessDefinitionStatus Status { get; set; } = ProcessDefinitionStatus.Draft;
    public List<WorkflowStatusDefinition> Statuses { get; set; } = [];
    public List<NodeDefinition> Nodes { get; set; } = [];
}

public sealed class NodeDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NodeType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Parallel { get; set; }
    public string? ConditionExpression { get; set; }
    public int DeadlineWorkingDays { get; set; }
    public bool NeedRework { get; set; }
    public BlockCompletionPolicy? BlockCompletionPolicy { get; set; }
    public ReworkPolicy? ReworkPolicy { get; set; }
    public AssigneeRule? AssigneeRule { get; set; }
    public List<StepStatusBinding> StatusBindings { get; set; } = [];
    public List<NodeDefinition> Children { get; set; } = [];
    public List<NotificationBinding> NotificationBindings { get; set; } = [];
}

public sealed class WorkflowStatusDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStatusSemantic Semantic { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class StepStatusBinding
{
    public WorkflowStatusSemantic Semantic { get; set; }
    public string StatusKey { get; set; } = string.Empty;
}

public sealed class AssigneeRule
{
    public AssigneeType Type { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class NotificationBinding
{
    public NotificationEventType EventType { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
}

public sealed class ProcessInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProcessKey { get; set; } = string.Empty;
    public Guid DefinitionId { get; set; }
    public int DefinitionVersion { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string State { get; set; } = "InProgress";
    public List<StepInstance> Steps { get; set; } = [];
}

public sealed class StepInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string CustomStatusKey { get; set; } = "pending";
    public string CustomStatusName { get; set; } = "Pending";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeadlineAtUtc { get; set; }
    public bool IsBlockStep { get; set; }
    public Guid? ParentBlockNodeId { get; set; }
    public List<Assignment> Assignments { get; set; } = [];
    public List<StepStatusHistoryItem> History { get; set; } = [];
}

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AssigneeType Type { get; set; }
    public string PrincipalId { get; set; } = string.Empty;
    public bool Claimed { get; set; }
    public string? ClaimedByUserId { get; set; }
}

public sealed class StepStatusHistoryItem
{
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public StepStatus NewStatus { get; set; }
    public string CustomStatusKey { get; set; } = string.Empty;
    public string CustomStatusName { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string? Comment { get; set; }
}
