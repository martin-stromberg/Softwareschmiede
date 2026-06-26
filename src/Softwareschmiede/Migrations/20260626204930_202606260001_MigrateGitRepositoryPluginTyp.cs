using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Softwareschmiede.Migrations
{
    /// <inheritdoc />
    public partial class _202606260001_MigrateGitRepositoryPluginTyp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alte Einträge mit PluginTyp='SourceCodeManagement' (gespeicherter PluginType.ToString()-Wert)
            // werden anhand der Repository-URL auf den korrekten PluginPrefix migriert.
            migrationBuilder.Sql(
                "UPDATE GitRepositories SET PluginTyp = 'Softwareschmiede.GitHub' " +
                "WHERE PluginTyp = 'SourceCodeManagement' AND RepositoryUrl LIKE '%github.com%'");

            migrationBuilder.Sql(
                "UPDATE GitRepositories SET PluginTyp = 'Softwareschmiede.Bitbucket' " +
                "WHERE PluginTyp = 'SourceCodeManagement' AND RepositoryUrl LIKE '%bitbucket.org%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE GitRepositories SET PluginTyp = 'SourceCodeManagement' " +
                "WHERE PluginTyp IN ('Softwareschmiede.GitHub', 'Softwareschmiede.Bitbucket')");
        }
    }
}
