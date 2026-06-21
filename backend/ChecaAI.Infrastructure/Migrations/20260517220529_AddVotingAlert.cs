using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChecaAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVotingAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VotingAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VotingSessionId = table.Column<int>(type: "integer", nullable: false),
                    AlertLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ScoreBreakdown = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SummaryText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SignalRSent = table.Column<bool>(type: "boolean", nullable: false),
                    PushSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotingAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VotingAlerts_VotingSessions_VotingSessionId",
                        column: x => x.VotingSessionId,
                        principalTable: "VotingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VotingAlerts_DetectedAt",
                table: "VotingAlerts",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VotingAlerts_VotingSessionId",
                table: "VotingAlerts",
                column: "VotingSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VotingAlerts");
        }
    }
}
