using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPageScopedConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_ParticipantOneId_ParticipantTwoId",
                table: "Conversations");

            migrationBuilder.AddColumn<int>(
                name: "OrganizerProfileId",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizerProfileId",
                table: "Conversations",
                column: "OrganizerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ParticipantOneId_ParticipantTwoId",
                table: "Conversations",
                columns: new[] { "ParticipantOneId", "ParticipantTwoId" },
                unique: true,
                filter: "[OrganizerProfileId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ParticipantOneId_ParticipantTwoId_OrganizerProfileId",
                table: "Conversations",
                columns: new[] { "ParticipantOneId", "ParticipantTwoId", "OrganizerProfileId" },
                unique: true,
                filter: "[OrganizerProfileId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_OrganizerProfiles_OrganizerProfileId",
                table: "Conversations",
                column: "OrganizerProfileId",
                principalTable: "OrganizerProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_OrganizerProfiles_OrganizerProfileId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrganizerProfileId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ParticipantOneId_ParticipantTwoId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ParticipantOneId_ParticipantTwoId_OrganizerProfileId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "OrganizerProfileId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ParticipantOneId_ParticipantTwoId",
                table: "Conversations",
                columns: new[] { "ParticipantOneId", "ParticipantTwoId" },
                unique: true);
        }
    }
}
