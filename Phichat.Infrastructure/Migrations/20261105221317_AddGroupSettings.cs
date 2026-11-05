using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phichat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Groups",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberPermissions",
                table: "Groups",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "SlowModeSeconds",
                table: "Groups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GroupBans",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BannedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupBans", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GroupBans_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupBans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_InviteCode",
                table: "Groups",
                column: "InviteCode",
                unique: true,
                filter: "[InviteCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBans_UserId",
                table: "GroupBans",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupBans");

            migrationBuilder.DropIndex(
                name: "IX_Groups_InviteCode",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "MemberPermissions",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "SlowModeSeconds",
                table: "Groups");
        }
    }
}
