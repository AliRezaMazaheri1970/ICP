using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Users1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "Project",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCurves_ProjectId",
                table: "CalibrationCurves",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_CalibrationCurves_Project_ProjectId",
                table: "CalibrationCurves",
                column: "ProjectId",
                principalTable: "Project",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalibrationCurves_Project_ProjectId",
                table: "CalibrationCurves");

            migrationBuilder.DropIndex(
                name: "IX_CalibrationCurves_ProjectId",
                table: "CalibrationCurves");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "Project");
        }
    }
}
