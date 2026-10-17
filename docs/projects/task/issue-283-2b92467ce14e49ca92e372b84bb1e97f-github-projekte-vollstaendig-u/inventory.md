# Bestandsaufnahme: GitHub-Projekte vollständig überwachen und Ereignisse ohne bestehende Aufgabe ausführen

Issue: #283 · Branch: `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u`
Anforderung: [`requirement.md`](requirement.md) · Stand der Analyse: HEAD `7f4524f` (= Merge von PR #281 „feat: GitHub-Ereignisse überwachen und an CLI zustellen")

## Kurzbefund

**Referenzliste der Überwachung (FR-1/FR-2):** `ScmEventMonitoringService.RunOnceAsync` (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs:25-36`) lädt ausschließlich `db.Aufgaben` mit `GitRepository != null && GitRepository.Aktiv` und sammelt daraus `IssueReferenz.IssueNummer` sowie `PullRequests`-Einträge. Der Klassenkommentar (Zeile 12) sagt es explizit: „Beobachtet ausschließlich lokal verknüpfte SCM-Referenzen". Neue Issues/PRs eines Repos ohne Aufgaben-Referenz werden nie gelesen — es existiert **kein** API-Pfad, der ein ganzes Repository enumeriert (`IScmEventSource.ReadEventsAsync` arbeitet immer auf genau einer `ScmEventReference` mit fester `Number`).

**Aufgabenanlage/Dedup (FR-3/FR-4):** Es gibt UI-Pfade (`AufgabeService.CreateFromIssueAsync`, `CreateFromPullRequestAsync` mit Duplikatprüfung über `PullRequestReferenzen`), aber keinen Dienst, der aus einem `ScmIncomingEvent` eine Aufgabe anlegt oder wiederverwendet. `ScmIncomingEvent.AufgabeId` ist nicht-nullable `Guid` — die Inbox setzt strukturell eine existierende Aufgabe voraus (`ScmEventStore.AcceptBatchAsync` bricht ab, wenn `aufgabeId` null ist, dann nur mit Diagnose „Mehrdeutige Zuordnung zu Aufgaben").

**„Wartend"-Deadlock (FR-7, wahrscheinlichste Ursache — als Vermutung gekennzeichnet):** Der rungebundene Dateitool-Nachweis (`ScmPromptSessionAdapter._accessProbe`, `ScmPromptSessionAdapter.cs:152-170`) wird genau einmal pro CLI-Lauf gesendet und nie wiederholt oder zurückgesetzt. Wird der Probe-Prompt abgeschickt, während die CLI noch im Vertrauensdialog („Do you trust the files in this folder?") steht — z. B. weil der Nutzer die in der UI angebotene Schaltfläche „Eingabebereitschaft bestätigen" (`ConfirmReady`, `ScmPromptSessionAdapter.cs:181-192`) vor der Trust-Bestätigung drückte — geht die Eingabe im Dialog verloren, `ReadAccessProbeAsync` schlägt danach bei jeder Prüfung fehl und der Eintrag bleibt permanent „Wartend" mit Reason „Nachweis des tatsächlichen Dateitool-Zugriffs steht aus …". Weder „Öffnen / CLI auswählen" (`OpenFromQueueAsync`) noch „Erneut prüfen" (`RetryAsync`) senden die Probe erneut. Details: [`inventory/trust-deadlock.md`](inventory/trust-deadlock.md).

**CLI-Startparameter (FR-8):** Die CLI wird über `IKiPlugin.GetTerminalStartSpecAsync` → `CliKiPluginBase.BuildTerminalStartSpec` → `BuildProcessStartInfo` gestartet; Claude erhält nur frei konfigurierbare `CommandLineParameters` (`plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs:144-149`). Der SCM-Antwortkanal verlangt bei Claude zwingend `--permission-mode acceptEdits` (`ScmCliAccessEvaluator.cs:185-196`). Kein Code setzt heute trust-bezogene Parameter oder prüft/schreibt `~/.claude.json` (`hasTrustDialogAccepted`) — Ansatzpunkte siehe [`inventory/trust-deadlock.md`](inventory/trust-deadlock.md).

## Relevante Bereiche (Detail-Unterdateien)

- [SCM-Ereignisüberwachung und GitHub-Quelle](inventory/scm-monitoring.md): Polling-Topologie, `IScmEventSource`/`IScmEventResponder`, GitHub-REST/GraphQL-Lesepfade, Checkpoints/Snapshots, Trigger- und Owner-Validierung, Outbox.
- [Projekte, Repositories und Aufgaben](inventory/tasks-projects.md): Projekt ↔ `GitRepository` (`Aktiv`) ↔ `Aufgabe`-Beziehung, Aufgabenanlage/-dedup, Startpfade, fehlende Stellen für FR-2/FR-3/FR-4.
- [CLI-Zustellung und Statusmodell](inventory/cli-delivery.md): Dispatch-Pipeline des `ScmEventOrchestrator`, Statuswerte, Standard-CLI-Auflösung, `ScmPromptSessionAdapter`/Eingabebereitschaft, Exchange-Protokoll, Queue-UI-Aktionen.
- [Trust-/„Wartend"-Analyse (Bugfix FR-7/FR-8)](inventory/trust-deadlock.md): Zustandsautomat im Detail, belegte Mechanismen vs. gekennzeichnete Vermutungen, Ansatzpunkte für Startparameter/Trust-Vorwegnahme.
- [Tests, Testinfrastruktur und Projektregeln](inventory/tests-conventions.md): Unit-/Integrations-/E2E-Abdeckung, `ScmDispatchFixture`, `ScmE2EFixture`, E2E-Simulatorinfrastruktur, Kategorien, `SOFTWARESCHMIEDE_*`-Variablen, Regeln aus CLAUDE.md.

## Architektur auf einen Blick

```
GitHub (REST/GraphQL via "gh api")
   │  GitHubPlugin : IScmEventSource + IScmEventResponder
   ▼
ScmEventMonitoringService (60 s Polling, nur task-verknüpfte Referenzen)
   │  ScmEventStore.AcceptBatchAsync (Snapshots, Deduplikation über EventKey)
   ▼
ScmIncomingEvent (SQLite-Inbox, Status: Aufgenommen/Wartend/Zugestellt/…)
   │  ScmEventValidationService ($Softwareschmiede-Trigger, Owner=Viewer=Autor)
   ▼
ScmEventOrchestrator (Task-Lease, Outbox-Reaktionen, CLI-Start/-Auswahl)
   │  ScmPromptSessionAdapter (Prompt-Erkennung, Dateitool-Probe)
   ▼
ConPTY-Terminal-Session der CLI (Codex v0.114.0 / Claude Code v2.1.0 / KiSimulator)
   │  Dateikanal .softwareschmiede-scm-exchange/ (accepted/actions/complete)
   ▼
ScmEventExchangeService (2 s) + Orchestrator.ReconcileAttemptAsync → "Verarbeitet"
```

## Wesentliche Lücken zur Anforderung

| FR | Ist-Stand | Lücke |
|----|-----------|-------|
| FR-2 Repo-weite Überwachung | Nur Referenzen an Aufgaben (`ScmEventMonitoringService.cs:25-36`) | Kein Enumerations-Pfad für „alle Issues/PRs eines aktiven Repos"; `IScmEventSource` ist referenzbasiert |
| FR-3 Aufgabenanlage aus Ereignis | `ScmIncomingEvent.AufgabeId` zwingend; nur `AcceptBatchAsync` mit aufgelöster Aufgabe | Kein Anlagedienst; Projekt für neue Aufgabe muss aus `GitRepository.ProjektId` abgeleitet werden |
| FR-4 Wiederverwendung/Dedup | UI-Dedup nur über `IssueReferenz.IssueNummer` (HashSet, `ProjectDetailViewModel.cs:882-884`) bzw. `IsPullRequestLinkedAsync` | Keine servicenahe „finde passende Aufgabe zu Referenz X"-Abfrage für den Eventpfad |
| FR-5 Auto-Start + Zustellung | Orchestrator startet Aufgabe (`SetStatusAsync`→`Gestartet`) und CLI bereits — aber nur für existierende Aufgaben | Anlagedienst muss denselben Sicherheits-/Owner-Prüfpfad nutzen |
| FR-6 fehlende Standard-CLI | Bereits implementiert: `WaitWithCommentAsync` „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." (`ScmEventOrchestrator.cs:143`) | Weitgehend erfüllt; Verhalten bei neuer Aufgabe verifizieren |
| FR-7 Trust-Fortsetzung | „Wartend" + `ConfirmReadyCommand` existiert | Probable Deadlock-Ursache: hängende `_accessProbe`, siehe Detaildatei |
| FR-8 Trust vermeiden | Keine trust-spezifischen Startparameter; `ScmCliAccessEvaluator` akzeptiert nur wenige Parameter | Prüfauftrag: `--permission-mode`, `hasTrustDialogAccepted`, ggf. Codex-Äquivalente |
| FR-9 Bestandsschutz | Trigger/Owner/Token/Dedup/Queue gut abgedeckt und getestet | Bei Erweiterung nicht aushebeln |

## Bekannte Einschränkungen des Bestands

- **Klemmte Versionen:** Der Adapter erkennt nur „OpenAI Codex (v0.114.0)" und „Claude Code v2.1.0" (`ScmPromptSessionAdapter.cs:230-233`); andere Versionen/CLIs (Copilot, Devin) liefern `Unknown` → „Wartend".
- **Dateitool-Nachweis:** Nur Codex `workspace-write` bzw. Claude `acceptEdits` als effektive Freigabe; danach zusätzlich die Live-Dateitool-Probe im rungebundenen Kanal.
- **Nur GitHub:** `IScmEventSource`/`IScmEventResponder` implementiert nur `GitHubPlugin`; Bitbucket/LocalDirectory werden übersprungen (`ScmEventMonitoringService.cs:37-40`).
- **Polling:** `ScmEventOptions.PollInterval` = 60 s (Konfigurationssektion `ScmEvents`, `App.xaml.cs:254`); `ScmEventExchangeService` pollte lokale Antwortdateien alle 2 s.
- **Trigger:** Case-insensitive Zeichenkette `$Softwareschmiede` im Text; Autor/letzter Bearbeiter muss der authentifizierte Repository-Owner sein (`ScmEventValidationService.cs`).

## Status und Umfang

Quellcodeanalyse ohne Build/Test-Ausführung und ohne Codeänderungen. Alle Angaben mit Dateipfad:Zeile; Vermutungen sind in den Detaildateien ausdrücklich als solche markiert.
