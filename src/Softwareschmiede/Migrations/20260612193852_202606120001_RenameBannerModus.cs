using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202606120001_RenameBannerModus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ToastModus",
                table: "BenachrichtigungsEinstellungen",
                newName: "BannerModus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BannerModus",
                table: "BenachrichtigungsEinstellungen",
                newName: "ToastModus");
        }
    }
}
