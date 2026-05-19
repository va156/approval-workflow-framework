using Microsoft.Extensions.Logging.Abstractions;
using Workflow.Abstractions;
using Workflow.Engine;
using Xunit;

namespace Workflow.Engine.Tests;

public sealed class WorkflowEngineTests
{
    [Fact]
    public async Task Preview_Resolves_DynamicField_Assignee()
    {
        var engine = await CreateEngineAsync();
        await SeedDefinitionAsync(engine.DefinitionRepository, "vacation", 1, ProcessDefinitionStatus.Published);

        var preview = await engine.Engine.PreviewAsync(new PreviewRequest
        {
            ProcessKey = "vacation",
            DraftData = new Dictionary<string, object?> { ["ManagerId"] = "manager.1" }
        }, CancellationToken.None);

        Assert.Contains(preview.Steps, s => s.Name == "Manager Approval" && s.ResponsibleUsers.Contains("manager.1"));
        Assert.Contains(preview.Steps, s => s.Name == "Manager Approval" && s.CustomStatusKey == "queued");
    }

    [Fact]
    public async Task Preview_Skips_Conditional_Steps_When_NotMatched()
    {
        var engine = await CreateEngineAsync(includeConditionalStep: true);
        await SeedDefinitionAsync(engine.DefinitionRepository, "vacation", 1, ProcessDefinitionStatus.Published, includeConditionalStep: true);

        var preview = await engine.Engine.PreviewAsync(new PreviewRequest
        {
            ProcessKey = "vacation",
            DraftData = new Dictionary<string, object?> { ["NeedsCeo"] = false, ["ManagerId"] = "manager.1" }
        }, CancellationToken.None);

        Assert.DoesNotContain(preview.Steps, s => s.Name == "CEO Approval");
    }

    [Fact]
    public async Task Start_Uses_Latest_Published_Version()
    {
        var engine = await CreateEngineAsync();
        await SeedDefinitionAsync(engine.DefinitionRepository, "vacation", 1, ProcessDefinitionStatus.Archived);
        await SeedDefinitionAsync(engine.DefinitionRepository, "vacation", 2, ProcessDefinitionStatus.Published);

        var result = await engine.Engine.StartAsync(new StartProcessRequest
        {
            ProcessKey = "vacation",
            RequestId = "REQ-1",
            RequestData = new Dictionary<string, object?> { ["ManagerId"] = "manager.1" }
        }, CancellationToken.None);

        Assert.Equal(2, result.DefinitionVersion);
    }

    [Fact]
    public async Task Claim_From_Group_Assignment_Works()
    {
        var ctx = await CreateEngineAsync();
        await SeedDefinitionAsync(ctx.DefinitionRepository, "vacation", 1, ProcessDefinitionStatus.Published);
        var started = await ctx.Engine.StartAsync(new StartProcessRequest
        {
            ProcessKey = "vacation",
            RequestId = "REQ-2",
            RequestData = new Dictionary<string, object?> { ["ManagerId"] = "manager.1" }
        }, CancellationToken.None);

        var groupStep = started.ActiveSteps.First(s => s.StepName == "HR Quota Check");
        var claimed = await ctx.Engine.ExecuteStepActionAsync(new StepActionRequest
        {
            StepId = groupStep.StepId,
            Action = StepActionType.Claim,
            UserId = "hr.1"
        }, CancellationToken.None);

        Assert.Equal(StepStatus.InProgress, claimed.StepStatus);
        Assert.Equal("under-review", claimed.CustomStatusKey);
    }

    [Fact]
    public async Task Approving_All_Steps_Approves_Process()
    {
        var ctx = await CreateEngineAsync();
        await SeedDefinitionAsync(ctx.DefinitionRepository, "vacation", 1, ProcessDefinitionStatus.Published);
        var started = await ctx.Engine.StartAsync(new StartProcessRequest
        {
            ProcessKey = "vacation",
            RequestId = "REQ-3",
            RequestData = new Dictionary<string, object?> { ["ManagerId"] = "manager.1" }
        }, CancellationToken.None);

        StepActionResult? final = null;
        foreach (var step in started.ActiveSteps)
        {
            final = await ctx.Engine.ExecuteStepActionAsync(new StepActionRequest
            {
                StepId = step.StepId,
                Action = StepActionType.Approve,
                UserId = step.ResponsibleUsers.FirstOrDefault() ?? "user"
            }, CancellationToken.None);
        }
        Assert.NotNull(final);
        Assert.Equal("Approved", final!.ProcessState);
        Assert.Equal("completed", final.CustomStatusKey);
    }

    [Fact]
    public async Task Rejecting_Any_Step_Rejects_Process()
    {
        var ctx = await CreateEngineAsync();
        await SeedDefinitionAsync(ctx.DefinitionRepository, "vacation", 1, ProcessDefinitionStatus.Published);
        var started = await ctx.Engine.StartAsync(new StartProcessRequest
        {
            ProcessKey = "vacation",
            RequestId = "REQ-4",
            RequestData = new Dictionary<string, object?> { ["ManagerId"] = "manager.1" }
        }, CancellationToken.None);

        var manager = started.ActiveSteps.First(s => s.StepName == "Manager Approval");
        var result = await ctx.Engine.ExecuteStepActionAsync(new StepActionRequest
        {
            StepId = manager.StepId,
            Action = StepActionType.Reject,
            UserId = "manager.1"
        }, CancellationToken.None);

        Assert.Equal("Rejected", result.ProcessState);
        Assert.Equal("declined", result.CustomStatusKey);
    }

