using VoiceByAuribus_API.Features.VoiceConversions.Application.Helpers;
using VoiceByAuribus_API.Features.VoiceConversions.Domain;

namespace VoiceByAuribus_API.Tests.Unit.Features.VoiceConversions.Helpers;

/// <summary>
/// Unit tests for PitchShiftHelper utility class.
/// Tests conversion between pitch_shift string (API) and Transposition enum (internal).
/// </summary>
public class PitchShiftHelperTests
{
    [Theory]
    [InlineData("same_octave", Transposition.SameOctave)]
    [InlineData("lower_octave", Transposition.LowerOctave)]
    [InlineData("higher_octave", Transposition.HigherOctave)]
    [InlineData("third_down", Transposition.ThirdDown)]
    [InlineData("third_up", Transposition.ThirdUp)]
    [InlineData("fifth_down", Transposition.FifthDown)]
    [InlineData("fifth_up", Transposition.FifthUp)]
    public void ToTransposition_WithValidPitchShift_ReturnsCorrectTransposition(
        string pitchShift,
        Transposition expectedTransposition)
    {
        // Act
        var result = PitchShiftHelper.ToTransposition(pitchShift);

        // Assert
        result.Should().Be(expectedTransposition);
    }

    [Fact]
    public void ToTransposition_WithInvalidPitchShift_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidPitchShift = "invalid_value";

        // Act
        var act = () => PitchShiftHelper.ToTransposition(invalidPitchShift);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid pitch shift*");
    }

    [Fact]
    public void ToTransposition_WithNull_ThrowsInvalidOperationException()
    {
        // Act
        var act = () => PitchShiftHelper.ToTransposition(null!);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be empty*");
    }

    [Theory]
    [InlineData(Transposition.SameOctave, "same_octave")]
    [InlineData(Transposition.LowerOctave, "lower_octave")]
    [InlineData(Transposition.HigherOctave, "higher_octave")]
    [InlineData(Transposition.ThirdDown, "third_down")]
    [InlineData(Transposition.ThirdUp, "third_up")]
    [InlineData(Transposition.FifthDown, "fifth_down")]
    [InlineData(Transposition.FifthUp, "fifth_up")]
    public void ToPitchShiftString_WithValidTransposition_ReturnsCorrectString(
        Transposition transposition,
        string expectedPitchShift)
    {
        // Act
        var result = PitchShiftHelper.ToPitchShiftString(transposition);

        // Assert
        result.Should().Be(expectedPitchShift);
    }

    [Fact]
    public void ToPitchShiftString_WithInvalidTransposition_ThrowsKeyNotFoundException()
    {
        // Arrange
        var invalidTransposition = (Transposition)999;

        // Act
        var act = () => PitchShiftHelper.ToPitchShiftString(invalidTransposition);

        // Assert
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void RoundTrip_ToTranspositionAndBack_ReturnsOriginalString()
    {
        // Arrange
        var originalPitchShift = "same_octave";

        // Act
        var transposition = PitchShiftHelper.ToTransposition(originalPitchShift);
        var result = PitchShiftHelper.ToPitchShiftString(transposition);

        // Assert
        result.Should().Be(originalPitchShift);
    }
}
