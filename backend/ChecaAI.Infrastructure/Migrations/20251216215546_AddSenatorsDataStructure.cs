using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChecaAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSenatorsDataStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentLegislaturePublicCode",
                table: "Politicians",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Politicians",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Politicians",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBoardMember",
                table: "Politicians",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLeadershipMember",
                table: "Politicians",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParlamentaryName",
                table: "Politicians",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParlamentaryPageUrl",
                table: "Politicians",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalPageUrl",
                table: "Politicians",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PoliticalBlocId",
                table: "Politicians",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Treatment",
                table: "Politicians",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PoliticalBlocs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nickname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticalBlocs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoliticianMandates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    MandateCode = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ParticipationDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TitularPoliticianId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticianMandates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoliticianMandates_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PoliticianMandates_Politicians_TitularPoliticianId",
                        column: x => x.TitularPoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PoliticianPhones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoliticianId = table.Column<int>(type: "integer", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublicationOrder = table.Column<int>(type: "integer", nullable: false),
                    IsFax = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticianPhones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoliticianPhones_Politicians_PoliticianId",
                        column: x => x.PoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Legislatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MandateId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LegislatureType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Legislatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Legislatures_PoliticianMandates_MandateId",
                        column: x => x.MandateId,
                        principalTable: "PoliticianMandates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MandateExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MandateId = table.Column<int>(type: "integer", nullable: false),
                    ExerciseCode = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeaveReasonCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LeaveReasonDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReadingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MandateExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MandateExercises_PoliticianMandates_MandateId",
                        column: x => x.MandateId,
                        principalTable: "PoliticianMandates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MandateSubstitutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MandateId = table.Column<int>(type: "integer", nullable: false),
                    SubstitutePoliticianId = table.Column<int>(type: "integer", nullable: false),
                    ParticipationDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MandateSubstitutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MandateSubstitutes_PoliticianMandates_MandateId",
                        column: x => x.MandateId,
                        principalTable: "PoliticianMandates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MandateSubstitutes_Politicians_SubstitutePoliticianId",
                        column: x => x.SubstitutePoliticianId,
                        principalTable: "Politicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Politicians_Email",
                table: "Politicians",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Politicians_PoliticalBlocId",
                table: "Politicians",
                column: "PoliticalBlocId");

            migrationBuilder.CreateIndex(
                name: "IX_Legislatures_MandateId",
                table: "Legislatures",
                column: "MandateId");

            migrationBuilder.CreateIndex(
                name: "IX_MandateExercises_ExerciseCode",
                table: "MandateExercises",
                column: "ExerciseCode");

            migrationBuilder.CreateIndex(
                name: "IX_MandateExercises_MandateId",
                table: "MandateExercises",
                column: "MandateId");

            migrationBuilder.CreateIndex(
                name: "IX_MandateSubstitutes_MandateId",
                table: "MandateSubstitutes",
                column: "MandateId");

            migrationBuilder.CreateIndex(
                name: "IX_MandateSubstitutes_SubstitutePoliticianId",
                table: "MandateSubstitutes",
                column: "SubstitutePoliticianId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticalBlocs_Code",
                table: "PoliticalBlocs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianMandates_MandateCode",
                table: "PoliticianMandates",
                column: "MandateCode");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianMandates_PoliticianId",
                table: "PoliticianMandates",
                column: "PoliticianId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianMandates_TitularPoliticianId",
                table: "PoliticianMandates",
                column: "TitularPoliticianId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticianPhones_PoliticianId",
                table: "PoliticianPhones",
                column: "PoliticianId");

            migrationBuilder.AddForeignKey(
                name: "FK_Politicians_PoliticalBlocs_PoliticalBlocId",
                table: "Politicians",
                column: "PoliticalBlocId",
                principalTable: "PoliticalBlocs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Politicians_PoliticalBlocs_PoliticalBlocId",
                table: "Politicians");

            migrationBuilder.DropTable(
                name: "Legislatures");

            migrationBuilder.DropTable(
                name: "MandateExercises");

            migrationBuilder.DropTable(
                name: "MandateSubstitutes");

            migrationBuilder.DropTable(
                name: "PoliticalBlocs");

            migrationBuilder.DropTable(
                name: "PoliticianPhones");

            migrationBuilder.DropTable(
                name: "PoliticianMandates");

            migrationBuilder.DropIndex(
                name: "IX_Politicians_Email",
                table: "Politicians");

            migrationBuilder.DropIndex(
                name: "IX_Politicians_PoliticalBlocId",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "CurrentLegislaturePublicCode",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "IsBoardMember",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "IsLeadershipMember",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "ParlamentaryName",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "ParlamentaryPageUrl",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "PersonalPageUrl",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "PoliticalBlocId",
                table: "Politicians");

            migrationBuilder.DropColumn(
                name: "Treatment",
                table: "Politicians");
        }
    }
}
