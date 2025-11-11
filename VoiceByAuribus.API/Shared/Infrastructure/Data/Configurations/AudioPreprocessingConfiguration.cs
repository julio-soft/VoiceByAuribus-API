using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoiceByAuribus_API.Features.AudioFiles.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for AudioPreprocessing (AudioFiles feature)
/// </summary>
public class AudioPreprocessingConfiguration : IEntityTypeConfiguration<AudioPreprocessing>
{
    public void Configure(EntityTypeBuilder<AudioPreprocessing> builder)
    {
        builder.ToTable("audio_preprocessing");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AudioFileId)
            .IsRequired();

        builder.HasIndex(x => x.AudioFileId)
            .IsUnique();

        builder.Property(x => x.ProcessingStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.HasIndex(x => x.ProcessingStatus);

        builder.Property(x => x.S3UriShort)
            .HasMaxLength(1000);

        builder.Property(x => x.S3UriInference)
            .HasMaxLength(1000);

        builder.Property(x => x.AudioDurationSeconds)
            .HasPrecision(18, 3);

        builder.Property(x => x.ProcessingStartedAt);

        builder.Property(x => x.ProcessingCompletedAt);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);
    }
}
