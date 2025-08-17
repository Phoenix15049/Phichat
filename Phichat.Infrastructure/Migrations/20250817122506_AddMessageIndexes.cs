using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phichat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_ReceiverId_SentAt",
                table: "Messages",
                columns: new[] { "SenderId", "ReceiverId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SentAt",
                table: "Messages",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId_ReceiverId_SentAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SentAt",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId",
                table: "Messages",
                column: "SenderId");
        }
    }
}
