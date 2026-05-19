using System.Text;
using Microsoft.AspNetCore.Mvc;
using Workflow.Abstractions;
using Workflow.Engine;
using Workflow.Persistence.EFCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddWorkflowEngine();
builder.Services.AddWorkflowPersistenceSqlServer(
    builder.Configuration.GetConnectionString("WorkflowPrimary")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=WorkflowEngine;Trusted_Connection=True;TrustServerCertificate=True");
builder.Services.AddSingleton<IGroupProvider>(new StaticGroupProvider(new Dictionary<string, List<string>>
{
    ["HR_GROUP"] = ["user.hr.1", "user.hr.2"],
    ["TEAMLEAD_GROUP"] = ["user.lead.1", "user.lead.2"]
}));

var app = builder.Build();

app.MapGet("/", () =>
{
    var html = """
               <html>
               <head><title>Workflow Admin MVP</title></head>
               <body style='font-family:Segoe UI;max-width:900px;margin:40px auto;'>
               <h2>Workflow Admin MVP</h2>
               <p>Use JSON endpoints to configure and preview workflows.</p>
               <ul>
                 <li>POST /admin/definitions</li>
                 <li>POST /admin/definitions/{id}/publish</li>
                 <li>POST /admin/definitions/{id}/statuses</li>
                 <li>POST /admin/preview</li>
               </ul>
               </body>
               </html>
               """;
    return Results.Content(html, "text/html", Encoding.UTF8);
});

app.MapPost("/admin/definitions", async (
    [FromBody] AdminCreateDefinitionRequest request,
    IProcessDefinitionRepository repo,
    IUnitOfWork unitOfWork,
    CancellationToken ct) =>
{
    var versions = await repo.GetByKeyAsync(request.ProcessKey, ct);
    var definition = new ProcessDefinition
    {
        ProcessKey = request.ProcessKey,
        Name = request.Name,
        Version = versions.Count == 0 ? 1 : versions.Max(x => x.Version) + 1,
        Status = ProcessDefinitionStatus.Draft,
        Statuses = request.Statuses,
        Nodes = request.Nodes
    };
    WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
    var errors = WorkflowDefinitionValidator.Validate(definition);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }
    await repo.UpsertAsync(definition, ct);
    await unitOfWork.CommitAsync(ct);
    return Results.Ok(definition);
});

app.MapPost("/admin/definitions/{id:guid}/publish", async (
    Guid id,
    IProcessDefinitionRepository repo,
    IUnitOfWork unitOfWork,
    CancellationToken ct) =>
{
    var definition = await repo.GetByIdAsync(id, ct);
    if (definition is null)
    {
        return Results.NotFound();
    }
    WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
    var validationErrors = WorkflowDefinitionValidator.Validate(definition);
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new { errors = validationErrors });
    }

    var all = await repo.GetByKeyAsync(definition.ProcessKey, ct);
    foreach (var item in all.Where(x => x.Status == ProcessDefinitionStatus.Published))
    {
        item.Status = ProcessDefinitionStatus.Archived;
        await repo.UpsertAsync(item, ct);
    }

    definition.Status = ProcessDefinitionStatus.Published;
    await repo.UpsertAsync(definition, ct);
    await unitOfWork.CommitAsync(ct);
    return Results.NoContent();
});

app.MapPost("/admin/preview", async ([FromBody] AdminPreviewRequest request, IWorkflowEngine engine, CancellationToken ct) =>
{
    var result = await engine.PreviewAsync(new PreviewRequest
    {
        ProcessKey = request.ProcessKey,
        DraftData = request.DraftData
    }, ct);
    return Results.Ok(result);
});

app.MapPost("/admin/definitions/{id:guid}/statuses", async (
    Guid id,
    [FromBody] AdminUpsertStatusRequest request,
    IProcessDefinitionRepository repo,
    IUnitOfWork unitOfWork,
    CancellationToken ct) =>
{
    var definition = await repo.GetByIdAsync(id, ct);
    if (definition is null)
    {
        return Results.NotFound();
    }

    WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
    if (definition.Statuses.Any(x => string.Equals(x.Key, request.Key, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict($"Status key '{request.Key}' already exists.");
    }

    definition.Statuses.Add(new WorkflowStatusDefinition
    {
        Key = request.Key,
        DisplayName = request.DisplayName,
        Description = request.Description,
        Semantic = request.Semantic,
        IsDefault = request.IsDefault
    });

    var errors = WorkflowDefinitionValidator.Validate(definition);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    await repo.UpsertAsync(definition, ct);
    await unitOfWork.CommitAsync(ct);
    return Results.Ok(definition.Statuses);
});

app.Run();

public sealed class AdminCreateDefinitionRequest
{
    public required string ProcessKey { get; init; }
    public required string Name { get; init; }
    public List<WorkflowStatusDefinition> Statuses { get; init; } = [];
    public required List<NodeDefinition> Nodes { get; init; }
}

public sealed class AdminPreviewRequest
{
    public required string ProcessKey { get; init; }
    public required Dictionary<string, object?> DraftData { get; init; }
}

public sealed class AdminUpsertStatusRequest
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public WorkflowStatusSemantic Semantic { get; init; }
    public bool IsDefault { get; init; }
}
