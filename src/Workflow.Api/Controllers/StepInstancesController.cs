using Microsoft.AspNetCore.Mvc;
using Workflow.Abstractions;

namespace Workflow.Api.Controllers;

[ApiController]
[Route("step-instances")]
public sealed class StepInstancesController(IWorkflowEngine engine) : ControllerBase
{
    [HttpPost("{id:guid}/claim")]
    public Task<StepActionResult> Claim(Guid id, [FromBody] StepActionApiRequest request, CancellationToken cancellationToken) =>
        Handle(id, StepActionType.Claim, request, cancellationToken);

    [HttpPost("{id:guid}/approve")]
    public Task<StepActionResult> Approve(Guid id, [FromBody] StepActionApiRequest request, CancellationToken cancellationToken) =>
        Handle(id, StepActionType.Approve, request, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    public Task<StepActionResult> Reject(Guid id, [FromBody] StepActionApiRequest request, CancellationToken cancellationToken) =>
        Handle(id, StepActionType.Reject, request, cancellationToken);

    [HttpPost("{id:guid}/rework")]
    public Task<StepActionResult> Rework(Guid id, [FromBody] StepActionApiRequest request, CancellationToken cancellationToken) =>
        Handle(id, StepActionType.Rework, request, cancellationToken);

    private Task<StepActionResult> Handle(Guid stepId, StepActionType action, StepActionApiRequest request, CancellationToken cancellationToken)
    {
        return engine.ExecuteStepActionAsync(new StepActionRequest
        {
            StepId = stepId,
            Action = action,
            UserId = request.UserId,
            Comment = request.Comment
        }, cancellationToken);
    }
}

public sealed class StepActionApiRequest
{
    public required string UserId { get; init; }
    public string? Comment { get; init; }
}
