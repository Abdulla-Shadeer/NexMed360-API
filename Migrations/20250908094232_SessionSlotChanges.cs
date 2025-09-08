using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinalProjectApi.Migrations
{
    /// <inheritdoc />
    public partial class SessionSlotChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BookingAllowed",
                table: "SessionSlots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SlotNumber",
                table: "SessionSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookingAllowed",
                table: "SessionSlots");

            migrationBuilder.DropColumn(
                name: "SlotNumber",
                table: "SessionSlots");
        }
    }
}
