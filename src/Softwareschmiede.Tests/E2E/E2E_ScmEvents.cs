using System.Text.Json;
using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Softwareschmiede.App.Services.Testing;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.E2E.Views.Dialogs;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.Helpers;
using Xunit;

namespace Softwareschmiede.Tests.E2E;

/// <summary>Real WPF, GitHub parser, SQLite inbox, orchestration and file-exchange flow with isolated remote transport.</summary>
[Trait("Category", "E2E")]
[Trait("Scenario", "ScmEvents")]
[OsInterface]
[Collection("E2E")]
public sealed class ScmEventsE2ETests(ITestOutputHelper output) : WpfTestBase
{
    /// <summary>Exercises queue navigation, replay, restart, selection/cancel, lifecycle, filters, blocked tasks and issue/PR replies.</summary>
    [Fact]
    public async Task ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline()
    {
        // Früh überspringen, bevor die Fixture ihre Git-Workspaces/DB aufbaut.
        SkipWhenWpfE2ENotAvailable();

        var fixture = new ScmE2EFixture();
        await fixture.InitializeAsync();
        output.WriteLine("SCM fixture and evidence: " + fixture.Configuration.TestRoot);
        var window = WaitOnRealMainwindow(LaunchApp(false, fixture.EnvironmentVariables()));
        NavigateQueue(window);
        const string original = "$sOfTwArEsChMiEdE Prüfe \"Original\"\nzweite Zeile\u001b\u0000";
        fixture.AddText(1, 101, original);
        var first = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 1);
        // Windows UI Automation schneidet einen Textwert am NUL-Zeichen ab; die Speicher- und Request-Asserts darunter behalten ihn bei.
        Assert.Equal(original.TrimEnd('\0'), Element(window, "ScmEventBody").AsTextBox().Text);
        Assert.Equal(original, ScmEventStore.Observation(first).Body);
        Assert.Equal("https://github.com/scm-owner/scm-e2e/issues/1#issuecomment-101", Element(window, "ScmEventOrigin").AsTextBox().Text);
        Assert.Contains("Keine Standard-CLI", Element(window, "ScmEventReason").Name);
        await fixture.WaitForPollAsync();
        Assert.Single(fixture.Responses(), x => x.Content == "eyes");
        Assert.Single(fixture.Responses(), x => x.Body.Contains(":waiting -->", StringComparison.Ordinal));
        Assert.Empty(fixture.Prompts());
        await using (var db = fixture.OpenDatabase())
        {
            Assert.Single(await db.ScmIncomingEvents.ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(WorkItemStatus.Started, (await db.WorkItems.FindAsync(new object?[] { fixture.TaskIds[1] }, TestContext.Current.CancellationToken))!.Status);
        }
        Click(window, "ScmEventBack");
        NavigateQueue(window);
        SelectEvent(window, 1);
        output.WriteLine("E1: real reception, original body/origin, one receipt/waiting comment, replay and back navigation passed.");

        // E5 nutzt den einzigen zweiten App-Start, bevor ein Simulator gestartet wird.
        window = RestartAppPreservingDatabase();
        NavigateQueue(window);
        SelectEvent(window, 1);
        Assert.Equal(first.Id, (await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Wartend)).Id);
        await fixture.WaitForPollAsync();
        Assert.Equal(2, fixture.Responses().Count);
        output.WriteLine("E5: regular app close/restart preserves inbox and does not repeat remote receipts.");

