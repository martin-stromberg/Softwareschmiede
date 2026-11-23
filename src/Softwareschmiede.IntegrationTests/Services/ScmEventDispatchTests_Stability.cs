using Microsoft.Extensions.DependencyInjection;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Stabilisierungsfrist vor der Prompt-Zustellung (Issue #463): Ein Prompt wird erst
/// nach <see cref="ScmEventOrchestrator.DeliveryStabilityPeriod"/> anhaltender, ununterbrochener
/// Eingabebereitschaft (<see cref="ScmPromptReadyStatus.Ready"/>) in die laufende CLI geschrieben;
/// vorher endet der Versuch ehrlich wartend mit verschobenem
/// <see cref="Softwareschmiede.Domain.Entities.ScmIncomingEvent.NextAttemptUtc"/>.</summary>
public sealed class ScmEventDispatchTests_Stability
{
    /// <summary>Akzeptanzszenario 4: Die frisch bereite Sitzung (&lt; 10 s) erhält noch keinen
    /// Prompt — das Ereignis bleibt <c>Wartend</c> mit <c>NextAttemptUtc</c> auf
    /// <c>ReadySinceUtc + Frist</c>; erst der Folgeversuch nach der Frist stellt zu.</summary>
    [Fact]
    public async Task Dispatch_ReadySinceYoungerThanDeadline_StaysWaiting()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        using var scope = fixture.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>();

        await orchestrator.DispatchAsync(eventId, ct: TestContext.Current.CancellationToken);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Stabilisierungsphase", item.Reason);
        // Eingefrorene Testzeit: die Sitzung wurde exakt jetzt bereit — die Frist endet
        // deterministisch bei Time.Now + DeliveryStabilityPeriod.
        Assert.Equal(
            fixture.Time.GetUtcNow().Add(ScmEventOrchestrator.DeliveryStabilityPeriod).ToUnixTimeMilliseconds(),
            item.NextAttemptUtc);
        Assert.Empty(fixture.Prompts);
        // Der generische Pfad quittiert das Warten wie die übrigen CLI-Wartegründe remote.
        Assert.Contains(fixture.Responses, x => x.Kind == ScmResponseKind.Comment);

        ((TestTimeProvider)fixture.Time).Advance(ScmEventOrchestrator.DeliveryStabilityPeriod + TimeSpan.FromSeconds(1));
        await orchestrator.DispatchAsync(eventId, ct: TestContext.Current.CancellationToken);

        item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.Null(item.NextAttemptUtc);
        Assert.Single(fixture.Prompts);
    }

    /// <summary>Akzeptanzszenario 5: Ein Statuswechsel innerhalb der Frist stempelt
    /// <c>ReadySinceUtc</c> neu — die Frist läuft faktisch von vorn; erst anhaltend stabile
    /// Bereitschaft führt zur Zustellung, nie ein Versuch innerhalb der instabilen Phase.</summary>
    [Fact]
    public async Task Dispatch_ReadyFlipsWithinDeadline_ResetsDeadline()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (taskId, eventId) = await fixture.AddAsync(configured: true);
        var clock = (TestTimeProvider)fixture.Time;
        using var scope = fixture.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>();

        await orchestrator.DispatchAsync(eventId, ct: TestContext.Current.CancellationToken);
        Assert.Equal("Wartend", (await fixture.EventAsync(eventId)).Status);

        // Innerhalb der Frist: Bereitschaft kippt weg und kommt zurück — ReadySinceUtc
        // wird auf den erneuten Übergang neu gestempelt.
        clock.Advance(TimeSpan.FromSeconds(4));
        fixture.SetPrompt(taskId, "SCM_UNKNOWN_V1");
        fixture.SetPrompt(taskId, fixture.Prompt);
        var restampUtc = clock.GetUtcNow();

        // Nach Ablauf der ursprünglichen Frist, aber vor der neuen: Der fällige Folgeversuch
        // erkennt den neuen Bereitschafts-Stempel und bleibt mit verschobener Frist wartend.
        clock.Advance(TimeSpan.FromSeconds(7));
        await orchestrator.DispatchAsync(eventId, ct: TestContext.Current.CancellationToken);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Wartend", item.Status);
        Assert.Contains("Stabilisierungsphase", item.Reason);
        Assert.Equal(
            restampUtc.Add(ScmEventOrchestrator.DeliveryStabilityPeriod).ToUnixTimeMilliseconds(),
            item.NextAttemptUtc);
        Assert.Empty(fixture.Prompts);

        // Erst anhaltend stabile Bereitschaft über die volle neue Frist liefert.
        clock.Advance(ScmEventOrchestrator.DeliveryStabilityPeriod);
        await orchestrator.DispatchAsync(eventId, ct: TestContext.Current.CancellationToken);

        item = await fixture.EventAsync(eventId);
        Assert.Equal("Zugestellt", item.Status);
        Assert.Single(fixture.Prompts);
    }
}
