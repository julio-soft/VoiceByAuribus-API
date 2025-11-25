using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoiceByAuribus_API.Features.WebhookSubscriptions.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for WebhookDeliveryLog (WebhookSubscriptions feature).
/// </summary>
public class WebhookDeliveryLogConfiguration : IEntityTypeConfiguration<WebhookDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryLog> builder)
    {
        builder.ToTable("webhook_delivery_logs");

        // Primary Key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.EntityType)
            .HasMaxLength(100);

        builder.Property(x => x.Event)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(WebhookDeliveryStatus.Pending);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.AttemptNumber)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.ResponseBody)
            .HasMaxLength(2000);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        // Optimistic Concurrency Control
        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(x => x.WebhookSubscriptionId);
        builder.HasIndex(x => x.EntityId); // Generic entity ID index
        builder.HasIndex(x => x.EntityType); // For filtering by entity type
        builder.HasIndex(x => x.EventType); // For filtering by event type
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.NextRetryAt);
        builder.HasIndex(x => new { x.Status, x.NextRetryAt, x.AttemptNumber }); // For background processor queries

        // Relationships
        builder.HasOne(x => x.WebhookSubscription)
            .WithMany(x => x.DeliveryLogs)
            .HasForeignKey(x => x.WebhookSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
