using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voice_models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    ImageUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SongUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VoiceModelIndexPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VoiceModelPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_models", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voice_models");
        }
    }
}
