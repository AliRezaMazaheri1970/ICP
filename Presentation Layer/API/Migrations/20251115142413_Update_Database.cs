using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class Update_Database : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Measurement_Sample_Element",
                table: "Measurements");

            migrationBuilder.RenameIndex(
                name: "IX_Measurement_SampleId",
                table: "Measurements",
                newName: "IX_Measurements_SampleId");

            migrationBuilder.RenameIndex(
                name: "IX_Measurement_ElementId",
                table: "Measurements",
                newName: "IX_Measurements_ElementId");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "Measurements",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Measurements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Isotope",
                table: "Measurements",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IsValid",
                table: "Measurements",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Measurements",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElementSymbol",
                table: "Measurements",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "RawIntensity",
                table: "Measurements",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElementSymbol",
                table: "Measurements");

            migrationBuilder.DropColumn(
                name: "RawIntensity",
                table: "Measurements");

            migrationBuilder.RenameIndex(
                name: "IX_Measurements_SampleId",
                table: "Measurements",
                newName: "IX_Measurement_SampleId");

            migrationBuilder.RenameIndex(
                name: "IX_Measurements_ElementId",
                table: "Measurements",
                newName: "IX_Measurement_ElementId");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "Measurements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Measurements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Isotope",
                table: "Measurements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsValid",
                table: "Measurements",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Measurements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Measurement_Sample_Element",
                table: "Measurements",
                columns: new[] { "SampleId", "ElementId" });
        }
    }
}
