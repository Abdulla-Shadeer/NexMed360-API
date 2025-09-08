using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinalProjectApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionSlotId",
                table: "Appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_SessionSlotId",
                table: "Appointments",
                column: "SessionSlotId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_SessionSlots_SessionSlotId",
                table: "Appointments",
                column: "SessionSlotId",
                principalTable: "SessionSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_SessionSlots_SessionSlotId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_SessionSlotId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SessionSlotId",
                table: "Appointments");
        }
    }
}
