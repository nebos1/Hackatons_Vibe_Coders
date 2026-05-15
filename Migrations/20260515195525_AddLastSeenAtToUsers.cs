using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSeenAtToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketSectionPrices_TicketId_SectionId",
                table: "TicketSectionPrices");

            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "TicketSectionPrices",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "Seats",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketSectionPrices_TicketId_SectionId_ColorHex",
                table: "TicketSectionPrices",
                columns: new[] { "TicketId", "SectionId", "ColorHex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketSectionPrices_TicketId_SectionId_ColorHex",
                table: "TicketSectionPrices");

            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "TicketSectionPrices");

            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSectionPrices_TicketId_SectionId",
                table: "TicketSectionPrices",
                columns: new[] { "TicketId", "SectionId" },
                unique: true);
        }
    }
}
