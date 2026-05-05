using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAttendeesAndUnlimitedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttendeeName",
                table: "UserTickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryInPurchase",
                table: "UserTickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseGroupId",
                table: "UserTickets",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAttendeeNames",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCapacityUnlimited",
                table: "Seats",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserTickets_PurchaseGroupId",
                table: "UserTickets",
                column: "PurchaseGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTickets_PurchaseGroupId",
                table: "UserTickets");

            migrationBuilder.DropColumn(
                name: "AttendeeName",
                table: "UserTickets");

            migrationBuilder.DropColumn(
                name: "IsPrimaryInPurchase",
                table: "UserTickets");

            migrationBuilder.DropColumn(
                name: "PurchaseGroupId",
                table: "UserTickets");

            migrationBuilder.DropColumn(
                name: "RequiresAttendeeNames",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsCapacityUnlimited",
                table: "Seats");
        }
    }
}
