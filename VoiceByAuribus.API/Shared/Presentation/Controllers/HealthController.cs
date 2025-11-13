using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoiceByAuribus_API.Shared.Application.Dtos;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Shared.Presentation.Controllers;

/// <summary>
/// Health check endpoint for monitoring application status
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;

    public HealthController(IHealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Performs health checks on critical services including database and AWS services
    /// </summary>
    /// <returns>Health status of all critical services</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealthAsync()
    {
        var healthStatus = await _healthCheckService.CheckHealthAsync();

        var response = ApiResponse<HealthCheckResponse>.SuccessResponse(
            healthStatus,
            $"Application is {healthStatus.Status}");

        // Return 503 Service Unavailable if unhealthy, otherwise 200 OK
        if (healthStatus.Status == "unhealthy")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }
}
