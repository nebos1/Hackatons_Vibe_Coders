using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageSharedCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SharedEventId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SharedPostId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SharedEventId",
                table: "Messages",
                column: "SharedEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SharedPostId",
                table: "Messages",
                column: "SharedPostId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Events_SharedEventId",
                table: "Messages",
                column: "SharedEventId",
                principalTable: "Events",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Posts_SharedPostId",
                table: "Messages",
                column: "SharedPostId",
                principalTable: "Posts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Events_SharedEventId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Posts_SharedPostId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SharedEventId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SharedPostId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SharedEventId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SharedPostId",
                table: "Messages");
        }
    }
}
