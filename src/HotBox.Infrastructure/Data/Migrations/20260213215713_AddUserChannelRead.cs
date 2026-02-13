using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotBox.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserChannelRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserChannelReads",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChannelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastReadMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastReadAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChannelReads", x => new { x.UserId, x.ChannelId });
                    table.ForeignKey(
                        name: "FK_UserChannelReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserChannelReads_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserChannelReads_Messages_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserChannelReads_ChannelId",
                table: "UserChannelReads",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChannelReads_LastReadMessageId",
                table: "UserChannelReads",
                column: "LastReadMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserChannelReads");
        }
    }
}