        Click(window, "ScmEventOpen");
        ChoosePlugin(window, confirm: false);
        await using (var db = fixture.OpenDatabase()) Assert.Equal(WorkItemStatus.Started, (await db.WorkItems.FindAsync(new object?[] { fixture.TaskIds[1] }, TestContext.Current.CancellationToken))!.Status);
        Assert.Empty(fixture.Prompts());
        await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 1);
        Click(window, "ScmEventOpen");
        ChoosePlugin(window);
        var delivered = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Zugestellt);
        SelectEvent(window, 1);
        WaitText(window, "ScmEventStatus", ScmIncomingEventStatus.Zugestellt);
        Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        Assert.Single(fixture.Prompts());
        var request = fixture.Prompts()[0].GetProperty("request");
        Assert.Equal(original, request.GetProperty("body").GetString());
        Assert.Equal("https://github.com/scm-owner/scm-e2e/issues/1#issuecomment-101", request.GetProperty("originUrl").GetString());
        fixture.Hold("accepted", false);
        await fixture.WaitEventAsync(1, ScmIncomingEventStatus.InBearbeitung);
        WaitText(window, "ScmEventStatus", "In Bearbeitung");
        Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        fixture.Hold("complete", false);
        await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Verarbeitet);
        await using (var db = fixture.OpenDatabase())
        {
            var task = (await db.WorkItems.FindAsync(new object?[] { fixture.TaskIds[1] }, TestContext.Current.CancellationToken))!;
            Assert.Equal(WorkItemStatus.Started, task.Status);
            Assert.NotNull(task.ActiveRunId);
        }
        fixture.AddText(1, 102, "$Softwareschmiede Folgeereignis");
        var followUp = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Verarbeitet, 102);
        Assert.Equal(delivered.CliRunId, followUp.CliRunId);
        Assert.Equal(2, fixture.Prompts().Count);
        output.WriteLine("E2: single-plugin dialog/cancel/reopen, active task, exactly one prompt per event, delivered/accepted/completed and live-run reuse passed. Worktree was pre-seeded.");

        fixture.AddText(2, 201, "$Softwareschmiede Bleibt wartend");
        await fixture.WaitEventAsync(2, ScmIncomingEventStatus.Wartend);
        var promptCount = fixture.Prompts().Count;
        var responseCount = fixture.Responses().Count;
        Filter(window, ScmIncomingEventStatus.Verarbeitet);
        SelectEvent(window, 1);
        Assert.Equal(ScmIncomingEventStatus.Verarbeitet, Element(window, "ScmEventStatus").Name);
        Assert.Contains("issues/1", Element(window, "ScmEventOrigin").AsTextBox().Text);
        Assert.Equal(2, Element(window, "ScmEventList").AsDataGridView().Rows.Length);
        Filter(window, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 2);
        Assert.Single(Element(window, "ScmEventList").AsDataGridView().Rows);
        Assert.Equal(promptCount, fixture.Prompts().Count);
        Assert.Equal(responseCount, fixture.Responses().Count);
        output.WriteLine("E2-Filter: processed/waiting rows and counts correct without dispatch/remote side effects.");

        foreach (var number in new[] { 3, 4, 5 })
        {
            fixture.AddText(number, number * 100 + 1, "$Softwareschmiede Gesperrte Aufgabe");
            await fixture.WaitEventAsync(number, ScmIncomingEventStatus.Wartend);
            SelectEvent(window, number);
            Assert.Contains(number == 5 ? "pausiert" : "beendet/archiviert", Element(window, "ScmEventReason").Name);
            Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        }
        Assert.Equal(promptCount, fixture.Prompts().Count);
        SelectEvent(window, 5);
        Click(window, "ScmEventTask");
        new WorkItemPauseDialogView(window).ForceShow().Lift();
        NavigateQueue(window);
        SelectEvent(window, 5);
        StartSelected(window);
        await fixture.WaitEventAsync(5, ScmIncomingEventStatus.Verarbeitet);
        await using (var db = fixture.OpenDatabase())
        {
            Assert.Equal(WorkItemStatus.Completed, (await db.WorkItems.FindAsync(new object?[] { fixture.TaskIds[3] }, TestContext.Current.CancellationToken))!.Status);
            Assert.Equal(WorkItemStatus.Archived, (await db.WorkItems.FindAsync(new object?[] { fixture.TaskIds[4] }, TestContext.Current.CancellationToken))!.Status);
            Assert.Null((await db.WorkItems.FindAsync(new object?[] { fixture.TaskIds[5] }, TestContext.Current.CancellationToken))!.PausedUntilUtc);
        }
        output.WriteLine("E4: completed/archived/paused remain blocked; regular pause removal permits processing.");

        fixture.Control("fail-start", true);
        fixture.AddText(6, 601, "$Softwareschmiede Startfehler und Retry");
        await fixture.WaitEventAsync(6, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 6);
        StartSelected(window);
        await WaitReasonAsync(fixture, 6, "Startfehler");
        SelectEvent(window, 6);
        Assert.Contains("Startfehler", Element(window, "ScmEventReason").Name);
        fixture.Control("fail-start", false);
        StartSelected(window);
        await fixture.WaitEventAsync(6, ScmIncomingEventStatus.Verarbeitet);
        output.WriteLine("E3: active task without session, visible simulator start failure and successful retry passed.");

        // Same source parser and response transport for issue/PR comments/descriptions.
        foreach (var (number, kind) in new[] { (7, "IssueComment"), (8, "Description"), (9, "Description") })
        {
            var body = "$Softwareschmiede Vollständiger Inhalt " + number + "\nZeile zwei mit \"Zitat\"";
            var receiptCount = fixture.Responses().Count(x => x.Content == "eyes");
            fixture.AddText(number, number * 100 + 1, body, kind);
            await fixture.WaitEventAsync(number, ScmIncomingEventStatus.Wartend);
            SelectEvent(window, number);
            Assert.Equal(body, Element(window, "ScmEventBody").AsTextBox().Text);
            Assert.Contains(number is 7 or 9 ? $"/pull/{number}" : $"/issues/{number}", Element(window, "ScmEventOrigin").AsTextBox().Text);
            StartSelected(window);
            await fixture.WaitEventAsync(number, ScmIncomingEventStatus.Verarbeitet);
            Assert.Equal(body, fixture.Prompts().Last().GetProperty("request").GetProperty("body").GetString());
            Assert.Equal(receiptCount + (kind == "Description" ? 0 : 1), fixture.Responses().Count(x => x.Content == "eyes"));
            Assert.Contains(fixture.Responses(), x => x.Endpoint.EndsWith($"/issues/{number}/comments", StringComparison.Ordinal) && x.Body.Contains("KI-Simulator:", StringComparison.Ordinal));
        }
        Assert.All(fixture.Responses().Where(x => x.Content is not null), x => Assert.Equal("eyes", x.Content));
        output.WriteLine("E6: issue/PR comment and description bodies reach real file channel, descriptions have no receipt, replies target stored reference and no completion reaction.");

        // Real remote permission failure after actual simulator acceptance; same attempt resumes.
        fixture.Scenario.RejectAiResponse = true;
        fixture.SaveScenario();
        fixture.AddText(1, 103, "$Softwareschmiede Antwortberechtigung wiederherstellen");
        var failed = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.VerarbeitungUngeklärt, 103);
        SelectEvent(window, 1);
        Assert.Contains("403", Element(window, "ScmEventReason").Name);
        promptCount = fixture.Prompts().Count;
        fixture.Scenario.RejectAiResponse = false;
        fixture.SaveScenario();
        Click(window, "ScmEventRetry");
        var recovered = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Verarbeitet, 103);
        Assert.Equal(failed.DeliveryAttemptId, recovered.DeliveryAttemptId);
        Assert.Equal(promptCount, fixture.Prompts().Count);
        output.WriteLine("E6-Wiederaufnahme (403 after acceptance): pending remote reply retried without new event prompt/attempt.");

        // Eine lebende, identifizierte CLI kann explizit bestätigt werden, ohne Bereitschaft aus I/O
        fixture.Control("unknown-ready", true);
        fixture.AddText(10, 1001, "$Softwareschmiede Bereitschaft ausdrücklich bestätigen");
        await fixture.WaitEventAsync(10, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 10);
        StartSelected(window);
        await WaitReasonAsync(fixture, 10, "Eingabebereitschaft unbekannt");
        SelectEvent(window, 10);
        var unknown = await fixture.WaitEventAsync(10, ScmIncomingEventStatus.Wartend);
        Assert.NotNull(unknown.CliRunId);
        promptCount = fixture.Prompts().Count;
        fixture.Control("unknown-ready", false);
        Click(window, "ScmEventConfirmReady");
        var ready = await fixture.WaitEventAsync(10, ScmIncomingEventStatus.Verarbeitet);
        Assert.Equal(unknown.CliRunId, ready.CliRunId);
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E3-Bereitschaft: identified live run with unknown readiness requires explicit confirmation and receives one prompt.");

        var occupiedChannel = System.IO.Path.Combine(fixture.Configuration.TestRoot, "workspace-11", ScmEventExchangeProtocol.DirectoryName);
        System.IO.Directory.CreateDirectory(occupiedChannel);
        var foreignFile = System.IO.Path.Combine(occupiedChannel, "user.txt");
        await System.IO.File.WriteAllTextAsync(foreignFile, "user content", TestContext.Current.CancellationToken);
        fixture.AddText(11, 1101, "$Softwareschmiede Antwortkanal wiederherstellen");
        await fixture.WaitEventAsync(11, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 11);
        promptCount = fixture.Prompts().Count;
        StartSelected(window);
        await WaitReasonAsync(fixture, 11, "fremde Dateien");
        Assert.Equal(promptCount, fixture.Prompts().Count);
        Assert.Equal("user content", await System.IO.File.ReadAllTextAsync(foreignFile, TestContext.Current.CancellationToken));
        System.IO.File.Delete(foreignFile);
        System.IO.Directory.Delete(occupiedChannel);
        SelectEvent(window, 11);
        Click(window, "ScmEventRetry");
        await fixture.WaitEventAsync(11, ScmIncomingEventStatus.Verarbeitet);
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E6-Kanal: unavailable channel blocks event input, retains foreign contents and resumes exactly once after repair.");

        fixture.AddText(12, 1201, "$Softwareschmiede Anmeldung und Berechtigung wiederherstellen");
        await fixture.WaitEventAsync(12, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 12);
        promptCount = fixture.Prompts().Count;
        fixture.Scenario.Authenticated = false;
        fixture.SaveScenario();
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 12, "401");
        fixture.Scenario.Authenticated = true;
        fixture.Scenario.CanWrite = false;
        fixture.SaveScenario();
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 12, "Schreibberechtigung");
        Assert.Equal(promptCount, fixture.Prompts().Count);
        fixture.Scenario.CanWrite = true;
        fixture.SaveScenario();
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 12, "Keine Standard-CLI");
        StartSelected(window);
        await fixture.WaitEventAsync(12, ScmIncomingEventStatus.Verarbeitet);
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E6-Wiederaufnahme: persisted waiting request survives 401 and missing repository permission, then processes once.");

        await MissingVariantsAsync(window, fixture);

        fixture.Control("uncertain-write", true);
        fixture.Control("ignore-next-attempt", true);
        fixture.AddText(1, 104, "$Softwareschmiede Ungewisse Zustellung prüfen");
        var uncertain = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.ZustellungUngewiss, 104);
        SelectEvent(window, 1);
        await WaitPromptCountAsync(fixture, fixture.Prompts().Count == 0 ? 1 : fixture.Prompts().Count,
            uncertain.DeliveryAttemptId);
        promptCount = fixture.Prompts().Count;
        Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        Click(window, "ScmEventCheckStatus");
        Assert.Equal(uncertain.DeliveryAttemptId, (await fixture.WaitEventAsync(1, ScmIncomingEventStatus.ZustellungUngewiss, 104)).DeliveryAttemptId);
        Assert.Equal(promptCount, fixture.Prompts().Count);
        Click(window, "ScmEventResend");
        ConfirmResend(window, false);
        Assert.Equal(uncertain.DeliveryAttemptId, (await fixture.WaitEventAsync(1, ScmIncomingEventStatus.ZustellungUngewiss, 104)).DeliveryAttemptId);
        fixture.Hold("accepted", true);
        Click(window, "ScmEventResend");
        ConfirmResend(window, true);
        var redelivered = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Zugestellt, 104);
        Assert.NotEqual(uncertain.DeliveryAttemptId, redelivered.DeliveryAttemptId);
        fixture.Control("complete-attempt-" + uncertain.DeliveryAttemptId, true);
        await ScmE2EFixture.WaitFileAsync(System.IO.Path.Combine(uncertain.ExchangePath!, "complete.json"));
        await fixture.WaitForPollAsync();
        Assert.Equal(redelivered.DeliveryAttemptId, (await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Zugestellt, 104)).DeliveryAttemptId);
        fixture.Hold("accepted", false);
        var resent = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Verarbeitet, 104);
        Assert.NotEqual(uncertain.DeliveryAttemptId, resent.DeliveryAttemptId);
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        await fixture.WaitForPollAsync();
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E3-Ungewissheit: status/cancel preserves attempt; confirmed resend once; late old completion cannot finish current attempt.");

        fixture.Control("uncertain-write", true);
        fixture.Control("ignore-next-attempt", true);
        fixture.AddText(1, 105, "$Softwareschmiede Verspäteten aktuellen Versuch prüfen");
        var current = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.ZustellungUngewiss, 105);
        await WaitPromptCountAsync(fixture, promptCount + 2, current.DeliveryAttemptId);
        // Beweist die explizite Status-Aktion, solange der aktuelle Versuch noch unsicher ist.
        // Sobald die Fertigstellung freigegeben ist, kann die Hintergrund-Abstimmung sie abschließen und die Aktion verbergen.
        SelectEvent(window, 1);
        Click(window, "ScmEventCheckStatus");
        Assert.Equal(current.DeliveryAttemptId, (await fixture.WaitEventAsync(1, ScmIncomingEventStatus.ZustellungUngewiss, 105)).DeliveryAttemptId);
        Assert.Equal(promptCount + 2, fixture.Prompts().Count);
        fixture.Control("complete-attempt-" + current.DeliveryAttemptId, true);
        await ScmE2EFixture.WaitFileAsync(System.IO.Path.Combine(current.ExchangePath!, "complete.json"));
        var late = await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Verarbeitet, 105);
        Assert.Equal(current.DeliveryAttemptId, late.DeliveryAttemptId);
        Assert.Equal(promptCount + 2, fixture.Prompts().Count);
        output.WriteLine("E3-Ungewissheit: late valid completion of still-current attempt resolves without any resend.");

        fixture.AddText(20, 2001, "$Softwareschmiede Aufgabe anlegen");
        WorkItem? created = null;
        await fixture.WaitForDatabaseAsync(async db =>
        {
            created = await db.WorkItems.Include(x => x.IssueReference)
                .SingleOrDefaultAsync(x => x.IssueReference != null && x.IssueReference.IssueNumber == 20);
            return created is not null;
        }, "Aufgabe für Issue 20 nicht angelegt");
        Assert.NotNull(created);
        Assert.Equal("SCM Referenz 20", created!.Title);
        Assert.Contains("$Softwareschmiede Aufgabe anlegen", created.RequirementDescription);
        await using (var db = fixture.OpenDatabase())
        {
            Assert.Equal(18, await db.WorkItems.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
            var item = Assert.Single(await db.ScmIncomingEvents.Where(x => x.WorkItemId == created.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(20, ScmEventStore.Observation(item).Reference.Number);
        }
        output.WriteLine("E7-Anlage: qualified event on unassigned reference creates exactly one task with issue reference and inbox entry.");

        // Die automatisch erstellte Aufgabe hat keine Standard-CLI, ihr Ereignis wartet also mit umsetzbarem Grund.
        var waiting = await fixture.WaitEventForTaskAsync(created.Id, ScmIncomingEventStatus.Wartend);
        Assert.Contains("Keine Standard-CLI", waiting.Reason);
        promptCount = fixture.Prompts().Count;
        SelectEventByTitle(window, "SCM Referenz 20", "/issues/20");
        Assert.Contains("Keine Standard-CLI", Element(window, "ScmEventReason").Name);
        StartSelected(window);
        await fixture.WaitEventForTaskAsync(created.Id, ScmIncomingEventStatus.Verarbeitet);
        await using (var db = fixture.OpenDatabase())
        {
            var deliveredTask = (await db.WorkItems.FindAsync(new object?[] { created.Id }, TestContext.Current.CancellationToken))!;
            Assert.Equal(WorkItemStatus.Started, deliveredTask.Status);
            Assert.NotNull(deliveredTask.LocalClonePath);
            Assert.StartsWith(fixture.Configuration.TestRoot, deliveredTask.LocalClonePath);
            Assert.True(System.IO.Directory.Exists(System.IO.Path.Combine(deliveredTask.LocalClonePath, ".git")));
            Assert.StartsWith("task/issue-20-", deliveredTask.BranchName);
            Assert.Equal("Softwareschmiede.KiSimulator", deliveredTask.AiPluginPrefix);
        }
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        Assert.Equal("$Softwareschmiede Aufgabe anlegen",
            fixture.Prompts().Last().GetProperty("request").GetProperty("body").GetString());
        output.WriteLine("E8: auto-created task waits actionably without default CLI; queue selection starts clone/CLI and delivers exactly once.");

        // Mehrinstanz-Schutz (Benutzerkonto): ein Kommentar, auf den das aktive Ereignis-Konto
        // bereits remote reagiert hat — etwa die Empfangsreaktion einer zweiten Installation —
        // erzeugt weder Ereignis noch Aufgabe; ein unberührter Kommentar läuft unverändert.
        var task6 = fixture.TaskIds[6];
        var events6Before = 0;
        await using (var db = fixture.OpenDatabase())
            events6Before = await db.ScmIncomingEvents.CountAsync(x => x.WorkItemId == task6, cancellationToken: TestContext.Current.CancellationToken);
        var eyesBefore = fixture.Responses().Count(x => x.Content == "eyes");
        var promptsBefore = fixture.Prompts().Count;
        fixture.AddText(6, 602, "$Softwareschmiede Von anderer Instanz quittiert", viewerReactions: ["EYES"]);
        fixture.AddText(22, 2201, "$Softwareschmiede Reagierte unzugeordnete Referenz", viewerReactions: ["EYES"]);
        fixture.AddText(6, 603, "$Softwareschmiede Unberührter Folgekommentar");
        await fixture.WaitEventAsync(6, ScmIncomingEventStatus.Verarbeitet, 603);
        await fixture.WaitForDatabaseAsync(async db =>
            await db.ScmContentSnapshots.AnyAsync(x => x.ContentKey.EndsWith("SCM_IssueComment_602")
                    && x.Diagnostic == ScmEventValidationService.AlreadyReactedRejection)
                && await db.ScmContentSnapshots.AnyAsync(x => x.ContentKey.EndsWith("SCM_IssueComment_2201")
                    && x.Diagnostic == ScmEventStore.UnassignedDiagnostic),
            "Snapshot-Diagnosen für Kommentare 602/2201 fehlen", TimeSpan.FromSeconds(90));
        await using (var db = fixture.OpenDatabase())
        {
            Assert.True(await db.ScmContentSnapshots.AnyAsync(x => x.ContentKey.EndsWith("SCM_IssueComment_602")
                && x.Diagnostic == ScmEventValidationService.AlreadyReactedRejection, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await db.ScmContentSnapshots.AnyAsync(x => x.ContentKey.EndsWith("SCM_IssueComment_2201")
                && x.Diagnostic == ScmEventStore.UnassignedDiagnostic, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(events6Before + 1, await db.ScmIncomingEvents.CountAsync(x => x.WorkItemId == task6, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(await db.WorkItems.Include(x => x.IssueReference)
                .AnyAsync(x => x.IssueReference != null && x.IssueReference.IssueNumber == 22, cancellationToken: TestContext.Current.CancellationToken));
        }
        Assert.Equal(eyesBefore + 1, fixture.Responses().Count(x => x.Content == "eyes"));
        Assert.Equal(promptsBefore + 1, fixture.Prompts().Count);
        output.WriteLine("E9-Mehrinstanz: reagierte Kommentare erzeugen weder Ereignis noch Aufgabe (Snapshot-Diagnose), unberührter Kommentar läuft durch.");

        // Bot-Phase ans Ende gelegt: der prozessgebundene ScmE2ECredentialStore verliert
        // eingetragene Secrets bei einem Neustart — danach findet keiner mehr statt.
        await BotModeAsync(window, fixture);
    }

    /// <summary>Kontomodus „Separates Konto“: UI-Validierung ohne Token, Token per Tastatur,
    /// Inbox-Polling am Agent-Client (scm-bot), Mention→Aufgabe→Dispatch→Antwort, Fremd-
    /// Verwerfungsdiagnose ohne Monitoring-Alarm und Rückschaltung auf Benutzerkonto.</summary>
    private async Task BotModeAsync(Window window, ScmE2EFixture fixture)
    {
        // Fehlerfall zuerst: Separates Konto ohne AgentToken wird mit sichtbarer
        // Fehlermeldung abgelehnt; DiscardChanges setzt Banner und Modus zurück.
        var settings = new SettingsView(window).ForceShow();
        settings.ExecuteOpenWorkItems();
        settings.SetAccountMode("Separates Konto");
        var rejected = Assert.Throws<InvalidOperationException>(() => settings.SaveSettings());
        Assert.Contains("Personal Access Token", rejected.Message);
        settings.DiscardChanges();
        settings.WaitForAccountMode("Benutzerkonto", Medium);

        settings.SwitchTab("Plugins");
        WaitForElement(window, cf => cf.ByName("Softwareschmiede.GitHub.Entry"), Long).ClickInForeground();
        settings.SetPluginSecretField("AgentToken", "fixture-agent-token");
        settings.ExecuteOpenWorkItems();
        settings.SetAccountMode("Separates Konto");
        settings.SaveSettings();
        output.WriteLine("Bot: Separates Konto ohne Token abgelehnt; AgentToken per Tastatur eingetragen und gemeinsam gespeichert.");

        // Der nächste Poll liest die Inbox des separaten Kontos am Agent-Client (scm-bot).
        await fixture.WaitForCallsAsync("notifications");
        NavigateQueue(window);

        // Mention-Benachrichtigung auf Issue 18: Referenz → Aufgabe → Ereignis → Dispatch → Antwort.
        var eyesBefore = fixture.Responses().Count(x => x.Content == "eyes");
        fixture.AddText(18, 1801, "@scm-bot Aufgabe aus Benachrichtigung anlegen");
        fixture.AddNotification(18);
        WorkItem? botTask = null;
        await fixture.WaitForDatabaseAsync(async db =>
        {
            botTask = await db.WorkItems.Include(x => x.IssueReference)
                .SingleOrDefaultAsync(x => x.IssueReference != null && x.IssueReference.IssueNumber == 18);
            return botTask is not null;
        }, "Aufgabe für Issue 18 nicht angelegt");
        Assert.NotNull(botTask);
        var waiting = await fixture.WaitEventForTaskAsync(botTask!.Id, ScmIncomingEventStatus.Wartend);
        Assert.Equal(ScmAccountMode.SeparateAccount, ScmEventStore.Observation(waiting).Reference.Repository.Account);
        SelectEventByTitle(window, "SCM Referenz 18", "/issues/18");
        StartSelected(window);
        await fixture.WaitEventForTaskAsync(botTask.Id, ScmIncomingEventStatus.Verarbeitet);
        Assert.Equal(eyesBefore + 1, fixture.Responses().Count(x => x.Content == "eyes"));
        Assert.Contains(fixture.Responses(), x => x.Endpoint.EndsWith("/issues/18/comments", StringComparison.Ordinal)
            && x.Body.Contains("KI-Simulator:", StringComparison.Ordinal));
        output.WriteLine("Bot: mention-Benachrichtigung legte Aufgabe an, Dispatch und Antwort liefen unter scm-bot.");

        // Mehrinstanz-Schutz im SeparatesKonto: ein Mention-Kommentar, auf den das Bot-Konto
        // bereits reagiert hat (viewerHasReacted bindet an den Agent-Client), erzeugt kein
        // zweites Ereignis auf derselben Aufgabe.
        var botEventsBefore = 0;
        await using (var db = fixture.OpenDatabase())
            botEventsBefore = await db.ScmIncomingEvents.CountAsync(x => x.WorkItemId == botTask.Id);
        fixture.AddText(18, 1802, "@scm-bot Bereits vom Bot quittiert", viewerReactions: ["EYES"]);
        fixture.AddNotification(18);
        await fixture.WaitForDatabaseAsync(async db =>
            await db.ScmContentSnapshots.AnyAsync(x => x.ContentKey.EndsWith("SCM_IssueComment_1802")
                && x.Diagnostic == ScmEventValidationService.AlreadyReactedRejection),
            "Snapshot-Diagnose für Kommentar 1802 fehlt");
        await using (var db = fixture.OpenDatabase())
        {
            Assert.True(await db.ScmContentSnapshots.AnyAsync(x => x.ContentKey.EndsWith("SCM_IssueComment_1802")
                && x.Diagnostic == ScmEventValidationService.AlreadyReactedRejection));
            Assert.Equal(botEventsBefore, await db.ScmIncomingEvents.CountAsync(x => x.WorkItemId == botTask.Id));
        }
        output.WriteLine("Bot: reagierter Mention-Kommentar erzeugt kein Inbox-Ereignis, Snapshot-Diagnose gesetzt.");

        // Fremd- und Nicht-mention-Benachrichtigungen: verworfen bzw. ignoriert, Diagnose
        // sichtbar in den Integrationshinweisen — ohne MonitoringNotice-Alarm.
        fixture.AddNotification(19, repositoryNodeId: "FREMD_REPO");
        fixture.AddNotification(21, reason: "assign");
        const string inboxKey = "notifications/softwareschmiede.github/github.com";
        string? diagnostic = null;
        await fixture.WaitForDatabaseAsync(async db =>
        {
            diagnostic = (await db.ScmObservationCheckpoints.FindAsync(inboxKey))?.Diagnostic;
            return diagnostic?.Contains("1 verworfen", StringComparison.Ordinal) == true;
        }, "Inbox-Verwerfungsdiagnose fehlt", TimeSpan.FromSeconds(30));
        Assert.NotNull(diagnostic);
        Assert.StartsWith(ScmEventStore.InboxDiscardDiagnosticPrefix, diagnostic);
        Assert.Contains("1 verworfen", diagnostic);
        await using (var db = fixture.OpenDatabase())
        {
            Assert.False(await db.WorkItems.Include(x => x.IssueReference)
                .AnyAsync(x => x.IssueReference != null && (x.IssueReference.IssueNumber == 19 || x.IssueReference.IssueNumber == 21)));
        }
        WaitForElement(window, cf => cf.ByName("Integrationshinweise"), Long)
            .Patterns.ExpandCollapse.Pattern.Expand();
        WaitContains(window, "ScmEventDiagnostics", "1 verworfen");
        var notice = window.FindFirstDescendant(cf => cf.ByAutomationId("ScmEventMonitoringNotice"));
        Assert.True(notice is null || notice.IsOffscreen || !notice.Name.Contains("verworfen", StringComparison.Ordinal),
            "Die Verwerfungsdiagnose darf keinen Monitoring-Alarm auslösen: " + notice?.Name);
        output.WriteLine("Bot: Fremd-Benachrichtigung verworfen und nur als Integrationshinweis sichtbar, kein Alarm.");

        // Rückschaltung: der nächste Poll enumeriert wieder repository-weit, der Keyword-
        // Trigger läuft wie bisher.
        await fixture.SetAccountMode(ScmAccountMode.UserAccount);
        fixture.AddText(1, 106, "$Softwareschmiede Zurück im Benutzerkonto");
        await fixture.WaitEventAsync(1, ScmIncomingEventStatus.Verarbeitet, 106);
        output.WriteLine("Bot: Rückschaltung auf Benutzerkonto — Enumeration und Keyword-Pfad laufen wieder.");
    }

    private async Task MissingVariantsAsync(Window window, ScmE2EFixture fixture)
    {
        var count = fixture.Prompts().Count;
        fixture.AddText(13, 1301, "$Softwareschmiede Arbeitsbereich erst erstellen");
        await fixture.WaitEventAsync(13, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 13);
        await using (var db = fixture.OpenDatabase()) Assert.Null((await db.WorkItems.FindAsync(fixture.TaskIds[13]))!.LocalClonePath);
        StartSelected(window);
        await fixture.WaitEventAsync(13, ScmIncomingEventStatus.Verarbeitet);
        await using (var db = fixture.OpenDatabase())
        {
            var task = (await db.WorkItems.FindAsync(fixture.TaskIds[13]))!;
            Assert.True(System.IO.Directory.Exists(System.IO.Path.Combine(task.LocalClonePath!, ".git")));
            Assert.StartsWith(fixture.Configuration.TestRoot, task.LocalClonePath);
            Assert.NotNull(task.ActiveRunId);
        }
        Assert.Equal(count + 1, fixture.Prompts().Count);
        output.WriteLine("E2-Clone: unprepared task creates real clone/branch under isolated workdir, starts CLI and dispatches once.");

        fixture.Scenario.PluginAvailable = false;
        fixture.SaveScenario();
        fixture.AddText(14, 1401, "$Softwareschmiede CLI aktivieren");
        var missing = await fixture.WaitEventAsync(14, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 14);
        Click(window, "ScmEventOpen");
        WaitContains(window, "ScmEventMessage", "Keine aktivierte CLI");
        Click(window, "ScmEventSettings");
        Assert.True(new SettingsView(window).IsVisible);
        fixture.Scenario.PluginAvailable = true;
        fixture.SaveScenario();
        NavigateQueue(window);
        SelectEvent(window, 14);
        Assert.Equal(missing.Id, (await fixture.WaitEventAsync(14, ScmIncomingEventStatus.Wartend)).Id);
        Click(window, "ScmEventOpen");
        _ = WaitForElement(window, cf => cf.ByName("PluginSelection"), Long);
        var concurrent = fixture.BeginRuntimeCommand(14);
        await ScmE2EFixture.WaitFileAsync(concurrent + ".received");
        ChoosePlugin(window);
        var processed = await fixture.WaitEventAsync(14, ScmIncomingEventStatus.Verarbeitet);
        Assert.Equal(processed.CliRunId, (await ScmE2EFixture.RuntimeResultAsync(concurrent)).ToString("D"));
        Assert.Equal(count + 2, fixture.Prompts().Count);
        output.WriteLine("E3-Dialog: missing CLI/settings retains inbox; concurrent real start during modal selection reuses single run and prompt.");

        fixture.Control("unknown-ready", true);
        fixture.AddText(15, 1501, "$Softwareschmiede Neue Sitzung ausdrücklich bestätigen");
        await fixture.WaitEventAsync(15, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 15);
        // Auf einen echten Doppelklick folgt ein Monitor-Zyklus, während der Dialog offen ist.
        EventRow(window, 15).DoubleClick();
        ChoosePlugin(window);
        await WaitReasonAsync(fixture, 15, "Eingabebereitschaft unbekannt");
        SelectEvent(window, 15);
        var runA = await fixture.WaitEventAsync(15, ScmIncomingEventStatus.Wartend);
        var replace = fixture.BeginRuntimeCommand(15, replace: true);
        await ScmE2EFixture.WaitFileAsync(replace + ".locked");
        Click(window, "ScmEventConfirmReady");
        await System.IO.File.WriteAllTextAsync(replace + ".release", "release");
        var runB = await ScmE2EFixture.RuntimeResultAsync(replace);
        Assert.NotEqual(runA.CliRunId, runB.ToString("D"));
        WaitContains(window, "ScmEventMessage", "CLI-Sitzung hat sich geändert");
        Assert.Equal(count + 2, fixture.Prompts().Count);
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 15, "Eingabebereitschaft unbekannt");
        SelectEvent(window, 15);
        WaitContains(window, "ScmEventRun", runB.ToString("D"));
        fixture.Control("unknown-ready", false);
        Click(window, "ScmEventConfirmReady");
        Assert.Equal(runB.ToString("D"), (await fixture.WaitEventAsync(15, ScmIncomingEventStatus.Verarbeitet)).CliRunId);
        await fixture.WaitForPollAsync();
        Assert.Equal(count + 3, fixture.Prompts().Count);
        output.WriteLine("E3-Bereitschaft: double click/poll dispatch once; confirmation captured for A rejects replacement B; fresh B confirmation required.");

        fixture.Control("unknown-ready", true);
        fixture.AddText(16, 1601, "$Softwareschmiede Bereitschaft erlaubt keinen fremden Kanal");
        await fixture.WaitEventAsync(16, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 16);
        StartSelected(window);
        await WaitReasonAsync(fixture, 16, "Eingabebereitschaft unbekannt");
        SelectEvent(window, 16);
        var channel = System.IO.Path.Combine(fixture.Configuration.TestRoot, "workspace-16", ScmEventExchangeProtocol.DirectoryName);
        System.IO.Directory.CreateDirectory(channel);
        var foreign = System.IO.Path.Combine(channel, "user.txt");
        await System.IO.File.WriteAllTextAsync(foreign, "retained");
        Click(window, "ScmEventConfirmReady");
        await WaitReasonAsync(fixture, 16, "fremde Dateien");
        Assert.Equal(count + 3, fixture.Prompts().Count);
        Assert.Equal("retained", await System.IO.File.ReadAllTextAsync(foreign));
        System.IO.File.Delete(foreign);
        System.IO.Directory.Delete(channel);
        fixture.Control("unknown-ready", false);
        SelectEvent(window, 16);
        Click(window, "ScmEventRetry");
        await fixture.WaitEventAsync(16, ScmIncomingEventStatus.Verarbeitet);
        Assert.Equal(count + 4, fixture.Prompts().Count);
        output.WriteLine("E3-Bereitschaft/Kanal: readiness confirmation cannot unblock occupied channel; repair sends exactly once.");

        fixture.Control("read-only", true);
        fixture.AddText(17, 1701, "$Softwareschmiede Schreibgeschützte CLI regulär neu starten");
        await fixture.WaitEventAsync(17, ScmIncomingEventStatus.Wartend);
        SelectEvent(window, 17);
        StartSelected(window);
        await WaitReasonAsync(fixture, 17, "schreibgeschützt");
        var readOnly = await fixture.WaitEventAsync(17, ScmIncomingEventStatus.Wartend);
        Assert.NotNull(readOnly.CliRunId);
        Assert.Equal(count + 4, fixture.Prompts().Count);
        // Hält den Dispatch an der Remote-Grenze blockiert, während der manuelle Stopp/Neustart bewiesen wird.
        // Sonst kann normales Polling die gestoppte Aufgabe neu starten, bevor der UI-Klick sie erreicht.
        fixture.Scenario.CanWrite = false;
        fixture.SaveScenario();
        SelectEvent(window, 17);
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 17, "Schreibberechtigung");
        SelectEvent(window, 17);
        Click(window, "ScmEventTask");
        new TaskDetailView(window).StopCli();
        fixture.Control("read-only", false);
        WaitForElement(window, cf => cf.ByName("RestartCli"), Long).AsButton().Invoke();
        new TaskDetailView(window).WaitForCliRunning();
        NavigateQueue(window);
        SelectEvent(window, 17);
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 17, "Schreibberechtigung");
        fixture.Scenario.CanWrite = true;
        fixture.SaveScenario();
        var writable = await fixture.WaitEventAsync(17, ScmIncomingEventStatus.Verarbeitet);
        Assert.NotEqual(readOnly.CliRunId, writable.CliRunId);
        Assert.Equal(count + 5, fixture.Prompts().Count);
        Assert.DoesNotContain(fixture.Prompts(), p => p.GetProperty("request").GetProperty("cliRunId").GetString() == readOnly.CliRunId);
        output.WriteLine("E6-Read-only: regular task stop/restart replaces denied run; only new supported run receives one event.");

        // Ein Simulator, der ohne sein Protokoll-Banner gestartet wurde, bleibt ehrlich unerkannt: der
        // Wartegrund benennt das fehlende Banner statt einer unbekannten Bereitschaft, kein Bestätigen-Button wird
        // angeboten, und ein regulärer Neustart mit Banner liefert dasselbe Ereignis.
        fixture.Control("unknown-banner", true);
        SelectEvent(window, 2);
        StartSelected(window);
        await WaitReasonAsync(fixture, 2, "kein unterstütztes Versionsbanner erkannt");
        SelectEvent(window, 2);
        WaitContains(window, "ScmEventReason", "Versionsbanner");
        var confirmButton = window.FindFirstDescendant(cf => cf.ByAutomationId("ScmEventConfirmReady"));
        Assert.True(confirmButton is null || confirmButton.IsOffscreen,
            "Der Bestätigen-Button darf bei nicht erkanntem Promptformat nicht angeboten werden.");
        var unrecognized = await fixture.WaitEventAsync(2, ScmIncomingEventStatus.Wartend);
        Assert.NotNull(unrecognized.CliRunId);
        Assert.Equal(count + 5, fixture.Prompts().Count);
        // Hält den Dispatch an der Remote-Grenze blockiert, während der manuelle Stopp/Neustart bewiesen wird.
        fixture.Scenario.CanWrite = false;
        fixture.SaveScenario();
        SelectEvent(window, 2);
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 2, "Schreibberechtigung");
        SelectEvent(window, 2);
        Click(window, "ScmEventTask");
        new TaskDetailView(window).StopCli();
        fixture.Control("unknown-banner", false);
        WaitForElement(window, cf => cf.ByName("RestartCli"), Long).AsButton().Invoke();
        new TaskDetailView(window).WaitForCliRunning();
        NavigateQueue(window);
        SelectEvent(window, 2);
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 2, "Schreibberechtigung");
        fixture.Scenario.CanWrite = true;
        fixture.SaveScenario();
        var recognized = await fixture.WaitEventAsync(2, ScmIncomingEventStatus.Verarbeitet);
        Assert.NotEqual(unrecognized.CliRunId, recognized.CliRunId);
        Assert.Equal(count + 6, fixture.Prompts().Count);
        output.WriteLine("E3-Erkennung: fehlendes Simulator-Banner meldet ehrlich ohne Bestätigen-Button; regulärer Neustart erkennt und liefert einmal.");
    }

    /// <summary>Auflösungsbudget über den echten App-Pfad (Issue #442): Der Auflösungscache begrenzt
    /// die GraphQL-Auflösung auf höchstens eine Erneuerung je TTL-Fenster (Fixture-TTL 3 s,
    /// Poll 300 ms), kein REST-Auflösungspaar (<c>user</c>/<c>repos/…</c>) erscheint mehr im
    /// Call-Protokoll, und ein erschöpftes REST-Kontingent sperrt Folgeaufrufe fail-fast —
    /// während das eigene GraphQL-Budget unabhängig weiterläuft.</summary>
    [Fact]
    public async Task ScmEvents_RepositoryResolution_StaysWithinBudgetAndGates_OnRateLimit()
    {
        SkipWhenWpfE2ENotAvailable();
        var fixture = new ScmE2EFixture();
        await fixture.InitializeAsync();
        output.WriteLine("SCM fixture and evidence: " + fixture.Configuration.TestRoot);
        // AlleAufgaben-Modus: die Abschlussüberwachung erzeugt pro Zyklus reguläre REST-Lesecalls
        // (Issue-Status je Aufgabe) als Messgröße für den Rate-Limit-Gate.
        await fixture.SetTaskCompletionCheckMode(ScmTaskCompletionCheckMode.AllWorkItems);
        WaitOnRealMainwindow(LaunchApp(false, fixture.EnvironmentVariables()));

        // Mehrere Poll-Zyklen über ein Zeitfenster jenseits der Fixture-TTL (3 s) abwarten.
        await fixture.WaitForCallsAsync("\"Endpoint\":\"graphql\"", 4);
        await Task.Delay(4000, TestContext.Current.CancellationToken);
        var calls = fixture.Calls();
        var resolutions = calls.Count(IsResolutionCall);
        // Auflösung höchstens je TTL-Fenster: Start + wenige Erneuerungen — ein Bruchteil des
        // Gesamtverkehrs (früher: zwei REST-Calls je Zyklus und Repository).
        Assert.True(resolutions <= 4, $"Auflösungen über Budget: {resolutions} von {calls.Count} Calls.");
        Assert.True(resolutions * 15 < calls.Count,
            $"Auflösungsanteil zu hoch: {resolutions} Auflösungen bei {calls.Count} Calls.");
        Assert.DoesNotContain(calls, x => Endpoint(x) == "user");
        Assert.DoesNotContain(calls, x => Endpoint(x) == "repos/scm-owner/scm-e2e" && Method(x) == "GET");
        output.WriteLine($"Phase A: {resolutions} Auflösungen bei {calls.Count} protokollierten Calls.");

        // REST-Primärkontingent erschöpft: die erste Antwort liefert 403 + X-RateLimit-Reset und
        // armiert den Gate; Folgeaufrufe scheitern vor dem Transport und erscheinen nicht mehr im
        // Protokoll, während die GraphQL-Enumeration und -Auflösung eigenständig weiterlaufen.
        fixture.Scenario.RateLimitRest = true;
        fixture.SaveScenario();
        await fixture.WaitForCallsAsync("repos/scm-owner/scm-e2e/issues/", 1);
        // Der Gate gilt ab der ersten 403 — erst nach weiteren GraphQL-Calls einfrieren messen.
        await fixture.WaitForCallsAsync("\"Endpoint\":\"graphql\"", 4);
        var frozen = fixture.Calls().Count(IsRestCall);
        await fixture.WaitForCallsAsync("\"Endpoint\":\"graphql\"", 4);
        var after = fixture.Calls();
        Assert.Equal(frozen, after.Count(IsRestCall));
        Assert.True(after.Count(x => Endpoint(x) == "graphql") > calls.Count(x => Endpoint(x) == "graphql"),
            "GraphQL muss trotz REST-Sperre eigenständig weiterlaufen.");
        output.WriteLine($"Phase B: REST eingefroren bei {frozen} Calls, GraphQL läuft weiter.");
    }

    /// <summary>Issue #453: ein ~520-stelliges <c>ghs_</c>-Installationstoken in den
    /// GitHub-Einstellungen löst den Hinweisdialog aus — SCM-E2E-Modus, weil nur dort das
    /// echte GitHub-Plugin samt prozessgebundenem <c>ScmE2ECredentialStore</c> geladen wird.
    /// Ein App-Prozess deckt die drei Teilflüsse ab: 1) Warnhinweis erscheint (BoundingBox
    /// im Bildschirm, Screenshot im TestRoot) und „Ja" persistiert; die Panel-Neuwahl lädt
    /// das Token erneut, der erneute Warnhinweis belegt den Roundtrip PasswordBox →
    /// PluginSettingsService → Credential Store → PasswordBox. 2) Ein PAT speichert ohne
    /// Hinweis. 3) Ein ghs_-Wert mit „Nein" wird vollständig verworfen — die Neuwahl lädt
    /// den zuvor persistierten PAT, der ohne Hinweis speichert.</summary>
    [Fact]
    public async Task ScmEvents_GitHubGhsTokenWarning_UiRoundtrip_E2E()
    {
        SkipWhenWpfE2ENotAvailable();

        // Schlankes SCM-Setup ohne die 17-Workspace-Initialisierung: Für Einstellungen,
        // GitHub-Plugin und prozessgebundenen Credential Store reichen TestRoot,
        // Konfiguration, leeres Szenario und eine migrierte Testdatenbank.
        var testRoot = Path.Combine(Path.GetTempPath(), "softwareschmiede_scm_e2e_" + Guid.NewGuid().ToString("N"));
        var configurationPath = Path.Combine(testRoot, "scm-config.json");
        var databasePath = Path.Combine(testRoot, "test.db");
        var configuration = new ScmE2ETestConfiguration { TestRoot = testRoot };
        Directory.CreateDirectory(testRoot);
        Directory.CreateDirectory(configuration.SimulatorDirectory);
        File.WriteAllText(configurationPath, JsonSerializer.Serialize(configuration));
        File.WriteAllText(configuration.ScenarioPath, JsonSerializer.Serialize(new ScmE2EScenario()));
        await using (var db = new SoftwareschmiedeDbContext(
            new DbContextOptionsBuilder<SoftwareschmiedeDbContext>().UseSqlite($"Data Source={databasePath}").Options))
        {
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var settingService = new AppSettingService(db, NullLogger<AppSettingService>.Instance);
            await settingService.SetUpdateSettingsAsync(
                new UpdateSettings(UpdateMode.Off, UpdatePathChannelResolver.StableVersionsPath), TestContext.Current.CancellationToken);
            await settingService.SetSettingAsync(AppSettingService.DefaultAiPluginKey, null, TestContext.Current.CancellationToken);
            await settingService.SetSettingAsync(WorkingDirectorySettingsService.RepositoriesWorkdirKey, testRoot, TestContext.Current.CancellationToken);
            // Die Ereignisüberwachung ist für diesen Test ohne Belang — deaktiviert hält
            // sie den Simulator und den UI-Pfad frei von Hintergrund-Polling.
            await settingService.SetBoolSettingAsync(AppSettingService.ScmMonitoringEnabledKey, false, TestContext.Current.CancellationToken);
        }
        var window = WaitOnRealMainwindow(LaunchApp(false, new Dictionary<string, string?>
        {
            [ScmE2ETestConfiguration.EnvironmentVariable] = configurationPath,
            ["SOFTWARESCHMIEDE_TEST_DB_PATH"] = databasePath
        }));
        output.WriteLine("ghs_-Warnhinweis SCM-TestRoot: " + testRoot);

        var settings = new SettingsView(window).ForceShow();
        settings.SwitchTab("Plugins");
        WaitForElement(window, cf => cf.ByName("Softwareschmiede.GitHub.Entry"), Long).ClickInForeground();
        var warning = new GhsTokenWarningDialogView(window);

        // Teilfluss 1: ghs_-Token eintragen → Warnhinweis → Ja → persistiert.
        settings.SetPluginSecretField("Token", TestTokens.GhsInstallationToken);
        settings.ClickSave();
        var dialog = warning.WaitForDialog();
        var screen = Automation.GetDesktop().BoundingRectangle;
        Assert.True(screen.Contains(dialog.BoundingRectangle),
            $"Warndialog {dialog.BoundingRectangle} liegt nicht vollständig im Bildschirm {screen}.");
        var screenshotPath = Path.Combine(testRoot, "ghs-installationstoken-warning.png");
        dialog.CaptureToFile(screenshotPath);
        Assert.True(File.Exists(screenshotPath));
        output.WriteLine("Warnhinweis-Screenshot: " + screenshotPath);
        warning.Confirm();
        settings.WaitForSettingsSaved();

        // Roundtrip-Beweis über die UI: SCM- und KI-Plugin liegen in getrennten Listen —
        // ein erneuter Klick auf den unverändert selektierten GitHub-Eintrag löst kein
        // SelectionChanged aus. „Verwerfen" (LoadAsync) baut die Listen neu auf, setzt die
        // Selektion zurück und lädt beim nächsten Klick das Panel mit dem persistierten
        // ghs_-Token — ohne Roundtrip käme beim Speichern kein Hinweis mehr.
        settings.DiscardChanges();
        WaitForElement(window, cf => cf.ByName("Softwareschmiede.GitHub.Entry"), Long).ClickInForeground();
        settings.ClickSave();
        warning.WaitForDialog();
        warning.Confirm();
        settings.WaitForSettingsSaved();
        output.WriteLine("ghs_ bestätigt+gespeichert; Panel-Neuwahl warnte erneut (Roundtrip).");

        // Teilfluss 2: PAT speichert ohne Installationstoken-Hinweis.
        settings.SetPluginSecretField("Token", "ghp_" + new string('a', 36));
        settings.ClickSave();
        settings.WaitForSettingsSaved();
        Assert.False(warning.IsVisible, "Bei einem PAT darf kein Installationstoken-Hinweis erscheinen.");
        output.WriteLine("PAT: kein Warnhinweis, direkte Speicherung.");

        // Teilfluss 3: ghs_-Wert eintragen → Warnhinweis → Nein → nichts persistiert.
        settings.SetPluginSecretField("Token", TestTokens.GhsInstallationToken);
        settings.ClickSave();
        warning.WaitForDialog();
        warning.Cancel();
        // SaveAsync bricht vor jeder Persistenz ab — das Erfolgsbanner darf nie erscheinen.
        var bannerDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < bannerDeadline && !settings.IsSettingsSavedVisible())
            Thread.Sleep(100);
        Assert.False(settings.IsSettingsSavedVisible(), "Nach Abbruch des Hinweises darf kein Erfolgsbanner erscheinen.");

        // Die Panel-Neuwahl lädt den zuletzt persistierten PAT — ein Speichern ohne
        // Warnhinweis belegt, dass der abgebrochene ghs_-Wert nicht persistiert wurde.
        settings.DiscardChanges();
        WaitForElement(window, cf => cf.ByName("Softwareschmiede.GitHub.Entry"), Long).ClickInForeground();
        settings.ClickSave();
        settings.WaitForSettingsSaved();
        Assert.False(warning.IsVisible, "Nach Abbruch wurde der ghs_-Wert nicht persistiert — das Panel lädt den PAT.");
        output.WriteLine("ghs_-Warnhinweis: Ja persistiert (Roundtrip), PAT ohne Hinweis, Nein verwirft.");
    }

    private static bool IsResolutionCall(JsonElement call)
        => Endpoint(call) == "graphql"
            && call.TryGetProperty("JsonBody", out var body) && body.ValueKind == JsonValueKind.String
            && body.GetString()!.Contains("viewerPermission", StringComparison.Ordinal);

    private static bool IsRestCall(JsonElement call)
        => Endpoint(call) is { } endpoint && endpoint != "graphql";

    private static string? Endpoint(JsonElement call)
        => call.TryGetProperty("Endpoint", out var endpoint) && endpoint.ValueKind == JsonValueKind.String
            ? endpoint.GetString()
            : null;

    private static string? Method(JsonElement call)
        => call.TryGetProperty("Method", out var method) && method.ValueKind == JsonValueKind.String
            ? method.GetString()
            : null;

    private static async Task WaitPromptCountAsync(ScmE2EFixture fixture, int minimum, Guid? attempt)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var prompts = fixture.Prompts();
            if (prompts.Count >= minimum && prompts.Any(x => x.GetProperty("request").GetProperty("attemptId").GetString() == attempt?.ToString("D"))) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Simulator hat Attempt nicht gelesen: " + attempt);
    }

    private static void WaitContains(Window window, string id, string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (Element(window, id).Name.Contains(text, StringComparison.Ordinal)) return;
            Thread.Sleep(50);
        }
        Assert.Contains(text, Element(window, id).Name);
    }

    private static DataGridViewRow EventRow(Window window, int number)
        => Element(window, "ScmEventList").AsDataGridView().Rows.Single(x => x.Cells[0].Value == "SCM Aufgabe " + number);

    private static AutomationElement Element(Window window, string id) => WaitForElement(window, cf => cf.ByAutomationId(id), Long);
    private static void Click(Window window, string id)
    {
        var deadline = DateTime.UtcNow + Long;
        while (DateTime.UtcNow < deadline)
        {
            var button = Element(window, id).AsButton();
            if (button.IsEnabled) { button.Invoke(); return; }
            Thread.Sleep(100);
        }
        throw new TimeoutException("SCM-Aktion bleibt gesperrt: " + id);
    }
    private static void NavigateQueue(Window window)
    {
        Click(window, "ScmEventQueueNavigation");
        _ = Element(window, "ScmEventList");
    }

    private static void SelectEvent(Window window, int number)
        => SelectEventByTitle(window, "SCM Aufgabe " + number, "/" + number);

    private static void SelectEventByTitle(Window window, string title, string originSuffix)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var rows = Element(window, "ScmEventList").AsDataGridView().Rows;
            var row = rows.FirstOrDefault(x => x.Cells.Length > 0 && x.Cells[0].Value == title);
            if (row is not null)
            {
                row.Patterns.SelectionItem.Pattern.Select();
                var origin = window.FindFirstDescendant(cf => cf.ByAutomationId("ScmEventOrigin"))?.AsTextBox().Text;
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && uri.AbsolutePath.EndsWith(originSuffix, StringComparison.Ordinal)) return;
            }
            Thread.Sleep(100);
        }
        throw new TimeoutException("SCM-Ereigniszeile fehlt: " + title);
    }

    private static void StartSelected(Window window)
    {
        Click(window, "ScmEventOpen");
        ChoosePlugin(window);
    }

    private static void ChoosePlugin(Window window, bool confirm = true)
    {
        var dialog = WaitForElement(window, cf => cf.ByName("KI-Plugin auswählen")
            .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)), Long);
        var choices = WaitForElement(dialog, cf => cf.ByName("PluginSelection"), Long).AsComboBox();
        // Der Dialog wird an UIA veröffentlicht, bevor WPF seine Items zwingend realisiert hat.
        var deadline = DateTime.UtcNow + Long;
        while (choices.Items.Length == 0 && DateTime.UtcNow < deadline) Thread.Sleep(100);
        Assert.Single(choices.Items);
        if (confirm) choices.Select(0);
        WaitForElement(dialog, cf => cf.ByName(confirm ? "OK" : "Cancel"), Long).AsButton().Invoke();
    }

    private static void ConfirmResend(Window window, bool confirmed)
    {
        var dialog = WaitForElement(window, cf => cf.ByName("Mögliche Doppelbearbeitung"), Long);
        Assert.Contains("Doppelbearbeitung", dialog.Name);
        WaitForElement(dialog, cf => cf.ByAutomationId(confirmed ? "6" : "7"), Long).AsButton().Invoke();
    }

    private static void Filter(Window window, string value)
    {
        Element(window, "ScmEventFilter").AsComboBox().Select(value);
        Click(window, "ScmEventRefresh");
    }

    private static void WaitText(Window window, string id, string expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (Element(window, id).Name == expected) return;
            Thread.Sleep(100);
        }
        Assert.Equal(expected, Element(window, id).Name);
    }

    private static async Task WaitReasonAsync(ScmE2EFixture fixture, int number, string reason)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var item = await fixture.WaitEventAsync(number, ScmIncomingEventStatus.Wartend);
            if (item.Reason?.Contains(reason, StringComparison.Ordinal) == true) return;
            await Task.Delay(100);
        }
        throw new TimeoutException("SCM-Fehlergrund fehlt: " + reason);
    }
}
