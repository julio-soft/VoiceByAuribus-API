using System;
using System.Collections.Generic;
using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Features.Voices.Domain;

public class VoiceModel : BaseAuditableEntity
{
    public required string Name { get; set; }
    public List<string> Tags { get; set; } = new();
    public required string ImageUri { get; set; }
    public required string SongUri { get; set; }
    public required string VoiceModelIndexPath { get; set; }
    public required string VoiceModelPath { get; set; }
}
