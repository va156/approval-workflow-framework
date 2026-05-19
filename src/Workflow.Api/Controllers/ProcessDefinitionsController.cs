using Microsoft.AspNetCore.Mvc;
using Workflow.Abstractions;

namespace Workflow.Api.Controllers;

/// <summary>
/// Endpoints for process definition versioning and custom status management.
/// </summary>
[ApiController]
[Route("process-definitions")]
public sealed class ProcessDefinitionsController(IProcessDefinitionRepository repository, IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>
    /// Creates a new draft process definition version.
    /// </summary>
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

    /// <summary>
    /// Updates an existing draft definition.
    /// </summary>
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

    /// <summary>
    /// Publishes selected definition and archives previous published version.
    /// </summary>
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

    /// <summary>
    /// Returns specific definition version for a process key.
    /// </summary>
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

    /// <summary>
    /// Returns status catalog configured for definition.
    /// </summary>
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

    /// <summary>
    /// Adds custom status to definition catalog.
    /// </summary>
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

    /// <summary>
    /// Updates existing status entry in definition catalog.
    /// </summary>
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

    /// <summary>
    /// Deletes status from definition catalog when it is not used by step bindings.
    /// </summary>
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

/// <summary>
/// Payload for creating a new definition draft.
/// </summary>
public sealed class CreateDefinitionRequest
{
    /// <summary>
    /// Stable process identifier used to resolve published versions.
    /// </summary>
    public required string ProcessKey { get; init; }

    /// <summary>
    /// Human-friendly process name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional status catalog configured by administrators.
    /// </summary>
    public List<WorkflowStatusDefinition> Statuses { get; init; } = [];

    /// <summary>
    /// Configured process nodes including steps and blocks.
    /// </summary>
    public required List<NodeDefinition> Nodes { get; init; }
}

/// <summary>
/// Payload for updating process definition content.
/// </summary>
public sealed class UpdateDefinitionRequest
{
    /// <summary>
    /// Human-friendly process name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Definition-level status catalog.
    /// </summary>
    public List<WorkflowStatusDefinition> Statuses { get; init; } = [];

    /// <summary>
    /// Process nodes including steps and blocks.
    /// </summary>
    public required List<NodeDefinition> Nodes { get; init; }
}

/// <summary>
/// Payload for creating or updating status entries.
/// </summary>
public sealed class UpsertStatusRequest
{
    /// <summary>
    /// Unique status key in definition scope.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Display text shown to end users.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional status description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Technical semantic mapped to runtime transitions.
    /// </summary>
    public WorkflowStatusSemantic Semantic { get; init; }

    /// <summary>
    /// Indicates whether status is fallback default for semantic.
    /// </summary>
    public bool IsDefault { get; init; }
}
