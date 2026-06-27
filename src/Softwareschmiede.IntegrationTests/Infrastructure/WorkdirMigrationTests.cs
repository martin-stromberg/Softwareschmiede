using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.IntegrationTests.Infrastructure;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

/// <summary>Prüft, dass kritische Migrationen vorwärts und rückwärts angewendet werden können.</summary>
public sealed class WorkdirMigrationTests
{
    private const string WorkdirMigration = "20260509200234_202605090001_Add_AppEinstellung_Workdir";
    private const string PreviousMigration = "20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering";
    private const string AddKiPluginPrefixMigration = "20260524151645_202605241703_AddKiPluginPrefix";
    private const string PreviousBeforeKiPluginPrefixMigration = "20260523113807_AddKiTaskNotifications";

    /// <summary>Prüft, dass die Workdir-Migration angewendet und zurückgerollt werden kann.</summary>
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

    /// <summary>Prüft, dass die KiPluginPrefix-Spalte als nullable hinzugefügt und entfernt werden kann.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldApplyAndRollbackKiPluginPrefixMigration_AsNullableColumn()
    {
        await using var db = await DatabaseFixture.CreateAsync();

        (await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix")).Should().NotBeNull();

        await db.Context.Database.MigrateAsync(PreviousBeforeKiPluginPrefixMigration);
        (await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix")).Should().BeNull();

        await db.Context.Database.MigrateAsync(AddKiPluginPrefixMigration);
        var column = await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix");
        column.Should().NotBeNull();
        column!.NotNull.Should().BeFalse("Migration muss rückwärtskompatibel als nullable sein.");
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

    private static async Task<TableColumnInfo?> ColumnInfoAsync(DatabaseFixture db, string tableName, string columnName)
    {
        await using var cmd = db.Context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"PRAGMA table_info([{tableName}]);";

        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
        {
            await cmd.Connection.OpenAsync();
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var currentName = reader.GetString(reader.GetOrdinal("name"));
            if (!string.Equals(currentName, columnName, StringComparison.Ordinal))
            {
                continue;
            }

            return new TableColumnInfo(currentName, reader.GetInt32(reader.GetOrdinal("notnull")) == 1);
        }

        return null;
    }

    private sealed record TableColumnInfo(string Name, bool NotNull);
}
