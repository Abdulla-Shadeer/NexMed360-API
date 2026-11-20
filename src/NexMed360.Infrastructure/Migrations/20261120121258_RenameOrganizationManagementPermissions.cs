using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrganizationManagementPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep existing role grants when the permission names change.
            migrationBuilder.Sql("""
                UPDATE "AccessRoles"
                SET "Permissions" = array_replace(
                    array_replace("Permissions", 'appointments.manage', 'appointments.manage-organization'),
                    'schedules.manage', 'schedules.manage-organization')
                WHERE "Permissions" && ARRAY['appointments.manage', 'schedules.manage'];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AccessRoles"
                SET "Permissions" = array_replace(
                    array_replace("Permissions", 'appointments.manage-organization', 'appointments.manage'),
                    'schedules.manage-organization', 'schedules.manage')
                WHERE "Permissions" && ARRAY['appointments.manage-organization', 'schedules.manage-organization'];
                """);
        }
    }
}
