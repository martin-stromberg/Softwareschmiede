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
public sealed class RemoveAutonomAufgabeModelsMigrationTests
{
    private const string RemoveMigration = "20261028142449_RemoveAutonomAufgabeModels";
    private const string PreviousMigration = "20261020062227_AddAufgabeScmAbschluss";
    private const string EntfernterEinstellungsSchluessel = "autonomeaufgaben.enabled";

    private static readonly string[] EntfernteTabellen =
        ["AutonomAufgabeKonfigurationen", "UnteragentSpezifikationen", "SkillDefinitionen"];

    /// <summary>Die Migration entfernt die drei Tabellen und die Einstellungszeile, andere Einstellungen bleiben erhalten.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomAufgabeModels_EntferntTabellenUndEinstellung()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await db.Context.Database.MigrateAsync(PreviousMigration);
        foreach (var tabelle in EntfernteTabellen)
        {
            (await TableExistsAsync(db, tabelle)).Should().BeTrue($"Vorbedingung: {tabelle} existiert vor der Migration");
        }

        var projektId = await ErstelleProjektAsync(db);
        var aufgabeId = await ErstelleAufgabeAsync(db, projektId, "Neu", "NichtGestartet", laufzustand: null);
        await FuegeKonfigurationMitKindzeilenEinAsync(db, aufgabeId);
        await ErstelleEinstellungAsync(db, EntfernterEinstellungsSchluessel, "true");
        await ErstelleEinstellungAsync(db, AppEinstellungService.ScmMonitoringEnabledKey, "false");

        await db.Context.Database.MigrateAsync(RemoveMigration);

        foreach (var tabelle in EntfernteTabellen)
        {
            (await TableExistsAsync(db, tabelle)).Should().BeFalse($"{tabelle} muss durch die Migration entfernt werden");
        }

