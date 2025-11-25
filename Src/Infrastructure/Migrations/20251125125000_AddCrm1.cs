using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrm1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmCertifiedValue_Crm_CrmId",
                table: "CrmCertifiedValue");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CrmCertifiedValue",
                table: "CrmCertifiedValue");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Crm",
                table: "Crm");

            migrationBuilder.RenameTable(
                name: "CrmCertifiedValue",
                newName: "CrmCertifiedValues");

            migrationBuilder.RenameTable(
                name: "Crm",
                newName: "Crms");

            migrationBuilder.RenameIndex(
                name: "IX_CrmCertifiedValue_CrmId_ElementName",
                table: "CrmCertifiedValues",
                newName: "IX_CrmCertifiedValues_CrmId_ElementName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CrmCertifiedValues",
                table: "CrmCertifiedValues",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Crms",
                table: "Crms",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CrmCertifiedValues_Crms_CrmId",
                table: "CrmCertifiedValues",
                column: "CrmId",
                principalTable: "Crms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmCertifiedValues_Crms_CrmId",
                table: "CrmCertifiedValues");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Crms",
                table: "Crms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CrmCertifiedValues",
                table: "CrmCertifiedValues");

            migrationBuilder.RenameTable(
                name: "Crms",
                newName: "Crm");

            migrationBuilder.RenameTable(
                name: "CrmCertifiedValues",
                newName: "CrmCertifiedValue");

            migrationBuilder.RenameIndex(
                name: "IX_CrmCertifiedValues_CrmId_ElementName",
                table: "CrmCertifiedValue",
                newName: "IX_CrmCertifiedValue_CrmId_ElementName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Crm",
                table: "Crm",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CrmCertifiedValue",
                table: "CrmCertifiedValue",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CrmCertifiedValue_Crm_CrmId",
                table: "CrmCertifiedValue",
                column: "CrmId",
                principalTable: "Crm",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
