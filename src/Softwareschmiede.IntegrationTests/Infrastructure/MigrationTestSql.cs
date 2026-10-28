using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

/// <summary>Roh-SQL-Hilfen für Migrationstests, die unabhängig vom aktuellen EF-Modell arbeiten.</summary>
internal static class MigrationTestSql
{
    /// <summary>Führt einen SQL-Befehl ohne Ergebnis auf der Verbindung der Fixture aus.</summary>
    /// <param name="db">Fixture, deren Verbindung verwendet wird.</param>
    /// <param name="commandText">Auszuführender SQL-Text.</param>
    /// <param name="parameters">Benannte Parameter für den SQL-Text.</param>
    /// <returns>Ein Task, der nach Ausführung des Befehls abgeschlossen ist.</returns>
    public static async Task ExecuteNonQueryAsync(DatabaseFixture db, string commandText, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = await CreateCommandAsync(db, commandText, parameters);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Führt einen SQL-Befehl aus und liefert den ersten Wert der ersten Ergebniszeile.</summary>
    /// <param name="db">Fixture, deren Verbindung verwendet wird.</param>
    /// <param name="commandText">Auszuführender SQL-Text.</param>
    /// <param name="parameters">Benannte Parameter für den SQL-Text.</param>
    /// <returns>Der erste Wert der ersten Ergebniszeile oder <c>null</c>.</returns>
    public static async Task<object?> ExecuteScalarAsync(DatabaseFixture db, string commandText, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = await CreateCommandAsync(db, commandText, parameters);
        return await cmd.ExecuteScalarAsync();
    }

    /// <summary>Prüft, ob eine Tabelle mit dem angegebenen Namen in der SQLite-Datenbank existiert.</summary>
    /// <param name="db">Fixture, deren Verbindung verwendet wird.</param>
    /// <param name="tableName">Name der gesuchten Tabelle.</param>
    /// <returns><c>true</c>, wenn die Tabelle existiert.</returns>
    public static async Task<bool> TableExistsAsync(DatabaseFixture db, string tableName)
        => await ExecuteScalarAsync(
            db,
            "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1;",
            ("$name", tableName)) is not null;

    private static async Task<DbCommand> CreateCommandAsync(DatabaseFixture db, string commandText, (string Name, object? Value)[] parameters)
    {
        var cmd = db.Context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = commandText;
        foreach (var (name, value) in parameters)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(parameter);
        }

        if (cmd.Connection!.State != ConnectionState.Open)
        {
            await cmd.Connection.OpenAsync();
        }

        return cmd;
    }
}
