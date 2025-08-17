using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phichat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageDeliveryReadTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAtUtc",
                table: "Messages",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReadAtUtc",
                table: "Messages");
        }
    }
}
