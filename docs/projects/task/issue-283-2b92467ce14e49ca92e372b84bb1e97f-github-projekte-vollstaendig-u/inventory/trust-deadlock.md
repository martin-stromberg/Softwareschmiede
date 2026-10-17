# Trust-/„Wartend"-Analyse (FR-7 / FR-8)

Zurück: [../inventory.md](../inventory.md) · Beobachteter Ablauf: requirement.md §4.1

**Lesart:** „Belegt" = direkt aus dem Code ableitbar. „Vermutung" = plausible, aber nicht verifizierte Ausführungsreihenfolge (kein Laufzeitbeweis).

## 1. Belegte Mechanismen, die zum Trust-Problem beitragen

1. **Jede Aufgabe bekommt ein frisches Arbeitsverzeichnis.** Klonpfad = `<Arbeitsverzeichnis>/softwareschmiede/<aufgabeId>` (`EntwicklungsprozessService.cs:524-540`). CLIs wie Claude Code fragen bei erstmaligem Start in einem neuen Verzeichnis „Do you trust the files in this folder?" — der Prompt ist pro Aufgabe zu erwarten.
2. **Während des Trust-Dialogs ist die Eingabebereitschaft `Unknown`.** `ScmPromptSessionAdapter.RefreshScreen` (`ScmPromptSessionAdapter.cs:236-262`) erkennt Ready nur am Promptzeichen der Cursorzeile (`❯`/`›`/…). Der Trust-Dialog erzeugt `Unknown` → Dispatch endet in `Wartend` mit Reason „Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen." (`ScmEventOrchestrator.cs:164-168`).
3. **Die UI bietet dann „Eingabebereitschaft bestätigen" an** (`CanConfirmReady`: AttemptId null + CliRunId + Reason-Substring, `ScmEventQueueViewModel.cs:295`). `ConfirmReady` setzt `_manuallyConfirmed=true` und Ready **erzwungen** — die einzige Bedingung ist `_recognizedCli && _ready != Busy` (`ScmPromptSessionAdapter.cs:181-192`). Es gibt **keine** Prüfung, ob die CLI tatsächlich am Eingabeprompt steht statt im Trust-Dialog.
4. **Der nächste Dispatch-Schritt nach Ready ist `CheckExchangeAccessAsync`** (`ScmEventOrchestrator.cs:171`). Für echte CLIs (Codex/Claude) erzeugt sie beim ersten Mal eine `_accessProbe` und **schreibt sofort einen Probe-Prompt in die Session** (`ScmPromptSessionAdapter.cs:159-167` via `Session.WritePromptAsync`). Anschließend: `Unknown` „Nachweis des tatsächlichen Dateitool-Zugriffs steht aus…" (Z. 169) → `Wartend`.
5. **`_accessProbe` wird nie zurückgesetzt oder erneut gesendet.** Alle Folgeprüfungen rufen nur `ReadAccessProbeAsync(_accessProbe)` auf (Z. 155); ist die Probe-Datei nie entstanden, bleibt der Status auf denselben `Unknown`-Reason — für immer (kein Timeout, kein Re-Probe, kein Reset bei App- oder Retry-Aufrufen; der Adapter ist prozessintern und lebt so lange wie das `CliProcessHandle`).
6. **Wartende Events werden zwar automatisch re-dispatchet** (`NextAttemptUtc == null`, `ScmEventMonitoringService.cs:121-124`), treffen dann aber immer auf denselben hängenden Probe-Pfad — der eigentliche Event-Prompt (`attempt.Prompt`) wird nur geschrieben, wenn `CheckExchangeAccessAsync == Supported` (Z. 171-191).
7. **„Erneut prüfen" und „Öffnen / CLI auswählen" führen in denselben Pfad.** `RetryAsync` → `DispatchCoreAsync` (lebt der Adapter → Schritt 14-15 → Probe hängt). `OpenFromQueueAsync` → Session lebt → Bestätigungsdialog → `DispatchCoreAsync` mit Prefix → gleiches Ergebnis. Das erklärt exakt den beobachteten „wirksamkeitslosen Wiederholungsweg".
8. **`InvalidOperationException` setzt `NextAttemptUtc = long.MaxValue`** (`ScmEventOrchestrator.cs:209-214`) — nach einem solchen Fehler (z. B. `CheckHealthAsync`-Fehlschlag „Die ausgewählte CLI ist nicht startbereit.") erfolgt **kein** automatischer Retry mehr, nur noch manuelle Prüfung. Der Status bleibt „Wartend".
9. **Trust-Status ist für die Evaluator-Dateiliste unsichtbar.** `ScmCliAccessEvaluator.Capture` erfasst für Claude nur `settings.json`/`settings.local.json`/Managed-Settings (`ScmCliAccessEvaluator.cs:53-62`) — nicht die benutzerbezogene `~/.claude.json`, in der Claude Code `hasTrustDialogAccepted` pro Projektverzeichnis speichert (externe Tatsache, nicht aus diesem Repo belegt). `IsUnchanged` merkt die Trust-Bestätigung also nicht als Konfigurationsänderung.
10. **Die Trust-Bestätigung selbst zählt als Eingabe.** Der Benutzer tippt im eingebetteten Terminal — `ScmCoordinatedTerminalSession.WriteInputAsync` feuert `InputStarted` → `MarkInputStarted` → `_ready=Busy`, `_manuallyConfirmed=false` (`ScmPromptSessionAdapter.cs:195-203`). Danach kann erst wieder eine neue Ausgabe + Promptzeile Ready setzen — grundsätzlich korrekt, aber relevant für das Timing.

## 2. Wahrscheinlichste Ursache (Vermutung, aus Code begründet)

**Hauptvermutung:** Der Nutzer hat — wie vorgesehen — „Eingabebereitschaft bestätigen" gedrückt (oder die CLI meldete kurz Ready), **bevor** die Trust-Abfrage beantwortet war. `ConfirmReady` erzwingt Ready → Dispatch schickt den Probe-Prompt (`access-probe`-JSON) in die PTY → die Eingabe landet im Trust-Dialog und wird verworfen/falsch interpretiert → die CLI führt die Dateitool-Challenge nie aus → `_accessProbe` bleibt permanent ausstehend → jeder weitere Dispatch/Retry endet in `Wartend` „Nachweis des tatsächlichen Dateitool-Zugriffs steht aus; separate lokale Kanalprobe muss erfolgreich abgeschlossen werden.". Da dieser Reason „Eingabebereitschaft unbekannt" nicht mehr enthält, wird auch `ConfirmReady` nicht mehr angeboten — es gibt **keinen UI-Weg** mehr, die Situation zu lösen (außer regulärer CLI-Neustart der Aufgabe, der einen neuen Adapter/Run erzeugt).

Verstärkende Beobachtung (Belegt): Der Reason-Text „…separate lokale Kanalprobe muss erfolgreich abgeschlossen werden." taucht in keiner UI-Aktion als behandelbarer Fall auf — es existiert kein „Probe zurücksetzen"-Pfad.

**Nebenvermutung A (falls `ConfirmReady` nicht gedrückt wurde):** `_recognizedCli` bleibt `false`, weil der Trust-Dialog den Versionsstring „Claude Code v2.1.0" nicht (mehr) im sichtbaren Puffer enthält (Erkennung nur über `_startupOutput` ≤ 32 KB oder `RefreshScreen`-Vollbild, `ScmPromptSessionAdapter.cs:218-234`). Dann schlägt `ConfirmReady` mit „CLI ist beschäftigt oder kann noch nicht bestätigt werden." fehl und auch nach Trust-Bestätigung bleibt alles `Unknown` → `Wartend`. Hängt davon ab, was die betroffene Claude-Version im Trust-Dialog rendern — im Code nicht prüfbar.

**Nebenvermutung B:** `InvalidOperationException` aus dem Startpfad (CheckHealth/Klon) hat `NextAttemptUtc=long.MaxValue` gesetzt und die Reason wurde später durch einen Probe-`Unknown` ersetzt — Auto-Retry wäre deaktiviert. Schwächer, da „Erneut prüfen" das Flag löscht.

## 3. Ansatzpunkte für FR-7 (Fortsetzung nach Trust-Bestätigung)

- **Probe-Wiederholung/-Reset:** `_accessProbe` nach N fehlgeschlagenen `ReadAccessProbeAsync` oder nach definierter Zeit verwerfen und neu senden; oder Probe-Prompt nicht bei `Unknown` einfrieren, sondern mit Backoff erneut zustellen — jeweils unter Wahrung der „keine Ereignistexte vor Kanalnachweis"-Regel (`ScmPromptSessionAdapter.cs:166`).
- **`ConfirmReady` verschärfen:** Bestätigung nur zulassen, wenn der Bildschirm ein echtes Promptzeichen zeigt (d. h. nicht mitten im Trust-Dialog), oder den Trust-Dialog als eigenen `ScmPromptReadyStatus`-Zustand erkennen („BlockedByTrustPrompt") und die UI-Aufforderung entsprechend formulieren.
- **Trust-Dialog aktiv erkennen:** Screen-Heuristik auf „trust"/„Do you trust" o. ä. prüfen und als eigenen Wartegrund persistieren — dann kann nach Verschwinden des Dialogs gezielt erneut geprüft werden statt generisch „unbekannt".
- **Auto-Resume:** Der Poll dispatchet `Wartend`+`NextAttemptUtc==null` bereits automatisch — der Mechanismus für „zuverlässig fortsetzen" existiert also; er scheitert nur am hängenden Probe-Zustand bzw. an `long.MaxValue`-Sperren. Eine `Wartend`-Ursache „TrustPrompt" könnte `NextAttemptUtc` gezielt kurz setzen.

## 4. Ansatzpunkte für FR-8 (Trust-Abfrage vermeiden/vorwegnehmen — Prüfauftrag)

Belegt aus dem Code:

- CLI-Startparameter kommen aus `BuildProcessStartInfo` des Plugins (`ClaudeCliPlugin.cs:128-158`, `CodexPlugin.cs`) + `optionalParameters` aus dem Orchestrator (`ScmEventOrchestrator.cs:146` — heute nur Simulator `--scm-exchange`) + benutzerkonfigurierten `CommandLineParameters` (`CliKiPluginBase.AppendCommandLineParameters`).
- `ScmCliAccessEvaluator` akzeptiert für Claude ausschließlich `--permission-mode acceptEdits` (plus `--model`); jeder andere freie Parameter → `Unknown` (`ScmCliAccessEvaluator.cs:167-196`). **Ein Trust-Bypass-Parameter müsste in der Whitelist ergänzt werden, sonst wird der Kanal immer `Unknown`.**
- Für `acceptEdits` verlangt der Evaluator effektive Freigabe; `--permission-mode acceptEdits` allein umgeht **nicht** die Workspace-Trust-Abfrage von Claude Code (die ist davon unabhängig; externe Tatsache, zu verifizieren).

Externe, zu prüfende Optionen (als Vermutung markiert — nicht aus dem Repo belegt):

- Claude Code: `hasTrustDialogAccepted` in `~/.claude.json` pro Projektverzeichnis vorbefüllen (Host kontrolliert die Datei) oder CLI-Flag wie `--dangerously-skip-permissions` (würde aber den Kanal-Sicherheitsvertrag des Evaluators unterlaufen → `Unknown` oder bewusst Denied gestalten). Auch `claude` non-interactive Einmalaufruf zum Vorweg-Bestätigen denkbar.
- Codex: Trust/Approval-Konfiguration in `config.toml` (`approval_policy`, `sandbox_mode`) — der Evaluator liest diese Datei bereits.
- Wichtig: Der Kanal-`Supported`-Pfad erfordert nach wie vor die Dateitool-Probe — ein Trust-Bypass ändert nur die Startphase, nicht den Kanalnachweis.

## 5. Reproduktions-/Verifikationspfade im Testbestand

- `ScmDispatchFixture` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`) erzeugt Adapter mit Simulator-Buffer (`SCM_READY_V1` umschaltbar via `SetPrompt`) — ein Trust-Dialog-Screen könnte als Buffer-Inhalt simuliert werden, um `ConfirmReady`/`Unknown`-Pfade unit-testbar zu machen.
- `ScmE2EFixture`+Simulator können den Kanal deterministisch steuern; ein „Probe bleibt aus"-Szenario ist über das Weglassen der Simulator-Probeausführung abbildbar (Simulator-Programm in `KiSimulatorPlugin.cs` ab Z. ~100 steuert `.softwareschmiede-scm-exchange`).
