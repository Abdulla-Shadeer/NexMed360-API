using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinalProjectApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_ClinicRecords_ClinicRecordId1",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_ClinicRecordId1",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "ClinicRecordId1",
                table: "Prescriptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClinicRecordId1",
                table: "Prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ClinicRecordId1",
                table: "Prescriptions",
                column: "ClinicRecordId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_ClinicRecords_ClinicRecordId1",
                table: "Prescriptions",
                column: "ClinicRecordId1",
                principalTable: "ClinicRecords",
                principalColumn: "Id");
        }
    }
}
