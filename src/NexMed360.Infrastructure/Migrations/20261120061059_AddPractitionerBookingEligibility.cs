using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPractitionerBookingEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsAppointments",
                table: "PractitionerRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsAppointments",
                table: "PractitionerRoles");
        }
    }
}
