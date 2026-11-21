using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFhirExchangeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ReceivingOrganizationId",
                table: "RecordSharingRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "PractitionerRoleId",
                table: "RecordSharingRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ReceivingPartnerId",
                table: "RecordSharingRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterContact",
                table: "RecordSharingRequests",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterName",
                table: "RecordSharingRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterRole",
                table: "RecordSharingRequests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Intent",
                table: "Prescriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Order");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Prescriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            // An old inactive flag does not tell us whether a prescription was stopped or completed.
            migrationBuilder.Sql("""
                UPDATE "Prescriptions"
                SET "Status" = CASE WHEN "IsActive" THEN 'Active' ELSE 'Unknown' END;
                """);

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Prescriptions");

            migrationBuilder.CreateTable(
                name: "ExternalPatientLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalPatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalPatientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfirmedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalPatientLinks", x => x.Id);
                    table.CheckConstraint("CK_ExternalPatientLinks_Identity", "length(btrim(\"PartnerId\")) > 0 AND length(btrim(\"ExternalPatientId\")) > 0 AND length(btrim(\"ConfirmedByUserId\")) > 0");
                    table.ForeignKey(
                        name: "FK_ExternalPatientLinks_AspNetUsers_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalPatientLinks_Patients_LocalPatientId",
                        column: x => x.LocalPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordSharingRequests_PatientId_ReceivingPartnerId_Status_E~",
                table: "RecordSharingRequests",
                columns: new[] { "PatientId", "ReceivingPartnerId", "Status", "ExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecordSharingRequests_Recipient",
                table: "RecordSharingRequests",
                sql: "(\"ReceivingPartnerId\" IS NULL AND \"ReceivingOrganizationId\" IS NOT NULL AND \"PractitionerRoleId\" IS NOT NULL)\nOR (\"ReceivingPartnerId\" IS NOT NULL AND length(btrim(\"ReceivingPartnerId\")) > 0\n    AND \"ReceivingOrganizationId\" IS NULL AND \"PractitionerRoleId\" IS NULL\n    AND \"RequesterName\" IS NOT NULL AND length(btrim(\"RequesterName\")) > 0\n    AND \"RequesterRole\" IS NOT NULL AND length(btrim(\"RequesterRole\")) > 0\n    AND \"RequesterContact\" IS NOT NULL AND length(btrim(\"RequesterContact\")) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Prescriptions_Intent",
                table: "Prescriptions",
                sql: "\"Intent\" IN ('Proposal','Plan','Order','OriginalOrder','ReflexOrder','FillerOrder','InstanceOrder','Option')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Prescriptions_Status",
                table: "Prescriptions",
                sql: "\"Status\" IN ('Active','OnHold','Cancelled','Completed','EnteredInError','Stopped','Draft','Unknown')");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPatientLinks_ConfirmedByUserId",
                table: "ExternalPatientLinks",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPatientLinks_LocalPatientId",
                table: "ExternalPatientLinks",
                column: "LocalPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPatientLinks_PartnerId_ExternalPatientId",
                table: "ExternalPatientLinks",
                columns: new[] { "PartnerId", "ExternalPatientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPatientLinks_PartnerId_LocalPatientId",
                table: "ExternalPatientLinks",
                columns: new[] { "PartnerId", "LocalPatientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema cannot represent external recipients or richer prescription values.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "ExternalPatientLinks")
                        OR EXISTS (SELECT 1 FROM "RecordSharingRequests" WHERE "ReceivingPartnerId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "Prescriptions" WHERE "Status" NOT IN ('Active', 'Unknown') OR "Intent" <> 'Order') THEN
                        RAISE EXCEPTION 'Cannot roll back while external links, external requests, or richer prescription values exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Prescriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Prescriptions" SET "IsActive" = ("Status" = 'Active');
                """);

            migrationBuilder.DropTable(
                name: "ExternalPatientLinks");

            migrationBuilder.DropIndex(
                name: "IX_RecordSharingRequests_PatientId_ReceivingPartnerId_Status_E~",
                table: "RecordSharingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecordSharingRequests_Recipient",
                table: "RecordSharingRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Prescriptions_Intent",
                table: "Prescriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Prescriptions_Status",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "ReceivingPartnerId",
                table: "RecordSharingRequests");

            migrationBuilder.DropColumn(
                name: "RequesterContact",
                table: "RecordSharingRequests");

            migrationBuilder.DropColumn(
                name: "RequesterName",
                table: "RecordSharingRequests");

            migrationBuilder.DropColumn(
                name: "RequesterRole",
                table: "RecordSharingRequests");

            migrationBuilder.DropColumn(
                name: "Intent",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Prescriptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReceivingOrganizationId",
                table: "RecordSharingRequests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PractitionerRoleId",
                table: "RecordSharingRequests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);


        }
    }
}
