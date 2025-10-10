using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddBackdropAndPosterToMovies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ giữ lại các lệnh thêm cột mới
            migrationBuilder.AddColumn<string>(
                name: "Backdrop",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Poster",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xóa các cột tương ứng nếu rollback
            migrationBuilder.DropColumn(
                name: "Backdrop",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Poster",
                table: "Movies");
        }
    }
}