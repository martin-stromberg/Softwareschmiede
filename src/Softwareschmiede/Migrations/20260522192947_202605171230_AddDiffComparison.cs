using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202605171230_AddDiffComparison : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiffResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AufgabeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitRepositoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProtokollEintragId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DiffType = table.Column<string>(type: "TEXT", nullable: false),
                    LineCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedLines = table.Column<int>(type: "INTEGER", nullable: false),
                    RemovedLines = table.Column<int>(type: "INTEGER", nullable: false),
                    ModifiedLines = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceContent = table.Column<string>(type: "TEXT", nullable: true),
                    TargetContent = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffResults_Aufgaben_AufgabeId",
                        column: x => x.AufgabeId,
                        principalTable: "Aufgaben",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiffResults_GitRepositories_GitRepositoryId",
                        column: x => x.GitRepositoryId,
                        principalTable: "GitRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiffResults_Protokolleintraege_ProtokollEintragId",
                        column: x => x.ProtokollEintragId,
                        principalTable: "Protokolleintraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DiffBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiffResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlockType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceStartLine = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceEndLine = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetStartLine = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetEndLine = table.Column<int>(type: "INTEGER", nullable: false),
                    BlockSequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffBlocks_DiffResults_DiffResultId",
                        column: x => x.DiffResultId,
                        principalTable: "DiffResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiffCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiffResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CacheKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CachedData = table.Column<string>(type: "TEXT", nullable: false),
                    CachedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CachingStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffCaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffCaches_DiffResults_DiffResultId",
                        column: x => x.DiffResultId,
                        principalTable: "DiffResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiffLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiffBlockId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    SourceLineNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetLineNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    LineSequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiffLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiffLines_DiffBlocks_DiffBlockId",
                        column: x => x.DiffBlockId,
                        principalTable: "DiffBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiffBlocks_DiffResultId",
                table: "DiffBlocks",
                column: "DiffResultId");

            migrationBuilder.CreateIndex(
                name: "IX_DiffBlocks_DiffResultId_BlockSequence",
                table: "DiffBlocks",
                columns: new[] { "DiffResultId", "BlockSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_DiffCaches_CacheKey",
                table: "DiffCaches",
                column: "CacheKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiffCaches_DiffResultId",
                table: "DiffCaches",
                column: "DiffResultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiffCaches_ExpiresAt",
                table: "DiffCaches",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiffLines_DiffBlockId",
                table: "DiffLines",
                column: "DiffBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_DiffLines_DiffBlockId_LineSequence",
                table: "DiffLines",
                columns: new[] { "DiffBlockId", "LineSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_DiffResults_AufgabeId",
                table: "DiffResults",
                column: "AufgabeId");

            migrationBuilder.CreateIndex(
                name: "IX_DiffResults_AufgabeId_FilePath",
                table: "DiffResults",
                columns: new[] { "AufgabeId", "FilePath" });

            migrationBuilder.CreateIndex(
                name: "IX_DiffResults_ExpiresAt",
                table: "DiffResults",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiffResults_GitRepositoryId",
                table: "DiffResults",
                column: "GitRepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DiffResults_ProtokollEintragId",
                table: "DiffResults",
                column: "ProtokollEintragId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiffResults_Status",
                table: "DiffResults",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiffCaches");

            migrationBuilder.DropTable(
                name: "DiffLines");

            migrationBuilder.DropTable(
                name: "DiffBlocks");

            migrationBuilder.DropTable(
                name: "DiffResults");
        }
    }
}
