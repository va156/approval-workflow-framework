namespace Workflow.Abstractions;

public interface IWorkflowEngine
{
    Task<WorkflowPreviewResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken);
    Task<StartProcessResult> StartAsync(StartProcessRequest request, CancellationToken cancellationToken);
    Task<StepActionResult> ExecuteStepActionAsync(StepActionRequest request, CancellationToken cancellationToken);
}

public interface IProcessDefinitionRepository
{
    Task<ProcessDefinition?> GetPublishedByKeyAsync(string processKey, CancellationToken cancellationToken);
    Task<ProcessDefinition?> GetByIdAsync(Guid definitionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessDefinition>> GetByKeyAsync(string processKey, CancellationToken cancellationToken);
    Task UpsertAsync(ProcessDefinition definition, CancellationToken cancellationToken);
}

public interface IProcessInstanceRepository
{
    Task<ProcessInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken);
    Task<ProcessInstance?> GetByStepIdAsync(Guid stepId, CancellationToken cancellationToken);
    Task UpsertAsync(ProcessInstance instance, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IIdentityProvider
{
    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken);
}

public interface IGroupProvider
{
    Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken);
}

public interface IRequestDataProvider
{
    Task<IReadOnlyDictionary<string, object?>> GetRequestDataAsync(string requestId, CancellationToken cancellationToken);
}

public interface IBusinessCalendar
{
    DateTimeOffset AddWorkingDays(DateTimeOffset startUtc, int days);
}

public interface IConditionEvaluator
{
    bool Evaluate(string expression, IReadOnlyDictionary<string, object?> data);
}

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}
