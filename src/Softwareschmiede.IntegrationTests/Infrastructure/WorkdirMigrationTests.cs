using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

public sealed class WorkdirMigrationTests
{
    private const string WorkdirMigration = "20260509200234_202605090001_Add_AppEinstellung_Workdir";
    private const string PreviousMigration = "20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering";

    [Fact]
    public async Task MigrateAsync_ShouldApplyAndRollbackWorkdirMigration()
    {
        await using var db = await DatabaseFixture.CreateAsync();

        (await TableExistsAsync(db, "AppEinstellungen")).Should().BeTrue();

        await db.Context.Database.MigrateAsync(PreviousMigration);
        (await TableExistsAsync(db, "AppEinstellungen")).Should().BeFalse();

        await db.Context.Database.MigrateAsync(WorkdirMigration);
        (await TableExistsAsync(db, "AppEinstellungen")).Should().BeTrue();
    }

    private static async Task<bool> TableExistsAsync(DatabaseFixture db, string tableName)
    {
        await using var cmd = db.Context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1;";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        cmd.Parameters.Add(parameter);

        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
        {
            await cmd.Connection.OpenAsync();
        }

        var result = await cmd.ExecuteScalarAsync();
        return result is not null;
    }
}