    [Fact]
    public async Task Rework_In_Block_Leads_To_ReworkRequested_State()
    {
        var ctx = await CreateEngineAsync();
        await SeedDefinitionAsync(ctx.DefinitionRepository, "vacation", 1, ProcessDefinitionStatus.Published);
        var started = await ctx.Engine.StartAsync(new StartProcessRequest
        {
            ProcessKey = "vacation",
            RequestId = "REQ-5",
            RequestData = new Dictionary<string, object?> { ["ManagerId"] = "manager.1" }
        }, CancellationToken.None);

        var blockSteps = started.ActiveSteps.Where(s => s.StepName is "HR Quota Check" or "Team Lead Substitution Check").ToList();
        await ctx.Engine.ExecuteStepActionAsync(new StepActionRequest
        {
            StepId = blockSteps[0].StepId,
            Action = StepActionType.Approve,
            UserId = blockSteps[0].ResponsibleUsers.First()
        }, CancellationToken.None);
        var result = await ctx.Engine.ExecuteStepActionAsync(new StepActionRequest
        {
            StepId = blockSteps[1].StepId,
            Action = StepActionType.Rework,
            UserId = blockSteps[1].ResponsibleUsers.First()
        }, CancellationToken.None);

        Assert.Equal("ReworkRequested", result.ProcessState);
        Assert.Equal("returned", result.CustomStatusKey);
    }

    [Fact]
    public void BusinessCalendar_Skips_Weekends()
    {
        var calendar = new WeekdayBusinessCalendar();
        var friday = new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero);
        var deadline = calendar.AddWorkingDays(friday, 1);
        Assert.Equal(DayOfWeek.Monday, deadline.DayOfWeek);
    }

    [Fact]
    public Task Definition_Validation_Fails_On_Unknown_Bound_Status()
    {
        var definition = BuildDefinition("vacation", 1, ProcessDefinitionStatus.Draft, includeConditionalStep: false);
        definition.Nodes[0].StatusBindings.Add(new StepStatusBinding
        {
            Semantic = WorkflowStatusSemantic.Pending,
            StatusKey = "does-not-exist"
        });

        var errors = WorkflowDefinitionValidator.Validate(definition);
        Assert.NotEmpty(errors);
        return Task.CompletedTask;
    }

    private static Task SeedDefinitionAsync(
        IProcessDefinitionRepository repository,
        string key,
        int version,
        ProcessDefinitionStatus status,
        bool includeConditionalStep = false)
    {
        var definition = BuildDefinition(key, version, status, includeConditionalStep);

        WorkflowDefinitionValidator.EnsureDefaultStatuses(definition);
        var errors = WorkflowDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }

        return repository.UpsertAsync(definition, CancellationToken.None);
    }

    private static Task<TestEngineContext> CreateEngineAsync(bool includeConditionalStep = false)
    {
        var definitionRepo = new InMemoryProcessDefinitionRepository();
        var instanceRepo = new InMemoryProcessInstanceRepository();
        var groups = new StaticGroupProvider(new Dictionary<string, List<string>>
        {
            ["HR_GROUP"] = ["hr.1", "hr.2"],
            ["TEAMLEAD_GROUP"] = ["lead.1", "lead.2"]
        });

        var engine = new WorkflowEngine(
            definitionRepo,
            instanceRepo,
            new InMemoryUnitOfWork(),
            groups,
            new WeekdayBusinessCalendar(),
            new FallbackConditionEvaluator(),
            new NullNotificationService(),
            NullLogger<WorkflowEngine>.Instance);

        return Task.FromResult(new TestEngineContext(engine, definitionRepo));
    }

    private static ProcessDefinition BuildDefinition(string key, int version, ProcessDefinitionStatus status, bool includeConditionalStep)
    {
        var definition = new ProcessDefinition
        {
            ProcessKey = key,
            Name = $"Vacation v{version}",
            Version = version,
            Status = status,
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
                    AssigneeRule = new AssigneeRule { Type = AssigneeType.DynamicField, Value = "ManagerId" }
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
                            StatusBindings =
                            [
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.Unclaimed, StatusKey = "awaiting-claim" },
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.InProgress, StatusKey = "under-review" },
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.Approved, StatusKey = "completed" },
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.ReworkRequested, StatusKey = "returned" }
                            ],
                            AssigneeRule = new AssigneeRule { Type = AssigneeType.Group, Value = "HR_GROUP" }
                        },
                        new NodeDefinition
                        {
                            Type = NodeType.Step,
                            Name = "Team Lead Substitution Check",
                            SortOrder = 2,
                            StatusBindings =
                            [
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.Unclaimed, StatusKey = "awaiting-claim" },
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.InProgress, StatusKey = "under-review" },
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.Approved, StatusKey = "completed" },
                                new StepStatusBinding { Semantic = WorkflowStatusSemantic.ReworkRequested, StatusKey = "returned" }
                            ],
                            AssigneeRule = new AssigneeRule { Type = AssigneeType.Group, Value = "TEAMLEAD_GROUP" }
                        }
                    ]
                }
            ]
        };

        if (includeConditionalStep)
        {
            definition.Nodes.Add(new NodeDefinition
            {
                Type = NodeType.Step,
                Name = "CEO Approval",
                SortOrder = 3,
                ConditionExpression = "NeedsCeo == true",
                StatusBindings =
                [
                    new StepStatusBinding { Semantic = WorkflowStatusSemantic.Pending, StatusKey = "queued" },
                    new StepStatusBinding { Semantic = WorkflowStatusSemantic.Approved, StatusKey = "completed" }
                ],
                AssigneeRule = new AssigneeRule { Type = AssigneeType.User, Value = "ceo.1" }
            });
        }

        return definition;
    }

    private sealed record TestEngineContext(IWorkflowEngine Engine, IProcessDefinitionRepository DefinitionRepository);
}
