using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static Softwareschmiede.IntegrationTests.Infrastructure.MigrationTestSql;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

/// <summary>Prüft, dass kritische Migrationen vorwärts und rückwärts angewendet werden können.</summary>
public sealed class WorkdirMigrationTests
{
    private const string WorkdirMigration = "20260509200234_202605090001_Add_AppEinstellung_Workdir";
    private const string PreviousMigration = "20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering";
    private const string AddAiPluginPrefixMigration = "20260524151645_202605241703_AddKiPluginPrefix";
    private const string PreviousBeforeAiPluginPrefixMigration = "20260523113807_AddKiTaskNotifications";
    private const string AddWorkItemExecutionStatusMigration = "20260816193940_AddAufgabeAusfuehrungsStatus";
    private const string PreviousBeforeExecutionStatusMigration = "20260805163701_AddTodoEntity";
    private const string AddIssueReferenceSourceKeyMigration = "20261017195348_AddIssueReferenzSourceKey";
    private const string PreviousBeforeIssueReferenceSourceKeyMigration = "20261011045239_AddScmEventInbox";

    /// <summary>Prüft, dass die SourceKey-Spalte nullable hinzugefügt und wieder entfernt werden kann.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldApplyAndRollbackIssueReferenceSourceKeyMigration()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);

        (await ColumnInfoAsync(db, "IssueReferenzen", "SourceKey")).Should().NotBeNull();

        await db.Context.Database.MigrateAsync(PreviousBeforeIssueReferenceSourceKeyMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await ColumnInfoAsync(db, "IssueReferenzen", "SourceKey")).Should().BeNull();

        await db.Context.Database.MigrateAsync(AddIssueReferenceSourceKeyMigration, cancellationToken: TestContext.Current.CancellationToken);
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
    public async Task MigrateAsync_ShouldApplyAndRollbackAiPluginPrefixMigration_AsNullableColumn()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);

        (await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix")).Should().NotBeNull();

        await db.Context.Database.MigrateAsync(PreviousBeforeAiPluginPrefixMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix")).Should().BeNull();

        await db.Context.Database.MigrateAsync(AddAiPluginPrefixMigration, cancellationToken: TestContext.Current.CancellationToken);
        var column = await ColumnInfoAsync(db, "Aufgaben", "KiPluginPrefix");
        column.Should().NotBeNull();
        column!.NotNull.Should().BeFalse("Migration muss rückwärtskompatibel als nullable sein.");
    }

    /// <summary>Prüft den Backfill der Ausführungsstatus-Migration für bestehende Aufgaben.</summary>
    [Fact]
    public async Task MigrateAsync_AddWorkItemExecutionStatus_BackfilledAllAltStatus()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);

        await db.Context.Database.MigrateAsync(PreviousBeforeExecutionStatusMigration, cancellationToken: TestContext.Current.CancellationToken);
        (await ColumnInfoAsync(db, "Aufgaben", "AusfuehrungsStatus")).Should().BeNull();

        var projectId = Guid.NewGuid().ToString();
        await ExecuteNonQueryAsync(
            db,
            "INSERT INTO Projekte (Id, Name, Beschreibung, ErstellungsDatum, Status) VALUES ($id, $name, NULL, $erstellt, $status);",
            ("$id", projectId),
            ("$name", "Backfill-Projekt"),
            ("$erstellt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            ("$status", "Aktiv"));

        var newId = await InsertLegacyWorkItemAsync(db, projectId, "Neu", null);
        var activeId = await InsertLegacyWorkItemAsync(db, projectId, "Gestartet", "lauf-aktiv");
        var activeTrimId = await InsertLegacyWorkItemAsync(db, projectId, "Wartend", "  ");
        var startedWithoutRunId = await InsertLegacyWorkItemAsync(db, projectId, "Gestartet", null);
        var waitingWithoutRunId = await InsertLegacyWorkItemAsync(db, projectId, "Wartend", null);
        var completedId = await InsertLegacyWorkItemAsync(db, projectId, "Beendet", null);
        var archivedId = await InsertLegacyWorkItemAsync(db, projectId, "Archiviert", "lauf-alt");

        await db.Context.Database.MigrateAsync(AddWorkItemExecutionStatusMigration, cancellationToken: TestContext.Current.CancellationToken);

        var column = await ColumnInfoAsync(db, "Aufgaben", "AusfuehrungsStatus");
        column.Should().NotBeNull();
        column!.NotNull.Should().BeTrue();

        (await GetWorkItemExecutionStatusAsync(db, newId)).Should().Be("NichtGestartet");
        (await GetWorkItemExecutionStatusAsync(db, activeId)).Should().Be("Aktiv");
        (await GetWorkItemExecutionStatusAsync(db, activeTrimId)).Should().Be("Beendet");
        (await GetWorkItemExecutionStatusAsync(db, startedWithoutRunId)).Should().Be("Beendet");
        (await GetWorkItemExecutionStatusAsync(db, waitingWithoutRunId)).Should().Be("Beendet");
        (await GetWorkItemExecutionStatusAsync(db, completedId)).Should().Be("Beendet");
        (await GetWorkItemExecutionStatusAsync(db, archivedId)).Should().Be("Beendet");
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

    private static async Task<string> InsertLegacyWorkItemAsync(DatabaseFixture db, string projectId, string status, string? activeRunId)
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

    private static async Task<string?> GetWorkItemExecutionStatusAsync(DatabaseFixture db, string workItemId)
        => (string?)await ExecuteScalarAsync(
            db,
            "SELECT AusfuehrungsStatus FROM Aufgaben WHERE Id = $id;",
            ("$id", workItemId));

    private sealed record TableColumnInfo(string Name, bool NotNull);
}
