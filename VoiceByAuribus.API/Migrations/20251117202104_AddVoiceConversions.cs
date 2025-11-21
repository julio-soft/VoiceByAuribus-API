using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voice_conversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AudioFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoiceModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Transposition = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OutputS3Uri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_conversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_voice_conversions_audio_files_AudioFileId",
                        column: x => x.AudioFileId,
                        principalTable: "audio_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voice_conversions_voice_models_VoiceModelId",
                        column: x => x.VoiceModelId,
                        principalTable: "voice_models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voice_conversions_AudioFileId",
                table: "voice_conversions",
                column: "AudioFileId");

            migrationBuilder.CreateIndex(
                name: "IX_voice_conversions_Status",
                table: "voice_conversions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_voice_conversions_status_retry_count",
                table: "voice_conversions",
                columns: new[] { "Status", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "IX_voice_conversions_UserId",
                table: "voice_conversions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_voice_conversions_VoiceModelId",
                table: "voice_conversions",
                column: "VoiceModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voice_conversions");
        }
    }
}
