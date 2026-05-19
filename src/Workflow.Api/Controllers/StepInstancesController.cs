using Microsoft.AspNetCore.Mvc;
using Workflow.Abstractions;

namespace Workflow.Api.Controllers;

/// <summary>
/// Endpoints for claim/approve/reject/rework step actions.
/// </summary>
[ApiController]
[Route("step-instances")]
public sealed class StepInstancesController(IWorkflowEngine engine) : ControllerBase
{
    /// <summary>
    /// Claims group step for a concrete user.
    /// </summary>
    [HttpPost("{id:guid}/claim")]
    public Task<StepActionResult> Claim(Guid id, [FromBody] StepActionApiRequest request, CancellationToken cancellationToken) =>
        Handle(id, StepActionType.Claim, request, cancellationToken);

    /// <summary>
    /// Approves step.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public Task<StepActionResult> Approve(Guid id, [FromBody] StepActionApiRequest request, CancellationToken cancellationToken) =>
        Handle(id, StepActionType.Approve, request, cancellationToken);

    /// <summary>
    /// Rejects step.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public Task<StepActionResult> Reject(Guid id, [FromBody] StepActionApiRequest request, CancellationToken cancellationToken) =>
        Handle(id, StepActionType.Reject, request, cancellationToken);

    /// <summary>
    /// Requests rework on step.
    /// </summary>
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

/// <summary>
/// Payload shared by all step action endpoints.
/// </summary>
public sealed class StepActionApiRequest
{
    /// <summary>
    /// Actor user identifier.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Optional action comment.
    /// </summary>
    public string? Comment { get; init; }
}
