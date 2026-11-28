using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentAnonymousAndNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentNickname",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                table: "Comments",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentNickname",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAnonymous",
                table: "Comments");
        }
    }
}
