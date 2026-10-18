using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views.Dialogs;
using Softwareschmiede.Tests.E2E.Views;
using Xunit.Abstractions;

namespace Softwareschmiede.Tests.E2E;

/// <summary>Real WPF, GitHub parser, SQLite inbox, orchestration and file-exchange flow with isolated remote transport.</summary>
[Trait("Category", "E2E")]
[Trait("Scenario", "ScmEvents")]
[OsInterface]
[Collection("E2E")]
public sealed class ScmEventsE2ETests(ITestOutputHelper output) : WpfTestBase
{
    /// <summary>Exercises queue navigation, replay, restart, selection/cancel, lifecycle, filters, blocked tasks and issue/PR replies.</summary>
    [SkippableFact]
    public async Task ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline()
    {
        // Früh überspringen, bevor die Fixture ihre Git-Workspaces/DB aufbaut.
        SkipWennWpfE2ENichtVerfuegbar();

        var fixture = new ScmE2EFixture();
        await fixture.InitializeAsync();
        output.WriteLine("SCM fixture and evidence: " + fixture.Configuration.TestRoot);
        var window = WarteAufEchtesHauptfenster(LaunchApp(false, fixture.EnvironmentVariables()));
        NavigateQueue(window);
        const string original = "$sOfTwArEsChMiEdE Prüfe \"Original\"\nzweite Zeile\u001b\u0000";
        fixture.AddText(1, 101, original);
        var first = await fixture.WaitEventAsync(1, "Wartend");
        SelectEvent(window, 1);
        // Windows UI Automation terminates a text value at NUL; storage and request assertions below retain it.
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
            Assert.Single(await db.ScmIncomingEvents.ToListAsync());
            Assert.Equal(AufgabeStatus.Gestartet, (await db.Aufgaben.FindAsync(fixture.TaskIds[1]))!.Status);
        }
        Click(window, "ScmEventBack");
        NavigateQueue(window);
        SelectEvent(window, 1);
        output.WriteLine("E1: real reception, original body/origin, one receipt/waiting comment, replay and back navigation passed.");

        // E5 uses the sole second app start, before any simulator is launched.
        window = RestartAppPreservingDatabase();
        NavigateQueue(window);
        SelectEvent(window, 1);
        Assert.Equal(first.Id, (await fixture.WaitEventAsync(1, "Wartend")).Id);
        await fixture.WaitForPollAsync();
        Assert.Equal(2, fixture.Responses().Count);
        output.WriteLine("E5: regular app close/restart preserves inbox and does not repeat remote receipts.");

        Click(window, "ScmEventOpen");
        ChoosePlugin(window, confirm: false);
        await using (var db = fixture.OpenDatabase()) Assert.Equal(AufgabeStatus.Gestartet, (await db.Aufgaben.FindAsync(fixture.TaskIds[1]))!.Status);
        Assert.Empty(fixture.Prompts());
        await fixture.WaitEventAsync(1, "Wartend");
        SelectEvent(window, 1);
        Click(window, "ScmEventOpen");
        ChoosePlugin(window);
        var delivered = await fixture.WaitEventAsync(1, "Zugestellt");
        SelectEvent(window, 1);
        WaitText(window, "ScmEventStatus", "Zugestellt");
        Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        Assert.Single(fixture.Prompts());
        var request = fixture.Prompts()[0].GetProperty("request");
        Assert.Equal(original, request.GetProperty("body").GetString());
        Assert.Equal("https://github.com/scm-owner/scm-e2e/issues/1#issuecomment-101", request.GetProperty("originUrl").GetString());
        fixture.Hold("accepted", false);
        await fixture.WaitEventAsync(1, "InBearbeitung");
        WaitText(window, "ScmEventStatus", "In Bearbeitung");
        Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        fixture.Hold("complete", false);
        await fixture.WaitEventAsync(1, "Verarbeitet");
        await using (var db = fixture.OpenDatabase())
        {
            var task = (await db.Aufgaben.FindAsync(fixture.TaskIds[1]))!;
            Assert.Equal(AufgabeStatus.Gestartet, task.Status);
            Assert.NotNull(task.AktiveRunId);
        }
        fixture.AddText(1, 102, "$Softwareschmiede Folgeereignis");
        var followUp = await fixture.WaitEventAsync(1, "Verarbeitet", 102);
        Assert.Equal(delivered.CliRunId, followUp.CliRunId);
        Assert.Equal(2, fixture.Prompts().Count);
        output.WriteLine("E2: single-plugin dialog/cancel/reopen, active task, exactly one prompt per event, delivered/accepted/completed and live-run reuse passed. Worktree was pre-seeded.");

