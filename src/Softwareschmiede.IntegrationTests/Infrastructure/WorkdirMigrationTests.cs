using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static Softwareschmiede.IntegrationTests.Infrastructure.MigrationTestSql;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

/// <summary>Prüft, dass kritische Migrationen vorwärts und rückwärts angewendet werden können.</summary>
public sealed class WorkdirMigrationTests
{
    private const string WorkdirMigration = "20260509200234_202605090001_Add_AppEinstellung_Workdir";
    private const string PreviousMigration = "20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering";
    private const string AddKiPluginPrefixMigration = "20260524151645_202605241703_AddKiPluginPrefix";
    private const string PreviousBeforeKiPluginPrefixMigration = "20260523113807_AddKiTaskNotifications";
    private const string AddAufgabeAusfuehrungsStatusMigration = "20260816193940_AddAufgabeAusfuehrungsStatus";
    private const string PreviousBeforeAusfuehrungsStatusMigration = "20260805163701_AddTodoEntity";
    private const string AddIssueReferenzSourceKeyMigration = "20261017195348_AddIssueReferenzSourceKey";
    private const string PreviousBeforeIssueReferenzSourceKeyMigration = "20261011045239_AddScmEventInbox";

    /// <summary>Prüft, dass die SourceKey-Spalte nullable hinzugefügt und wieder entfernt werden kann.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldApplyAndRollbackIssueReferenzSourceKeyMigration()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);

        (await ColumnInfoAsync(db, "IssueReferenzen", "SourceKey")).Should().NotBeNull();

        await db.Context.Database.MigrateAsync(PreviousBeforeIssueReferenzSourceKeyMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await ColumnInfoAsync(db, "IssueReferenzen", "SourceKey")).Should().BeNull();

