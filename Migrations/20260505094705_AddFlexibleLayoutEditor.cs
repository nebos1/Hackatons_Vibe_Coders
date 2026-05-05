using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFlexibleLayoutEditor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Seats_SectionId",
                table: "Seats");

            migrationBuilder.DropIndex(
                name: "IX_Seats_VenueLayoutId_Row_Number",
                table: "Seats");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Seats",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Seats",
                type: "nvarchar(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Radius",
                table: "Seats",
                type: "float",
                nullable: false,
                defaultValue: 16.0);

            migrationBuilder.AddColumn<double>(
                name: "Rotation",
                table: "Seats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "FloorName",
                table: "LayoutSections",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Floor 1");

            migrationBuilder.AddColumn<double>(
                name: "Rotation",
                table: "LayoutSections",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Shape",
                table: "LayoutSections",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Rectangle");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_SectionId_Row_Number",
                table: "Seats",
                columns: new[] { "SectionId", "Row", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seats_VenueLayoutId",
                table: "Seats",
                column: "VenueLayoutId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Seats_SectionId_Row_Number",
                table: "Seats");

            migrationBuilder.DropIndex(
                name: "IX_Seats_VenueLayoutId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Radius",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "FloorName",
                table: "LayoutSections");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "LayoutSections");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "LayoutSections");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_SectionId",
                table: "Seats",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_VenueLayoutId_Row_Number",
                table: "Seats",
                columns: new[] { "VenueLayoutId", "Row", "Number" },
                unique: true);
        }
    }
}
