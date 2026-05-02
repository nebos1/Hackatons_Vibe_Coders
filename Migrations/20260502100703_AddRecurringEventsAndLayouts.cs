using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringEventsAndLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventOccurrenceId",
                table: "UserTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatId",
                table: "UserTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketingMode",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VenueLayoutId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    OrganizerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecurrenceType = table.Column<int>(type: "int", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSeries_AspNetUsers_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSeries_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VenueLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VenueName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueLayouts_AspNetUsers_OrganizerId",
                        column: x => x.OrganizerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventSeriesId = table.Column<int>(type: "int", nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CapacityOverride = table.Column<int>(type: "int", nullable: true),
                    PriceOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventOccurrences_EventSeries_EventSeriesId",
                        column: x => x.EventSeriesId,
                        principalTable: "EventSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LayoutSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueLayoutId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    PriceModifier = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LayoutSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LayoutSections_VenueLayouts_VenueLayoutId",
                        column: x => x.VenueLayoutId,
                        principalTable: "VenueLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueLayoutId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    Row = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    SeatType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seats_LayoutSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "LayoutSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seats_VenueLayouts_VenueLayoutId",
                        column: x => x.VenueLayoutId,
                        principalTable: "VenueLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventSeatInventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: true),
                    EventOccurrenceId = table.Column<int>(type: "int", nullable: true),
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReservedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReservedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSeatInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSeatInventories_AspNetUsers_ReservedByUserId",
                        column: x => x.ReservedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EventSeatInventories_EventOccurrences_EventOccurrenceId",
                        column: x => x.EventOccurrenceId,
                        principalTable: "EventOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSeatInventories_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSeatInventories_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSeatInventories_UserTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "UserTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTickets_EventOccurrenceId",
                table: "UserTickets",
                column: "EventOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTickets_SeatId",
                table: "UserTickets",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueLayoutId",
                table: "Events",
                column: "VenueLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_EventOccurrences_EventSeriesId_StartDateTime",
                table: "EventOccurrences",
                columns: new[] { "EventSeriesId", "StartDateTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventOccurrences_Status",
                table: "EventOccurrences",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeatInventories_EventId_SeatId",
                table: "EventSeatInventories",
                columns: new[] { "EventId", "SeatId" },
                unique: true,
                filter: "[EventId] IS NOT NULL AND [EventOccurrenceId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeatInventories_EventOccurrenceId_SeatId",
                table: "EventSeatInventories",
                columns: new[] { "EventOccurrenceId", "SeatId" },
                unique: true,
                filter: "[EventOccurrenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeatInventories_ReservedByUserId",
                table: "EventSeatInventories",
                column: "ReservedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeatInventories_SeatId",
                table: "EventSeatInventories",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeatInventories_Status_ReservedUntil",
                table: "EventSeatInventories",
                columns: new[] { "Status", "ReservedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSeatInventories_TicketId",
                table: "EventSeatInventories",
                column: "TicketId",
                unique: true,
                filter: "[TicketId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_EventId",
                table: "EventSeries",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_OrganizerId_Status",
                table: "EventSeries",
                columns: new[] { "OrganizerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LayoutSections_VenueLayoutId_Name",
                table: "LayoutSections",
                columns: new[] { "VenueLayoutId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Seats_SectionId",
                table: "Seats",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_VenueLayoutId_Row_Number",
                table: "Seats",
                columns: new[] { "VenueLayoutId", "Row", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueLayouts_OrganizerId_Name_Version",
                table: "VenueLayouts",
                columns: new[] { "OrganizerId", "Name", "Version" });

            migrationBuilder.AddForeignKey(
                name: "FK_Events_VenueLayouts_VenueLayoutId",
                table: "Events",
                column: "VenueLayoutId",
                principalTable: "VenueLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTickets_EventOccurrences_EventOccurrenceId",
                table: "UserTickets",
                column: "EventOccurrenceId",
                principalTable: "EventOccurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTickets_Seats_SeatId",
                table: "UserTickets",
                column: "SeatId",
                principalTable: "Seats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_VenueLayouts_VenueLayoutId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTickets_EventOccurrences_EventOccurrenceId",
                table: "UserTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTickets_Seats_SeatId",
                table: "UserTickets");

            migrationBuilder.DropTable(
                name: "EventSeatInventories");

            migrationBuilder.DropTable(
                name: "EventOccurrences");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "EventSeries");

            migrationBuilder.DropTable(
                name: "LayoutSections");

            migrationBuilder.DropTable(
                name: "VenueLayouts");

            migrationBuilder.DropIndex(
                name: "IX_UserTickets_EventOccurrenceId",
                table: "UserTickets");

            migrationBuilder.DropIndex(
                name: "IX_UserTickets_SeatId",
                table: "UserTickets");

            migrationBuilder.DropIndex(
                name: "IX_Events_VenueLayoutId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EventOccurrenceId",
                table: "UserTickets");

            migrationBuilder.DropColumn(
                name: "SeatId",
                table: "UserTickets");

            migrationBuilder.DropColumn(
                name: "TicketingMode",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "VenueLayoutId",
                table: "Events");
        }
    }
}
