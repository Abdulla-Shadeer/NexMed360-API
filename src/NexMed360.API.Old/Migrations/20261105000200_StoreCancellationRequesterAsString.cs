using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexMed360Api.Migrations
{
    public partial class StoreCancellationRequesterAsString : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "CancellationRequests"
                ALTER COLUMN "RequestedById" TYPE text USING "RequestedById"::text;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL rejects a rollback if a stored string cannot be represented as UUID.
            // No history is deleted or replaced to make that conversion succeed.
            migrationBuilder.Sql("""
                ALTER TABLE "CancellationRequests"
                ALTER COLUMN "RequestedById" TYPE uuid USING "RequestedById"::uuid;
                """);
        }
    }
}
