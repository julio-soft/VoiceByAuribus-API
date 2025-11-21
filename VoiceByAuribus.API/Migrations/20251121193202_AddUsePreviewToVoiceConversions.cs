using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class AddUsePreviewToVoiceConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsePreview",
                table: "voice_conversions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsePreview",
                table: "voice_conversions");
        }
    }
}
