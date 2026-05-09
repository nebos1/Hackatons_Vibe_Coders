using System;
using EventsApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EventsApp.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260509120500_AddTicketSectionPrices")]
    public partial class AddTicketSectionPrices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketSectionPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSectionPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketSectionPrices_LayoutSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "LayoutSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketSectionPrices_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketSectionPrices_SectionId",
                table: "TicketSectionPrices",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketSectionPrices_TicketId_SectionId",
                table: "TicketSectionPrices",
                columns: new[] { "TicketId", "SectionId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSectionPrices");
        }
    }
}
