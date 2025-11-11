using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioFilesFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audio_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    S3Uri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UploadStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_files", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audio_preprocessing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "text", nullable: false),
                    S3UriShort = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    S3UriInference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AudioDurationSeconds = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_preprocessing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audio_preprocessing_audio_files_AudioFileId",
                        column: x => x.AudioFileId,
                        principalTable: "audio_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audio_files_S3Uri",
                table: "audio_files",
                column: "S3Uri");

            migrationBuilder.CreateIndex(
                name: "IX_audio_files_UploadStatus",
                table: "audio_files",
                column: "UploadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_audio_files_UserId",
                table: "audio_files",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_audio_preprocessing_AudioFileId",
                table: "audio_preprocessing",
                column: "AudioFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_preprocessing_ProcessingStatus",
                table: "audio_preprocessing",
                column: "ProcessingStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_preprocessing");

            migrationBuilder.DropTable(
                name: "audio_files");
        }
    }
}
