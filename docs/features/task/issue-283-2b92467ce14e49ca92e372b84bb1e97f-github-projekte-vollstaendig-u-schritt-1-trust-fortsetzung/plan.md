# Umsetzungsplan: Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage (inkl. Prüfauftrag Startparameter)

## Übersicht

Der Trust-Deadlock wird auf drei Ebenen behoben: (1) `ScmPromptSessionAdapter` erkennt den Vertrauensdialog als eigenen `ScmPromptReadyStatus.TrustPrompt`, härtet `ConfirmReady` dagegen ab und sendet eine ausstehende Kanalprobe begrenzt erneut; (2) `ScmEventOrchestrator` bildet den neuen Zustand auf einen klaren Wartegrund ab und hebt bei `ConfirmReadyAsync` die `NextAttemptUtc`-Sperre auf; (3) der Prüfauftrag ergibt eine host-seitige Trust-Vorbelegung (`~/.claude.json` bzw. `~/.codex/config.toml`), die nur greift, wenn das Arbeitsverzeichnis keine projektgebundene CLI-Konfiguration enthält — mit gezielter Evaluator-Whitelist für `[projects.*]`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Trust-Dialog-Erkennung | Eigener `ScmPromptReadyStatus.TrustPrompt` statt nur anderem Reason-Text | Der Zustand ist fachlich eigenständig (blockierender Dialog), trennt Erkennung (`Adapter`) und Darstellung (`Orchestrator`-Wartegrund) sauber und deaktiviert `CanConfirmReady` automatisch über den bestehenden Reason-Substring-Mechanismus — keine UI-Änderung nötig. |
| Dialog-Marker | Explizite, gepinnte Texte („Do you trust the files in this folder", „Is this a project you created or one you trust", „Do you trust the contents of this directory") vor den Prompt-Zeichen-Prüfungen in `RefreshScreen` | Konsistent mit der bewusst konservativen Versionserkennung (`RecognizeStartup`); generisches „trust"-Substring würde falsch positive Treffer (Repo-/Textinhalte) riskieren. Fehlschläge der Erkennung degradieren nur auf den bisherigen `Unknown`-Pfad. |
| Kanalprobe-Wiederholung | Denselben `ScmExchangeAccessProbe`-Prompt nach fester Frist (`AccessProbeResendDelay = 60 s`, `TimeProvider`-gestützt) bis zu `AccessProbeMaxSends = 3` erneut senden — nur bei sichtbarem `Ready` | Eine neue Probe pro Versand würde Verzeichnisse/Nonces vermehren, ohne den Verlustmechanismus zu ändern; der Prompt ist idempotent (gleiche Dateioperationen). Nur bei `Ready` senden verhindert erneuten Verlust im Dialog. Kappe verhindert Prompt-Spam in eine dauerhaft taube Sitzung; danach bleibt ein klarer „regulärer Neustart"-Grund. |
| `long.MaxValue`-Fortsetzung | `ConfirmReadyAsync` setzt `item.NextAttemptUtc = null` (analog `OpenFromQueueAsync` Z. 58) | Kleinste wirksame Änderung: die Bestätigung wird wie die anderen expliziten Benutzeraktionen zu einem echten Retry. `RetryAsync`/`ResendAsync`/`OpenFromQueueAsync` enthalten die Aufhebung bereits. |
| Trust-Vorbelegung (Prüfauftrag-Ergebnis) | Host schreibt benutzerbezogene CLI-Konfiguration vor dem Start: Claude `projects["<dir>"].hasTrustDialogAccepted = true` in `~/.claude.json` (JsonNode-Merge, atomares Ersetzen); Codex `[projects."<dir>"] trust_level = "trusted"` in `<CODEX_HOME\|~/.codex>/config.toml` (Anhängen, falls Eintrag fehlt) — jeweils nur, wenn `<trustTarget>/.claude` bzw. `<trustTarget>/.codex` **nicht** existiert | Beide Mechanismen sind dokumentierte, herstellerseitig vorgesehene Wege (kein Parameter existiert; `--dangerously-skip-permissions`/`--yolo` verworfen). Die Vorbelegung ist pfad-genau auf das vom Host erzeugte Aufgabenverzeichnis begrenzt, respektiert vorhandene Nutzerentscheidungen (`false`/`"untrusted"` → nicht überschreiben) und berührt keine Berechtigungen — `--permission-mode acceptEdits`/`workspace-write` + Dateitool-Probe bleiben Pflicht. Der `.claude`/`.codex`-Guard verhindert, dass Trust projektgebundene Hooks/Exec-Policies aus dem ungeprüften Klon aktiviert. |
| Trust-Zielverzeichnis | `git rev-parse --show-toplevel` (best-effort, kurzer Timeout); Fallback `WorkingDirectory`; Schlüssel für beide Pfade schreiben, wenn sie abweichen | Codex wendet Trust auf die Git-Wurzel an (`trust_target`); Claude verlangt für das Settings-Gate den exakten Startpfad. Beide Schlüssel zu schreiben deckt Unterverzeichnis-Starts ab, ohne Zusatzannahmen. |
| Platz der Vorbelegung | Neuer statischer Helfer `ScmCliWorkspaceTrust` in `Application/Services`, aufgerufen in `StartTerminalSessionCoreAsync` zwischen Spec-Erstellung und `ScmCliAccessEvaluator.Capture` | Einziger gemeinsamer Trichter aller Terminal-Starts (manuell + SCM-Dispatch); Schreiben vor `Capture`, damit der geänderte `config.toml` in Fingerprint/Evaluierung einfließt; best-effort (Fehler nur `LogWarning`, Start nie blockieren). |

## Programmabläufe

### Fortsetzung nach Vertrauensdialog (Hauptfluss)

1. CLI startet in neuem Aufgabenverzeichnis; Bildschirm zeigt Trust-Dialog → `RefreshScreen` erkennt Marker → `SetReady(TrustPrompt)`.
2. Poll-Dispatch: `WaitForInputReadyAsync` → `TrustPrompt` → `WaitWithCommentAsync` mit neuem Grund (enthält nicht „Eingabebereitschaft unbekannt" → `CanConfirmReady` = false). `NextAttemptUtc` bleibt `null` → Auto-Dispatch weiterhin aktiv.
3. Benutzer beantwortet Dialog im Terminal → `MarkInputStarted` → `Busy` → neue Ausgabe mit Promptzeile → `Ready`.
4. Nächster Poll-Dispatch: `CheckExchangeAccessAsync` → Probe ggf. (erst-)gesendet → `Supported` nach Nachweis → `exchange.PrepareAsync` → `WritePromptAsync` → `Zugestellt`.

### Verlorene Kanalprobe (ConfirmReady vor Trust-Bestätigung o. ä.)

1. Probe wurde in den Dialog gesendet → `ReadAccessProbeAsync` dauerhaft falsch → Wartegrund „Kanalprobe steht aus".
2. Sobald `ReadyStatus == Ready` und `_accessProbeSentUtc` älter als `AccessProbeResendDelay` (und `_accessProbeSends < AccessProbeMaxSends`): `MarkInputStarted` + erneutes `Session.WritePromptAsync(BuildAccessProbePrompt(_accessProbe))` + Zähler/Zeitstempel aktualisieren.
3. Nachweis erfolgt → `Supported` → Zustellung. Nach `AccessProbeMaxSends` ohne Nachweis → `Unknown` mit Grund „…regulärer Neustart erforderlich" (bedienbar über regulären CLI-Neustart der Aufgabe).

Beteiligte Klassen: `ScmPromptSessionAdapter`, `ScmEventOrchestrator`, `ScmEventExchangeProtocol`, `ScmEventMonitoringService`.

### Trust-Vorbelegung beim CLI-Start

1. `StartTerminalSessionCoreAsync`: nach `GetTerminalStartSpecAsync`, vor `ScmCliAccessEvaluator.Capture` → `ScmCliWorkspaceTrust.EnsureTrusted(pluginPrefix, spec)` (try/catch → `LogWarning`).
2. `EnsureTrusted`: `trustTarget` = Git-Toplevel (best-effort) bzw. `spec.WorkingDirectory`; Guard: `<dir>/.claude` (Claude) bzw. `<dir>/.codex` (Codex) vorhanden → überspringen.
3. Claude: `~/.claude.json` lesen (fehlend/ungültig → nur minimale Struktur bzw. überspringen), `projects[<key>].hasTrustDialogAccepted` fehlt → `true` setzen (explizites `false` respektieren), atomar zurückschreiben.
4. Codex: `config.toml` lesen (fehlend → anlegen); Abschnitt `[projects."<escaped>"]` fehlt → `\n[projects."<escaped>"]\ntrust_level = "trusted"\n` anhängen; vorhanden → unverändert lassen.
5. `ScmCliAccessEvaluator.Capture`/`Evaluate`: `[projects.*]`-Abschnitte mit ausschließlich `trust_level = "trusted"|"untrusted"` zulässig; andere Schlüssel/Werte → `Unknown` wie bisher.

Beteiligte Klassen: `KiAusfuehrungsService`, `ScmCliWorkspaceTrust` (neu), `ScmCliAccessEvaluator`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScmCliWorkspaceTrust` | statische Klasse (`Application/Services`) | Best-effort-Vorbelegung des Workspace-Trust-Status pro CLI (`EnsureTrusted(prefix, spec)`), mit internen, pfad-parametrisierten Methoden für testbare JSON-/TOML-Merges. |

## Änderungen an bestehenden Klassen

### `ScmPromptReadyStatus` (Enum, `ScmPromptSessionAdapter.cs`)

- **Neuer Wert:** `TrustPrompt` — „Die CLI wartet auf eine Vertrauensentscheidung für das Arbeitsverzeichnis."

### `ScmPromptSessionAdapter` (Klasse)

- **Neue Felder:** `TimeProvider _time` (optionaler Ctor-Parameter, Default `TimeProvider.System`), `DateTimeOffset _accessProbeSentUtc`, `int _accessProbeSends`.
- **Neue Konstanten:** `AccessProbeResendDelay` (60 s), `AccessProbeMaxSends` (3).
- **Geänderte Methoden:**
  - `RefreshScreen` — nach `!_recognizedCli`/`_manuallyConfirmed`-Guards, vor Busy/Prompt-Auswertung: Bildschirm enthält Trust-Marker → `SetReady(TrustPrompt)`.
  - `ConfirmReady` — zuerst `RefreshScreen()` (Lock ist reentrant); ablehnen bei `!_recognizedCli || _ready is Busy or TrustPrompt`.
  - `CheckExchangeAccessCoreAsync` — Probe-pendent-Branch: bei `ReadyStatus == Ready` + Frist abgelaufen + Sends < Max → erneut senden (Alive/Ready-Recheck, `MarkInputStarted`, `WritePromptAsync`); bei erschöpften Sends → `Unknown`-Grund mit „regulärer Neustart"-Hinweis; sonst bisheriger „Nachweis steht aus"-Grund.
- **Ctor:** optionaler Parameter `TimeProvider? timeProvider = null` (Kompatibilität bestehender Aufrufe/Tests).

### `ScmEventOrchestrator` (Klasse)

- **Geänderte Methoden:**
  - `DispatchCoreAsync` — `ready == ScmPromptReadyStatus.TrustPrompt` → `WaitWithCommentAsync` „Die CLI wartet auf die Bestätigung der Vertrauenswürdigkeit dieses Ordners. Bitte die Abfrage im Terminal der laufenden CLI-Sitzung beantworten; die Zustellung wird danach automatisch fortgesetzt."
  - `ConfirmReadyAsync` — nach erfolgreicher `session.ConfirmReady`: `item.NextAttemptUtc = null` setzen.

### `ScmCliAccessEvaluator` (statische Klasse)

- **Geänderte Methode:** `Evaluate` (Codex-TOML-Parsing) — Abschnitte mit Präfix `[projects.` zulassen; darin ausschließlich Schlüssel `trust_level` mit Wert `"trusted"`/`"untrusted"`; sonst wie bisher `Unknown`.

### `KiAusfuehrungsService` (Klasse)

- **Geänderte Methode:** `StartTerminalSessionCoreAsync` — `ScmCliWorkspaceTrust.EnsureTrusted(kiPlugin.PluginPrefix, spec)` vor `ScmCliAccessEvaluator.Capture` (try/catch → `LogWarning`); `_timeProvider` an `ScmPromptSessionAdapter`-Ctor übergeben.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Codex `config.toml` `[projects.*]` | Nur `trust_level` ∈ {`"trusted"`,`"untrusted"`}; ein Abschnitt je Zeile, keine weiteren Schlüssel | Evaluator → `Unknown` (wie bisher bei unbekannten Vorgaben) |
| `~/.claude.json` Merge | Vorhandene Felder unverändert lassen; `hasTrustDialogAccepted: false` respektieren; ungültiges JSON → überspringen | Keine (best-effort; nie werfen) |
| Codex `config.toml` Append | Vorhandener `[projects."<dir>"]`-Eintrag (auch `"untrusted"`) nicht überschreiben | Keine (best-effort) |
| Trust-Vorbelegung | Nur bei fehlendem `<dir>/.claude` bzw. `<dir>/.codex` | Überspringen (Dialog bleibt manuell) |

## Konfigurationsänderungen

Keine Anwendungskonfiguration. Neue interne Konstanten: `AccessProbeResendDelay = 60 s`, `AccessProbeMaxSends = 3`.

## Seiteneffekte und Risiken

- **`ScmPromptReadyStatus`-Erweiterung:** Öffentliches Enum; Konsumenten sind `ScmEventOrchestrator` (wird angepasst), `IsBusy`-Property (unverändert) und Tests. Externe Nutzer außerhalb des Repos: keine bekannt (interner Vertrag).
- **Probe-Wiederholung:** Bestehender Integrationstest `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` erwartet `WritePromptAsync` `Times.Once` — bleibt bestehen, solange kein Fristablauf simuliert wird (Default `TimeProvider.System`, zweiter Aufruf < 60 s).
- **`[projects.*]`-Whitelist:** Nutzer mit bestehenden `projects`-Einträgen erhalten neu `Supported` statt `Unknown` — erwünscht (Trust-Eintrag ist sicher auswertbar); Einträge mit anderen Schlüsseln bleiben `Unknown`.
- **Trust-Vorbelegung schreibt Benutzerdateien:** `~/.claude.json`/`config.toml` werden außerhalb des Repos verändert — bewusst kleiner, dokumentierter Eingriff; Guards verhindern Überschreiben; `.claude`/`.codex`-Presence erhält den manuellen Dialog für riskante Inhalte.
- **`ConfirmReady` mit `RefreshScreen`:** Liest `Session.Buffer` synchron — wie in `WaitForInputReadyAsync` üblich; keine neue Race-Fläche.
- **Bestandsschutz:** Keine Ereignisinhalte vor Kanalnachweis (Probe-Prompt unverändert inhaltsfrei); deduplizierte Zustellung/`DeliveryAttemptId`-Logik unverändert.

## Umsetzungsreihenfolge

1. **Adapter: Trust-Zustand + ConfirmReady-Härtung + Probe-Wiederholung**
   - Voraussetzungen: Keine (`TimeProvider` über DI bereits verfügbar; optionaler Ctor-Parameter hält Testaufrufe kompatibel).
   - Beschreibung: Enum-Wert, `RefreshScreen`-Marker, `ConfirmReady`-Guard, Probe-Resend mit Zähler/Zeitstempel, `TimeProvider`-Parameter.
2. **Orchestrator: Trust-Wartegrund + ConfirmReady-Sperraufhebung**
   - Voraussetzungen: Schritt 1 (neuer Enum-Wert).
   - Beschreibung: `DispatchCoreAsync`-Reason-Mapping, `NextAttemptUtc = null` in `ConfirmReadyAsync`.
3. **Trust-Vorbelegung `ScmCliWorkspaceTrust` + Evaluator-Whitelist + Startpfad-Verdrahtung**
   - Voraussetzungen: Keine weiteren (nutzt `JsonNode`, TOML-Textoperationen, `ProcessStartInfo` für `git rev-parse`).
   - Beschreibung: Neue Helferklasse, `Evaluate`-Erweiterung, Aufruf in `StartTerminalSessionCoreAsync`.
4. **Unit-Tests (Softwareschmiede.Tests): Trust-Wartegrund, ConfirmReady-Sperre, Fortsetzung nach Dialog**
   - Voraussetzungen: Schritte 1-2; `ScmDispatchFixture` (Trust-Text als `SetPrompt`-Inhalt).
5. **Integrationstests (Softwareschmiede.IntegrationTests): Probe-Wiederholung/-Kappe, Trust-Status im Adapter, Evaluator-`[projects.*]`, `ScmCliWorkspaceTrust`-Merges**
   - Voraussetzungen: Schritte 1-3; vorhandene `Session`/`Spec`/`CompleteProbeAsync`-Hilfen.
6. **Build + stabiler Test-Lane + `dotnet format`**
   - Voraussetzungen: Schritte 1-5.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `TrustDialog_WaitsWithActionableReason_AndConfirmReadyIsRejected` | `ScmEventOrchestratorTests` | Trust-Buffer → `Wartend` + Trust-Grund (ohne „Eingabebereitschaft unbekannt"); `ConfirmReadyAsync` → `InvalidOperationException`; `adapter.ReadyStatus == TrustPrompt` |
| `TrustDialogResolved_AutoDispatchDelivers` | `ScmEventOrchestratorTests` | Nach Trust-Buffer → `SetPrompt("SCM_READY_V1")` → `RetryAsync`/Dispatch → `Zugestellt`/`DeliveryAttemptId` gesetzt (Fortsetzung ohne Neustart/DB-Eingriff) |
| `ConfirmReady_ClearsNextAttemptLock` | `ScmEventOrchestratorTests` | `NextAttemptUtc = long.MaxValue` per DB setzen → `ConfirmReadyAsync` → Dispatch läuft weiter (Sperre aufgehoben) |
| `PendingProbe_ResendsAfterDelay_UpToCap` | `ScmEventCliExchangeAccessTests` (oder neue Partial-Datei) | Adapter mit `TimeProvider`-Fake: 1. Check → Probe gesendet; weitere Checks ohne Frist → kein Resend; Frist überschritten + Ready → Resend (max. `AccessProbeMaxSends`); danach „Neustart"-Grund, kein weiterer Prompt |
| `LostProbeAfterTrustDialog_RecoversOnReady` | `ScmEventCliExchangeAccessTests` | Probe beim ersten Ready gesendet → Bildschirm wechselt auf Trust (`TrustPrompt`, `ConfirmReady` false, kein Write) → zurück auf Ready → Frist → Resend → `CompleteProbeAsync` → `Supported` |
| `ProjectsTrustLevel_IsAccepted_OtherKeysRejected` | `ScmEventCliExchangeAccessTests` | `config.toml` mit `[projects."x"] trust_level = "trusted"` → Evaluator akzeptiert Abschnitt; `[projects."x"]` mit anderem Schlüssel/Wert → `Unknown` |
| `EnsureTrusted_WritesScopedEntries_AndRespectsGuards` | neue `ScmCliWorkspaceTrustTests` | JsonNode-Merge erhält Fremdfelder, respektiert `false`; TOML-Append/Dedupe/`"untrusted"`-Respekt; `.claude`/`.codex`-Guard überspringt; `CODEX_HOME`-Auflösung |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` (`ScmEventCliExchangeAccessTests`) | Erwartung `Times.Once` verifizieren — sollte unverändert bestehen (kein Fristablauf); ggf. Kommentar ergänzen. Keine Codeänderung erwartet. |

### E2E-Tests (Pflicht)

Der Fehler betrifft einen Hintergrund-Dispatch-Pfad ohne neue UI-Interaktion (der Trust-Dialog wird im eingebetteten Terminal beantwortet, nicht in WPF-Controls). Die Sollbruch-Absicherung erfolgt — wie in der Anforderung ausdrücklich erlaubt — „über die bestehende Simulator-/Fixture-Infrastruktur mit steuerbarem Bildschirmpuffer", also Unit-/Integrationstests statt FlaUI. Ein neuer FlaUI-E2E ist nicht erforderlich (kein neues UI-Element, keine geänderte Benutzeraktion in der WPF-Oberfläche); der bestehende konsolidierte `E2E_ScmEvents`-Test bleibt unverändert und deckt die Queue-Bedienung weiter ab.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Trust-Dialog → Wartegrund → Fortsetzung nach Terminal-Bestätigung | `ScmEventOrchestratorTests` (Fixture-Buffer, kein FlaUI nötig) | „Zustellung setzt zuverlässig fort … ohne CLI-Neustart" |
| Verlorene Kanalprobe → Resend → `Supported` | `ScmEventCliExchangeAccessTests` | „Sollbruch reproduzierbar abgesichert" |

## Offene Punkte

Keine.

## Ergebnis des Prüfauftrags (Empfehlung)

| CLI | Mechanismus | Parameter/Flag? | Umsetzung |
|-----|-------------|-----------------|-----------|
| Claude Code | `~/.claude.json` → `projects["<dir>"].hasTrustDialogAccepted = true` (herstellereigen dokumentierter Weg) | Kein Trust-Flag existiert; `--dangerously-skip-permissions` verworfen (umgeht alle Berechtigungen → Sicherheitsvertrag verletzt) | Umgesetzt via `ScmCliWorkspaceTrust` mit `.claude`-Guard |
| OpenAI Codex | `~/.codex/config.toml` → `[projects."<dir>"] trust_level = "trusted"` (offizielle Dokumentation) | Kein Trust-Flag; `--yolo` zeigt den Dialog seit v0.113 ohnehin wieder | Umgesetzt via `ScmCliWorkspaceTrust` mit `.codex`-Guard + `[projects.*]`-Evaluator-Whitelist |

Sicherheitsbewertung: Beide Wege ändern nur die Startphase (Trust-Entscheidung für genau das vom Host angelegte Aufgabenverzeichnis) und ersetzen weder Berechtigungsmodus noch den rungebundenen Dateitool-Nachweis; sie greifen nur, wenn keine projektgebundene CLI-Konfiguration im Verzeichnis liegt, die durch Trust aktiviert würde. Vorhandene explizite Nutzerentscheidungen (`false`/`"untrusted"`) werden respektiert. Fallback bei jedem Restrisiko (abweichender Pfadschlüssel, anderer Dialogtext, neue CLI-Version) bleibt der erkannte `TrustPrompt`-Wartezustand mit automatischer Fortsetzung.
