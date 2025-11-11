using System.Threading.Tasks;
using VoiceByAuribus_API.Features.Auth.Application.Dtos;
using VoiceByAuribus_API.Shared.Interfaces;

namespace VoiceByAuribus_API.Features.Auth.Application.Services;

public class AuthReadService(ICurrentUserService currentUserService) : IAuthReadService
{
    public Task<CurrentUserResponse> GetCurrentUserAsync()
    {
        var response = new CurrentUserResponse(
            currentUserService.UserId,
            currentUserService.Username,
            currentUserService.Email,
            currentUserService.Scopes,
            currentUserService.IsAdmin);

        return Task.FromResult(response);
    }

    public Task<AuthStatusResponse> GetStatusAsync()
    {
        var response = new AuthStatusResponse(
            currentUserService.IsAuthenticated,
            currentUserService.UserId,
            currentUserService.IsAdmin,
            currentUserService.Scopes);

        return Task.FromResult(response);
    }
}
