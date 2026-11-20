using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using static Softwareschmiede.IntegrationTests.Infrastructure.MigrationTestSql;

namespace Softwareschmiede.IntegrationTests.Infrastructure;

/// <summary>
/// Prüft die Migration <c>RemoveAutonomAufgabeModels</c> an einer SQLite-Datenbankdatei.
/// Testdaten werden per Roh-SQL gegen das Schema angelegt und gelesen, damit spätere
/// Modelländerungen diese Tests nicht beeinflussen.
/// </summary>
public sealed class RemoveAutonomWorkItemModelsMigrationTests
{
    private const string RemoveMigration = "20261028142449_RemoveAutonomAufgabeModels";
    private const string PreviousMigration = "20261020062227_AddAufgabeScmAbschluss";
    private const string RemovedSettingKey = "autonomeaufgaben.enabled";

    private static readonly string[] RemovedTable =
        ["AutonomAufgabeKonfigurationen", "UnteragentSpezifikationen", "SkillDefinitionen"];

    /// <summary>Die Migration entfernt die drei Tabellen und die Einstellungszeile, andere Einstellungen bleiben erhalten.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomWorkItemModels_RemovesTableAndSetting()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        await db.Context.Database.MigrateAsync(PreviousMigration, cancellationToken: TestContext.Current.CancellationToken);
        foreach (var table in RemovedTable)
        {
            (await TableExistsAsync(db, table)).Should().BeTrue($"Vorbedingung: {table} existiert vor der Migration");
        }

        var projectId = await CreateProjectAsync(db);
        var workItemId = await CreateWorkItemAsync(db, projectId, "Neu", "NichtGestartet", runstate: null);
        await AddConfigurationWithChildlinesAAsync(db, workItemId);
        await CreateSettingAsync(db, RemovedSettingKey, "true");
        await CreateSettingAsync(db, AppSettingService.ScmMonitoringEnabledKey, "false");

        await db.Context.Database.MigrateAsync(RemoveMigration, cancellationToken: TestContext.Current.CancellationToken);

        foreach (var table in RemovedTable)
        {
            (await TableExistsAsync(db, table)).Should().BeFalse($"{table} muss durch die Migration entfernt werden");
        }

