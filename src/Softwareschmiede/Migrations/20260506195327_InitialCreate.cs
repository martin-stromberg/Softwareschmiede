using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PluginKonfigurationen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginTyp = table.Column<string>(type: "TEXT", nullable: false),
                    PluginKategorie = table.Column<string>(type: "TEXT", nullable: false),
                    AnzeigeName = table.Column<string>(type: "TEXT", nullable: false),
                    CredentialStoreKey = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Aktiviert = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginKonfigurationen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projekte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Beschreibung = table.Column<string>(type: "TEXT", nullable: true),
                    ErstellungsDatum = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projekte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitRepositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjektId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginTyp = table.Column<string>(type: "TEXT", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "TEXT", nullable: false),
                    RepositoryName = table.Column<string>(type: "TEXT", nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitRepositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitRepositories_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Aufgaben",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjektId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitRepositoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    AnforderungsBeschreibung = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", nullable: true),
                    LokalerKlonPfad = table.Column<string>(type: "TEXT", nullable: true),
                    AgentenpaketName = table.Column<string>(type: "TEXT", nullable: true),
                    AgentenName = table.Column<string>(type: "TEXT", nullable: true),
                    ErstellungsDatum = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AbschlussDatum = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aufgaben", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aufgaben_GitRepositories_GitRepositoryId",
                        column: x => x.GitRepositoryId,
                        principalTable: "GitRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Aufgaben_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueReferenzen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssueNummer = table.Column<int>(type: "INTEGER", nullable: true),
                    Titel = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    LabelsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Milestone = table.Column<string>(type: "TEXT", nullable: true),
                    IssueUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueReferenzen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueReferenzen_Aufgaben_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgaben",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Protokolleintraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Typ = table.Column<string>(type: "TEXT", nullable: false),
                    Inhalt = table.Column<string>(type: "TEXT", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    Zeitstempel = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Protokolleintraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Protokolleintraege_Aufgaben_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgaben",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestErgebnisse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProtokollEintragId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Fehlermeldung = table.Column<string>(type: "TEXT", nullable: true),
                    Dauer = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestErgebnisse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestErgebnisse_Protokolleintraege_ProtokollEintragId",
                        column: x => x.ProtokollEintragId,
                        principalTable: "Protokolleintraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aufgaben_GitRepositoryId",
                table: "Aufgaben",
                column: "GitRepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Aufgaben_ProjektId",
                table: "Aufgaben",
                column: "ProjektId");

            migrationBuilder.CreateIndex(
                name: "IX_GitRepositories_ProjektId",
                table: "GitRepositories",
                column: "ProjektId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueReferenzen_AufgabeId",
                table: "IssueReferenzen",
                column: "AufgabeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Protokolleintraege_AufgabeId",
                table: "Protokolleintraege",
                column: "AufgabeId");

            migrationBuilder.CreateIndex(
                name: "IX_TestErgebnisse_ProtokollEintragId",
                table: "TestErgebnisse",
                column: "ProtokollEintragId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueReferenzen");

            migrationBuilder.DropTable(
                name: "PluginKonfigurationen");

            migrationBuilder.DropTable(
                name: "TestErgebnisse");

            migrationBuilder.DropTable(
                name: "Protokolleintraege");

            migrationBuilder.DropTable(
                name: "Aufgaben");

            migrationBuilder.DropTable(
                name: "GitRepositories");

            migrationBuilder.DropTable(
                name: "Projekte");
        }
    }
}
