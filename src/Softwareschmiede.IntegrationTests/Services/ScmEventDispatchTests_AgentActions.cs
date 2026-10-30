using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Vollständiger SCM-Pfad für selbst ausgeführte Agenten-Aktionen: Das Repository-Plugin
/// stellt dem CLI-Kindprozess seine SCM-Anbindung als Prozessumgebung bereit, und ein korrelierter
/// Abschluss mit Ergebnisbericht verarbeitet das Ereignis ohne Kanal-Aktionskind.</summary>
public sealed class ScmEventDispatchTests_AgentActions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Ein SCM-Plugin mit Agenten-Anbindung reicht seine Umgebungsvariablen (z. B. GH_TOKEN)
    /// unverändert in die Startbeschreibung des CLI-Prozesses durch; die Werte tauchen im Prompt nicht auf.</summary>
    [Fact]
    public async Task Dispatch_InjectsAgentEnvironmentIntoSessionSpec()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        fixture.Git.As<IScmAgentAccess>().Setup(x => x.GetAgentEnvironmentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>
            {
                ["GH_TOKEN"] = "agent-secret-token",
                ["GIT_TERMINAL_PROMPT"] = "0",
            });
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        var spec = Assert.Single(fixture.StartSpecs);
        Assert.Equal("agent-secret-token", spec.EnvironmentVariables["GH_TOKEN"]);
        Assert.Equal("0", spec.EnvironmentVariables["GIT_TERMINAL_PROMPT"]);
        // Der Simulator-Vertrag des Plugins bleibt erhalten; Geheimnisse kommen nie in den Prompt.
        Assert.Equal("1", spec.EnvironmentVariables["SOFTWARESCHMIEDE_SCM_SIMULATOR"]);
        Assert.DoesNotContain("agent-secret-token", Assert.Single(fixture.Prompts));
    }

    /// <summary>Ein Fehler beim Auflösen der Agenten-Anbindung blockiert den regulären CLI-Start
    /// nicht: Die Spec bleibt unverändert und das Ereignis wird zugestellt.</summary>
    [Fact]
    public async Task Dispatch_FailingAgentEnvironment_KeepsSpecUntouched()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        fixture.Git.As<IScmAgentAccess>().Setup(x => x.GetAgentEnvironmentAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Credential-Store nicht erreichbar."));
        var (_, eventId) = await fixture.AddAsync(configured: true);

        await fixture.DispatchAsync(eventId);

        Assert.Equal("Zugestellt", (await fixture.EventAsync(eventId)).Status);
        var spec = Assert.Single(fixture.StartSpecs);
        Assert.False(spec.EnvironmentVariables.ContainsKey("GH_TOKEN"));
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Der Agent meldet Annahme und schließt mit einem Bericht selbst ausgeführter
    /// Remoteaktionen ab — ohne jede Kanalaktion wird das Ereignis verarbeitet.</summary>
    [Fact]
    public async Task Dispatch_AgentReportCompletion_ProcessesWithoutChannelAction()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true, referenceKind: Domain.ValueObjects.ScmReferenceKind.PullRequest);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", delivered.Status);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var actionsBefore = fixture.Responses.Count;

        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"),
            JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json));
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"),
            JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "Konflikt im Arbeitsverzeichnis behoben; PR #7 per gh pr merge --admin gemergt."), Json));

        // Der stabile zweite Lesevorgang des Kanals verarbeitet die Meldungen.
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        Assert.Null(item.Reason);
        Assert.NotNull(item.AcceptedUtc);
        // Keine KI-Kanalaktion wurde remote ausgeführt — nur die Empfangsreaktion des Hosts.
        Assert.Equal(actionsBefore, fixture.Responses.Count);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.DoesNotContain(await db.ScmOutgoingActions.ToListAsync(), x => x.Purpose.StartsWith("ai:", StringComparison.Ordinal));
    }

    /// <summary>Ein Abschluss ohne Kanalaktionen, Bericht und Begründung bleibt ein Protokollfehler
    /// und wird nicht als verarbeitet gewertet.</summary>
    [Fact]
    public async Task Dispatch_EmptyCompletionWithoutReportOrReason_StaysUnresolved()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;

        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"),
            JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json));
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"),
            JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId, []), Json));

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", item.Status);
        Assert.Contains("Remoteaktionen", item.Reason);
    }

    /// <summary>Der dokumentierte noRemoteAction-Pfad bleibt unverändert: Begründete Bearbeitung
    /// ohne jede Remoteaktion verarbeitet das Ereignis.</summary>
    [Fact]
    public async Task Dispatch_NoRemoteActionCompletion_StillProcesses()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;

        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"),
            JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json));
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"),
            JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], true, "Keine Schreibberechtigung für den erlaubten Schritt; lokal nichts geändert."), Json));

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
    }
}
