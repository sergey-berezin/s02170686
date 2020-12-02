using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DataBaseEntityFramework.Migrations
{
    public partial class BaseMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageDetails",
                columns: table => new
                {
                    ImageDetailId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ByteImage = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageDetails", x => x.ImageDetailId);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedImages",
                columns: table => new
                {
                    ImageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImageName = table.Column<string>(type: "TEXT", nullable: true),
                    ImageLabel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedImages", x => x.ImageId);
                });

            migrationBuilder.CreateTable(
                name: "ImageDetailDBProcessedImageDB",
                columns: table => new
                {
                    AdditionalInfoImageDetailId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryInfoImageId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageDetailDBProcessedImageDB", x => new { x.AdditionalInfoImageDetailId, x.PrimaryInfoImageId });
                    table.ForeignKey(
                        name: "FK_ImageDetailDBProcessedImageDB_ImageDetails_AdditionalInfoImageDetailId",
                        column: x => x.AdditionalInfoImageDetailId,
                        principalTable: "ImageDetails",
                        principalColumn: "ImageDetailId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImageDetailDBProcessedImageDB_ProcessedImages_PrimaryInfoImageId",
                        column: x => x.PrimaryInfoImageId,
                        principalTable: "ProcessedImages",
                        principalColumn: "ImageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageDetailDBProcessedImageDB_PrimaryInfoImageId",
                table: "ImageDetailDBProcessedImageDB",
                column: "PrimaryInfoImageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageDetailDBProcessedImageDB");

            migrationBuilder.DropTable(
                name: "ImageDetails");

            migrationBuilder.DropTable(
                name: "ProcessedImages");
        }
    }
}
