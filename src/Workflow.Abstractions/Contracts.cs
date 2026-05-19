namespace Workflow.Abstractions;

/// <summary>
/// Primary workflow orchestration contract used by API and integrations.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Calculates the future approval route without creating runtime records.
    /// </summary>
    Task<WorkflowPreviewResult> PreviewAsync(PreviewRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a workflow instance for a business request.
    /// </summary>
    Task<StartProcessResult> StartAsync(StartProcessRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Executes an action on an existing step instance.
    /// </summary>
    Task<StepActionResult> ExecuteStepActionAsync(StepActionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Repository abstraction for process definition storage.
/// </summary>
public interface IProcessDefinitionRepository
{
    /// <summary>
    /// Returns the latest published definition for a process key.
    /// </summary>
    Task<ProcessDefinition?> GetPublishedByKeyAsync(string processKey, CancellationToken cancellationToken);

    /// <summary>
    /// Returns definition by technical identifier.
    /// </summary>
    Task<ProcessDefinition?> GetByIdAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns all definition versions for a process key.
    /// </summary>
    Task<IReadOnlyList<ProcessDefinition>> GetByKeyAsync(string processKey, CancellationToken cancellationToken);

    /// <summary>
    /// Returns all process definitions across keys and versions.
    /// </summary>
    Task<IReadOnlyList<ProcessDefinition>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates a definition snapshot.
    /// </summary>
    Task UpsertAsync(ProcessDefinition definition, CancellationToken cancellationToken);
}

/// <summary>
/// Repository abstraction for runtime process instances.
/// </summary>
public interface IProcessInstanceRepository
{
    /// <summary>
    /// Returns runtime instance by identifier.
    /// </summary>
    Task<ProcessInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns runtime instance that owns the specified step.
    /// </summary>
    Task<ProcessInstance?> GetByStepIdAsync(Guid stepId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns recent runtime instances ordered by latest update descending.
    /// </summary>
    Task<IReadOnlyList<ProcessInstance>> GetRecentAsync(int take, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates a runtime instance snapshot.
    /// </summary>
    Task UpsertAsync(ProcessInstance instance, CancellationToken cancellationToken);
}

/// <summary>
/// Unit of work abstraction for committing storage operations.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists pending data changes.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Resolves user existence and identity-related checks.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Checks whether a user exists in underlying identity system.
    /// </summary>
    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves group members for dynamic assignment.
/// </summary>
public interface IGroupProvider
{
    /// <summary>
    /// Returns current user members of a logical group.
    /// </summary>
    Task<IReadOnlyList<string>> GetGroupMembersAsync(string groupId, CancellationToken cancellationToken);
}

/// <summary>
/// Provides business request data used in dynamic field resolution and conditions.
/// </summary>
public interface IRequestDataProvider
{
    /// <summary>
    /// Retrieves request payload by request identifier.
    /// </summary>
    Task<IReadOnlyDictionary<string, object?>> GetRequestDataAsync(string requestId, CancellationToken cancellationToken);
}

/// <summary>
/// Calendar service for business-day calculations.
/// </summary>
public interface IBusinessCalendar
{
    /// <summary>
    /// Adds working days to a UTC date while skipping non-working days.
    /// </summary>
    DateTimeOffset AddWorkingDays(DateTimeOffset startUtc, int days);
}

/// <summary>
/// Evaluates configured condition expressions against data.
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// Evaluates expression against input dictionary.
    /// </summary>
    bool Evaluate(string expression, IReadOnlyDictionary<string, object?> data);
}

/// <summary>
/// Repository abstraction for notification templates.
/// </summary>
public interface INotificationTemplateRepository
{
    /// <summary>
    /// Returns template by unique key.
    /// </summary>
    Task<NotificationTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// Sends workflow notifications to users.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends prepared notification message.
    /// </summary>
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}
