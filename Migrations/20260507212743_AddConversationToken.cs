using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApp.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Token",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.Sql(
                "UPDATE \"Conversations\" SET \"Token\" = gen_random_uuid() WHERE \"Token\" = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Token",
                table: "Conversations",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_Token",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Conversations");
        }
    }
}
