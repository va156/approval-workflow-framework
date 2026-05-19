using Microsoft.Extensions.Logging.Abstractions;
using Workflow.Abstractions;
using Workflow.Engine;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IProcessDefinitionRepository, InMemoryProcessDefinitionRepository>();
builder.Services.AddSingleton<IProcessInstanceRepository, InMemoryProcessInstanceRepository>();
builder.Services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
builder.Services.AddSingleton<IGroupProvider>(new StaticGroupProvider(new Dictionary<string, List<string>>
{
    ["HR_GROUP"] = ["hr.alex", "hr.irina"],
    ["TEAMLEAD_GROUP"] = ["lead.boris", "lead.elena"]
}));
builder.Services.AddSingleton<IBusinessCalendar, WeekdayBusinessCalendar>();
builder.Services.AddSingleton<IConditionEvaluator, FallbackConditionEvaluator>();
builder.Services.AddSingleton<INotificationService, NullNotificationService>();
builder.Services.AddSingleton<IWorkflowEngine>(sp =>
    new WorkflowEngine(
        sp.GetRequiredService<IProcessDefinitionRepository>(),
        sp.GetRequiredService<IProcessInstanceRepository>(),
        sp.GetRequiredService<IUnitOfWork>(),
        sp.GetRequiredService<IGroupProvider>(),
        sp.GetRequiredService<IBusinessCalendar>(),
        sp.GetRequiredService<IConditionEvaluator>(),
        sp.GetRequiredService<INotificationService>(),
        NullLogger<WorkflowEngine>.Instance));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/vacation/seed", async (IProcessDefinitionRepository repo, IUnitOfWork uow, CancellationToken ct) =>
{
    var definition = BuildVacationProcess();
    await repo.UpsertAsync(definition, ct);
    await uow.CommitAsync(ct);
    return Results.Ok(definition);
});

app.MapPost("/vacation/preview", async (Dictionary<string, object?> payload, IWorkflowEngine engine, CancellationToken ct) =>
{
    var result = await engine.PreviewAsync(new PreviewRequest
    {
        ProcessKey = "vacation_approval",
        DraftData = payload
    }, ct);
    return Results.Ok(result);
});

app.MapPost("/vacation/submit/{requestId}", async (string requestId, Dictionary<string, object?> payload, IWorkflowEngine engine, CancellationToken ct) =>
{
    var result = await engine.StartAsync(new StartProcessRequest
    {
        ProcessKey = "vacation_approval",
        RequestId = requestId,
        RequestData = payload
    }, ct);
    return Results.Ok(result);
});

app.Run();

static ProcessDefinition BuildVacationProcess()
{
    return new ProcessDefinition
    {
        ProcessKey = "vacation_approval",
        Name = "Vacation Approval",
        Version = 1,
        Status = ProcessDefinitionStatus.Published,
        Statuses =
        [
            new WorkflowStatusDefinition { Key = "queued", DisplayName = "Queued", Semantic = WorkflowStatusSemantic.Pending, IsDefault = true },
            new WorkflowStatusDefinition { Key = "awaiting-claim", DisplayName = "Awaiting Claim", Semantic = WorkflowStatusSemantic.Unclaimed, IsDefault = true },
            new WorkflowStatusDefinition { Key = "under-review", DisplayName = "Under Review", Semantic = WorkflowStatusSemantic.InProgress, IsDefault = true },
            new WorkflowStatusDefinition { Key = "completed", DisplayName = "Completed", Semantic = WorkflowStatusSemantic.Approved, IsDefault = true },
            new WorkflowStatusDefinition { Key = "declined", DisplayName = "Declined", Semantic = WorkflowStatusSemantic.Rejected, IsDefault = true },
            new WorkflowStatusDefinition { Key = "returned", DisplayName = "Returned For Rework", Semantic = WorkflowStatusSemantic.ReworkRequested, IsDefault = true },
            new WorkflowStatusDefinition { Key = "cancelled", DisplayName = "Cancelled", Semantic = WorkflowStatusSemantic.Cancelled, IsDefault = true },
            new WorkflowStatusDefinition { Key = "expired", DisplayName = "Expired", Semantic = WorkflowStatusSemantic.Expired, IsDefault = true }
        ],
        Nodes =
        [
            new NodeDefinition
            {
                Type = NodeType.Step,
                Name = "Manager Approval",
                SortOrder = 1,
                DeadlineWorkingDays = 2,
                StatusBindings =
                [
                    new StepStatusBinding { Semantic = WorkflowStatusSemantic.Pending, StatusKey = "queued" },
                    new StepStatusBinding { Semantic = WorkflowStatusSemantic.Approved, StatusKey = "completed" },
                    new StepStatusBinding { Semantic = WorkflowStatusSemantic.Rejected, StatusKey = "declined" },
                    new StepStatusBinding { Semantic = WorkflowStatusSemantic.ReworkRequested, StatusKey = "returned" }
                ],
                AssigneeRule = new AssigneeRule
                {
                    Type = AssigneeType.DynamicField,
                    Value = "ManagerId"
                }
            },
            new NodeDefinition
            {
                Type = NodeType.Block,
                Name = "Parallel Checks",
                SortOrder = 2,
                BlockCompletionPolicy = BlockCompletionPolicy.All,
                ReworkPolicy = ReworkPolicy.Deferred,
                Children =
                [
                    new NodeDefinition
                    {
                        Type = NodeType.Step,
                        Name = "HR Quota Check",
                        SortOrder = 1,
                        DeadlineWorkingDays = 1,
                        StatusBindings =
                        [
                            new StepStatusBinding { Semantic = WorkflowStatusSemantic.Unclaimed, StatusKey = "awaiting-claim" },
                            new StepStatusBinding { Semantic = WorkflowStatusSemantic.InProgress, StatusKey = "under-review" },
                            new StepStatusBinding { Semantic = WorkflowStatusSemantic.Approved, StatusKey = "completed" }
                        ],
                        AssigneeRule = new AssigneeRule { Type = AssigneeType.Group, Value = "HR_GROUP" }
                    },
                    new NodeDefinition
                    {
                        Type = NodeType.Step,
                        Name = "Team Lead Substitution Check",
                        SortOrder = 2,
                        DeadlineWorkingDays = 1,
                        StatusBindings =
                        [
                            new StepStatusBinding { Semantic = WorkflowStatusSemantic.Unclaimed, StatusKey = "awaiting-claim" },
                            new StepStatusBinding { Semantic = WorkflowStatusSemantic.InProgress, StatusKey = "under-review" },
                            new StepStatusBinding { Semantic = WorkflowStatusSemantic.Approved, StatusKey = "completed" }
                        ],
                        AssigneeRule = new AssigneeRule { Type = AssigneeType.Group, Value = "TEAMLEAD_GROUP" }
                    }
                ]
            }
        ]
    };
}
