using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotBox.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByApiKeyId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAgent",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KeyValue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    KeyPrefix = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CreatedByApiKeyId",
                table: "AspNetUsers",
                column: "CreatedByApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyValue",
                table: "ApiKeys",
                column: "KeyValue",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_ApiKeys_CreatedByApiKeyId",
                table: "AspNetUsers",
                column: "CreatedByApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_ApiKeys_CreatedByApiKeyId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CreatedByApiKeyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedByApiKeyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsAgent",
                table: "AspNetUsers");
        }
    }
}
