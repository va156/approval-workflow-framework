using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Workflow.Abstractions;

namespace Workflow.Engine;

/// <summary>
/// Default workflow engine implementation used for preview, start, and step actions.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IProcessDefinitionRepository _definitionRepository;
    private readonly IProcessInstanceRepository _instanceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGroupProvider _groupProvider;
    private readonly IBusinessCalendar _businessCalendar;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly SemaphoreSlim _parallelismGate = new(4, 4);
    private readonly ConcurrentDictionary<Guid, object> _instanceLocks = new();
    private readonly ConcurrentDictionary<string, Func<IReadOnlyDictionary<string, object?>, bool>> _conditionCache = new();

    /// <summary>
    /// Creates engine with required repositories and adapters.
    /// </summary>
    public WorkflowEngine(
        IProcessDefinitionRepository definitionRepository,
        IProcessInstanceRepository instanceRepository,
        IUnitOfWork unitOfWork,
        IGroupProvider groupProvider,
        IBusinessCalendar businessCalendar,
        IConditionEvaluator conditionEvaluator,
        INotificationService notificationService,
        ILogger<WorkflowEngine> logger)
    {
        _definitionRepository = definitionRepository;
        _instanceRepository = instanceRepository;
        _unitOfWork = unitOfWork;
        _groupProvider = groupProvider;
        _businessCalendar = businessCalendar;
        _conditionEvaluator = conditionEvaluator;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WorkflowPreviewResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken)
    {
        var definition = await GetPublishedDefinitionOrThrow(request.ProcessKey, cancellationToken);
        var steps = await MaterializeStepProjectionsAsync(definition, request.DraftData, cancellationToken);
        return new WorkflowPreviewResult
        {
            ProcessKey = request.ProcessKey,
            DefinitionVersion = definition.Version,
            Steps = steps
        };
    }

    /// <inheritdoc />
    public async Task<StartProcessResult> StartAsync(StartProcessRequest request, CancellationToken cancellationToken)
    {
        var definition = await GetPublishedDefinitionOrThrow(request.ProcessKey, cancellationToken);
        var projections = await MaterializeStepProjectionsAsync(definition, request.RequestData, cancellationToken);

        var instance = new ProcessInstance
        {
            ProcessKey = request.ProcessKey,
            DefinitionId = definition.Id,
            DefinitionVersion = definition.Version,
            RequestId = request.RequestId,
            State = "InProgress",
            Steps = projections.Select(ToStepInstance).ToList()
        };
        ApplyInitialCustomStatuses(definition, instance);

        await _instanceRepository.UpsertAsync(instance, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return new StartProcessResult
        {
            ProcessInstanceId = instance.Id,
            DefinitionVersion = definition.Version,
            ActiveSteps = instance.Steps.Select(ToProjection).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<StepActionResult> ExecuteStepActionAsync(StepActionRequest request, CancellationToken cancellationToken)
    {
        var instance = await _instanceRepository.GetByStepIdAsync(request.StepId, cancellationToken)
            ?? throw new InvalidOperationException($"Step {request.StepId} not found.");
        var definition = await _definitionRepository.GetByIdAsync(instance.DefinitionId, cancellationToken)
            ?? throw new InvalidOperationException($"Definition '{instance.DefinitionId}' not found.");
        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        var nodes = FlattenNodes(definition.Nodes).ToDictionary(x => x.Id);
        var sync = _instanceLocks.GetOrAdd(instance.Id, _ => new object());

        // Protect transitions on one process instance from concurrent mutations.
        lock (sync)
        {
            var targetStep = instance.Steps.SingleOrDefault(x => x.Id == request.StepId)
                ?? throw new InvalidOperationException("Step does not exist.");
            if (!nodes.TryGetValue(targetStep.NodeId, out var nodeDefinition))
            {
                throw new InvalidOperationException("Step definition is missing.");
            }

            if (targetStep.Status is StepStatus.Approved or StepStatus.Rejected or StepStatus.Cancelled)
            {
                throw new InvalidOperationException("Step is already closed.");
            }

            switch (request.Action)
            {
                case StepActionType.Claim:
                    ClaimStep(targetStep, request.UserId, nodeDefinition, definition);
                    break;
                case StepActionType.Approve:
                    Transition(targetStep, StepStatus.Approved, request.UserId, request.Comment, nodeDefinition, definition);
                    break;
                case StepActionType.Reject:
                    Transition(targetStep, StepStatus.Rejected, request.UserId, request.Comment, nodeDefinition, definition);
                    break;
                case StepActionType.Rework:
                    Transition(targetStep, StepStatus.ReworkRequested, request.UserId, request.Comment, nodeDefinition, definition);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Action), request.Action, "Action is not supported.");
            }
        }

        await ApplyBlockAndProcessTransitionsAsync(instance, cancellationToken);
        await _instanceRepository.UpsertAsync(instance, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var updated = instance.Steps.Single(x => x.Id == request.StepId);
        return new StepActionResult
        {
            ProcessInstanceId = instance.Id,
            StepId = updated.Id,
            StepStatus = updated.Status,
            CustomStatusKey = updated.CustomStatusKey,
            CustomStatusName = updated.CustomStatusName,
            ProcessState = instance.State
        };
    }

    private async Task<ProcessDefinition> GetPublishedDefinitionOrThrow(string processKey, CancellationToken cancellationToken)
    {
        var definition = await _definitionRepository.GetPublishedByKeyAsync(processKey, cancellationToken);
        if (definition is null)
        {
            throw new InvalidOperationException($"Published definition for key '{processKey}' does not exist.");
        }

        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        return definition;
    }

    private async Task<List<PreviewStep>> MaterializeStepProjectionsAsync(
        ProcessDefinition definition,
        IReadOnlyDictionary<string, object?> requestData,
        CancellationToken cancellationToken)
    {
        var result = new List<PreviewStep>();
        var sorted = definition.Nodes.OrderBy(n => n.SortOrder).ToList();

        foreach (var node in sorted)
        {
            if (!MatchesCondition(node.ConditionExpression, requestData))
            {
                continue;
            }

            if (node.Type == NodeType.Step)
            {
                var users = await ResolveResponsibleUsersAsync(node.AssigneeRule, requestData, cancellationToken);
                result.Add(CreateProjection(node, users, null, definition));
                continue;
            }

            foreach (var child in node.Children.OrderBy(x => x.SortOrder))
            {
                if (!MatchesCondition(child.ConditionExpression, requestData))
                {
                    continue;
                }

                var users = await ResolveResponsibleUsersAsync(child.AssigneeRule, requestData, cancellationToken);
                result.Add(CreateProjection(child, users, node.Id, definition));
            }
        }

        return result;
    }

    private bool MatchesCondition(string? expression, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        var predicate = _conditionCache.GetOrAdd(expression, static e => input =>
        {
            // Common cases are evaluated through fast local parser.
            if (e.Contains("==", StringComparison.Ordinal))
            {
                var parts = e.Split("==", 2, StringSplitOptions.TrimEntries);
                var key = parts[0];
                var expected = parts[1].Trim('\'', '"');
                return input.TryGetValue(key, out var raw) && string.Equals(raw?.ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }

            if (e.Contains("!=", StringComparison.Ordinal))
            {
                var parts = e.Split("!=", 2, StringSplitOptions.TrimEntries);
                var key = parts[0];
                var expected = parts[1].Trim('\'', '"');
                return input.TryGetValue(key, out var raw) && !string.Equals(raw?.ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        });

        return predicate(data) || _conditionEvaluator.Evaluate(expression, data);
    }

    private async Task<List<string>> ResolveResponsibleUsersAsync(
        AssigneeRule? assigneeRule,
        IReadOnlyDictionary<string, object?> requestData,
        CancellationToken cancellationToken)
    {
        if (assigneeRule is null || string.IsNullOrWhiteSpace(assigneeRule.Value))
        {
            return [];
        }

        return assigneeRule.Type switch
        {
            AssigneeType.User => [assigneeRule.Value],
            AssigneeType.DynamicField => requestData.TryGetValue(assigneeRule.Value, out var dynamicUser) && dynamicUser is not null
                ? [dynamicUser.ToString()!]
                : [],
            AssigneeType.Group => (await _groupProvider.GetGroupMembersAsync(assigneeRule.Value, cancellationToken)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            _ => []
        };
    }

    private PreviewStep CreateProjection(NodeDefinition node, IReadOnlyList<string> users, Guid? parentBlockNodeId, ProcessDefinition definition)
    {
        DateTimeOffset? deadline = null;
        if (node.DeadlineWorkingDays > 0)
        {
            deadline = _businessCalendar.AddWorkingDays(DateTimeOffset.UtcNow, node.DeadlineWorkingDays);
        }
        var custom = ResolveCustomStatus(node, definition, WorkflowStatusSemantic.Pending);

        return new PreviewStep
        {
            NodeId = node.Id,
            Name = node.Name,
            IsBlockStep = parentBlockNodeId.HasValue,
            ParentBlockNodeId = parentBlockNodeId,
            ResponsibleUsers = users.ToList(),
            CustomStatusKey = custom.Key,
            CustomStatusName = custom.Name,
            DeadlineAtUtc = deadline
        };
    }

    private static StepInstance ToStepInstance(PreviewStep projection)
    {
        var assignments = projection.ResponsibleUsers
            .Select(u => new Assignment
            {
                Type = AssigneeType.User,
                PrincipalId = u,
                Claimed = false
            })
            .ToList();

        return new StepInstance
        {
            NodeId = projection.NodeId,
            Name = projection.Name,
            Status = assignments.Count > 1 ? StepStatus.Unclaimed : StepStatus.Pending,
            CustomStatusKey = projection.CustomStatusKey,
            CustomStatusName = projection.CustomStatusName,
            ParentBlockNodeId = projection.ParentBlockNodeId,
            IsBlockStep = projection.IsBlockStep,
            DeadlineAtUtc = projection.DeadlineAtUtc,
            Assignments = assignments
        };
    }

    private static StepActionProjection ToProjection(StepInstance step) =>
        new()
        {
            StepId = step.Id,
            StepName = step.Name,
            Status = step.Status,
            CustomStatusKey = step.CustomStatusKey,
            CustomStatusName = step.CustomStatusName,
            ResponsibleUsers = step.Assignments.Select(a => a.PrincipalId).ToList(),
            DeadlineAtUtc = step.DeadlineAtUtc
        };

    private static void ClaimStep(StepInstance step, string userId, NodeDefinition node, ProcessDefinition definition)
    {
        var assignment = step.Assignments.SingleOrDefault(x => string.Equals(x.PrincipalId, userId, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            throw new InvalidOperationException("User cannot claim this step.");
        }

        foreach (var item in step.Assignments)
        {
            item.Claimed = false;
            item.ClaimedByUserId = null;
        }

        assignment.Claimed = true;
        assignment.ClaimedByUserId = userId;
        step.Status = StepStatus.InProgress;
        var custom = ResolveCustomStatus(node, definition, WorkflowStatusSemantic.InProgress);
        step.CustomStatusKey = custom.Key;
        step.CustomStatusName = custom.Name;
        step.History.Add(new StepStatusHistoryItem
        {
            ChangedBy = userId,
            NewStatus = StepStatus.InProgress,
            CustomStatusKey = custom.Key,
            CustomStatusName = custom.Name,
            Comment = "Claimed from group"
        });
    }

    private static void Transition(StepInstance step, StepStatus nextStatus, string userId, string? comment, NodeDefinition node, ProcessDefinition definition)
    {
        var semantic = MapSemantic(nextStatus);
        var custom = ResolveCustomStatus(node, definition, semantic);
        step.Status = nextStatus;
        step.CustomStatusKey = custom.Key;
        step.CustomStatusName = custom.Name;
        step.History.Add(new StepStatusHistoryItem
        {
            ChangedBy = userId,
            NewStatus = nextStatus,
            CustomStatusKey = custom.Key,
            CustomStatusName = custom.Name,
            Comment = comment
        });
    }

    private async Task ApplyBlockAndProcessTransitionsAsync(ProcessInstance instance, CancellationToken cancellationToken)
    {
        var byBlock = instance.Steps
            .Where(x => x.ParentBlockNodeId.HasValue)
            .GroupBy(x => x.ParentBlockNodeId!.Value)
            .ToList();

        foreach (var block in byBlock)
        {
            var steps = block.ToList();
            var anyRework = steps.Any(s => s.Status == StepStatus.ReworkRequested);
            var allDone = steps.All(s => s.Status is StepStatus.Approved or StepStatus.Rejected or StepStatus.ReworkRequested);

            if (anyRework && allDone)
            {
                instance.State = "ReworkRequested";
                continue;
            }
        }

        if (instance.Steps.All(x => x.Status == StepStatus.Approved))
        {
            instance.State = "Approved";
        }
        else if (instance.Steps.Any(x => x.Status == StepStatus.Rejected))
        {
            instance.State = "Rejected";
        }

        await RecalculateDynamicAssignmentsAsync(instance, cancellationToken);
        await SendStatusNotificationsAsync(instance, cancellationToken);
    }

    private async Task RecalculateDynamicAssignmentsAsync(ProcessInstance instance, CancellationToken cancellationToken)
    {
        await _parallelismGate.WaitAsync(cancellationToken);
        try
        {
            foreach (var step in instance.Steps.Where(s => s.Status is StepStatus.Pending or StepStatus.Unclaimed or StepStatus.InProgress))
            {
                var groupAssignments = step.Assignments.Where(a => a.Type == AssigneeType.Group).ToList();
                if (groupAssignments.Count == 0)
                {
                    continue;
                }

                var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var assignment in groupAssignments)
                {
                    var members = await _groupProvider.GetGroupMembersAsync(assignment.PrincipalId, cancellationToken);
                    foreach (var member in members)
                    {
                        users.Add(member);
                    }
                }

                step.Assignments = users.Select(x => new Assignment
                {
                    Type = AssigneeType.User,
                    PrincipalId = x
                }).ToList();
            }
        }
        finally
        {
            _parallelismGate.Release();
        }
    }

    private async Task SendStatusNotificationsAsync(ProcessInstance instance, CancellationToken cancellationToken)
    {
        foreach (var step in instance.Steps)
        {
            if (step.History.Count == 0)
            {
                continue;
            }

            var last = step.History[^1];
            if (last.NewStatus is not (StepStatus.Approved or StepStatus.Rejected or StepStatus.ReworkRequested))
            {
                continue;
            }

            foreach (var assignment in step.Assignments)
            {
                await _notificationService.SendAsync(new NotificationMessage
                {
                    TemplateKey = "default-step-status",
                    UserId = assignment.PrincipalId,
                    Subject = $"Step {step.Name} changed",
                    Body = $"Step status is now {last.NewStatus}."
                }, cancellationToken);
            }
        }
    }

    private static IEnumerable<NodeDefinition> FlattenNodes(IEnumerable<NodeDefinition> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in FlattenNodes(root.Children))
            {
                yield return child;
            }
        }
    }

    private static WorkflowStatusSemantic MapSemantic(StepStatus status) =>
        status switch
        {
            StepStatus.Pending => WorkflowStatusSemantic.Pending,
            StepStatus.Unclaimed => WorkflowStatusSemantic.Unclaimed,
            StepStatus.InProgress => WorkflowStatusSemantic.InProgress,
            StepStatus.Approved => WorkflowStatusSemantic.Approved,
            StepStatus.Rejected => WorkflowStatusSemantic.Rejected,
            StepStatus.ReworkRequested => WorkflowStatusSemantic.ReworkRequested,
            StepStatus.Cancelled => WorkflowStatusSemantic.Cancelled,
            StepStatus.Expired => WorkflowStatusSemantic.Expired,
            _ => WorkflowStatusSemantic.Pending
        };

    private static (string Key, string Name) ResolveCustomStatus(NodeDefinition node, ProcessDefinition definition, WorkflowStatusSemantic semantic)
    {
        var boundKey = node.StatusBindings.FirstOrDefault(x => x.Semantic == semantic)?.StatusKey;
        if (!string.IsNullOrWhiteSpace(boundKey))
        {
            var status = definition.Statuses.FirstOrDefault(x => string.Equals(x.Key, boundKey, StringComparison.OrdinalIgnoreCase));
            if (status is not null)
            {
                return (status.Key, status.DisplayName);
            }
        }

        var semanticDefault = definition.Statuses.FirstOrDefault(x => x.Semantic == semantic && x.IsDefault);
        if (semanticDefault is not null)
        {
            return (semanticDefault.Key, semanticDefault.DisplayName);
        }

        var anySemantic = definition.Statuses.FirstOrDefault(x => x.Semantic == semantic);
        if (anySemantic is not null)
        {
            return (anySemantic.Key, anySemantic.DisplayName);
        }

        return (semantic.ToString().ToLowerInvariant(), semantic.ToString());
    }

    private static void ApplyInitialCustomStatuses(ProcessDefinition definition, ProcessInstance instance)
    {
        var nodeMap = FlattenNodes(definition.Nodes).ToDictionary(x => x.Id);
        foreach (var step in instance.Steps)
        {
            if (!nodeMap.TryGetValue(step.NodeId, out var node))
            {
                continue;
            }

            var semantic = MapSemantic(step.Status);
            var custom = ResolveCustomStatus(node, definition, semantic);
            step.CustomStatusKey = custom.Key;
            step.CustomStatusName = custom.Name;
        }
    }
}
