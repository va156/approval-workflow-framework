using Microsoft.AspNetCore.Mvc;
using Workflow.Abstractions;

namespace Workflow.Api.Controllers;

[ApiController]
[Route("process-instances")]
public sealed class ProcessInstancesController(IWorkflowEngine engine) : ControllerBase
{
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

public sealed class PreviewApiRequest
{
    public required string ProcessKey { get; init; }
    public required Dictionary<string, object?> DraftData { get; init; }
}

public sealed class StartApiRequest
{
    public required string ProcessKey { get; init; }
    public required string RequestId { get; init; }
    public required Dictionary<string, object?> RequestData { get; init; }
}
