using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChecaAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBacklogEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    ElectionYear = table.Column<int>(type: "integer", nullable: false),
                    AssetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeclaredValue = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDeclarations_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    ElectionYear = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderCnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignExpenses_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Committees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Acronym = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CommitteeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Chamber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Committees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectionResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    ElectionYear = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VotesReceived = table.Column<long>(type: "bigint", nullable: false),
                    TotalVotes = table.Column<long>(type: "bigint", nullable: false),
                    VoteShare = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    IsElected = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionResults_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoliticianSalaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    NetSalary = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Allowances = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticianSalaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoliticianSalaries_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    SessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsPresent = table.Column<bool>(type: "boolean", nullable: false),
                    AbsenceReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AbsenceJustification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Chamber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionAttendances_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommitteeId = table.Column<int>(type: "integer", nullable: false),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeMemberships_Committees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommitteeMemberships_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDeclarations_ExternalId",
                table: "AssetDeclarations",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDeclarations_PoliticianId",
                table: "AssetDeclarations",
                column: "PoliticianId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignExpenses_ExternalId",
                table: "CampaignExpenses",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignExpenses_PoliticianId",
                table: "CampaignExpenses",
                column: "PoliticianId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_CommitteeId_PoliticianId",
                table: "CommitteeMemberships",
                columns: new[] { "CommitteeId", "PoliticianId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMemberships_PoliticianId",
                table: "CommitteeMemberships",
                column: "PoliticianId");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_ExternalId",
                table: "Committees",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_ExternalId",
                table: "ElectionResults",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionResults_PoliticianId_ElectionYear",
                table: "ElectionResults",
                columns: new[] { "PoliticianId", "ElectionYear" });

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianSalaries_ExternalId",
                table: "PoliticianSalaries",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianSalaries_PoliticianId_Year_Month",
                table: "PoliticianSalaries",
                columns: new[] { "PoliticianId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionAttendances_ExternalId",
                table: "SessionAttendances",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionAttendances_PoliticianId_SessionDate",
                table: "SessionAttendances",
                columns: new[] { "PoliticianId", "SessionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetDeclarations");

            migrationBuilder.DropTable(
                name: "CampaignExpenses");

            migrationBuilder.DropTable(
                name: "CommitteeMemberships");

            migrationBuilder.DropTable(
                name: "ElectionResults");

            migrationBuilder.DropTable(
                name: "PoliticianSalaries");

            migrationBuilder.DropTable(
                name: "SessionAttendances");

            migrationBuilder.DropTable(
                name: "Committees");
        }
    }
}
