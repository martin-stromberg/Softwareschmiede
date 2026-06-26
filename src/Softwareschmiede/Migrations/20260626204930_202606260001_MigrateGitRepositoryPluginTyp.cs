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

            // On-Premises-URLs: PluginTyp='SourceCodeManagement' ohne github.com oder bitbucket.org
            migrationBuilder.Sql(
                "UPDATE GitRepositories SET PluginTyp = 'LocalDirectoryPlugin' " +
                "WHERE PluginTyp = 'SourceCodeManagement' " +
                "AND RepositoryUrl NOT LIKE '%github.com%' " +
                "AND RepositoryUrl NOT LIKE '%bitbucket.org%'");

            // Blazor-UI-Legacy: PluginTyp='GitHub'
            migrationBuilder.Sql(
                "UPDATE GitRepositories SET PluginTyp = 'Softwareschmiede.GitHub' " +
                "WHERE PluginTyp = 'GitHub'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE GitRepositories SET PluginTyp = 'GitHub' " +
                "WHERE PluginTyp = 'Softwareschmiede.GitHub'");

            migrationBuilder.Sql(
                "UPDATE GitRepositories SET PluginTyp = 'SourceCodeManagement' " +
                "WHERE PluginTyp IN ('Softwareschmiede.GitHub', 'Softwareschmiede.Bitbucket', 'LocalDirectoryPlugin')");
        }
    }
}