        fixture.AddText(2, 201, "$Softwareschmiede Bleibt wartend");
        await fixture.WaitEventAsync(2, "Wartend");
        var promptCount = fixture.Prompts().Count;
        var responseCount = fixture.Responses().Count;
        Filter(window, "Verarbeitet");
        SelectEvent(window, 1);
        Assert.Equal("Verarbeitet", Element(window, "ScmEventStatus").Name);
        Assert.Contains("issues/1", Element(window, "ScmEventOrigin").AsTextBox().Text);
        Assert.Equal(2, Element(window, "ScmEventList").AsDataGridView().Rows.Length);
        Filter(window, "Wartend");
        SelectEvent(window, 2);
        Assert.Single(Element(window, "ScmEventList").AsDataGridView().Rows);
        Assert.Equal(promptCount, fixture.Prompts().Count);
        Assert.Equal(responseCount, fixture.Responses().Count);
        output.WriteLine("E2-Filter: processed/waiting rows and counts correct without dispatch/remote side effects.");

        foreach (var number in new[] { 3, 4, 5 })
        {
            fixture.AddText(number, number * 100 + 1, "$Softwareschmiede Gesperrte Aufgabe");
            await fixture.WaitEventAsync(number, "Wartend");
            SelectEvent(window, number);
            Assert.Contains(number == 5 ? "pausiert" : "beendet/archiviert", Element(window, "ScmEventReason").Name);
            Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        }
        Assert.Equal(promptCount, fixture.Prompts().Count);
        SelectEvent(window, 5);
        Click(window, "ScmEventTask");
        new AufgabePausierenDialogView(window).ForceShow().Aufheben();
        NavigateQueue(window);
        SelectEvent(window, 5);
        StartSelected(window);
        await fixture.WaitEventAsync(5, "Verarbeitet");
        await using (var db = fixture.OpenDatabase())
        {
            Assert.Equal(AufgabeStatus.Beendet, (await db.Aufgaben.FindAsync(fixture.TaskIds[3]))!.Status);
            Assert.Equal(AufgabeStatus.Archiviert, (await db.Aufgaben.FindAsync(fixture.TaskIds[4]))!.Status);
            Assert.Null((await db.Aufgaben.FindAsync(fixture.TaskIds[5]))!.PausiertBisUtc);
        }
        output.WriteLine("E4: completed/archived/paused remain blocked; regular pause removal permits processing.");

        fixture.Control("fail-start", true);
        fixture.AddText(6, 601, "$Softwareschmiede Startfehler und Retry");
        await fixture.WaitEventAsync(6, "Wartend");
        SelectEvent(window, 6);
        StartSelected(window);
        await WaitReasonAsync(fixture, 6, "Startfehler");
        SelectEvent(window, 6);
        Assert.Contains("Startfehler", Element(window, "ScmEventReason").Name);
        fixture.Control("fail-start", false);
        StartSelected(window);
        await fixture.WaitEventAsync(6, "Verarbeitet");
        output.WriteLine("E3: active task without session, visible simulator start failure and successful retry passed.");

