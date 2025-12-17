using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorSessionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    SessionStatus = table.Column<int>(type: "integer", nullable: false),
                    LastKnownAppointmentNo = table.Column<int>(type: "integer", nullable: true),
                    EncountersCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionHistories_DoctorSessions_DoctorSessionId",
                        column: x => x.DoctorSessionId,
                        principalTable: "DoctorSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionHistories_DoctorSessionId",
                table: "SessionHistories",
                column: "DoctorSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionHistories");
        }
    }
}
