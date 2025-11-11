using System;
using System.Collections.Generic;

namespace VoiceByAuribus_API.Features.Voices.Application.Dtos;

public record VoiceModelResponse(
    Guid Id,
    string Name,
    IReadOnlyCollection<string> Tags,
    string ImageUrl,
    string SongUrl,
    string? VoiceModelIndexPath,
    string? VoiceModelPath);
