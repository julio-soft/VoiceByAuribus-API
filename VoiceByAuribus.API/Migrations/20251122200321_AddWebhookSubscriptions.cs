using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubscribedEvents = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastSuccessAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AutoDisableOnFailure = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MaxConsecutiveFailures = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_delivery_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoiceConversionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Event = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_delivery_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_logs_voice_conversions_VoiceConversionId",
                        column: x => x.VoiceConversionId,
                        principalTable: "voice_conversions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_logs_webhook_subscriptions_WebhookSubscrip~",
                        column: x => x.WebhookSubscriptionId,
                        principalTable: "webhook_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_NextRetryAt",
                table: "webhook_delivery_logs",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_Status",
                table: "webhook_delivery_logs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_Status_NextRetryAt_AttemptNumber",
                table: "webhook_delivery_logs",
                columns: new[] { "Status", "NextRetryAt", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_VoiceConversionId",
                table: "webhook_delivery_logs",
                column: "VoiceConversionId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_WebhookSubscriptionId",
                table: "webhook_delivery_logs",
                column: "WebhookSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_IsActive",
                table: "webhook_subscriptions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_UserId",
                table: "webhook_subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_UserId_IsActive",
                table: "webhook_subscriptions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_delivery_logs");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");
        }
    }
}
