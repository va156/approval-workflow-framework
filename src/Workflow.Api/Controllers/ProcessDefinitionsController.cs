using Microsoft.AspNetCore.Mvc;
using Workflow.Abstractions;

namespace Workflow.Api.Controllers;

[ApiController]
[Route("process-definitions")]
public sealed class ProcessDefinitionsController(IProcessDefinitionRepository repository, IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProcessDefinition>> CreateDraft([FromBody] CreateDefinitionRequest request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByKeyAsync(request.ProcessKey, cancellationToken);
        var version = existing.Count == 0 ? 1 : existing.Max(x => x.Version) + 1;

        var definition = new ProcessDefinition
        {
            ProcessKey = request.ProcessKey,
            Name = request.Name,
            Version = version,
            Status = ProcessDefinitionStatus.Draft,
            Statuses = request.Statuses,
            Nodes = request.Nodes
        };
        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        var errors = WorkflowDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        await repository.UpsertAsync(definition, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Ok(definition);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProcessDefinition>> Update(Guid id, [FromBody] UpdateDefinitionRequest request, CancellationToken cancellationToken)
    {
        var definition = await repository.GetByIdAsync(id, cancellationToken);
        if (definition is null)
        {
            return NotFound();
        }

        definition.Name = request.Name;
        definition.Statuses = request.Statuses;
        definition.Nodes = request.Nodes;
        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        var errors = WorkflowDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }
        await repository.UpsertAsync(definition, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Ok(definition);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var definition = await repository.GetByIdAsync(id, cancellationToken);
        if (definition is null)
        {
            return NotFound();
        }

        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        var errors = WorkflowDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        var all = await repository.GetByKeyAsync(definition.ProcessKey, cancellationToken);
        foreach (var item in all.Where(x => x.Status == ProcessDefinitionStatus.Published))
        {
            item.Status = ProcessDefinitionStatus.Archived;
            await repository.UpsertAsync(item, cancellationToken);
        }

        definition.Status = ProcessDefinitionStatus.Published;
        await repository.UpsertAsync(definition, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/versions/{version:int}")]
    public async Task<ActionResult<ProcessDefinition>> GetVersion(Guid id, int version, CancellationToken cancellationToken)
    {
        var root = await repository.GetByIdAsync(id, cancellationToken);
        if (root is null)
        {
            return NotFound();
        }

        var all = await repository.GetByKeyAsync(root.ProcessKey, cancellationToken);
        var definition = all.FirstOrDefault(x => x.Version == version);
        return definition is null ? NotFound() : Ok(definition);
    }

    [HttpGet("{id:guid}/statuses")]
    public async Task<ActionResult<IReadOnlyList<WorkflowStatusDefinition>>> GetStatuses(Guid id, CancellationToken cancellationToken)
    {
        var definition = await repository.GetByIdAsync(id, cancellationToken);
        if (definition is null)
        {
            return NotFound();
        }

        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        return Ok(definition.Statuses);
    }

    [HttpPost("{id:guid}/statuses")]
    public async Task<ActionResult<WorkflowStatusDefinition>> CreateStatus(Guid id, [FromBody] UpsertStatusRequest request, CancellationToken cancellationToken)
    {
        var definition = await repository.GetByIdAsync(id, cancellationToken);
        if (definition is null)
        {
            return NotFound();
        }

        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        if (definition.Statuses.Any(x => string.Equals(x.Key, request.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict($"Status key '{request.Key}' already exists.");
        }

        var created = new WorkflowStatusDefinition
        {
            Key = request.Key,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Semantic = request.Semantic,
            IsDefault = request.IsDefault
        };
        definition.Statuses.Add(created);
        var errors = WorkflowDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        await repository.UpsertAsync(definition, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Ok(created);
    }

    [HttpPut("{id:guid}/statuses/{statusKey}")]
    public async Task<ActionResult<WorkflowStatusDefinition>> UpdateStatus(Guid id, string statusKey, [FromBody] UpsertStatusRequest request, CancellationToken cancellationToken)
    {
        var definition = await repository.GetByIdAsync(id, cancellationToken);
        if (definition is null)
        {
            return NotFound();
        }

        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        var existing = definition.Statuses.FirstOrDefault(x => string.Equals(x.Key, statusKey, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return NotFound();
        }

        existing.DisplayName = request.DisplayName;
        existing.Description = request.Description;
        existing.Semantic = request.Semantic;
        existing.IsDefault = request.IsDefault;

        var errors = WorkflowDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        await repository.UpsertAsync(definition, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return Ok(existing);
    }

    [HttpDelete("{id:guid}/statuses/{statusKey}")]
    public async Task<ActionResult> DeleteStatus(Guid id, string statusKey, CancellationToken cancellationToken)
    {
        var definition = await repository.GetByIdAsync(id, cancellationToken);
        if (definition is null)
        {
            return NotFound();
        }

        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        if (IsStatusUsed(definition.Nodes, statusKey))
        {
            return Conflict("Status is bound to one or more steps.");
        }

        var deleted = definition.Statuses.RemoveAll(x => string.Equals(x.Key, statusKey, StringComparison.OrdinalIgnoreCase));
        if (deleted == 0)
        {
            return NotFound();
        }

        var errors = WorkflowDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        await repository.UpsertAsync(definition, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return NoContent();
    }

    private static bool IsStatusUsed(IEnumerable<NodeDefinition> nodes, string statusKey)
    {
        foreach (var node in nodes)
        {
            if (node.StatusBindings.Any(x => string.Equals(x.StatusKey, statusKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (IsStatusUsed(node.Children, statusKey))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class CreateDefinitionRequest
{
    public required string ProcessKey { get; init; }
    public required string Name { get; init; }
    public List<WorkflowStatusDefinition> Statuses { get; init; } = [];
    public required List<NodeDefinition> Nodes { get; init; }
}

public sealed class UpdateDefinitionRequest
{
    public required string Name { get; init; }
    public List<WorkflowStatusDefinition> Statuses { get; init; } = [];
    public required List<NodeDefinition> Nodes { get; init; }
}

public sealed class UpsertStatusRequest
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public WorkflowStatusSemantic Semantic { get; init; }
    public bool IsDefault { get; init; }
}
