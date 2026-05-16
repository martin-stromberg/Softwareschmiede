using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202605141200_AddRepositoryStartKonfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryStartKonfigurationen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitRepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartScriptRelativePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    StartScriptArgumentsTemplate = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PortModus = table.Column<string>(type: "TEXT", nullable: false),
                    PortBereichVon = table.Column<int>(type: "INTEGER", nullable: true),
                    PortBereichBis = table.Column<int>(type: "INTEGER", nullable: true),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryStartKonfigurationen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryStartKonfigurationen_GitRepositories_GitRepositoryId",
                        column: x => x.GitRepositoryId,
                        principalTable: "GitRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryStartKonfigurationen_GitRepositoryId",
                table: "RepositoryStartKonfigurationen",
                column: "GitRepositoryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryStartKonfigurationen");
        }
    }
}
