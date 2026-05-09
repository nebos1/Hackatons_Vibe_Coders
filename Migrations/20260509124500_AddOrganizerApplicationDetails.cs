using EventsApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260509124500_AddOrganizerApplicationDetails")]
    public partial class AddOrganizerApplicationDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "OrganizerData",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "OrganizerData",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralSource",
                table: "OrganizerData",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "OrganizerData");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "OrganizerData");

            migrationBuilder.DropColumn(
                name: "ReferralSource",
                table: "OrganizerData");
        }
    }
}
