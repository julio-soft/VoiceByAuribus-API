using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoiceByAuribus_API.Features.Voices.Application.Dtos;

namespace VoiceByAuribus_API.Features.Voices.Application.Services;

public interface IVoiceModelService
{
    Task<IReadOnlyCollection<VoiceModelResponse>> GetVoicesAsync(CancellationToken cancellationToken);
    Task<VoiceModelResponse?> GetVoiceAsync(Guid id, CancellationToken cancellationToken);
}
