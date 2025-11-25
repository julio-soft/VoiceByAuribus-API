using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class RefactorWebhookDeliveryLogToBeEventAgnostic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_webhook_delivery_logs_voice_conversions_VoiceConversionId",
                table: "webhook_delivery_logs");

            migrationBuilder.RenameColumn(
                name: "VoiceConversionId",
                table: "webhook_delivery_logs",
                newName: "EntityId");

            migrationBuilder.RenameIndex(
                name: "IX_webhook_delivery_logs_VoiceConversionId",
                table: "webhook_delivery_logs",
                newName: "IX_webhook_delivery_logs_EntityId");

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "webhook_delivery_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "webhook_delivery_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_EntityType",
                table: "webhook_delivery_logs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_EventType",
                table: "webhook_delivery_logs",
                column: "EventType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_delivery_logs_EntityType",
                table: "webhook_delivery_logs");

            migrationBuilder.DropIndex(
                name: "IX_webhook_delivery_logs_EventType",
                table: "webhook_delivery_logs");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "webhook_delivery_logs");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "webhook_delivery_logs");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "webhook_delivery_logs",
                newName: "VoiceConversionId");

            migrationBuilder.RenameIndex(
                name: "IX_webhook_delivery_logs_EntityId",
                table: "webhook_delivery_logs",
                newName: "IX_webhook_delivery_logs_VoiceConversionId");

            migrationBuilder.AddForeignKey(
                name: "FK_webhook_delivery_logs_voice_conversions_VoiceConversionId",
                table: "webhook_delivery_logs",
                column: "VoiceConversionId",
                principalTable: "voice_conversions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
