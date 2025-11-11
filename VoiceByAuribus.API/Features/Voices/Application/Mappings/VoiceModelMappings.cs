using VoiceByAuribus_API.Features.Voices.Application.Dtos;
using VoiceByAuribus_API.Features.Voices.Domain;

namespace VoiceByAuribus_API.Features.Voices.Application.Mappings;

public static class VoiceModelMappings
{
    public static VoiceModelResponse ToResponse(
        this VoiceModel voice,
        string imageUrl,
        string songUrl,
        bool includeInternalPaths)
    {
        return new VoiceModelResponse(
            voice.Id,
            voice.Name,
            voice.Tags.AsReadOnly(),
            imageUrl,
            songUrl,
            includeInternalPaths ? voice.VoiceModelIndexPath : null,
            includeInternalPaths ? voice.VoiceModelPath : null);
    }
}
