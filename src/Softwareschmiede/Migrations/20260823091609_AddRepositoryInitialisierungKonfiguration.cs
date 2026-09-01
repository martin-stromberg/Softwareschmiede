using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryInitialisierungKonfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositoryInitialisierungKonfigurationen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitRepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InitialisierungsskriptRelativePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryInitialisierungKonfigurationen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryInitialisierungKonfigurationen_GitRepositories_GitRepositoryId",
                        column: x => x.GitRepositoryId,
                        principalTable: "GitRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryInitialisierungKonfigurationen_GitRepositoryId",
                table: "RepositoryInitialisierungKonfigurationen",
                column: "GitRepositoryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryInitialisierungKonfigurationen");
        }
    }
}
