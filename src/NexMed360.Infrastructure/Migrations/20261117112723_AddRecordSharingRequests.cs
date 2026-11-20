using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordSharingRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecordSharingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivingOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PractitionerRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecisionByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordSharingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordSharingRequests_Organizations_ReceivingOrganizationId",
                        column: x => x.ReceivingOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecordSharingRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecordSharingRequests_PractitionerRoles_PractitionerRoleId_~",
                        columns: x => new { x.PractitionerRoleId, x.ReceivingOrganizationId },
                        principalTable: "PractitionerRoles",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordSharingRequests_PatientId_ReceivingOrganizationId_Sta~",
                table: "RecordSharingRequests",
                columns: new[] { "PatientId", "ReceivingOrganizationId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordSharingRequests_PractitionerRoleId_ReceivingOrganizat~",
                table: "RecordSharingRequests",
                columns: new[] { "PractitionerRoleId", "ReceivingOrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordSharingRequests_ReceivingOrganizationId",
                table: "RecordSharingRequests",
                column: "ReceivingOrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordSharingRequests");
        }
    }
}
