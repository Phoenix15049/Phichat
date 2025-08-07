using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phichat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameEncryptedKeyToSymmetricKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedPrivateKey",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicKey",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "EncryptedSymmetricKey",
                table: "ChatKeys",
                newName: "SymmetricKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SymmetricKey",
                table: "ChatKeys",
                newName: "EncryptedSymmetricKey");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedPrivateKey",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicKey",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
