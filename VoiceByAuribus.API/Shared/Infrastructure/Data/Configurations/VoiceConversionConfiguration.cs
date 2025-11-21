using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoiceByAuribus_API.Features.VoiceConversions.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Data.Configurations;

/// <summary>Entity Framework configuration for VoiceConversion (VoiceConversions feature)</summary>
public class VoiceConversionConfiguration : IEntityTypeConfiguration<VoiceConversion>
{
    public void Configure(EntityTypeBuilder<VoiceConversion> builder)
    {
        builder.ToTable("voice_conversions");

        builder.Property(x => x.Transposition)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.UsePreview)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.OutputS3Uri)
            .HasMaxLength(500);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Optimistic Locking - RowVersion for concurrency control
        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired();

        // Foreign keys
        builder.HasOne(x => x.AudioFile)
            .WithMany()
            .HasForeignKey(x => x.AudioFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VoiceModel)
            .WithMany()
            .HasForeignKey(x => x.VoiceModelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.AudioFileId);
        builder.HasIndex(x => x.VoiceModelId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Status, x.RetryCount })
            .HasDatabaseName("ix_voice_conversions_status_retry_count");
    }
}
