namespace VoiceByAuribus_API.Shared.Domain;

/// <summary>
/// Interface for entities that are owned by a user.
/// The UserId is a string to support various identity providers (Cognito M2M client_id, user sub, etc.).
/// </summary>
public interface IHasUserId
{
    string? UserId { get; set; }
}
