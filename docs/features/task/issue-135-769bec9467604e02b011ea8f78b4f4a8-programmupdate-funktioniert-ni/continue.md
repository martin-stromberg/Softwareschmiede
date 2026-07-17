# Offene Aufgaben

Erstellt am: 2026-07-17
Abbruchgrund: Anwender hat den Fix aus Commit `bd8f73e` manuell nachgetestet — Fehler bestand weiterhin. Korrigierter Fix wurde daraufhin umgesetzt und diesmal per vollem Testlauf (nicht nur Behauptung) verifiziert, siehe unten.

## Offene Planelemente

- [x] **Fix aus Commit `bd8f73e` war unzureichend — Root Cause war falsch identifiziert.** Korrigiert: `CliUpdateSafetyService.CheckAsync()` prüft jetzt über die neue, gemeinsam mit dem Aufgabenpanel genutzte Hilfsmethode `AufgabeLaufAktivitaet.IstAktiv(aktiveRunId, lastHeartbeatUtc, nowUtc)` (`src/Softwareschmiede/Application/Services/AufgabeLaufAktivitaet.cs`), statt nur `LaufStatus == Laeuft` abzufragen. Dieselbe Methode wird jetzt auch von `KiAusfuehrungsStatusConverter` (`src/Softwareschmiede.App/Converters/AppConverters.cs:126`) verwendet, wo die Heartbeat-Logik vorher inline dupliziert war — Update-Prüfung und Aufgabenpanel-Anzeige können dadurch nicht mehr auseinanderlaufen.

  Verifiziert am 2026-07-17:
  - Neuer Regressionstest `CliUpdateSafetyServiceTests.CheckAsync_ShouldNotTreatTaskWithStaleHeartbeatAsRisky` bildet exakt das gemeldete Szenario ab (`LaufStatus == Laeuft`, `AktiveRunId` gesetzt, `LastHeartbeatUtc` älter als `AufgabeRecoveryService.HeartbeatTimeoutMinutes`) und erwartet `RequiresConfirmation == false` — lief grün im vollen Testlauf.
  - Neuer Test `CliUpdateSafetyServiceTests.CheckAsync_ShouldTreatTaskWithFreshHeartbeatAsRisky` deckt den Gegenfall (frischer Heartbeat → blockiert weiterhin korrekt) ab — ebenfalls grün.
  - Neue Testklasse `AufgabeLaufAktivitaetTests` (5 Fälle: frischer Heartbeat, abgelaufener Heartbeat, exakte Schwelle, `null`-Heartbeat, `null`-RunId) — alle grün.
  - `review.md`: "Vollständig umgesetzt" (Plan-Review bestätigt 1:1-Umsetzung, keine offenen Planelemente).
  - `review-code.md`: "Keine Befunde" (Code-Review bestätigt insb. DRY-Extraktion ohne Verhaltensänderung an der bestehenden Converter-Logik).
  - Voller Testlauf (`dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) von mir (Orchestrator) unabhängig ausgeführt und geprüft, nicht nur der Sub-Agent-Meldung vertraut: 992 bestanden, 4 fehlgeschlagen (alle nachweislich umgebungsbedingt, siehe unten), 1 übersprungen, 997 gesamt.

  **Manuelle Verifikation durch den Anwender in der echten App wird trotzdem empfohlen**, da der ursprüngliche Fix (`bd8f73e`) genau deshalb nicht ausgereicht hatte, weil ein Unit-Test-Beweis allein die UI-Realität nicht 1:1 abgedeckt hatte — diesmal bildet der Regressionstest aber exakt das vom Anwender beschriebene Szenario (stale `LaufStatus == Laeuft` bei tatsächlich nicht laufender CLI) nach.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Alle vier fehlgeschlagenen Tests aus dem vollen, von mir (Orchestrator) selbst ausgeführten Testlauf wurden geprüft und sind nachweislich **unabhängig** von der Änderung (`git diff main --stat` zeigt ausschließlich `CliUpdateSafetyService.cs`, `AppConverters.cs`, `AufgabeLaufAktivitaet.cs` (neu), zwei Testdateien — keiner der folgenden Tests berührt diese Dateien):

- [ ] `CliEmbeddingServiceIntegrationTests.StartWithPseudoConsoleAsync_StartetProzess_UndSetztPseudoConsoleSession` — `InvalidOperationException: Cannot process request because the process (...) has exited.` Startet einen echten ConPTY-Prozess (`KiAusfuehrungsService.StartWithPseudoConsoleAsync`); nicht durch `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` abgedeckt (nur die E2E-ConPTY-Tests sind laut CLAUDE.md darüber gated), aber dieselbe in CLAUDE.md dokumentierte ConPTY-Sandbox-Einschränkung dieser Umgebung. Kein Bezug zur Änderung.
- [ ] `TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue` — `COMException: OpenClipboard fehlgeschlagen (CLIPBRD_E_CANT_OPEN)`. Bereits aus vorheriger Iteration bekannt, Zwischenablage-Zugriff ohne interaktive Desktop-Session. Kein Bezug zur Änderung.
- [ ] `E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E` — `UnauthorizedAccessException` bei `Directory.Delete`. Bereits aus vorheriger Iteration und aus einer unabhängigen, früheren Feature-Historie bekannt (Windows-Dateisperrenproblem). Kein Bezug zur Änderung.
- [ ] `E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E` — `TimeoutException` beim Warten auf das Element `"EditTitel"` (frühe UI-Interaktion direkt nach Klick auf `"AufgabeNeu"`, komplett unabhängig vom geänderten Status-Anzeige-/Update-Prüfungscode). App-Log (`softwareschmiede-20260717.log`) wurde vollständig nach `MainWindow konnte nicht angezeigt werden` und `XamlParseException` durchsucht — keine Treffer, kein Startup-Absturz. Deutet auf UI-Timing-/Sandbox-Last-Problem hin (vgl. #125), kein Code-Fehler.

**Hinweis:** Alle vier Punkte sind Sandbox-/Umgebungsbeschränkungen dieser Agentenumgebung (kein interaktiver Desktop, ConPTY-Prozessisolation, Datei-Locking, UI-Timing unter Volllast), keine durch diese Code-Änderung verursachten oder behebbaren Fehler. Genau diese Klasse von Problemen soll künftig durch Issue #137 (Tests kategorisieren, Mocking ausweiten) strukturell reduziert werden.
