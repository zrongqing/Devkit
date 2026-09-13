using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Devkit.Server.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(ISystemInfoService systemInfoService) : ControllerBase
{
    [HttpGet("info", Name = "GetSystemInfo")]
    [EndpointSummary("Returns server identity and runtime status")]
    [ProducesResponseType(typeof(ApiResponse<SystemInfoResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SystemInfoResponse>> GetInfo() =>
        Ok(new ApiResponse<SystemInfoResponse>(systemInfoService.GetInfo(), HttpContext.TraceIdentifier));
}
