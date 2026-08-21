using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Verwaltet Session-Pause/Resume, Token-Budget und Heartbeat-basierte Unterbrechungserkennung für Autonome Aufgaben.</summary>
public sealed class SessionManagementService
{
    private readonly SoftwareschmiededDbContext _db;
    private readonly ILogger<SessionManagementService> _logger;

    /// <inheritdoc cref="SessionManagementService"/>
    public SessionManagementService(SoftwareschmiededDbContext db, ILogger<SessionManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Pausiert die Aufgabe wegen Erreichens des Token-Budgets: setzt <see cref="Aufgabe.SessionPauseUtc"/> und aktualisiert state.json.</summary>
    public async Task PauseAufgabeBeiBudgetLimitAsync(Aufgabe aufgabe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);

        var entity = await _db.Aufgaben
            .Include(a => a.AutonomKonfiguration)
            .FirstOrDefaultAsync(a => a.Id == aufgabe.Id, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabe.Id} nicht gefunden.");

        var now = DateTimeOffset.UtcNow;
        entity.SessionPauseUtc = now;
        await _db.SaveChangesAsync(ct);

        if (entity.AutonomKonfiguration is not null)
        {
            await AktualisierePausedUtcInStateJsonAsync(entity.AutonomKonfiguration.ArbeitsverzeichnisPfad, now, ct);
        }

        _logger.LogInformation("Aufgabe {AufgabeId} wegen Budget-Limit pausiert (SessionPauseUtc={PauseUtc}).", aufgabe.Id, now);
    }

    /// <summary>Setzt die Aufgabe nach einer Session-Pause fort: generiert einen "Weitermachen"-Prompt und setzt den Ausführungsstatus zurück auf aktiv.</summary>
    public async Task SetzeFortAsync(Aufgabe aufgabe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);

        var entity = await _db.Aufgaben
            .Include(a => a.AutonomKonfiguration)
            .FirstOrDefaultAsync(a => a.Id == aufgabe.Id, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabe.Id} nicht gefunden.");

        var weitermachenPrompt = ErstelleWeitermachenPrompt(entity.AutonomKonfiguration);

        entity.SessionPauseUtc = null;
        entity.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
        entity.VorschlagPrompt = weitermachenPrompt;
        entity.VorschlagAusfuehrenAbUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (entity.AutonomKonfiguration is not null)
        {
            await AktualisierePausedUtcInStateJsonAsync(entity.AutonomKonfiguration.ArbeitsverzeichnisPfad, null, ct);
        }

        _logger.LogInformation("Aufgabe {AufgabeId}: Session fortgesetzt, Weitermachen-Prompt gesendet.", aufgabe.Id);
    }

    /// <summary>Prüft mittels Heartbeat, ob die Ausführung unterbrochen wurde. Ist kein Session-Limit aktiv und der letzte Heartbeat älter als <paramref name="heartbeatTimeout"/>, wird ein "Wurdest du unterbrochen?"-Prompt gesendet.</summary>
    public async Task<bool> PruefeAusfuehrungAsync(Aufgabe aufgabe, TimeSpan heartbeatTimeout, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aufgabe);

        var entity = await _db.Aufgaben.FirstOrDefaultAsync(a => a.Id == aufgabe.Id, ct)
            ?? throw new InvalidOperationException($"Aufgabe {aufgabe.Id} nicht gefunden.");

        if (entity.SessionPauseUtc is not null)
        {
            return true;
        }

        if (entity.LastHeartbeatUtc is null)
        {
            return true;
        }

        var alter = DateTimeOffset.UtcNow - entity.LastHeartbeatUtc.Value;
        if (alter <= heartbeatTimeout)
        {
            return true;
        }

        entity.VorschlagPrompt = "Wurdest du unterbrochen? Bitte bestätige, ob die Ausführung fortgesetzt wird, oder melde einen Fehler.";
        entity.VorschlagAusfuehrenAbUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Aufgabe {AufgabeId}: Heartbeat-Timeout überschritten ({Alter} > {Timeout}), Unterbrechungs-Prompt gesendet.",
            aufgabe.Id,
            alter,
            heartbeatTimeout);

        return false;
    }

    private static string ErstelleWeitermachenPrompt(AutonomAufgabeKonfiguration? konfiguration)
    {
        if (konfiguration is null)
        {
            return "Weitermachen: Bitte setze die Arbeit an der Autonomen Aufgabe fort.";
        }

        return "Weitermachen: Setze die Arbeit an der Autonomen Aufgabe im Arbeitsverzeichnis " +
               $"'{konfiguration.ArbeitsverzeichnisPfad}' fort. Prüfe state.json, plan.md und progress.md " +
               "für den aktuellen Stand, bevor du weitermachst.";
    }

    private async Task AktualisierePausedUtcInStateJsonAsync(string arbeitsverzeichnisPfad, DateTimeOffset? pausedUtc, CancellationToken ct)
    {
        var stateJsonPfad = Path.Combine(arbeitsverzeichnisPfad, "state.json");
        var node = await StateJsonHelper.LeseAsync(stateJsonPfad, _logger, ct);
        if (node is null)
        {
            return;
        }

        if (node["runtime"] is not JsonObject runtime)
        {
            runtime = new JsonObject();
            node["runtime"] = runtime;
        }

        runtime["paused_utc"] = pausedUtc?.ToString("O");

        await StateJsonHelper.SchreibeAsync(stateJsonPfad, node, ct);
    }
}
