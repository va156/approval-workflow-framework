using Workflow.Abstractions;

namespace Workflow.Engine;

/// <summary>
/// In-memory definition repository for tests and local demos.
/// </summary>
public sealed class InMemoryProcessDefinitionRepository : IProcessDefinitionRepository
{
    private readonly List<ProcessDefinition> _definitions = [];

    /// <inheritdoc />
    public Task<ProcessDefinition?> GetPublishedByKeyAsync(string processKey, CancellationToken cancellationToken)
    {
        var value = _definitions
            .Where(x => x.ProcessKey == processKey && x.Status == ProcessDefinitionStatus.Published)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public Task<ProcessDefinition?> GetByIdAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_definitions.FirstOrDefault(x => x.Id == definitionId));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessDefinition>> GetByKeyAsync(string processKey, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ProcessDefinition>>(_definitions.Where(x => x.ProcessKey == processKey).ToList());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessDefinition>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ProcessDefinition>>(_definitions.ToList());
    }

    /// <inheritdoc />
    public Task UpsertAsync(ProcessDefinition definition, CancellationToken cancellationToken)
    {
        var existing = _definitions.FindIndex(x => x.Id == definition.Id);
        if (existing >= 0)
        {
            _definitions[existing] = definition;
            return Task.CompletedTask;
        }

        _definitions.Add(definition);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory process instance repository for tests and local demos.
/// </summary>
public sealed class InMemoryProcessInstanceRepository : IProcessInstanceRepository
{
    private readonly List<ProcessInstance> _instances = [];

    /// <inheritdoc />
    public Task<ProcessInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken) =>
        Task.FromResult(_instances.FirstOrDefault(x => x.Id == instanceId));

    /// <inheritdoc />
    public Task<ProcessInstance?> GetByStepIdAsync(Guid stepId, CancellationToken cancellationToken) =>
        Task.FromResult(_instances.FirstOrDefault(x => x.Steps.Any(s => s.Id == stepId)));

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessInstance>> GetRecentAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProcessInstance>>(
            _instances
                .OrderByDescending(x => x.Steps.Select(s => s.History.LastOrDefault()?.ChangedAtUtc ?? s.CreatedAtUtc).DefaultIfEmpty().Max())
                .Take(Math.Max(take, 1))
                .ToList());

    /// <inheritdoc />
    public Task UpsertAsync(ProcessInstance instance, CancellationToken cancellationToken)
    {
        var existing = _instances.FindIndex(x => x.Id == instance.Id);
        if (existing >= 0)
        {
            _instances[existing] = instance;
            return Task.CompletedTask;
        }

        _instances.Add(instance);
        return Task.CompletedTask;
    }
}

/// <summary>
/// No-op unit of work implementation for in-memory mode.
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
