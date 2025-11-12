using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoiceByAuribus_API.Features.AudioFiles.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for AudioFile (AudioFiles feature)
/// </summary>
public class AudioFileConfiguration : IEntityTypeConfiguration<AudioFile>
{
    public void Configure(EntityTypeBuilder<AudioFile> builder)
    {
        builder.ToTable("audio_files");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.FileSize)
            .IsRequired(false);

        builder.Property(x => x.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.S3Uri)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasIndex(x => x.S3Uri);

        builder.Property(x => x.UploadStatus)
            .IsRequired()
            .HasConversion<string>();

        builder.HasIndex(x => x.UploadStatus);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        // Navigation property
        builder.HasOne(x => x.Preprocessing)
            .WithOne(x => x.AudioFile)
            .HasForeignKey<AudioPreprocessing>(x => x.AudioFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
