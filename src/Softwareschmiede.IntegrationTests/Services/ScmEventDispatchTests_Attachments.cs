using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.TestSupport;

namespace Softwareschmiede.IntegrationTests.Services;

/// <summary>Ende-zu-End-Pfad des Screenshot-Nachweises: Abgelegte oder gemeldete Bilder eines
/// akzeptierten Abschlusses erzeugen eine eigene, versuchsübergreifende Outbox-Aktion, die auch
/// nach „Verarbeitet" ausgeführt und wiederholt werden kann.</summary>
public sealed class ScmEventDispatchTests_Attachments
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    /// <summary>Ein abgelegtes PNG erzeugt nach dem akzeptierten Abschluss genau eine
    /// attachments-Kommentaranfrage mit der validierten Dateispezifikation — der Folge-Dispatch
    /// bleibt ohne neue Meldungen idempotent.</summary>
    [Fact]
    public async Task Dispatch_ScreenshotInConventionDirectory_CreatesAttachmentsAction()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllBytesAsync(Path.Combine(directory, "screenshots", "nachweis.png"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "UI angepasst."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        var request = Assert.Single(fixture.Responses, x => x.Attachments is { Count: > 0 });
        var attachment = Assert.Single(request.Attachments!);
        Assert.Equal("nachweis.png", attachment.Name);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.StartsWith(directory, attachment.Path);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var action = await db.ScmOutgoingActions.SingleAsync(x => x.Purpose == "attachments", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Succeeded", action.Status);
            Assert.Null(action.AttemptId);
        }
        var responsesBefore = fixture.Responses.Count;
        await fixture.DispatchAsync(eventId);
        Assert.Equal(responsesBefore, fixture.Responses.Count);
    }

    /// <summary>Deklarierte Bilder tragen ihre Unterschrift mit; deklarierte Ausbrüche werden
    /// übersprungen und als Hinweis im Kommentartext mitgeführt.</summary>
    [Fact]
    public async Task Dispatch_DeclaredAttachments_CarryCaptionAndSkipOutsidePaths()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllBytesAsync(Path.Combine(directory, "screenshots", "gemeldet.png"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "nichtgeprueft.exe"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "UI angepasst.",
                Attachments:
                [
                    new ScmExchangeAttachment("screenshots/gemeldet.png", "Vorher"),
                    new ScmExchangeAttachment("../escape.png")
                ]), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        var request = Assert.Single(fixture.Responses, x => x.Attachments is { Count: > 0 });
        // Deklaration und Scan finden dieselbe Datei: Die deklarierte Unterschrift gewinnt.
        var attachment = Assert.Single(request.Attachments!);
        Assert.Equal("gemeldet.png", attachment.Name);
        Assert.Equal("Vorher", attachment.Caption);
        Assert.Contains("Übersprungen", request.Body);
        Assert.Contains("escape.png", request.Body);
    }

    /// <summary>Viele übersprungene Dateien werden im öffentlichen Kommentartext wie im
    /// Aufgabenprotokoll gekappt: höchstens acht Einzelzeilen plus Sammelzeile, damit der
    /// Body das Remote-Limit (65 536 Zeichen) niemals erreicht.</summary>
    [Fact]
    public async Task Dispatch_ManySkippedAttachments_CapsCommentBody()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllBytesAsync(Path.Combine(directory, "screenshots", "nachweis.png"), Png, TestContext.Current.CancellationToken);
        for (var i = 0; i < 12; i++)
            await File.WriteAllTextAsync(Path.Combine(directory, "screenshots", $"ungueltig-{i:D2}.png"), "kein bild", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "UI angepasst."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        var request = Assert.Single(fixture.Responses, x => x.Attachments is { Count: > 0 });
        var body = request.Body!;
        Assert.Equal(8, body.Split("Übersprungen:").Length - 1);
        Assert.Contains("4 weitere übersprungene Dateien", body);
        Assert.True(body.Length < 65536);
    }

    /// <summary>Werden alle abgelegten Dateien verworfen, entsteht trotzdem eine sichtbare
    /// attachments-Kommentaraktion mit den Übersprungen-Hinweisen — eine versuchte, vollständig
    /// verworfene Lieferung bleibt auf GitHub und in der Queue nachvollziehbar.</summary>
    [Fact]
    public async Task Dispatch_AllScreenshotsSkipped_StillCreatesAttachmentsAction()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllTextAsync(Path.Combine(directory, "screenshots", "ungueltig.png"), "kein bild", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "UI angepasst."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        var request = Assert.Single(fixture.Responses, x => x.Body?.Contains("Screenshot-Nachweise") == true);
        Assert.Empty(request.Attachments!);
        Assert.Contains("Übersprungen: ungueltig.png (Datei-Signatur passt nicht zum Bildformat)", request.Body);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal("Succeeded", (await db.ScmOutgoingActions.SingleAsync(x => x.Purpose == "attachments", cancellationToken: TestContext.Current.CancellationToken)).Status);
    }

    /// <summary>Eine ungültige attachments-Deklaration kippt den sonst gültigen Abschluss nicht
    /// mehr auf „Verarbeitung ungeklärt": Das Ereignis wird „Verarbeitet" und die verworfenen
    /// Einträge erscheinen als Übersprungen-Hinweise im Nachweis-Kommentar.</summary>
    [Fact]
    public async Task Dispatch_InvalidAttachmentDeclarations_StillProcessesAndNotesSkipped()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "UI angepasst.",
                Attachments:
                [
                    new ScmExchangeAttachment(""),
                    new ScmExchangeAttachment("fehlend.png", new string('x', ScmEventExchangeProtocol.MaximumCaptionChars + 1))
                ]), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("Verarbeitet", item.Status);
        var request = Assert.Single(fixture.Responses, x => x.Body?.Contains("Screenshot-Nachweise") == true);
        Assert.Contains("ohne verwertbaren Pfad", request.Body);
        Assert.Contains("Längenlimit", request.Body);
    }

    /// <summary>Nimmt der Agent die errechenbare attachments-Aktions-ID mit einer eigenen Aktion
    /// vorweg, wird sie nicht still als Nachweis wiederverwendet: Das Ereignis endet sichtbar als
    /// „Verarbeitung ungeklärt", während die gemeldete Agentenaktion regulär ausgeführt wurde.</summary>
    [Fact]
    public async Task Dispatch_AgentUsurpsAttachmentsActionId_EventBecomesUnclear()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        var usurped = ScmEventStore.ActionId(eventId, "attachments");
        await File.WriteAllBytesAsync(Path.Combine(directory, "screenshots", "nachweis.png"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "actions", usurped.ToString("D") + ".json"), JsonSerializer.Serialize(new ScmExchangeAction(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                usurped, "comment", Body: "Agenten-Kommentar unter der Nachweis-ID."), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [usurped], false, Report: "UI angepasst."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        var item = await fixture.EventAsync(eventId);
        Assert.Equal("VerarbeitungUngeklärt", item.Status);
        Assert.Contains("Aktionskennung", item.Reason);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        // Die vorweggenommene ai-Aktion wurde regulär ausgeführt; keine attachments-Aktion
        // hat sie still als Succeeded-Nachweis übernommen.
        var action = await db.ScmOutgoingActions.SingleAsync(x => x.Id == usurped, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("ai:" + usurped.ToString("D"), action.Purpose);
        Assert.Equal("Succeeded", action.Status);
        Assert.Empty(await db.ScmOutgoingActions.Where(x => x.Purpose == "attachments").ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.DoesNotContain(fixture.Responses, x => x.Attachments is { Count: > 0 });
    }

    /// <summary>Ohne verwertbare Bilder wird keine attachments-Aktion angelegt — das Abschluss-
    /// verhalten des Ereignisses bleibt unverändert.</summary>
    [Fact]
    public async Task Dispatch_NoScreenshots_CreatesNoAttachmentsAction()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "UI angepasst."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        Assert.DoesNotContain(fixture.Responses, x => x.Attachments is { Count: > 0 });
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Empty(await db.ScmOutgoingActions.Where(x => x.Purpose == "attachments").ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Auch ein begründeter Abschluss ohne Remoteaktionen sendet vorhandene
    /// Nachweis-Bilder.</summary>
    [Fact]
    public async Task Dispatch_NoRemoteAction_WithScreenshot_StillCreatesAttachmentsAction()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllBytesAsync(Path.Combine(directory, "screenshots", "nachweis.png"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], true, "Nur Screenshots zur Dokumentation; nichts geändert."), Json), TestContext.Current.CancellationToken);

        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        var request = Assert.Single(fixture.Responses, x => x.Attachments is { Count: > 0 });
        Assert.Equal("nachweis.png", Assert.Single(request.Attachments!).Name);
    }

    /// <summary>Ein Upload-Fehlschlag der attachments-Aktion bleibt wiederholbar (Failed, nie
    /// Uncertain); der explizite Retry führt die Aktion erneut aus, ohne das verarbeitete
    /// Ereignis zu verändern.</summary>
    [Fact]
    public async Task Dispatch_AttachmentsActionFailure_RetriesOnRetryAsync()
    {
        await using var fixture = new ScmDispatchFixture();
        await fixture.InitializeAsync();
        var attempts = 0;
        fixture.Git.As<IScmEventResponder>().Setup(x => x.ExecuteResponseAsync(
            It.Is<ScmResponseRequest>(r => r.Attachments != null && r.Attachments.Count > 0), It.IsAny<CancellationToken>()))
            .Returns((ScmResponseRequest request, CancellationToken _) =>
            {
                if (++attempts == 1) throw new ScmProviderException(ScmProviderFailureKind.Temporary, "Upload fehlgeschlagen.");
                fixture.Responses.Add(request);
                return Task.FromResult(new ScmResponseResult("remote", null, false));
            });
        var (_, eventId) = await fixture.AddAsync(configured: true);
        await fixture.DispatchAsync(eventId);
        var delivered = await fixture.EventAsync(eventId);
        var runId = Guid.Parse(delivered.CliRunId!);
        var directory = delivered.ExchangePath!;
        await File.WriteAllBytesAsync(Path.Combine(directory, "screenshots", "nachweis.png"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "accepted.json"), JsonSerializer.Serialize(new ScmExchangeAccepted(1, eventId, delivered.DeliveryAttemptId!.Value, runId), Json), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "complete.json"), JsonSerializer.Serialize(new ScmExchangeComplete(1, eventId, delivered.DeliveryAttemptId!.Value, runId,
                [], false, Report: "UI angepasst."), Json), TestContext.Current.CancellationToken);
        await fixture.DispatchAsync(eventId);
        await fixture.DispatchAsync(eventId);

        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
            var action = await db.ScmOutgoingActions.SingleAsync(x => x.Purpose == "attachments", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Failed", action.Status);
            Assert.NotNull(action.NextAttemptUtc);
        }

        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ScmEventOrchestrator>().RetryAsync(eventId, TestContext.Current.CancellationToken);

        Assert.Equal(2, attempts);
        using var verification = fixture.Services.CreateScope();
        var db2 = verification.ServiceProvider.GetRequiredService<SoftwareschmiededDbContext>();
        Assert.Equal("Succeeded", (await db2.ScmOutgoingActions.SingleAsync(x => x.Purpose == "attachments", cancellationToken: TestContext.Current.CancellationToken)).Status);
        Assert.Equal("Verarbeitet", (await fixture.EventAsync(eventId)).Status);
    }
}
