using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizerPublicProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizerProfileId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizerProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AvatarImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TikTokUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BrandColor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizerProfiles_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganizerProfileId",
                table: "Events",
                column: "OrganizerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerProfiles_OwnerId_DisplayName",
                table: "OrganizerProfiles",
                columns: new[] { "OwnerId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerProfiles_OwnerId_IsDefault",
                table: "OrganizerProfiles",
                columns: new[] { "OwnerId", "IsDefault" });

            migrationBuilder.Sql("""
                INSERT INTO OrganizerProfiles
                    (OwnerId, DisplayName, Description, Website, PhoneNumber, IsDefault, IsActive, CreatedAt)
                SELECT
                    OrganizerId,
                    OrganizationName,
                    Description,
                    Website,
                    PhoneNumber,
                    CAST(1 AS bit),
                    CAST(1 AS bit),
                    CreatedAt
                FROM OrganizerData
                WHERE NOT EXISTS (
                    SELECT 1 FROM OrganizerProfiles WHERE OrganizerProfiles.OwnerId = OrganizerData.OrganizerId
                );
                """);

            migrationBuilder.Sql("""
                UPDATE e
                SET OrganizerProfileId = p.Id
                FROM Events AS e
                INNER JOIN OrganizerProfiles p ON p.OwnerId = e.OrganizerId AND p.IsDefault = 1
                WHERE e.OrganizerProfileId IS NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_OrganizerProfiles_OrganizerProfileId",
                table: "Events",
                column: "OrganizerProfileId",
                principalTable: "OrganizerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_OrganizerProfiles_OrganizerProfileId",
                table: "Events");

            migrationBuilder.DropTable(
                name: "OrganizerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Events_OrganizerProfileId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OrganizerProfileId",
                table: "Events");
        }
    }
}
