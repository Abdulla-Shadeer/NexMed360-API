using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360Api.Migrations
{
    /// <inheritdoc />
    public partial class SessionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Location",
                table: "Appointments");

            migrationBuilder.CreateTable(
                name: "DoctorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Occurrences = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSessions_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DoctorSessions_Hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalTable: "Hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionSlots_DoctorSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DoctorSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSessions_DoctorId",
                table: "DoctorSessions",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSessions_HospitalId",
                table: "DoctorSessions",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionSlots_SessionId",
                table: "SessionSlots",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionSlots");

            migrationBuilder.DropTable(
                name: "DoctorSessions");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Appointments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
