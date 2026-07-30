using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202607290001_AddPullRequestMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PullRequestReferenzen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PullRequestNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderPullRequestId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Titel = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceBranch = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    TargetBranch = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    HeadSha = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MergeCommitSha = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    MergeStatus = table.Column<string>(type: "TEXT", nullable: false),
                    MonitoringPhase = table.Column<string>(type: "TEXT", nullable: false),
                    LastCheckedUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    NextCheckUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestReferenzen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PullRequestReferenzen_Aufgaben_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgaben",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PullRequestWorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PullRequestReferenzId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderRunId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    HeadSha = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Conclusion = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    IsPostMerge = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestWorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PullRequestWorkflowRuns_PullRequestReferenzen_PullRequestReferenzId",
                        column: x => x.PullRequestReferenzId,
                        principalTable: "PullRequestReferenzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReferenzen_AufgabeId",
                table: "PullRequestReferenzen",
                column: "AufgabeId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReferenzen_MonitoringPhase_LastCheckedUtc",
                table: "PullRequestReferenzen",
                columns: new[] { "MonitoringPhase", "LastCheckedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReferenzen_Provider_RepositoryId_PullRequestNumber",
                table: "PullRequestReferenzen",
                columns: new[] { "Provider", "RepositoryId", "PullRequestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestWorkflowRuns_ProviderRunId",
                table: "PullRequestWorkflowRuns",
                column: "ProviderRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestWorkflowRuns_PullRequestReferenzId",
                table: "PullRequestWorkflowRuns",
                column: "PullRequestReferenzId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestWorkflowRuns_PullRequestReferenzId_ProviderRunId",
                table: "PullRequestWorkflowRuns",
                columns: new[] { "PullRequestReferenzId", "ProviderRunId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PullRequestWorkflowRuns");

            migrationBuilder.DropTable(
                name: "PullRequestReferenzen");
        }
    }
}
