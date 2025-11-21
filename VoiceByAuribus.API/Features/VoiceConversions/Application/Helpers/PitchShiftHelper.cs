using System;
using System.Collections.Generic;
using VoiceByAuribus_API.Features.VoiceConversions.Domain;

namespace VoiceByAuribus_API.Features.VoiceConversions.Application.Helpers;

/// <summary>
/// Helper for converting between user-facing pitch shift strings and internal Transposition enum.
/// </summary>
public static class PitchShiftHelper
{
    private static readonly Dictionary<string, Transposition> StringToTransposition = new(StringComparer.OrdinalIgnoreCase)
    {
        { "same_octave", Transposition.SameOctave },
        { "lower_octave", Transposition.LowerOctave },
        { "higher_octave", Transposition.HigherOctave },
        { "third_down", Transposition.ThirdDown },
        { "third_up", Transposition.ThirdUp },
        { "fifth_down", Transposition.FifthDown },
        { "fifth_up", Transposition.FifthUp }
    };

    private static readonly Dictionary<Transposition, string> TranspositionToString = new()
    {
        { Transposition.SameOctave, "same_octave" },
        { Transposition.LowerOctave, "lower_octave" },
        { Transposition.HigherOctave, "higher_octave" },
        { Transposition.ThirdDown, "third_down" },
        { Transposition.ThirdUp, "third_up" },
        { Transposition.FifthDown, "fifth_down" },
        { Transposition.FifthUp, "fifth_up" }
    };

    /// <summary>
    /// Converts a pitch shift string to a Transposition enum.
    /// </summary>
    /// <param name="pitchShift">The pitch shift string (e.g., "same_octave", "lower_octave")</param>
    /// <returns>The corresponding Transposition enum value</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pitch shift string is invalid</exception>
    public static Transposition ToTransposition(string pitchShift)
    {
        if (string.IsNullOrWhiteSpace(pitchShift))
        {
            throw new InvalidOperationException("Pitch shift cannot be empty");
        }

        if (!StringToTransposition.TryGetValue(pitchShift, out var transposition))
        {
            var validValues = string.Join(", ", StringToTransposition.Keys);
            throw new InvalidOperationException(
                $"Invalid pitch shift '{pitchShift}'. Valid values are: {validValues}");
        }

        return transposition;
    }

    /// <summary>
    /// Converts a Transposition enum to a pitch shift string.
    /// </summary>
    /// <param name="transposition">The transposition enum value</param>
    /// <returns>The corresponding pitch shift string</returns>
    public static string ToPitchShiftString(Transposition transposition)
    {
        return TranspositionToString[transposition];
    }

    /// <summary>
    /// Gets all valid pitch shift strings.
    /// </summary>
    public static IEnumerable<string> GetValidPitchShifts()
    {
        return StringToTransposition.Keys;
    }
}
