using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phichat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityKeysDropChatKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatKeys");

            migrationBuilder.CreateTable(
                name: "UserIdentityKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BackupCiphertext = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BackupSalt = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BackupIv = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    BackupKdf = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    BackupIterations = table.Column<int>(type: "int", nullable: true),
                    BackupUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdentityKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserIdentityKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentityKeys_KeyId",
                table: "UserIdentityKeys",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentityKeys_UserId_Active",
                table: "UserIdentityKeys",
                column: "UserId",
                unique: true,
                filter: "[RevokedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserIdentityKeys");

            migrationBuilder.CreateTable(
                name: "ChatKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SymmetricKey = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatKeys", x => x.Id);
                });
        }
    }
}
