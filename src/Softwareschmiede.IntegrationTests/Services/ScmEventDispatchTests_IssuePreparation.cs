using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Dedizierter Ausführungspfad der Issue-Aufbereitung (Schritt 6): Der interne,
/// trigger-freie Auftrag startet eine reguläre, in die #410-Begrenzung einzählende
/// CLI-Analyse, sendet den festen Analyse-Prompt und wendet das Exchange-Ergebnis als
/// IssueBodyUpdate (+ optionaler Dokumentations-Kommentar) remote an.</summary>
public sealed class ScmEventDispatchTests_IssuePreparation
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Happy Path: Die Analyse läuft als reguläre CLI-Ausführung; der Prompt trägt
    /// Titel, Beschreibung und alle Qualitätskriterien; das gelieferte Ergebnis ersetzt die
    /// Beschreibung remote und legt den Dokumentations-Kommentar an.</summary>
    [Fact]
    public async Task Dispatch_Preparation_DeliversAnalysisPromptAndAppliesResult()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);

        await fixture.DispatchAsync(eventId);

        var delivered = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", delivered.Status);
        Assert.Single(fixture.Sessions);
        var prompt = Assert.Single(fixture.Prompts);
        // Analyse-Prompt: Titel, Beschreibung und die Qualitätskriterien — nicht der rohe Body.
        // Der Prompt ist ein JSON-Umschlag; der Analyse-Text liegt (JSON-escaped) in request.body.
        var promptBody = JsonDocument.Parse(prompt).RootElement.GetProperty("request").GetProperty("body").GetString()!;
        Assert.Contains("Issue Titel 9", promptBody);
        Assert.Contains("Issue-Beschreibung", promptBody);
        Assert.Contains("Verständlichkeit", promptBody);
        Assert.Contains("Vollständigkeit", promptBody);
        Assert.Contains("offene Fragen", promptBody);
        Assert.Contains("Risiken", promptBody);
        Assert.Contains("Zielkonflikte", promptBody);
        Assert.Contains("Lösungsansätze", promptBody);
        Assert.Contains("Fehlinterpretationen", promptBody);
        Assert.Contains("issueBodyUpdate", promptBody);
        Assert.Contains("IssueAufbereitung", prompt); // EventKind im Umschlag.
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var bodyActionId = Guid.NewGuid();
        var commentActionId = Guid.NewGuid();

        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(directory, "actions"));
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", bodyActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, bodyActionId,
                "issueBodyUpdate", Body: "Optimierte Anforderungsbeschreibung."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", commentActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, commentActionId,
                "comment", Body: "Analyse: Schwachstellen, offene Fragen, Risiken."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [bodyActionId, commentActionId], false, Report: "Beschreibung optimiert."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        Assert.Null(item.Reason);
        var update = Assert.Single(fixture.Responses, x => x.Kind == ScmResponseKind.IssueBodyUpdate);
        Assert.Equal("Optimierte Anforderungsbeschreibung.", update.Body);
        var comment = Assert.Single(fixture.Responses, x => x.Kind == ScmResponseKind.Comment);
        Assert.Contains("Analyse", comment.Body);
    }

    /// <summary>Kommentar-Schalter aus: Der gelieferte Dokumentations-Kommentar wird hostseitig
    /// verworfen (ehrliches Result), nur die Beschreibung wird remote ersetzt.</summary>
    [Fact]
    public async Task Dispatch_Preparation_CommentDisabled_SkipsComment()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetSettingAsync(AppSettingService.ScmIssuePreparationCommentEnabledKey, "false");
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var bodyActionId = Guid.NewGuid();
        var commentActionId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(directory, "actions"));
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", bodyActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, bodyActionId,
                "issueBodyUpdate", Body: "Optimierte Beschreibung."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", commentActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, commentActionId,
                "comment", Body: "Analyse-Dokumentation."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [bodyActionId, commentActionId]), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        Assert.Single(fixture.Responses, x => x.Kind == ScmResponseKind.IssueBodyUpdate);
        Assert.DoesNotContain(fixture.Responses, x => x.Kind == ScmResponseKind.Comment);
    }

    /// <summary>Bei aktivem Kommentar-Schalter ist der Analyse-Kommentar Abschlusskriterium:
    /// Fehlt er, bleibt das Ergebnis ehrlich unklar — die Beschreibung ist bereits remote.</summary>
    [Fact]
    public async Task Dispatch_Aufbereitung_KommentarFehlt_Ungeklärt()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var bodyActionId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(directory, "actions"));
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", bodyActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, bodyActionId,
                "issueBodyUpdate", Body: "Optimierte Beschreibung."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [bodyActionId]), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", item.Status);
        Assert.Contains("Kommentar", item.Reason);
        Assert.Single(fixture.Responses, x => x.Kind == ScmResponseKind.IssueBodyUpdate);
    }

    /// <summary>Kein ausführbares Plugin (nichts konfiguriert): Kein Start, ehrlicher
    /// Wartegrund, der Auftrag bleibt eingereiht.</summary>
    [Fact]
    public async Task Dispatch_Preparation_NoPlugin_WaitsWithReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, aiPluginPrefix: null);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Keine ausführbare CLI", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>Konfiguriertes, aber nicht vorhandenes Plugin: Der Wartegrund nennt die
    /// ignorierte Zuordnung und den fehlenden Fallback — ehrlich statt stiller Fehlschlag.</summary>
    [Fact]
    public async Task Dispatch_Preparation_DisabledPlugin_WaitsWithReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        await fixture.SetSettingAsync(AppSettingService.ScmIssuePreparationPluginKey, "custom.auf");
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, aiPluginPrefix: null);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("custom.auf", item.Reason);
        Assert.Contains("Issue-Aufbereitung", item.Reason);
        Assert.Empty(fixture.Sessions);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Contains(await db.LogEntries.ToListAsync(cancellationToken: TestContext.Current.CancellationToken), x => x.Content.Contains("custom.auf"));
    }

    /// <summary>Die Ausführung zählt in die #410-Begrenzung: Bei erreichtem globalem Limit
    /// startet die Aufbereitung nicht und wartet mit Limit-Grund.</summary>
    [Fact]
    public async Task Dispatch_Preparation_GlobalLimitReaches_WaitsWithReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        await fixture.SetSettingAsync(ScmEventExecutionLimitService.GlobalLimitKey, "1");
        var (_, blockerId) = await fixture.AddAsync(configured: true);
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);
        await fixture.DispatchAsync(blockerId);
        Assert.Equal("Zugestellt", (await fixture.EventAsync(blockerId)).Status);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Limit", item.Reason);
        Assert.Single(fixture.Sessions); // nur die blockierende Ausführung
    }

    /// <summary>Deaktivierter Funktionsschalter: Der eingereihte Auftrag bleibt mit ehrlichem
    /// Grund wartend — ohne Start, ohne Remote-Änderung.</summary>
    [Fact]
    public async Task Dispatch_Preparation_FunctionDisabled_WaitsWithReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("deaktiviert", item.Reason);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>Abgewähltes Repository-Flag: gleiche ehrliche Wartehaltung.</summary>
    [Fact]
    public async Task Dispatch_Preparation_RepositoryNotEnabled_WaitsWithReason()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix, issuePreparationEnabled: false);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Repository", item.Reason);
        Assert.Empty(fixture.Sessions);
    }

    /// <summary>Ein bereits geschlossenes Issue wird nicht analysiert und nicht remote
    /// verändert; der Auftrag endet sauber mit Begründung im Protokoll.</summary>
    [Fact]
    public async Task Dispatch_Aufbereitung_IssueGeschlossen_VerarbeitetOhneÄnderung()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new Issue(reference.Number,
                "Issue Titel " + reference.Number, "Beschreibungstext", Array.Empty<string>(), null,
                "https://github.com/owner/repo/issues/" + reference.Number)
            { IsClosed = true });
        var (taskId, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        Assert.Empty(fixture.Sessions);
        Assert.Empty(fixture.Responses);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Contains(await db.LogEntries.Where(x => x.WorkItemId == taskId).ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("geschlossen"));
    }

    /// <summary>Fehlschlag der Rückschreibung: Der Remote-Fehler wird als unklarer Zustand
    /// mit Protokolleintrag festgehalten — kein Fehlerkommentar am Issue.</summary>
    [Fact]
    public async Task Dispatch_Aufbereitung_RückschreibungFehler_UngeklärtOhneFehlerkommentar()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        var (taskId, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Kind == ScmResponseKind.IssueBodyUpdate), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScmProviderException(ScmProviderFailureKind.Permanent, "Remote-Fehler beim Schreiben."));
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var bodyActionId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(directory, "actions"));
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", bodyActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, bodyActionId,
                "issueBodyUpdate", Body: "Optimierte Beschreibung."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [bodyActionId]), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", item.Status);
        Assert.Contains("Remote-Fehler", item.Reason);
        Assert.DoesNotContain(fixture.Responses, x => x.Kind == ScmResponseKind.Comment); // kein Fehlerkommentar
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Contains(await db.LogEntries.Where(x => x.WorkItemId == taskId).ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("Issue-Aufbereitung fehlgeschlagen"));
    }

    /// <summary>Ein begründeter Abschluss ohne Remoteaktion verarbeitet den Auftrag —
    /// das Issue bleibt vollständig unverändert.</summary>
    [Fact]
    public async Task Dispatch_Preparation_NoRemoteAction_ProcessesWithoutWrite()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], true, "Issue konnte nicht sinnvoll verbessert werden."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        Assert.Empty(fixture.Responses);
    }

    /// <summary>Issue war beim Dispatch-Check offen und wird vor der Zustellung
    /// geschlossen: Die CLI startet noch, aber kein Prompt wird zugestellt, keine
    /// Remote-Aktion läuft — der Auftrag endet sauber mit Begründung.</summary>
    [Fact]
    public async Task Dispatch_Aufbereitung_IssueVorZustellungGeschlossen_VerarbeitetOhneZustellung()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        var reads = 0;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new Issue(reference.Number,
                "Issue Titel " + reference.Number, "Beschreibungstext", Array.Empty<string>(), null,
                "https://github.com/owner/repo/issues/" + reference.Number)
            { IsClosed = Interlocked.Increment(ref reads) > 1 });
        var (taskId, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        Assert.Single(fixture.Sessions); // CLI startete noch vor dem finalen Check
        Assert.Empty(fixture.Prompts);
        Assert.Empty(fixture.Responses);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Contains(await db.LogEntries.Where(x => x.WorkItemId == taskId).ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("geschlossen"));
    }

    /// <summary>Issue wurde während der Analyse geschlossen: Die gelieferten
    /// Aktionen werden nicht remote angewendet (Ergebnis-Dateien mit ehrlicher
    /// Begründung), der Auftrag endet ohne Remote-Änderung als verarbeitet.</summary>
    [Fact]
    public async Task Dispatch_Aufbereitung_IssueWährendAnalyseGeschlossen_KeinRemoteUpdate()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        var closed = false;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => new Issue(reference.Number,
                "Issue Titel " + reference.Number, "Beschreibungstext", Array.Empty<string>(), null,
                "https://github.com/owner/repo/issues/" + reference.Number)
            { IsClosed = closed });
        var (taskId, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", delivered.Status);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var bodyActionId = Guid.NewGuid();
        var commentActionId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(directory, "actions"));
        Directory.CreateDirectory(Path.Combine(directory, "results"));
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", bodyActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, bodyActionId,
                "issueBodyUpdate", Body: "Optimierte Beschreibung."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", commentActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, commentActionId,
                "comment", Body: "Analyse-Dokumentation."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [bodyActionId, commentActionId]), Json), TestContext.Current.CancellationToken);
        closed = true;

        // Zwei Abgleich-Runden: die erste beobachtet die Nachrichten nur
        // (Stabilitätsprüfung des Antwortkanals), die zweite verarbeitet sie.
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        Assert.Empty(fixture.Responses); // keine Remote-Aktion auf geschlossenem Issue
        var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "results", bodyActionId + ".json"), TestContext.Current.CancellationToken)).RootElement;
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.Contains("geschlossen", result.GetProperty("error").GetString());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        Assert.Contains(await db.LogEntries.Where(x => x.WorkItemId == taskId).ToListAsync(cancellationToken: TestContext.Current.CancellationToken),
            x => x.Content.Contains("geschlossen"));
    }

    /// <summary>Unlesbares Issue unmittelbar vor der Zustellung: ehrlich wartend
    /// statt als geschlossen interpretiert — nach Rückkehr der Lesbarkeit läuft
    /// die Zustellung regulär weiter.</summary>
    [Fact]
    public async Task Dispatch_Aufbereitung_IssueVorZustellungUnlesbar_WartetDannZugestellt()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        var reads = 0;
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScmEventReference reference, CancellationToken _) => Interlocked.Increment(ref reads) == 1
                ? new Issue(reference.Number, "Issue Titel " + reference.Number, "Beschreibungstext",
                    Array.Empty<string>(), null, "https://github.com/owner/repo/issues/" + reference.Number)
                : (Issue?)null);
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);

        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("nicht gelesen", item.Reason);
        Assert.Empty(fixture.Prompts);

        fixture.ConfigureIssue();

        await fixture.DispatchAsync(eventId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Unlesbares Issue im Abgleich: Der Versand gilt weder als
    /// geschlossen noch als freigegeben — das Ereignis bleibt in Bearbeitung,
    /// keine Aktion wird angewendet; nach Rückkehr der Lesbarkeit verarbeitet
    /// der nächste Abgleich regulär.</summary>
    [Fact]
    public async Task Dispatch_Aufbereitung_IssueImAbgleichUnlesbar_WartetDannVerarbeitet()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        await EnableAsync(fixture);
        fixture.ConfigureIssue();
        var (_, eventId) = await AddPreparationAsync(fixture, ScmDispatchFixture.AiPrefix);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", delivered.Status);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var bodyActionId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(directory, "actions"));
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", bodyActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, bodyActionId,
                "issueBodyUpdate", Body: "Optimierte Beschreibung."), Json), TestContext.Current.CancellationToken);
        fixture.Git.As<IScmEventSource>().Setup(x => x.ReadIssueAsync(It.IsAny<ScmEventReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Issue?)null);

        // Zwei Runden: Stabilitätsbeobachtung, dann Verarbeitung bis zur Lese-Prüfung.
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("InBearbeitung", item.Status);
        Assert.Empty(fixture.Responses);

        fixture.ConfigureIssue();
        var commentActionId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", commentActionId + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId, commentActionId,
                "comment", Body: "Analyse-Dokumentation."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [bodyActionId, commentActionId]), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        Assert.Equal(2, fixture.Responses.Count);
    }

    private static async Task EnableAsync(ScmDispatchFixture fixture)
        => await fixture.SetSettingAsync(AppSettingService.ScmIssuePreparationEnabledKey, "true");

    /// <summary>Legt die Schritt-5-Artefakte direkt an: Zweck-markierte Aufgabe mit
    /// IssueReferenz und internem, sofort fälligem Aufbereitungsereignis.</summary>
    private static async Task<(Guid TaskId, Guid EventId)> AddPreparationAsync(ScmDispatchFixture fixture,
        string? aiPluginPrefix, bool issuePreparationEnabled = true)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiedeDbContext>();
        var project = new Project { Id = Guid.NewGuid(), Name = "Aufbereitung" };
        var repository = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            RepositoryUrl = "https://github.com/owner/repo",
            RepositoryName = "repo",
            PluginType = fixture.Repository.ProviderPrefix,
            Active = true,
            IssuePreparationEnabled = issuePreparationEnabled
        };
        var task = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Issue Titel 9",
            ProjectId = project.Id,
            GitRepositoryId = repository.Id,
            Purpose = ScmIssuePreparationService.Purpose,
            AiPluginPrefix = aiPluginPrefix
        };
        var reference = new IssueReference
        {
            Id = Guid.NewGuid(),
            WorkItemId = task.Id,
            IssueNumber = 9,
            Title = "Issue Titel 9",
            Body = "Issue-Beschreibung",
            IssueUrl = "https://github.com/owner/repo/issues/9",
            SourceKey = "test-scm/github.com/R_1/IssueAufbereitung/9"
        };
        var time = DateTimeOffset.UtcNow;
        var observation = new ScmEventObservation(new(fixture.Repository, ScmReferenceKind.Issue, 9),
            ScmContentKind.Description, "I_9", null, time, time, null, "U_1", "owner", null, null,
            "Issue-Beschreibung", "https://github.com/owner/repo/issues/9", time);
        var item = new ScmIncomingEvent
        {
            WorkItemId = task.Id,
            EventKey = observation.ContentKey + "/" + ScmIssuePreparationService.Purpose,
            ObservationJson = JsonSerializer.Serialize(observation, ScmEventStore.JsonOptions),
            ReceivedUtc = time.ToUnixTimeMilliseconds(),
            Purpose = ScmIssuePreparationService.Purpose,
            Reason = "Interner Aufbereitungsauftrag für Issue #9."
        };
        db.AddRange(project, repository, task, reference, item);
        await db.SaveChangesAsync();
        return (task.Id, item.Id);
    }
}
