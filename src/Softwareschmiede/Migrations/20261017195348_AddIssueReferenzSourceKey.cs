using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueReferenzSourceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "IssueReferenzen",
                type: "TEXT",
                maxLength: 700,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueReferenzen_SourceKey",
                table: "IssueReferenzen",
                column: "SourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IssueReferenzen_SourceKey",
                table: "IssueReferenzen");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "IssueReferenzen");
        }
    }
}
