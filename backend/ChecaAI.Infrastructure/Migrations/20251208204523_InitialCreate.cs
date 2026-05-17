using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChecaAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Politicians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Cpf = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PoliticalPosition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Party = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Politicians", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Chamber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoliticianExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExpenseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Month = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticianExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoliticianExpenses_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VotingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VotingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalVotes = table.Column<int>(type: "integer", nullable: false),
                    VotesYes = table.Column<int>(type: "integer", nullable: false),
                    VotesNo = table.Column<int>(type: "integer", nullable: false),
                    VotesAbstention = table.Column<int>(type: "integer", nullable: false),
                    VotesAbsent = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Chamber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VotingSessions_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    VotingSessionId = table.Column<int>(type: "integer", nullable: false),
                    VoteValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Justification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Votes_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_VotingSessions_VotingSessionId",
                        column: x => x.VotingSessionId,
                        principalTable: "VotingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianExpenses_ExternalId",
                table: "PoliticianExpenses",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianExpenses_PoliticianId",
                table: "PoliticianExpenses",
                column: "PoliticianId");

            migrationBuilder.CreateIndex(
                name: "IX_Politicians_Cpf",
                table: "Politicians",
                column: "Cpf");

            migrationBuilder.CreateIndex(
                name: "IX_Politicians_ExternalId",
                table: "Politicians",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ExternalId",
                table: "Proposals",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PoliticianId_VotingSessionId",
                table: "Votes",
                columns: new[] { "PoliticianId", "VotingSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VotingSessionId",
                table: "Votes",
                column: "VotingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_VotingSessions_ExternalId",
                table: "VotingSessions",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_VotingSessions_ProposalId",
                table: "VotingSessions",
                column: "ProposalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PoliticianExpenses");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "Politicians");

            migrationBuilder.DropTable(
                name: "VotingSessions");

            migrationBuilder.DropTable(
                name: "Proposals");
        }
    }
}
