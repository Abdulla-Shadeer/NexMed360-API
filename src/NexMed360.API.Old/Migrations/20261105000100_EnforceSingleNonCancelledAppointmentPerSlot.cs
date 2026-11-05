using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360Api.Migrations
{
    public partial class EnforceSingleNonCancelledAppointmentPerSlot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_Appointments_NonCancelledSlot",
                table: "Appointments",
                column: "SessionSlotId",
                unique: true,
                filter: "\"Status\" <> 5");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Appointments_NonCancelledSlot",
                table: "Appointments");
        }
    }
}