        // Same source parser and response transport for issue/PR comments/descriptions.
        foreach (var (number, kind) in new[] { (7, "IssueComment"), (8, "Description"), (9, "Description") })
        {
            var body = "$Softwareschmiede Vollständiger Inhalt " + number + "\nZeile zwei mit \"Zitat\"";
            var receiptCount = fixture.Responses().Count(x => x.Content == "eyes");
            fixture.AddText(number, number * 100 + 1, body, kind);
            await fixture.WaitEventAsync(number, "Wartend");
            SelectEvent(window, number);
            Assert.Equal(body, Element(window, "ScmEventBody").AsTextBox().Text);
            Assert.Contains(number is 7 or 9 ? $"/pull/{number}" : $"/issues/{number}", Element(window, "ScmEventOrigin").AsTextBox().Text);
            StartSelected(window);
            await fixture.WaitEventAsync(number, "Verarbeitet");
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
        var failed = await fixture.WaitEventAsync(1, "VerarbeitungUngeklärt", 103);
        SelectEvent(window, 1);
        Assert.Contains("403", Element(window, "ScmEventReason").Name);
        promptCount = fixture.Prompts().Count;
        fixture.Scenario.RejectAiResponse = false;
        fixture.SaveScenario();
        Click(window, "ScmEventRetry");
        var recovered = await fixture.WaitEventAsync(1, "Verarbeitet", 103);
        Assert.Equal(failed.DeliveryAttemptId, recovered.DeliveryAttemptId);
        Assert.Equal(promptCount, fixture.Prompts().Count);
        output.WriteLine("E6-Wiederaufnahme (403 after acceptance): pending remote reply retried without new event prompt/attempt.");

        // A living, identified CLI can be explicitly confirmed without inventing readiness from I/O silence.
        fixture.Control("unknown-ready", true);
        fixture.AddText(10, 1001, "$Softwareschmiede Bereitschaft ausdrücklich bestätigen");
        await fixture.WaitEventAsync(10, "Wartend");
        SelectEvent(window, 10);
        StartSelected(window);
        await WaitReasonAsync(fixture, 10, "Eingabebereitschaft unbekannt");
        SelectEvent(window, 10);
        var unknown = await fixture.WaitEventAsync(10, "Wartend");
        Assert.NotNull(unknown.CliRunId);
        promptCount = fixture.Prompts().Count;
        fixture.Control("unknown-ready", false);
        Click(window, "ScmEventConfirmReady");
        var ready = await fixture.WaitEventAsync(10, "Verarbeitet");
        Assert.Equal(unknown.CliRunId, ready.CliRunId);
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E3-Bereitschaft: identified live run with unknown readiness requires explicit confirmation and receives one prompt.");

        var occupiedChannel = System.IO.Path.Combine(fixture.Configuration.TestRoot, "workspace-11", ScmEventExchangeProtocol.DirectoryName);
        System.IO.Directory.CreateDirectory(occupiedChannel);
        var foreignFile = System.IO.Path.Combine(occupiedChannel, "user.txt");
        await System.IO.File.WriteAllTextAsync(foreignFile, "user content");
        fixture.AddText(11, 1101, "$Softwareschmiede Antwortkanal wiederherstellen");
        await fixture.WaitEventAsync(11, "Wartend");
        SelectEvent(window, 11);
        promptCount = fixture.Prompts().Count;
        StartSelected(window);
        await WaitReasonAsync(fixture, 11, "fremde Dateien");
        Assert.Equal(promptCount, fixture.Prompts().Count);
        Assert.Equal("user content", await System.IO.File.ReadAllTextAsync(foreignFile));
        System.IO.File.Delete(foreignFile);
        System.IO.Directory.Delete(occupiedChannel);
        SelectEvent(window, 11);
        Click(window, "ScmEventRetry");
        await fixture.WaitEventAsync(11, "Verarbeitet");
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E6-Kanal: unavailable channel blocks event input, retains foreign contents and resumes exactly once after repair.");

        fixture.AddText(12, 1201, "$Softwareschmiede Anmeldung und Berechtigung wiederherstellen");
        await fixture.WaitEventAsync(12, "Wartend");
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
        await fixture.WaitEventAsync(12, "Verarbeitet");
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E6-Wiederaufnahme: persisted waiting request survives 401 and missing repository permission, then processes once.");

        await MissingVariantsAsync(window, fixture);

        fixture.Control("uncertain-write", true);
        fixture.Control("ignore-next-attempt", true);
        fixture.AddText(1, 104, "$Softwareschmiede Ungewisse Zustellung prüfen");
        var uncertain = await fixture.WaitEventAsync(1, "ZustellungUngewiss", 104);
        SelectEvent(window, 1);
        await WaitPromptCountAsync(fixture, fixture.Prompts().Count == 0 ? 1 : fixture.Prompts().Count,
            uncertain.DeliveryAttemptId);
        promptCount = fixture.Prompts().Count;
        Assert.False(Element(window, "ScmEventOpen").IsEnabled);
        Click(window, "ScmEventCheckStatus");
        Assert.Equal(uncertain.DeliveryAttemptId, (await fixture.WaitEventAsync(1, "ZustellungUngewiss", 104)).DeliveryAttemptId);
        Assert.Equal(promptCount, fixture.Prompts().Count);
        Click(window, "ScmEventResend");
        ConfirmResend(window, false);
        Assert.Equal(uncertain.DeliveryAttemptId, (await fixture.WaitEventAsync(1, "ZustellungUngewiss", 104)).DeliveryAttemptId);
        fixture.Hold("accepted", true);
        Click(window, "ScmEventResend");
        ConfirmResend(window, true);
        var redelivered = await fixture.WaitEventAsync(1, "Zugestellt", 104);
        Assert.NotEqual(uncertain.DeliveryAttemptId, redelivered.DeliveryAttemptId);
        fixture.Control("complete-attempt-" + uncertain.DeliveryAttemptId, true);
        await ScmE2EFixture.WaitFileAsync(System.IO.Path.Combine(uncertain.ExchangePath!, "complete.json"));
        await fixture.WaitForPollAsync();
        Assert.Equal(redelivered.DeliveryAttemptId, (await fixture.WaitEventAsync(1, "Zugestellt", 104)).DeliveryAttemptId);
        fixture.Hold("accepted", false);
        var resent = await fixture.WaitEventAsync(1, "Verarbeitet", 104);
        Assert.NotEqual(uncertain.DeliveryAttemptId, resent.DeliveryAttemptId);
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        await fixture.WaitForPollAsync();
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        output.WriteLine("E3-Ungewissheit: status/cancel preserves attempt; confirmed resend once; late old completion cannot finish current attempt.");

        fixture.Control("uncertain-write", true);
        fixture.Control("ignore-next-attempt", true);
        fixture.AddText(1, 105, "$Softwareschmiede Verspäteten aktuellen Versuch prüfen");
        var current = await fixture.WaitEventAsync(1, "ZustellungUngewiss", 105);
        await WaitPromptCountAsync(fixture, promptCount + 2, current.DeliveryAttemptId);
        // Prove the explicit status action while the current attempt is still uncertain.
        // Once completion is released, background reconciliation may finish it and hide the action.
        SelectEvent(window, 1);
        Click(window, "ScmEventCheckStatus");
        Assert.Equal(current.DeliveryAttemptId, (await fixture.WaitEventAsync(1, "ZustellungUngewiss", 105)).DeliveryAttemptId);
        Assert.Equal(promptCount + 2, fixture.Prompts().Count);
        fixture.Control("complete-attempt-" + current.DeliveryAttemptId, true);
        await ScmE2EFixture.WaitFileAsync(System.IO.Path.Combine(current.ExchangePath!, "complete.json"));
        var late = await fixture.WaitEventAsync(1, "Verarbeitet", 105);
        Assert.Equal(current.DeliveryAttemptId, late.DeliveryAttemptId);
        Assert.Equal(promptCount + 2, fixture.Prompts().Count);
        output.WriteLine("E3-Ungewissheit: late valid completion of still-current attempt resolves without any resend.");

        fixture.AddText(20, 2001, "$Softwareschmiede Aufgabe anlegen");
        var deadline = DateTime.UtcNow.AddSeconds(60);
        Aufgabe? created = null;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = fixture.OpenDatabase();
            created = await db.Aufgaben.Include(x => x.IssueReferenz)
                .SingleOrDefaultAsync(x => x.IssueReferenz != null && x.IssueReferenz.IssueNummer == 20);
            if (created is not null) break;
            await Task.Delay(200);
        }
        Assert.NotNull(created);
        Assert.Equal("SCM Referenz 20", created!.Titel);
        Assert.Contains("$Softwareschmiede Aufgabe anlegen", created.AnforderungsBeschreibung);
        await using (var db = fixture.OpenDatabase())
        {
            Assert.Equal(18, await db.Aufgaben.CountAsync());
            var item = Assert.Single(await db.ScmIncomingEvents.Where(x => x.AufgabeId == created.Id).ToListAsync());
            Assert.Equal(20, ScmEventStore.Observation(item).Reference.Number);
        }
        output.WriteLine("E7-Anlage: qualified event on unassigned reference creates exactly one task with issue reference and inbox entry.");

