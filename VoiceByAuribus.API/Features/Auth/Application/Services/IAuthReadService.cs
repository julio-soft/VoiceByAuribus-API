using System.Threading.Tasks;
using VoiceByAuribus_API.Features.Auth.Application.Dtos;

namespace VoiceByAuribus_API.Features.Auth.Application.Services;

public interface IAuthReadService
{
    Task<CurrentUserResponse> GetCurrentUserAsync();
    Task<AuthStatusResponse> GetStatusAsync();
}
