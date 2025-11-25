using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for WebhookSubscription (WebhookSubscriptions feature).
/// </summary>
public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");

        // Primary Key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.EncryptedSecret)
            .IsRequired()
            .HasMaxLength(1000); // Increased for encrypted format: nonce:ciphertext:tag

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // Enum stored as array of strings in JSON format
        builder.Property(x => x.SubscribedEvents)
            .IsRequired()
            .HasConversion(
                v => string.Join(',', v.Select(e => e.ToString())),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => Enum.Parse<WebhookEvent>(s))
                      .ToArray()
            )
            .Metadata.SetValueComparer(
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<WebhookEvent[]>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()
                )
            );

        builder.Property(x => x.SubscribedEvents)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.ConsecutiveFailures)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.AutoDisableOnFailure)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.MaxConsecutiveFailures)
            .IsRequired()
            .HasDefaultValue(10);

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.UserId, x.IsActive });

        // Relationships
        builder.HasMany(x => x.DeliveryLogs)
            .WithOne(x => x.WebhookSubscription)
            .HasForeignKey(x => x.WebhookSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
