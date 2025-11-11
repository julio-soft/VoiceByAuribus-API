using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoiceByAuribus_API.Features.Voices.Domain;

namespace VoiceByAuribus_API.Features.Voices.Infrastructure.Data;

public class VoiceModelConfiguration : IEntityTypeConfiguration<VoiceModel>
{
    public void Configure(EntityTypeBuilder<VoiceModel> builder)
    {
        builder.ToTable("voice_models");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Tags)
            .HasColumnType("text[]")
            .HasConversion(
                list => list.ToArray(),
                array => array == null ? new List<string>() : array.ToList())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (l1, l2) => l1!.SequenceEqual(l2!),
                l => l.Aggregate(0, (hash, value) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(value))),
                l => l.ToList()));

        builder.Property(x => x.ImageUri)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.SongUri)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.VoiceModelIndexPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.VoiceModelPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);
    }
}