        // The auto-created task has no default CLI, so its event waits with an actionable reason.
        var waiting = await fixture.WaitEventForTaskAsync(created.Id, "Wartend");
        Assert.Contains("Keine Standard-CLI", waiting.Reason);
        promptCount = fixture.Prompts().Count;
        SelectEventByTitle(window, "SCM Referenz 20", "/issues/20");
        Assert.Contains("Keine Standard-CLI", Element(window, "ScmEventReason").Name);
        StartSelected(window);
        await fixture.WaitEventForTaskAsync(created.Id, "Verarbeitet");
        await using (var db = fixture.OpenDatabase())
        {
            var deliveredTask = (await db.Aufgaben.FindAsync(created.Id))!;
            Assert.Equal(AufgabeStatus.Gestartet, deliveredTask.Status);
            Assert.NotNull(deliveredTask.LokalerKlonPfad);
            Assert.StartsWith(fixture.Configuration.TestRoot, deliveredTask.LokalerKlonPfad);
            Assert.True(System.IO.Directory.Exists(System.IO.Path.Combine(deliveredTask.LokalerKlonPfad, ".git")));
            Assert.StartsWith("task/issue-20-", deliveredTask.BranchName);
            Assert.Equal("Softwareschmiede.KiSimulator", deliveredTask.KiPluginPrefix);
        }
        Assert.Equal(promptCount + 1, fixture.Prompts().Count);
        Assert.Equal("$Softwareschmiede Aufgabe anlegen",
            fixture.Prompts().Last().GetProperty("request").GetProperty("body").GetString());
        output.WriteLine("E8: auto-created task waits actionably without default CLI; queue selection starts clone/CLI and delivers exactly once.");
    }

    private async Task MissingVariantsAsync(Window window, ScmE2EFixture fixture)
    {
        var count = fixture.Prompts().Count;
        fixture.AddText(13, 1301, "$Softwareschmiede Arbeitsbereich erst erstellen");
        await fixture.WaitEventAsync(13, "Wartend");
        SelectEvent(window, 13);
        await using (var db = fixture.OpenDatabase()) Assert.Null((await db.Aufgaben.FindAsync(fixture.TaskIds[13]))!.LokalerKlonPfad);
        StartSelected(window);
        await fixture.WaitEventAsync(13, "Verarbeitet");
        await using (var db = fixture.OpenDatabase())
        {
            var task = (await db.Aufgaben.FindAsync(fixture.TaskIds[13]))!;
            Assert.True(System.IO.Directory.Exists(System.IO.Path.Combine(task.LokalerKlonPfad!, ".git")));
            Assert.StartsWith(fixture.Configuration.TestRoot, task.LokalerKlonPfad);
            Assert.NotNull(task.AktiveRunId);
        }
        Assert.Equal(count + 1, fixture.Prompts().Count);
        output.WriteLine("E2-Clone: unprepared task creates real clone/branch under isolated workdir, starts CLI and dispatches once.");

        fixture.Scenario.PluginAvailable = false;
        fixture.SaveScenario();
        fixture.AddText(14, 1401, "$Softwareschmiede CLI aktivieren");
        var missing = await fixture.WaitEventAsync(14, "Wartend");
        SelectEvent(window, 14);
        Click(window, "ScmEventOpen");
        WaitContains(window, "ScmEventMessage", "Keine aktivierte CLI");
        Click(window, "ScmEventSettings");
        Assert.True(new SettingsView(window).IsVisible);
        fixture.Scenario.PluginAvailable = true;
        fixture.SaveScenario();
        NavigateQueue(window);
        SelectEvent(window, 14);
        Assert.Equal(missing.Id, (await fixture.WaitEventAsync(14, "Wartend")).Id);
        Click(window, "ScmEventOpen");
        _ = WaitForElement(window, cf => cf.ByName("PluginAuswahl"), Long);
        var concurrent = fixture.BeginRuntimeCommand(14);
        await ScmE2EFixture.WaitFileAsync(concurrent + ".received");
        ChoosePlugin(window);
        var processed = await fixture.WaitEventAsync(14, "Verarbeitet");
        Assert.Equal(processed.CliRunId, (await ScmE2EFixture.RuntimeResultAsync(concurrent)).ToString("D"));
        Assert.Equal(count + 2, fixture.Prompts().Count);
        output.WriteLine("E3-Dialog: missing CLI/settings retains inbox; concurrent real start during modal selection reuses single run and prompt.");

        fixture.Control("unknown-ready", true);
        fixture.AddText(15, 1501, "$Softwareschmiede Neue Sitzung ausdrücklich bestätigen");
        await fixture.WaitEventAsync(15, "Wartend");
        SelectEvent(window, 15);
        // A real double click is followed by a monitor cycle while the dialog is open.
        EventRow(window, 15).DoubleClick();
        ChoosePlugin(window);
        await WaitReasonAsync(fixture, 15, "Eingabebereitschaft unbekannt");
        SelectEvent(window, 15);
        var runA = await fixture.WaitEventAsync(15, "Wartend");
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
        Assert.Equal(runB.ToString("D"), (await fixture.WaitEventAsync(15, "Verarbeitet")).CliRunId);
        await fixture.WaitForPollAsync();
        Assert.Equal(count + 3, fixture.Prompts().Count);
        output.WriteLine("E3-Bereitschaft: double click/poll dispatch once; confirmation captured for A rejects replacement B; fresh B confirmation required.");

        fixture.Control("unknown-ready", true);
        fixture.AddText(16, 1601, "$Softwareschmiede Bereitschaft erlaubt keinen fremden Kanal");
        await fixture.WaitEventAsync(16, "Wartend");
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
        await fixture.WaitEventAsync(16, "Verarbeitet");
        Assert.Equal(count + 4, fixture.Prompts().Count);
        output.WriteLine("E3-Bereitschaft/Kanal: readiness confirmation cannot unblock occupied channel; repair sends exactly once.");

        fixture.Control("read-only", true);
        fixture.AddText(17, 1701, "$Softwareschmiede Schreibgeschützte CLI regulär neu starten");
        await fixture.WaitEventAsync(17, "Wartend");
        SelectEvent(window, 17);
        StartSelected(window);
        await WaitReasonAsync(fixture, 17, "schreibgeschützt");
        var readOnly = await fixture.WaitEventAsync(17, "Wartend");
        Assert.NotNull(readOnly.CliRunId);
        Assert.Equal(count + 4, fixture.Prompts().Count);
        // Keep dispatch blocked at the remote boundary while proving the manual stop/restart.
        // Otherwise normal polling may restart the stopped task before the UI click reaches it.
        fixture.Scenario.CanWrite = false;
        fixture.SaveScenario();
        SelectEvent(window, 17);
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 17, "Schreibberechtigung");
        SelectEvent(window, 17);
        Click(window, "ScmEventTask");
        new TaskDetailView(window).StopCli();
        fixture.Control("read-only", false);
        WaitForElement(window, cf => cf.ByName("CliNeustarten"), Long).AsButton().Invoke();
        new TaskDetailView(window).WaitForCliRunning();
        NavigateQueue(window);
        SelectEvent(window, 17);
        Click(window, "ScmEventRetry");
        await WaitReasonAsync(fixture, 17, "Schreibberechtigung");
        fixture.Scenario.CanWrite = true;
        fixture.SaveScenario();
        var writable = await fixture.WaitEventAsync(17, "Verarbeitet");
        Assert.NotEqual(readOnly.CliRunId, writable.CliRunId);
        Assert.Equal(count + 5, fixture.Prompts().Count);
        Assert.DoesNotContain(fixture.Prompts(), p => p.GetProperty("request").GetProperty("cliRunId").GetString() == readOnly.CliRunId);
        output.WriteLine("E6-Read-only: regular task stop/restart replaces denied run; only new supported run receives one event.");
    }

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
        var choices = WaitForElement(dialog, cf => cf.ByName("PluginAuswahl"), Long).AsComboBox();
        // The dialog is published to UIA before WPF has necessarily realized its items.
        var deadline = DateTime.UtcNow + Long;
        while (choices.Items.Length == 0 && DateTime.UtcNow < deadline) Thread.Sleep(100);
        Assert.Single(choices.Items);
        if (confirm) choices.Select(0);
        WaitForElement(dialog, cf => cf.ByName(confirm ? "OK" : "Abbrechen"), Long).AsButton().Invoke();
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
            var item = await fixture.WaitEventAsync(number, "Wartend");
            if (item.Reason?.Contains(reason, StringComparison.Ordinal) == true) return;
            await Task.Delay(100);
        }
        throw new TimeoutException("SCM-Fehlergrund fehlt: " + reason);
    }
}
