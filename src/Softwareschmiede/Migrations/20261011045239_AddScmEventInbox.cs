using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddScmEventInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScmContentSnapshots",
                columns: table => new
                {
                    ContentKey = table.Column<string>(type: "TEXT", nullable: false),
                    BodyHash = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<string>(type: "TEXT", nullable: false),
                    IdentityResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    Diagnostic = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScmContentSnapshots", x => x.ContentKey);
                });

            migrationBuilder.CreateTable(
                name: "ScmIncomingEvents",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObservationJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    ReceivedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    LeaseOwner = table.Column<string>(type: "TEXT", nullable: true),
                    LeaseUntilUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    DeliveryAttemptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CliRunId = table.Column<string>(type: "TEXT", nullable: true),
                    ExchangePath = table.Column<string>(type: "TEXT", nullable: true),
                    ExchangeRestrictedWriteSidsJson = table.Column<string>(type: "TEXT", nullable: true),
                    DeliveredUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    AcceptedUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ReadyConfirmedRunId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScmIncomingEvents", x => x.Sequence);
                });

            migrationBuilder.CreateTable(
                name: "ScmObservationCheckpoints",
                columns: table => new
                {
                    SourceKey = table.Column<string>(type: "TEXT", nullable: false),
                    WatermarkUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    Failures = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    Diagnostic = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScmObservationCheckpoints", x => x.SourceKey);
                });

            migrationBuilder.CreateTable(
                name: "ScmOutgoingActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttemptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RemoteId = table.Column<string>(type: "TEXT", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScmOutgoingActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScmTaskLeases",
                columns: table => new
                {
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScmTaskLeases", x => x.AufgabeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScmIncomingEvents_AufgabeId_Sequence",
                table: "ScmIncomingEvents",
                columns: new[] { "AufgabeId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ScmIncomingEvents_EventKey",
                table: "ScmIncomingEvents",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScmIncomingEvents_Id",
                table: "ScmIncomingEvents",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScmIncomingEvents_Status_NextAttemptUtc",
                table: "ScmIncomingEvents",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScmOutgoingActions_EventId_Status",
                table: "ScmOutgoingActions",
                columns: new[] { "EventId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScmContentSnapshots");

            migrationBuilder.DropTable(
                name: "ScmIncomingEvents");

            migrationBuilder.DropTable(
                name: "ScmObservationCheckpoints");

            migrationBuilder.DropTable(
                name: "ScmOutgoingActions");

            migrationBuilder.DropTable(
                name: "ScmTaskLeases");
        }
    }
}
