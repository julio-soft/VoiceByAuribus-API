using System.Threading.Tasks;
using VoiceByAuribus_API.Shared.Application.Dtos;

namespace VoiceByAuribus_API.Shared.Interfaces;

/// <summary>
/// Service for performing health checks on critical application services
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Performs health checks on all critical services
    /// </summary>
    Task<HealthCheckResponse> CheckHealthAsync();
}
