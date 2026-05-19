using Microsoft.AspNetCore.Mvc;
using Workflow.Abstractions;

namespace Workflow.Api.Controllers;

/// <summary>
/// Endpoints for previewing and starting workflow runtime instances.
/// </summary>
[ApiController]
[Route("process-instances")]
public sealed class ProcessInstancesController(IWorkflowEngine engine) : ControllerBase
{
    /// <summary>
    /// Returns a calculated approval route without persisting runtime state.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<WorkflowPreviewResult>> Preview([FromBody] PreviewApiRequest request, CancellationToken cancellationToken)
    {
        var result = await engine.PreviewAsync(new PreviewRequest
        {
            ProcessKey = request.ProcessKey,
            DraftData = request.DraftData
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Creates runtime process instance for request payload.
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<StartProcessResult>> Start([FromBody] StartApiRequest request, CancellationToken cancellationToken)
    {
        var result = await engine.StartAsync(new StartProcessRequest
        {
            ProcessKey = request.ProcessKey,
            RequestId = request.RequestId,
            RequestData = request.RequestData
        }, cancellationToken);

        return Ok(result);
    }
}

/// <summary>
/// API payload for preview endpoint.
/// </summary>
public sealed class PreviewApiRequest
{
    /// <summary>
    /// Stable process key.
    /// </summary>
    public required string ProcessKey { get; init; }

    /// <summary>
    /// Draft form values used for conditions and dynamic approvers.
    /// </summary>
    public required Dictionary<string, object?> DraftData { get; init; }
}

/// <summary>
/// API payload for start endpoint.
/// </summary>
public sealed class StartApiRequest
{
    /// <summary>
    /// Stable process key.
    /// </summary>
    public required string ProcessKey { get; init; }

    /// <summary>
    /// External request identifier from source system.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Request payload snapshot used to initialize workflow.
    /// </summary>
    public required Dictionary<string, object?> RequestData { get; init; }
}
