using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddPullRequestReviewSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PullRequestReferenzen_AufgabeId",
                table: "PullRequestReferenzen");

            migrationBuilder.AddColumn<string>(
                name: "Rolle",
                table: "PullRequestReferenzen",
                type: "TEXT",
                nullable: false,
                defaultValue: "CreatedByTask");

            migrationBuilder.AddColumn<string>(
                name: "SourceRef",
                table: "PullRequestReferenzen",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRepositoryId",
                table: "PullRequestReferenzen",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceRepositoryUrl",
                table: "PullRequestReferenzen",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReferenzen_AufgabeId_ReviewSource",
                table: "PullRequestReferenzen",
                column: "AufgabeId",
                unique: true,
                filter: "[Rolle] = 'ReviewSource'");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReferenzen_AufgabeId",
                table: "PullRequestReferenzen",
                column: "AufgabeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PullRequestReferenzen_AufgabeId_ReviewSource",
                table: "PullRequestReferenzen");

            migrationBuilder.DropIndex(
                name: "IX_PullRequestReferenzen_AufgabeId",
                table: "PullRequestReferenzen");

            migrationBuilder.DropColumn(
                name: "Rolle",
                table: "PullRequestReferenzen");

            migrationBuilder.DropColumn(
                name: "SourceRef",
                table: "PullRequestReferenzen");

            migrationBuilder.DropColumn(
                name: "SourceRepositoryId",
                table: "PullRequestReferenzen");

            migrationBuilder.DropColumn(
                name: "SourceRepositoryUrl",
                table: "PullRequestReferenzen");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReferenzen_AufgabeId",
                table: "PullRequestReferenzen",
                column: "AufgabeId");
        }
    }
}
