using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecentApptIdToDoctorSessionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastKnownAppointmentNo",
                table: "SessionHistories");

            migrationBuilder.AddColumn<Guid>(
                name: "LastKnownAppointmentId",
                table: "SessionHistories",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastKnownAppointmentId",
                table: "SessionHistories");

            migrationBuilder.AddColumn<int>(
                name: "LastKnownAppointmentNo",
                table: "SessionHistories",
                type: "integer",
                nullable: true);
        }
    }
}
