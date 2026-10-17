# Abnahmeprüfung – Entwicklungsschritt 1

**Schritt:** Verlässliche Fortsetzung der CLI-Zustellung nach der Vertrauenswürdigkeitsabfrage (inkl. Prüfauftrag Startparameter)
**Branch:** `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u-schritt-1-trust-fortsetzung`
**Diff-Basis:** `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u...HEAD` (12 Dateien, +537/−12)

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Prüfung der Akzeptanzkriterien

**Kriterium 1 – Fortsetzung nach manueller Trust-Bestätigung ohne Neustart/DB-Eingriff: erfüllt.**

- `ScmPromptSessionAdapter.cs:61-66` pinnt drei Trust-Dialog-Marker; `RefreshScreen` (Z. 293-298) bildet sie auf den neuen `ScmPromptReadyStatus.TrustPrompt` (Z. 17-18) ab — ein eigener, auflösbarer Wartezustand statt generischem `Unknown`.
- `ScmEventOrchestrator.cs:164-172` übersetzt `TrustPrompt` in einen klaren Wartegrund („…Bitte die Abfrage im Terminal der laufenden CLI-Sitzung beantworten; die Zustellung wird danach automatisch fortgesetzt."). `WaitWithCommentAsync` setzt `NextAttemptUtc` nicht → der Eintrag bleibt `Wartend` mit `NextAttemptUtc = null` und wird vom bestehenden Wiederholungs-Dispatch (`ScmEventMonitoringService.cs:121-131`, `PollInterval` 60 s in `ScmEventOptions.cs:7`) automatisch erneut dispatchet.
- Kernfix: `ScmPromptSessionAdapter.cs:185-194` sendet eine ausstehende Kanalprobe (`_accessProbe`, identische Challenge) erneut, sobald `ReadyStatus == Ready` und die 60-s-Frist (`AccessProbeResendDelay`, `TimeProvider`-gestützt) verstrichen ist — begrenzt auf `AccessProbeMaxSends = 3`; bei Ausschöpfung bleibt ein klarer „CLI-Sitzung regulär neu starten"-Grund (Z. 195-201).
- Nachweis: `ProbeLostToTrustDialog_ResumesAfterAnswer` durchläuft Probe-Verlust im Dialog → Antwort → Resend → `CompleteProbeAsync` → `Supported`; `TrustDialog_BlocksUntilAnswered_ThenDeliversAutomatically` zeigt auf Orchesterebene `Wartend` → `DispatchAsync` → `Zugestellt` ohne Neustart.

**Kriterium 2 – Reproduzierbare Absicherung: erfüllt.**

- `ScmEventCliExchangeAccessTests.PendingProbe.cs` (IntegrationTests): 3 Theory-Methoden (6 Fälle, Codex + Claude) mit echtem `TerminalBuffer`, `Mock<ITerminalSession>` und steuerbarem `SettableTimeProvider`; Fixture-Helfer (`Spec`, `GitInit`, `Session`, `CompleteProbeAsync`) vorhanden und konsistent.
- `ScmEventOrchestratorTests.cs:110-159` (Unit-Tests): Trust-Wartegrund, `ConfirmReadyAsync`-Ablehnung während Dialog, automatische Zustellung nach Antwort, `NextAttemptUtc`-Sperraufhebung — über `ScmDispatchFixture` mit umschaltbarem Bildschirmpuffer (`SetPrompt`, `Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs:148-161`, in IntegrationTests per Compile-Link eingebunden).
- `ScmCliWorkspaceTrustTests.cs`: 7 Methoden (JSON-Merge mit Feld-Erhalt, Respektierung vorhandener `false`/`"untrusted"`-Entscheidungen, `.claude`-/`.codex`-Guards, ungültiges JSON unberührt, TOML-Idempotenz).

**Kriterium 3 – Wirksame UI-Aktionen bzw. Alternativaufforderung: erfüllt.**

- „Erneut prüfen" (`RetryAsync`, `ScmEventOrchestrator.cs:238-247`) löscht `NextAttemptUtc` und dispatchet; der zuvor permanent hängende Probe-Pfad ist jetzt durch den begrenzten Resend lösbar.
- „Öffnen / CLI auswählen" (`OpenFromQueueAsync`, Z. 58) setzt `NextAttemptUtc = null` vor dem Dispatch (Bestand).
- `ConfirmReadyAsync` hebt jetzt eine frühere `long.MaxValue`-Hartsperre auf (Z. 260-262) — genau der gemeldete wirkungslose Weg; Regressionstest `ConfirmReady_LiftsNextAttemptLock`.
- `ConfirmReady` prüft vor Annahme den Bildschirm erneut (`RefreshScreen`, Z. 228) und lehnt bei `Busy`/`TrustPrompt` ab (Z. 231). Der Bestätigen-Button wird im Trust-Wartezustand korrekt nicht angeboten (`ScmEventQueueViewModel.cs:295` verlangt den Grundtext „Eingabebereitschaft unbekannt", den der Trust-Grund bewusst nicht enthält) — der Wartegrund selbst ist die eindeutige, bedienbare Aufforderung (im Terminal antworten; optional „Erneut prüfen").

**Kriterium 4 – Prüfauftrag mit dokumentierter Empfehlung: erfüllt.**

- Dokumentierte, begründete Empfehlung je CLI inkl. Sicherheitsbewertung und verworfenen Alternativen (`--dangerously-skip-permissions`, `--yolo`) liegt im Umsetzungsplan (`plan.md`, Abschnitt „Ergebnis des Prüfauftrags (Empfehlung)", Commit `f021174`) vor; das Feature-Verzeichnis wurde konventionsgemäß am Lifecycle-Ende gelöscht, der Nachweis bleibt in der Git-Historie. Persistiert dokumentiert sind Mechanismus und Sicherheitsbegründung zusätzlich in `docs/help/plugins/github-ereignisse/troubleshooting.md` (zwei neue Abschnitte), `README.md` (SCM-Absatz) und den XML-Kommentaren von `ScmCliWorkspaceTrust.cs:9-17`.
- Umgesetzter sicherer Weg: `ScmCliWorkspaceTrust.EnsureTrusted` schreibt pfadgenau vor dem Start — Claude `projects.<dir>.hasTrustDialogAccepted` in `~/.claude.json` (JsonNode-Merge, atomare Datei, vorhandene Entscheidungen/Fremdfelder unberührt, ungültiges JSON übersprungen), Codex `[projects."<dir>"] trust_level = "trusted"` in `<CODEX_HOME|~/.codex>/config.toml` (Append, idempotent, `CODEX_HOME` aus Spec/Umgebung aufgelöst). Guard: Bei projektgebundener `.claude`-/`.codex`-Konfiguration auf dem Pfad Arbeitsverzeichnis→Git-Wurzel (`HasProjectConfiguration`, Z. 55-63) bleibt der manuelle Dialog — Trust aktiviert so keine ungeprüften Projekt-Hooks.
- Einordnung im Startpfad: `KiAusfuehrungsService.cs:239-245` ruft `EnsureTrusted` nach `GetTerminalStartSpecAsync` und vor `ScmCliAccessEvaluator.Capture` auf (best-effort, `LogWarning`, blockiert nie) — die `config.toml`-Änderung fließt dadurch in Fingerprint/Evaluierung ein.
- Evaluator-Whitelist (`ScmCliAccessEvaluator.cs:113-114, 137-142`): ausschließlich `[projects.*]`-Abschnitte mit einzigem Schlüssel `trust_level` ∈ {`trusted`, `untrusted`}; andere Abschnitte/Schlüssel/Werte → `Unknown` wie bisher. InlineData-Fälle (Z. 102-105 der Testdatei) belegen Akzeptanz und Ablehnung.

**Kriterium 5 – Bestandsschutz: erfüllt.**

- Kein Ereignisinhalt vor Kanalnachweis: `SendAccessProbeAsync` (Z. 213-220) sendet nur `BuildAccessProbePrompt(_accessProbe)`; der Ereignis-Prompt wird weiterhin erst nach `access.Status == Supported` über `adapter.WritePromptAsync(attempt.Prompt)` geschrieben (`ScmEventOrchestrator.cs:174-195`). Bestandstest `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` prüft, dass der Probe-Prompt `challengePath` enthält und kein `request`.
- Während `TrustPrompt` wird nie gesendet (Resend-Zweig erfordert `Ready`); `TrustPrompt_NeverReceivesProbeOrConfirmation` verifiziert `Times.Never`.
- Deduplizierte Zustellung unverändert: `ResendAsync`/`ReconcileAttemptAsync`/Outbox/Attempt-Korrelation im Diff nicht angefasst; `IsUnchanged`-Konfigurationswächter besteht fort. Die Whitelist-Erweiterung bewertet Trust-Einträge als rechte-neutral (Kommentar Z. 139).

### Einschränkungen des Nachweises

- Aggregierte Testlauf-Zahlen („Unit 1979/1980, Integration 125/125, format sauber") sind in keinem verbliebenen Artefakt belegt — `test-results.md`/`review-code.md` wurden mit dem Feature-Verzeichnis konventionsgemäß entfernt; verblieben ist `-tasks.md` mit testfallgenauen Nachweisen je Aufgabe. Die Testdateien wurden vollständig gelesen und sind plausibel konsistent (Konstruktorsignatur mit optionalem `TimeProvider`, vorhandene Fixture-Member, korrekte Moq-/FluentAssertions-Verwendung); ein eigener Build/Testlauf wurde dem Prüfauftrag gemäß nicht durchgeführt.
- Trust-Dialog-Erkennung ist bewusst auf drei gepinnte englische Dialogtexte beschränkt (Kommentar `ScmPromptSessionAdapter.cs:60`); davon abweichende Dialogformulierungen fallen auf das bisherige `Unknown`-Verhalten zurück — der Resend-Mechanismus deckt einen dabei verlorenen Probe-Versand dennoch ab, sobald ein echtes Promptzeichen sichtbar wird. Keine Abweichung vom Soll, dokumentierte Designentscheidung.
- `-tasks.md` vermerkt selbst transparent: Kein direkter Test des Startpfads `EnsureTrusted` vor `Capture` (Aufgabe 9) und kein separater `CODEX_HOME`-Test über `EnsureTrusted` (Aufgabe 12); die indirekte Absicherung über `EnsureCodexTrusted`-Pfadparameter und die Code-Stichprobe am Startpfad (`KiAusfuehrungsService.cs:236-252`) sind vorhanden.