        (await EinstellungExistiertAsync(db, EntfernterEinstellungsSchluessel)).Should().BeFalse();
        (await EinstellungExistiertAsync(db, AppEinstellungService.ScmMonitoringEnabledKey)).Should().BeTrue();
    }

    /// <summary>Eine laufende Aufgabe mit Konfigurationszeile bleibt erhalten, ihr gesamter Laufzustand wird zurückgesetzt.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomAufgabeModels_SetztLaufzustandZurueck()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await db.Context.Database.MigrateAsync(PreviousMigration);

        var projektId = await ErstelleProjektAsync(db);
        var aufgabeId = await ErstelleAufgabeAsync(db, projektId, "Gestartet", "Aktiv", ErstelleLaufzustand("feature/aktiv"));
        await FuegeKonfigurationMitKindzeilenEinAsync(db, aufgabeId);

        await db.Context.Database.MigrateAsync(RemoveMigration);

        var aufgabe = await LeseAufgabeAsync(db, aufgabeId);
        aufgabe.Should().NotBeNull("die Aufgabe-Zeile muss erhalten bleiben");
        aufgabe!.Status.Should().Be("Gestartet");
        aufgabe.AusfuehrungsStatus.Should().Be("NichtGestartet");
        aufgabe.BranchName.Should().Be("feature/aktiv");
        aufgabe.AktiveRunId.Should().BeNull();
        aufgabe.PausiertBisUtc.Should().BeNull();
        aufgabe.LastHeartbeatUtc.Should().BeNull();
        aufgabe.LetzterCliStartUtc.Should().BeNull();
        aufgabe.LaufStatus.Should().BeNull();
    }

    /// <summary>Abgeschlossene Aufgaben mit Konfigurationszeile behalten ihren Ausführungsstatus.</summary>
    /// <param name="status">Abgeschlossener Aufgabenstatus vor der Migration.</param>
    [Theory]
    [InlineData("Beendet")]
    [InlineData("Archiviert")]
    public async Task Migration_RemoveAutonomAufgabeModels_BehaeltAusfuehrungsStatusAbgeschlossenerAufgaben(string status)
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await db.Context.Database.MigrateAsync(PreviousMigration);

        var projektId = await ErstelleProjektAsync(db);
        var aufgabeId = await ErstelleAufgabeAsync(db, projektId, status, "Beendet", ErstelleLaufzustand("feature/fertig"));
        await FuegeKonfigurationMitKindzeilenEinAsync(db, aufgabeId);

        await db.Context.Database.MigrateAsync(RemoveMigration);

        var aufgabe = await LeseAufgabeAsync(db, aufgabeId);
        aufgabe.Should().NotBeNull("die Aufgabe-Zeile muss erhalten bleiben");
        aufgabe!.Status.Should().Be(status);
        aufgabe.AusfuehrungsStatus.Should().Be("Beendet");
        aufgabe.AktiveRunId.Should().BeNull();
        aufgabe.PausiertBisUtc.Should().BeNull();
        aufgabe.LastHeartbeatUtc.Should().BeNull();
        aufgabe.LetzterCliStartUtc.Should().BeNull();
        aufgabe.LaufStatus.Should().BeNull();
    }

    /// <summary>Aufgaben ohne Konfigurationszeile bleiben durch die Migration unverändert.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomAufgabeModels_BehaeltRegulaereAufgaben()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await db.Context.Database.MigrateAsync(PreviousMigration);

        var projektId = await ErstelleProjektAsync(db);
        var laufzustand = ErstelleLaufzustand("feature/regulaer");
        var regulaereId = await ErstelleAufgabeAsync(db, projektId, "Gestartet", "Aktiv", laufzustand);
        var konfigurierteId = await ErstelleAufgabeAsync(db, projektId, "Gestartet", "Aktiv", ErstelleLaufzustand("feature/konfiguriert"));
        await FuegeKonfigurationMitKindzeilenEinAsync(db, konfigurierteId);

        await db.Context.Database.MigrateAsync(RemoveMigration);

        var aufgabe = await LeseAufgabeAsync(db, regulaereId);
        aufgabe.Should().Be(new AufgabeZeile(
            "Gestartet",
            "Aktiv",
            laufzustand.AktiveRunId,
            laufzustand.BranchName,
            laufzustand.PausiertBisUtc,
            laufzustand.LastHeartbeatUtc,
            laufzustand.LetzterCliStartUtc,
            laufzustand.LaufStatus));
    }

    /// <summary><c>Down()</c> stellt die drei Tabellen wieder her, ohne die gelöschten Daten zurückzuholen.</summary>
    [Fact]
    public async Task Migration_RemoveAutonomAufgabeModels_Down_ErzeugtSchemaOhneDaten()
    {
        await using var db = await DatabaseFixture.CreateAsync();
        await db.Context.Database.MigrateAsync(PreviousMigration);
        var projektId = await ErstelleProjektAsync(db);
        var aufgabeId = await ErstelleAufgabeAsync(db, projektId, "Neu", "NichtGestartet", laufzustand: null);
        await FuegeKonfigurationMitKindzeilenEinAsync(db, aufgabeId);
        await db.Context.Database.MigrateAsync(RemoveMigration);

        await db.Context.Database.MigrateAsync(PreviousMigration);

        foreach (var tabelle in EntfernteTabellen)
        {
            (await TableExistsAsync(db, tabelle)).Should().BeTrue($"{tabelle} muss durch Down() wiederhergestellt werden");
            (await ZaehleZeilenAsync(db, tabelle)).Should().Be(0, $"{tabelle} darf nach Down() keine Daten enthalten");
        }
    }

    private static Laufzustand ErstelleLaufzustand(string branchName)
    {
        var jetzt = DateTimeOffset.UtcNow;
        return new Laufzustand(
            "lauf-1",
            branchName,
            jetzt.AddHours(2).ToUnixTimeMilliseconds(),
            jetzt.AddMinutes(-1).ToUnixTimeMilliseconds(),
            jetzt.AddMinutes(-5).ToUnixTimeMilliseconds(),
            "Laeuft");
    }

    private static async Task<string> ErstelleProjektAsync(DatabaseFixture db)
    {
        var id = NeueId();
        await ExecuteNonQueryAsync(
            db,
            "INSERT INTO Projekte (Id, Name, Beschreibung, ErstellungsDatum, Status) VALUES ($id, $name, NULL, $erstellt, 'Aktiv');",
            ("$id", id),
            ("$name", $"Projekt {id[..8]}"),
            ("$erstellt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        return id;
    }

    private static async Task<string> ErstelleAufgabeAsync(
        DatabaseFixture db,
        string projektId,
        string status,
        string ausfuehrungsStatus,
        Laufzustand? laufzustand)
    {
        var id = NeueId();
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
            ("$projektId", projektId),
            ("$titel", $"Aufgabe {status} {id[..8]}"),
            ("$status", status),
            ("$ausfuehrungsStatus", ausfuehrungsStatus),
            ("$branchName", laufzustand?.BranchName),
            ("$erstellt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            ("$aktiveRunId", laufzustand?.AktiveRunId),
            ("$pausiertBisUtc", laufzustand?.PausiertBisUtc),
            ("$lastHeartbeatUtc", laufzustand?.LastHeartbeatUtc),
            ("$letzterCliStartUtc", laufzustand?.LetzterCliStartUtc),
            ("$laufStatus", laufzustand?.LaufStatus));
        return id;
    }

    private static async Task ErstelleEinstellungAsync(DatabaseFixture db, string schluessel, string wert)
        => await ExecuteNonQueryAsync(
            db,
            "INSERT INTO AppEinstellungen (Id, Schluessel, Wert, AktualisiertAm) VALUES ($id, $schluessel, $wert, $aktualisiert);",
            ("$id", NeueId()),
            ("$schluessel", schluessel),
            ("$wert", wert),
            ("$aktualisiert", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    private static async Task<bool> EinstellungExistiertAsync(DatabaseFixture db, string schluessel)
        => await ExecuteScalarAsync(
            db,
            "SELECT 1 FROM AppEinstellungen WHERE Schluessel = $schluessel LIMIT 1;",
            ("$schluessel", schluessel)) is not null;

    private static async Task<AufgabeZeile?> LeseAufgabeAsync(DatabaseFixture db, string aufgabeId)
    {
        if (await ExecuteScalarAsync(db, "SELECT 1 FROM Aufgaben WHERE Id = $id;", ("$id", aufgabeId)) is null)
        {
            return null;
        }

        return new AufgabeZeile(
            (string)(await LeseSpalteAsync(db, aufgabeId, "Status"))!,
            (string)(await LeseSpalteAsync(db, aufgabeId, "AusfuehrungsStatus"))!,
            (string?)await LeseSpalteAsync(db, aufgabeId, "AktiveRunId"),
            (string?)await LeseSpalteAsync(db, aufgabeId, "BranchName"),
            (long?)await LeseSpalteAsync(db, aufgabeId, "PausiertBisUtc"),
            (long?)await LeseSpalteAsync(db, aufgabeId, "LastHeartbeatUtc"),
            (long?)await LeseSpalteAsync(db, aufgabeId, "LetzterCliStartUtc"),
            (string?)await LeseSpalteAsync(db, aufgabeId, "LaufStatus"));
    }

    private static async Task<object?> LeseSpalteAsync(DatabaseFixture db, string aufgabeId, string spalte)
    {
        var wert = await ExecuteScalarAsync(db, $"SELECT [{spalte}] FROM Aufgaben WHERE Id = $id;", ("$id", aufgabeId));
        return wert is DBNull ? null : wert;
    }

    private static async Task FuegeKonfigurationMitKindzeilenEinAsync(DatabaseFixture db, string aufgabeId)
    {
        var konfigurationId = NeueId();
        var jetzt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

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
            ("$id", konfigurationId),
            ("$aufgabeId", aufgabeId));

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO UnteragentSpezifikationen
                (Id, AutonomAufgabeId, AbschlussDatum, Branch, ClonePfad, ErzeugungsDatum, ExterneAgentId,
                 Prompt, Scope, Status, TaskId, VerzeichnisPfad)
            VALUES ($id, $konfigurationId, NULL, 'feature/unter', 'C:\arbeit\clone', $jetzt, 'agent-1',
                    'Prompt', 'scope', 'Aktiv', 'task-1', 'C:\arbeit\unter');
            """,
            ("$id", NeueId()),
            ("$konfigurationId", konfigurationId),
            ("$jetzt", jetzt));

        await ExecuteNonQueryAsync(
            db,
            """
            INSERT INTO SkillDefinitionen
                (Id, AutonomAufgabeId, Content, ErstellungsDatum, FreigabeDatum, Name, Status, Version)
            VALUES ($id, $konfigurationId, 'Inhalt', $jetzt, NULL, 'skill', 'Entwurf', '1.0');
            """,
            ("$id", NeueId()),
            ("$konfigurationId", konfigurationId),
            ("$jetzt", jetzt));

        (await ZaehleZeilenAsync(db, "AutonomAufgabeKonfigurationen")).Should().BeGreaterThan(0, "Vorbedingung: Konfigurationszeile angelegt");
    }

    private static async Task<long> ZaehleZeilenAsync(DatabaseFixture db, string tableName)
        => (long)(await ExecuteScalarAsync(db, $"SELECT COUNT(*) FROM [{tableName}];"))!;

    private static string NeueId() => Guid.NewGuid().ToString().ToUpperInvariant();

    private sealed record Laufzustand(
        string AktiveRunId,
        string BranchName,
        long PausiertBisUtc,
        long LastHeartbeatUtc,
        long LetzterCliStartUtc,
        string LaufStatus);

    private sealed record AufgabeZeile(
        string Status,
        string AusfuehrungsStatus,
        string? AktiveRunId,
        string? BranchName,
        long? PausiertBisUtc,
        long? LastHeartbeatUtc,
        long? LetzterCliStartUtc,
        string? LaufStatus);
}
