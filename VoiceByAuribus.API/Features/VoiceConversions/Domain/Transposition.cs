namespace VoiceByAuribus_API.Features.VoiceConversions.Domain;

/// <summary>
/// Represents the transposition options for voice conversion.
/// Values correspond to semitone shifts that will be sent to the external processing service.
/// </summary>
public enum Transposition
{
    /// <summary>
    /// No transposition - same octave (0 semitones).
    /// </summary>
    SameOctave = 0,

    /// <summary>
    /// Transpose down by one octave (-12 semitones).
    /// </summary>
    LowerOctave = -12,

    /// <summary>
    /// Transpose up by one octave (+12 semitones).
    /// </summary>
    HigherOctave = 12,

    /// <summary>
    /// Transpose down by a minor third (-4 semitones).
    /// </summary>
    ThirdDown = -4,

    /// <summary>
    /// Transpose up by a major third (+4 semitones).
    /// </summary>
    ThirdUp = 4,

    /// <summary>
    /// Transpose down by a perfect fifth (-7 semitones).
    /// </summary>
    FifthDown = -7,

    /// <summary>
    /// Transpose up by a perfect fifth (+7 semitones).
    /// </summary>
    FifthUp = 7
}
