using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarksBaseballCards.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CardPhotoOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Cards",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Cards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CardPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<int>(type: "int", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPhotos_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_IsPublished_Id",
                table: "Cards",
                columns: new[] { "IsPublished", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_IsPublished_IsForSale_IsSold_Id",
                table: "Cards",
                columns: new[] { "IsPublished", "IsForSale", "IsSold", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CardPhotos_CardId_Position_Id",
                table: "CardPhotos",
                columns: new[] { "CardId", "Position", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardPhotos");

            migrationBuilder.DropIndex(
                name: "IX_Cards_IsPublished_Id",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_IsPublished_IsForSale_IsSold_Id",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Cards");
        }
    }
}
