using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieSourceSlugAndRedirects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceSlug",
                table: "Movies",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MovieSlugRedirects",
                columns: table => new
                {
                    OldSlug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MovieId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieSlugRedirects", x => x.OldSlug);
                    table.ForeignKey(
                        name: "FK_MovieSlugRedirects_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "MovieId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movies_SourceSlug",
                table: "Movies",
                column: "SourceSlug");

            migrationBuilder.CreateIndex(
                name: "IX_MovieSlugRedirects_MovieId",
                table: "MovieSlugRedirects",
                column: "MovieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieSlugRedirects");

            migrationBuilder.DropIndex(
                name: "IX_Movies_SourceSlug",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "SourceSlug",
                table: "Movies");
        }
    }
}
