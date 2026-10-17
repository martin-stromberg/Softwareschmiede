# Übersetzte Anforderung: Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage (inkl. Prüfauftrag Startparameter)

## Fachliche Zusammenfassung

Die SCM-Ereigniszustellung (`ScmEventOrchestrator` → `ScmPromptSessionAdapter`) kann nach einer Vertrauenswürdigkeitsabfrage der KI-CLI („Do you trust …") dauerhaft im Status `Wartend` hängen bleiben: Die einmalig pro CLI-Lauf gesendete lokale Kanalprobe (`_accessProbe`) geht im Blockierdialog verloren und wird nie erneut zugestellt; alle UI-Wiederholungswege fragen nur die hängende Probe ab. Zu liefern ist eine Korrektur, die (a) eine verlorene/unausgeführte Kanalprobe begrenzt erneut zustellt, (b) den Vertrauensdialog als eigenen verständlichen Wartezustand erkennt, (c) die manuelle Bereitschaftsbestätigung gegen Bestätigung mitten im Blockierdialog absichert und (d) einmal auf `NextAttemptUtc = long.MaxValue` gesetzte Ereignisse wieder fortsetzbar macht. Zusätzlich ist zu prüfen und ggf. umzusetzen, ob die CLIs (Claude Code, Codex) per sicherer Startkonfiguration so vorbereitet werden können, dass die Trust-Abfrage kontrolliert vermieden wird — ohne den Sicherheitsvertrag des Antwortkanals auszuhebeln.

## Betroffene Klassen und Komponenten

- `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`
  - `ScmPromptReadyStatus` (Enum): voraussichtlich neuer Wert für „Vertrauensdialog blockiert".
  - `ScmPromptSessionAdapter`: `RefreshScreen` (Trust-Dialog-Erkennung), `ConfirmReady` (Härtung), `CheckExchangeAccessCoreAsync` (begrenzte Probe-Wiederholung, Zeitstempel/Zähler, `TimeProvider`).
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
  - `DispatchCoreAsync`: eigenständiger Wartegrund für den Vertrauensdialog.
  - `ConfirmReadyAsync`: `NextAttemptUtc`-Sperre aufheben, damit die Bestätigung auch nach `long.MaxValue` wirkt.
- `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`
  - Codex-`config.toml`-Parser: Whitelist-Erweiterung für `[projects.*]`-Abschnitte (nur `trust_level`), falls die Trust-Vorbelegung für Codex umgesetzt wird.
- `src/Softwareschmiede/Application/Services/` (neu, voraussichtlich `ScmCliWorkspaceTrust` o. ä.)
  - Host-seitige Vorbelegung des Workspace-Trust-Status (Claude: `~/.claude.json` `projects.<dir>.hasTrustDialogAccepted`; Codex: `~/.codex/config.toml` `[projects."<dir>"] trust_level = "trusted"`).
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
  - `StartTerminalSessionCoreAsync`: Aufruf der Trust-Vorbelegung vor `ScmCliAccessEvaluator.Capture`; `TimeProvider` an den Adapter durchreichen.
- `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`
  - `CanConfirmReady` (Reason-Substring-Logik) — vermutlich ohne Änderung, muss aber in Tests abgesichert werden (Trust-Grund darf die Aktion nicht anbieten).
- Tests
  - `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` (Trust-Wartegrund, ConfirmReady-Sperre, Fortsetzung nach Trust-Bestätigung — über `ScmDispatchFixture`-Buffer).
  - `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.cs` bzw. neue Datei (Probe-Wiederholung, Probe-Reset, Evaluator-`[projects.*]`).
  - Neue Tests für die Trust-Vorbelegung (JSON/TOML-Merge, Guard bei projektgebundener Konfiguration).

## Implementierungsansatz

- **Trust-Dialog als eigener Zustand:** `RefreshScreen` erkennt die bekannten Dialog-Texte („Do you trust the files in this folder", „Is this a project you created or one you trust", „Do you trust the contents of this directory") und setzt einen eigenen `ScmPromptReadyStatus` statt generisch `Unknown`. Der Orchestrator bildet daraus einen klaren, handlungsweisenden Wartegrund ab, der die Aktion „Eingabebereitschaft bestätigen" nicht anbietet. Wartende Einträge bleiben mit `NextAttemptUtc == null` im 60-s-Auto-Dispatch — nach dem Beantworten des Dialogs setzt der nächste Poll automatisch fort.
- **Kanalprobe-Wiederholung:** `_accessProbe` erhält Sendezeitpunkt + Zähler (über `TimeProvider`). Steht `ReadAccessProbeAsync` weiter aus und die CLI ist sichtbar eingabebereit, wird derselbe Probe-Prompt nach definierter Frist bis zu einer Höchstzahl erneut gesendet; danach bleibt ein klarer, regulär-neustartbarer Wartegrund. Die Regel „keine Ereignisinhalte vor Kanalnachweis" bleibt unangetastet — der Probe-Prompt enthält keinen Ereignistext.
- **Bestätigungs-Härtung:** `ConfirmReady` wertet zuvor den aktuellen Bildschirm aus und lehnt im Zustand „Vertrauensdialog" (wie bei `Busy`) ab.
- **`long.MaxValue`-Fortsetzung:** `ConfirmReadyAsync` hebt `NextAttemptUtc` auf (analog `RetryAsync`/`OpenFromQueueAsync`), damit die Bestätigung ein wirksamer Fortsetzungsweg ist.
- **Prüfauftrag:** Dokumentierte Bewertung der Kandidaten (kein dediziertes CLI-Flag; `--dangerously-skip-permissions`/`--yolo` verworfen). Sicherer Weg = host-seitige Vorbelegung des Trust-Status pro Arbeitsverzeichnis in der benutzerbezogenen CLI-Konfiguration — nur wenn das Arbeitsverzeichnis keine projektgebundene CLI-Konfiguration (`.claude`/`.codex`) enthält, die durch Trust aktiviert würde. Für Codex ist die Evaluator-Whitelist gezielt für `[projects.*]`/`trust_level` zu erweitern.

## Konfiguration

Keine neue Benutzerkonfiguration erforderlich. Zeitkonstanten (Probe-Wiederholungsfrist, maximale Probe-Sendungen) sind interne Konstanten. Die Trust-Vorbelegung schreibt ausschließlich benutzerbezogene CLI-Konfigurationsdateien (`~/.claude.json`, `<CODEX_HOME>/config.toml`) — best-effort, niemals startblockierend.

## Offene Fragen

- Keine fachlich blocking Fragen. Interne Entscheidungen (als Annahmen markiert):
  - Trust-Vorbelegung wird nur vorgenommen, wenn kein `<workdir>/.claude` bzw. `<workdir>/.codex` existiert — andernfalls bleibt die manuelle Abfrage bewusst erhalten (Annahme: konservativste Lesart der Sicherheitsanforderung).
  - Der exakte Pfadschlüssel (`Path.GetFullPath`, Windows-Backslash-Form) wird für beide CLIs verwendet; bei Schlüssel-Mismatch greift weiterhin die Dialog-Erkennung als Fallback (kein funktionaler Schaden).