        await db.Context.Database.MigrateAsync(AddIssueReferenzSourceKeyMigration, cancellationToken: TestContext.Current.CancellationToken);
        var column = await ColumnInfoAsync(db, "IssueReferenzen", "SourceKey");
        column.Should().NotBeNull();
        column!.NotNull.Should().BeFalse("Migration muss rückwärtskompatibel als nullable sein.");
    }

    /// <summary>Prüft, dass die Workdir-Migration angewendet und zurückgerollt werden kann.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldApplyAndRollbackWorkdirMigration()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);

        (await TableExistsAsync(db, "AppEinstellungen")).Should().BeTrue();

        await db.Context.Database.MigrateAsync(PreviousMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await TableExistsAsync(db, "AppEinstellungen")).Should().BeFalse();

        await db.Context.Database.MigrateAsync(WorkdirMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await TableExistsAsync(db, "AppEinstellungen")).Should().BeTrue();
    }

    /// <summary>Prüft, dass die KiPluginPrefix-Spalte als nullable hinzugefügt und entfernt werden kann.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldApplyAndRollbackKiPluginPrefixMigration_AsNullableColumn()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);

        (await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix")).Should().NotBeNull();

        await db.Context.Database.MigrateAsync(PreviousBeforeKiPluginPrefixMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix")).Should().BeNull();

        await db.Context.Database.MigrateAsync(AddKiPluginPrefixMigration, cancellationToken: TestContext.Current.CancellationToken);
        var column = await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix");
        column.Should().NotBeNull();
        column!.NotNull.Should().BeFalse("Migration muss rückwärtskompatibel als nullable sein.");
    }

    /// <summary>Prüft den Backfill der Ausführungsstatus-Migration für bestehende Aufgaben.</summary>
    [Fact]
    public async Task MigrateAsync_AddAufgabeAusfuehrungsStatus_BackfilledAlleAltStatus()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);

        await db.Context.Database.MigrateAsync(PreviousBeforeAusfuehrungsStatusMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await ColumnInfoAsync(db, "Aufgaben", "AusfuehrungsStatus")).Should().BeNull();

        var projectId = Guid.NewGuid().ToString();
        await ExecuteNonQueryAsync(
            db,
            "INSERT INTO Projekte (Id, Name, Beschreibung, ErstellungsDatum, Status) VALUES ($id, $name, NULL, $erstellt, $status);",
            ("$id", projectId),
            ("$name", "Backfill-Projekt"),
            ("$erstellt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            ("$status", "Aktiv"));

        var neuId = await InsertLegacyAufgabeAsync(db, projectId, "Neu", null);
        var aktivId = await InsertLegacyAufgabeAsync(db, projectId, "Gestartet", "lauf-aktiv");
        var aktivTrimId = await InsertLegacyAufgabeAsync(db, projectId, "Wartend", "  ");
        var gestartetOhneRunId = await InsertLegacyAufgabeAsync(db, projectId, "Gestartet", null);
        var wartendOhneRunId = await InsertLegacyAufgabeAsync(db, projectId, "Wartend", null);
        var beendetId = await InsertLegacyAufgabeAsync(db, projectId, "Beendet", null);
        var archiviertId = await InsertLegacyAufgabeAsync(db, projectId, "Archiviert", "lauf-alt");

        await db.Context.Database.MigrateAsync(AddAufgabeAusfuehrungsStatusMigration, cancellationToken: TestContext.Current.CancellationToken);

        var column = await ColumnInfoAsync(db, "Aufgaben", "AusfuehrungsStatus");
        column.Should().NotBeNull();
        column!.NotNull.Should().BeTrue();

        (await GetAufgabeAusfuehrungsStatusAsync(db, neuId)).Should().Be("NichtGestartet");
        (await GetAufgabeAusfuehrungsStatusAsync(db, aktivId)).Should().Be("Aktiv");
        (await GetAufgabeAusfuehrungsStatusAsync(db, aktivTrimId)).Should().Be("Beendet");
        (await GetAufgabeAusfuehrungsStatusAsync(db, gestartetOhneRunId)).Should().Be("Beendet");
        (await GetAufgabeAusfuehrungsStatusAsync(db, wartendOhneRunId)).Should().Be("Beendet");
        (await GetAufgabeAusfuehrungsStatusAsync(db, beendetId)).Should().Be("Beendet");
        (await GetAufgabeAusfuehrungsStatusAsync(db, archiviertId)).Should().Be("Beendet");
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

    private static async Task<string> InsertLegacyAufgabeAsync(DatabaseFixture db, string projectId, string status, string? activeRunId)
    {
        var id = Guid.NewGuid().ToString();
        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO Aufgaben
                (Id, ProjektId, Titel, AnforderungsBeschreibung, Status, BranchName, LokalerKlonPfad,
                 AgentenpaketName, AgentenName, KiPluginPrefix, ErstellungsDatum, AbschlussDatum,
                 AktiveRunId, LastHeartbeatUtc, LetzterCliStartUtc, LaufStatus, RecoveryVersion,
                 VorschlagPrompt, VorschlagAusfuehrenAbUtc, GitRepositoryId)
            VALUES
                ($id, $projektId, $titel, NULL, $status, NULL, NULL,
                 NULL, NULL, NULL, $erstellt, NULL,
                 $aktiveRunId, NULL, NULL, NULL, 0,
                 NULL, NULL, NULL);
            """,
            ("$id", id),
            ("$projektId", projectId),
            ("$titel", $"Alt-{status}-{id[..6]}"),
            ("$status", status),
            ("$erstellt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            ("$aktiveRunId", activeRunId));
        return id;
    }

    private static async Task<string?> GetAufgabeAusfuehrungsStatusAsync(DatabaseFixture db, string workItemId)
        => (string?)await ExecuteScalarAsync(
            db,
            "SELECT AusfuehrungsStatus FROM Aufgaben WHERE Id = $id;",
            ("$id", workItemId));

    private sealed record TableColumnInfo(string Name, bool NotNull);
}
