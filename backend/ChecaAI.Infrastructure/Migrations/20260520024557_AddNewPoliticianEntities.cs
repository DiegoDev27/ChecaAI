using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChecaAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewPoliticianEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartyId",
                table: "Politicians",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Allowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    AllowanceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allowances_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CabinetStaff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GrossSalary = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    NetSalary = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Cpf = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Month = table.Column<int>(type: "integer", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CabinetStaff_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Acronym = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: true),
                    FoundedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    President = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Politicians_PartyId",
                table: "Politicians",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Allowances_ExternalId",
                table: "Allowances",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Allowances_PoliticianId_Year_Month_AllowanceType",
                table: "Allowances",
                columns: new[] { "PoliticianId", "Year", "Month", "AllowanceType" });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetStaff_ExternalId",
                table: "CabinetStaff",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_CabinetStaff_PoliticianId_Year_Month",
                table: "CabinetStaff",
                columns: new[] { "PoliticianId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Acronym",
                table: "Parties",
                column: "Acronym",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parties_ExternalId",
                table: "Parties",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Number",
                table: "Parties",
                column: "Number");

            migrationBuilder.AddForeignKey(
                name: "FK_Politicians_Parties_PartyId",
                table: "Politicians",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Politicians_Parties_PartyId",
                table: "Politicians");

            migrationBuilder.DropTable(
                name: "Allowances");

            migrationBuilder.DropTable(
                name: "CabinetStaff");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Politicians_PartyId",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "Politicians");
        }
    }
}