        (await SettingExistsAsync(db, RemovedSettingKey)).Should().BeFalse();
        (await SettingExistsAsync(db, AppSettingService.ScmMonitoringEnabledKey)).Should().BeTrue();
    }

    /// <summary>Eine laufende Aufgabe mit Konfigurationszeile bleibt erhalten, ihr gesamter Laufzustand wird zurückgesetzt.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomWorkItemModels_SetsRunstateBack()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        await db.Context.Database.MigrateAsync(PreviousMigration, cancellationToken: TestContext.Current.CancellationToken);

        var projectId = await CreateProjectAsync(db);
        var workItemId = await CreateWorkItemAsync(db, projectId, "Gestartet", "Aktiv", CreateRunstate("feature/aktiv"));
        await AddConfigurationWithChildlinesAAsync(db, workItemId);

        await db.Context.Database.MigrateAsync(RemoveMigration, cancellationToken: TestContext.Current.CancellationToken);

        var workItem = await ReadWorkItemAsync(db, workItemId);
        workItem.Should().NotBeNull("die Aufgabe-Zeile muss erhalten bleiben");
        workItem!.Status.Should().Be("Gestartet");
        workItem.ExecutionStatus.Should().Be("NichtGestartet");
        workItem.BranchName.Should().Be("feature/aktiv");
        workItem.ActiveRunId.Should().BeNull();
        workItem.PausedUntilUtc.Should().BeNull();
        workItem.LastHeartbeatUtc.Should().BeNull();
        workItem.LastCliStartUtc.Should().BeNull();
        workItem.RunStatus.Should().BeNull();
    }

    /// <summary>Abgeschlossene Aufgaben mit Konfigurationszeile behalten ihren Ausführungsstatus.</summary>
    /// <param name="status">Abgeschlossener Aufgabenstatus vor der Migration.</param>
    [Theory]
    [InlineData("Beendet")]
    [InlineData("Archiviert")]
    public async Task Migration_RemoveAutonomWorkItemModels_KeepsExecutionStatusCompletedWorkItems(string status)
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        await db.Context.Database.MigrateAsync(PreviousMigration, cancellationToken: TestContext.Current.CancellationToken);

        var projectId = await CreateProjectAsync(db);
        var workItemId = await CreateWorkItemAsync(db, projectId, status, "Beendet", CreateRunstate("feature/fertig"));
        await AddConfigurationWithChildlinesAAsync(db, workItemId);

        await db.Context.Database.MigrateAsync(RemoveMigration, cancellationToken: TestContext.Current.CancellationToken);

        var workItem = await ReadWorkItemAsync(db, workItemId);
        workItem.Should().NotBeNull("die Aufgabe-Zeile muss erhalten bleiben");
        workItem!.Status.Should().Be(status);
        workItem.ExecutionStatus.Should().Be("Beendet");
        workItem.ActiveRunId.Should().BeNull();
        workItem.PausedUntilUtc.Should().BeNull();
        workItem.LastHeartbeatUtc.Should().BeNull();
        workItem.LastCliStartUtc.Should().BeNull();
        workItem.RunStatus.Should().BeNull();
    }

    /// <summary>Aufgaben ohne Konfigurationszeile bleiben durch die Migration unverändert.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomWorkItemModels_KeepsRegularWorkItems()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        await db.Context.Database.MigrateAsync(PreviousMigration, cancellationToken: TestContext.Current.CancellationToken);

        var projectId = await CreateProjectAsync(db);
        var runstate = CreateRunstate("feature/regulaer");
        var regularId = await CreateWorkItemAsync(db, projectId, "Gestartet", "Aktiv", runstate);
        var configuredId = await CreateWorkItemAsync(db, projectId, "Gestartet", "Aktiv", CreateRunstate("feature/konfiguriert"));
        await AddConfigurationWithChildlinesAAsync(db, configuredId);

        await db.Context.Database.MigrateAsync(RemoveMigration, cancellationToken: TestContext.Current.CancellationToken);

        var workItem = await ReadWorkItemAsync(db, regularId);
        workItem.Should().Be(new WorkItemLine(
            "Gestartet",
            "Aktiv",
            runstate.ActiveRunId,
            runstate.BranchName,
            runstate.PausedUntilUtc,
            runstate.LastHeartbeatUtc,
            runstate.LastCliStartUtc,
            runstate.RunStatus));
    }

    /// <summary><c>Down()</c> stellt die drei Tabellen wieder her, ohne die gelöschten Daten zurückzuholen.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomWorkItemModels_Down_CreatesSchemaWithoutData()
    {
        await using var db = await DatabaseFixture.CreateAsync(TestContext.Current.CancellationToken);
        await db.Context.Database.MigrateAsync(PreviousMigration, cancellationToken: TestContext.Current.CancellationToken);
        var projectId = await CreateProjectAsync(db);
        var workItemId = await CreateWorkItemAsync(db, projectId, "Neu", "NichtGestartet", runstate: null);
        await AddConfigurationWithChildlinesAAsync(db, workItemId);
        await db.Context.Database.MigrateAsync(RemoveMigration, cancellationToken: TestContext.Current.CancellationToken);

        await db.Context.Database.MigrateAsync(PreviousMigration, cancellationToken: TestContext.Current.CancellationToken);

        foreach (var table in RemovedTable)
        {
            (await TableExistsAsync(db, table)).Should().BeTrue($"{table} muss durch Down() wiederhergestellt werden");
            (await CountLinesAsync(db, table)).Should().Be(0, $"{table} darf nach Down() keine Daten enthalten");
        }
    }

    private static Runstate CreateRunstate(string branchName)
    {
        var now = DateTimeOffset.UtcNow;
        return new Runstate(
            "lauf-1",
            branchName,
            now.AddHours(2).ToUnixTimeMilliseconds(),
            now.AddMinutes(-1).ToUnixTimeMilliseconds(),
            now.AddMinutes(-5).ToUnixTimeMilliseconds(),
            "Laeuft");
    }

    private static async Task<string> CreateProjectAsync(DatabaseFixture db)
    {
        var id = NewId();
        await ExecuteNonQueryAsync(
            db,
            "INSERT INTO Projekte (Id, Name, Beschreibung, ErstellungsDatum, Status) VALUES ($id, $name, NULL, $erstellt, 'Aktiv');",
            ("$id", id),
            ("$name", $"Projekt {id[..8]}"),
            ("$erstellt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        return id;
    }

    private static async Task<string> CreateWorkItemAsync(
        DatabaseFixture db,
        string projectId,
        string status,
        string executionStatus,
        Runstate? runstate)
    {
        var id = NewId();
        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO Aufgaben
                (Id, ProjektId, Titel, AnforderungsBeschreibung, Status, AusfuehrungsStatus, BranchName,
                 ErstellungsDatum, AktiveRunId, PausiertBisUtc, LastHeartbeatUtc, LetzterCliStartUtc, LaufStatus,
                 RecoveryVersion)
            VALUES
                ($id, $projektId, $titel, 'Beschreibung', $status, $ausfuehrungsStatus, $branchName,
                 $erstellt, $aktiveRunId, $pausiertBisUtc, $lastHeartbeatUtc, $letzterCliStartUtc, $laufStatus,
                 0);
            """,
            ("$id", id),
            ("$projektId", projectId),
            ("$titel", $"Aufgabe {status} {id[..8]}"),
            ("$status", status),
            ("$ausfuehrungsStatus", executionStatus),
            ("$branchName", runstate?.BranchName),
            ("$erstellt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            ("$aktiveRunId", runstate?.ActiveRunId),
            ("$pausiertBisUtc", runstate?.PausedUntilUtc),
            ("$lastHeartbeatUtc", runstate?.LastHeartbeatUtc),
            ("$letzterCliStartUtc", runstate?.LastCliStartUtc),
            ("$laufStatus", runstate?.RunStatus));
        return id;
    }

    private static async Task CreateSettingAsync(DatabaseFixture db, string key, string value)
        => await ExecuteNonQueryAsync(
            db,
            "INSERT INTO AppEinstellungen (Id, Schluessel, Wert, AktualisiertAm) VALUES ($id, $schluessel, $wert, $aktualisiert);",
            ("$id", NewId()),
            ("$schluessel", key),
            ("$wert", value),
            ("$aktualisiert", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    private static async Task<bool> SettingExistsAsync(DatabaseFixture db, string key)
        => await ExecuteScalarAsync(
            db,
            "SELECT 1 FROM AppEinstellungen WHERE Schluessel = $schluessel LIMIT 1;",
            ("$schluessel", key)) is not null;

    private static async Task<WorkItemLine?> ReadWorkItemAsync(DatabaseFixture db, string workItemId)
    {
        if (await ExecuteScalarAsync(db, "SELECT 1 FROM Aufgaben WHERE Id = $id;", ("$id", workItemId)) is null)
        {
            return null;
        }

        return new WorkItemLine(
            (string)(await ReadColumnAsync(db, workItemId, "Status"))!,
            (string)(await ReadColumnAsync(db, workItemId, "AusfuehrungsStatus"))!,
            (string?)await ReadColumnAsync(db, workItemId, "AktiveRunId"),
            (string?)await ReadColumnAsync(db, workItemId, "BranchName"),
            (long?)await ReadColumnAsync(db, workItemId, "PausiertBisUtc"),
            (long?)await ReadColumnAsync(db, workItemId, "LastHeartbeatUtc"),
            (long?)await ReadColumnAsync(db, workItemId, "LetzterCliStartUtc"),
            (string?)await ReadColumnAsync(db, workItemId, "LaufStatus"));
    }

    private static async Task<object?> ReadColumnAsync(DatabaseFixture db, string workItemId, string column)
    {
        var value = await ExecuteScalarAsync(db, $"SELECT [{column}] FROM Aufgaben WHERE Id = $id;", ("$id", workItemId));
        return value is DBNull ? null : value;
    }

    private static async Task AddConfigurationWithChildlinesAAsync(DatabaseFixture db, string workItemId)
    {
        var configurationId = NewId();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO AutonomAufgabeKonfigurationen
                (Id, AufgabeId, AktiveUnteragenten, ArbeitsverzeichnisPfad, ExplizitGestoppt, InitialPrompt,
                 LaufzeitLimitMinuten, PermissionsJsonPfad, PersistenzModus, ProjektBranchName,
                 ProjektleiterAgentId, SessionPauseUtc, SkillAutogeneration, TokenBudget, TokenBudgetErweitert)
            VALUES ($id, $aufgabeId, NULL, 'C:\arbeit', 0, 'Prompt', 60, 'C:\arbeit\permissions.json', 'Persistent',
                    'feature/projekt', NULL, NULL, 0, 1000, NULL);
            """,
            ("$id", configurationId),
            ("$aufgabeId", workItemId));

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO UnteragentSpezifikationen
                (Id, AutonomAufgabeId, AbschlussDatum, Branch, ClonePfad, ErzeugungsDatum, ExterneAgentId,
                 Prompt, Scope, Status, TaskId, VerzeichnisPfad)
            VALUES ($id, $konfigurationId, NULL, 'feature/unter', 'C:\arbeit\clone', $jetzt, 'agent-1',
                    'Prompt', 'scope', 'Aktiv', 'task-1', 'C:\arbeit\unter');
            """,
            ("$id", NewId()),
            ("$konfigurationId", configurationId),
            ("$jetzt", now));

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO SkillDefinitionen
                (Id, AutonomAufgabeId, Content, ErstellungsDatum, FreigabeDatum, Name, Status, Version)
            VALUES ($id, $konfigurationId, 'Inhalt', $jetzt, NULL, 'skill', 'Entwurf', '1.0');
            """,
            ("$id", NewId()),
            ("$konfigurationId", configurationId),
            ("$jetzt", now));

        (await CountLinesAsync(db, "AutonomAufgabeKonfigurationen")).Should().BeGreaterThan(0, "Vorbedingung: Konfigurationszeile angelegt");
    }

    private static async Task<long> CountLinesAsync(DatabaseFixture db, string tableName)
        => (long)(await ExecuteScalarAsync(db, $"SELECT COUNT(*) FROM [{tableName}];"))!;

    private static string NewId() => Guid.NewGuid().ToString().ToUpperInvariant();

    private sealed record Runstate(
        string ActiveRunId,
        string BranchName,
        long PausedUntilUtc,
        long LastHeartbeatUtc,
        long LastCliStartUtc,
        string RunStatus);

    private sealed record WorkItemLine(
        string Status,
        string ExecutionStatus,
        string? ActiveRunId,
        string? BranchName,
        long? PausedUntilUtc,
        long? LastHeartbeatUtc,
        long? LastCliStartUtc,
        string? RunStatus);
}
