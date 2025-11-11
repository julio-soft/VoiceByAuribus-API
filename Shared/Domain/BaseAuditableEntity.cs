using System;

namespace VoiceByAuribus_API.Shared.Domain;

public abstract class BaseAuditableEntity : ISoftDelete
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
}
