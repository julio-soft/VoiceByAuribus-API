using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoiceByAuribus_API.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSecretHashWithEncryptedSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecretHash",
                table: "webhook_subscriptions");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedSecret",
                table: "webhook_subscriptions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedSecret",
                table: "webhook_subscriptions");

            migrationBuilder.AddColumn<string>(
                name: "SecretHash",
                table: "webhook_subscriptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
