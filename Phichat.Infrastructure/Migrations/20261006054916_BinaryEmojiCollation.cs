using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phichat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BinaryEmojiCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The column is part of the primary key, which must not exist while its collation changes.
            migrationBuilder.DropPrimaryKey(name: "PK_MessageReactions", table: "MessageReactions");

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "MessageReactions",
                type: "nvarchar(450)",
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MessageReactions",
                table: "MessageReactions",
                columns: new[] { "MessageId", "UserId", "Emoji" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_MessageReactions", table: "MessageReactions");

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "MessageReactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MessageReactions",
                table: "MessageReactions",
                columns: new[] { "MessageId", "UserId", "Emoji" });
        }
    }
}
