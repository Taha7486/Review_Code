using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dotnet_api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeRawOutputToLongText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Change raw_output column from JSON to LONGTEXT to support compressed data
            migrationBuilder.AlterColumn<string>(
                name: "raw_output",
                table: "analysis_runs",
                type: "LONGTEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "JSON",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert back to JSON type (note: this may fail if data is not valid JSON)
            migrationBuilder.AlterColumn<string>(
                name: "raw_output",
                table: "analysis_runs",
                type: "JSON",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "LONGTEXT",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
