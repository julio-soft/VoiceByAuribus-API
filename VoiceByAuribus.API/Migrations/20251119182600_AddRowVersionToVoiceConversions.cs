using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionToVoiceConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "voice_conversions",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "voice_conversions");
        }
    }
}
