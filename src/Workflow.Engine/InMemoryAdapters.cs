using Workflow.Abstractions;

namespace Workflow.Engine;

public sealed class InMemoryProcessDefinitionRepository : IProcessDefinitionRepository
{
    private readonly List<ProcessDefinition> _definitions = [];

    public Task<ProcessDefinition?> GetPublishedByKeyAsync(string processKey, CancellationToken cancellationToken)
    {
        var value = _definitions
            .Where(x => x.ProcessKey == processKey && x.Status == ProcessDefinitionStatus.Published)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();
        return Task.FromResult(value);
    }

    public Task<ProcessDefinition?> GetByIdAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_definitions.FirstOrDefault(x => x.Id == definitionId));
    }

    public Task<IReadOnlyList<ProcessDefinition>> GetByKeyAsync(string processKey, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ProcessDefinition>>(_definitions.Where(x => x.ProcessKey == processKey).ToList());
    }

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

public sealed class InMemoryProcessInstanceRepository : IProcessInstanceRepository
{
    private readonly List<ProcessInstance> _instances = [];

    public Task<ProcessInstance?> GetByIdAsync(Guid instanceId, CancellationToken cancellationToken) =>
        Task.FromResult(_instances.FirstOrDefault(x => x.Id == instanceId));

    public Task<ProcessInstance?> GetByStepIdAsync(Guid stepId, CancellationToken cancellationToken) =>
        Task.FromResult(_instances.FirstOrDefault(x => x.Steps.Any(s => s.Id == stepId)));

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

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
