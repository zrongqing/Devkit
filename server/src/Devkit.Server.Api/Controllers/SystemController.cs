using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Devkit.Server.Application.Workspace;

namespace Devkit.Server.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(ISystemInfoService systemInfoService) : ControllerBase
{
    [Authorize]
    [HttpGet("runtime", Name = "GetSystemRuntime")]
    [EndpointSummary("Returns live server process metrics; requires system.monitor.view")]
    [ProducesResponseType(typeof(ApiResponse<SystemRuntimeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SystemRuntimeResponse>>> GetRuntime([FromServices] IWorkspaceAccess access, CancellationToken ct)
    {
        (await access.CurrentAsync(ct)).Require("system.monitor.view");
        return Ok(new ApiResponse<SystemRuntimeResponse>(systemInfoService.GetRuntime(), HttpContext.TraceIdentifier));
    }

    [HttpGet("info", Name = "GetSystemInfo")]
    [EndpointSummary("Returns server identity and runtime status")]
    [ProducesResponseType(typeof(ApiResponse<SystemInfoResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SystemInfoResponse>> GetInfo() =>
        Ok(new ApiResponse<SystemInfoResponse>(systemInfoService.GetInfo(), HttpContext.TraceIdentifier));
}
