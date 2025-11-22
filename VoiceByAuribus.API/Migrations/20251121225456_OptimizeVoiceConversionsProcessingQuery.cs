using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeVoiceConversionsProcessingQuery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_voice_conversions_status_retry_count",
                table: "voice_conversions");

            migrationBuilder.CreateIndex(
                name: "ix_voice_conversions_processing_query",
                table: "voice_conversions",
                columns: new[] { "Status", "RetryCount", "LastRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_voice_conversions_processing_query",
                table: "voice_conversions");

            migrationBuilder.CreateIndex(
                name: "ix_voice_conversions_status_retry_count",
                table: "voice_conversions",
                columns: new[] { "Status", "RetryCount" });
        }
    }
}
