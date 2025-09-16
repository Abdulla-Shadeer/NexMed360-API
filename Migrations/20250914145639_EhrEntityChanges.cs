using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinalProjectApi.Migrations
{
    /// <inheritdoc />
    public partial class EhrEntityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "Prescriptions");

            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "Prescriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLongTermUse",
                table: "Prescriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "IsLongTermUse",
                table: "Prescriptions");

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "Prescriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
