using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChecaAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCabinetStaffPoliticianIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CabinetStaff_Politicians_PoliticianId",
                table: "CabinetStaff");

            migrationBuilder.AlterColumn<int>(
                name: "PoliticianId",
                table: "CabinetStaff",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_CabinetStaff_Politicians_PoliticianId",
                table: "CabinetStaff",
                column: "PoliticianId",
                principalTable: "Politicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CabinetStaff_Politicians_PoliticianId",
                table: "CabinetStaff");

            migrationBuilder.AlterColumn<int>(
                name: "PoliticianId",
                table: "CabinetStaff",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CabinetStaff_Politicians_PoliticianId",
                table: "CabinetStaff",
                column: "PoliticianId",
                principalTable: "Politicians",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
