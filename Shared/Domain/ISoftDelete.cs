namespace VoiceByAuribus_API.Shared.Domain;

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}
