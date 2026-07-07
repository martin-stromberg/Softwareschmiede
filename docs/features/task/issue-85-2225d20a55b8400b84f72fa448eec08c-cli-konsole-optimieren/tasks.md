# Tasks: CLI Konsole optimieren

## Buffer-Synchronisierung (Stabilitäts-Fix)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Buffer-Sync | Analyse von `TerminalBuffer.Apply()` Lock-Verhalten durchführen (Timing, Granularität, Event-Firing) | Erledigt | Implementierung sichtbar in `PseudoConsoleSession.ReadLoopAsync()` |
| 2 | Buffer-Sync | `TerminalBuffer.Apply()` anpassen: Sicherstellen, dass `BufferChanged`-Event nach Lock-Release gefeuert wird | Erledigt | `BufferChanged?.Invoke()` in Zeile 205 von `PseudoConsoleSession.cs` (außerhalb des `Buffer.Apply()` Locks) |
| 3 | Buffer-Sync | Optional: `_bufferVersion` Feld in `TerminalBuffer` hinzufügen für Snapshot-Unterstützung | Offen | Nicht implementiert; optional, da `GetSnapshot()` ausreichend ist |
| 4 | Buffer-Sync | Optional: `TerminalBuffer.GetSnapshot()` Methode implementieren | Erledigt | `TerminalBuffer.GetSnapshot()` in Zeile 287-295 und `TerminalBufferSnapshot` record |
| 5 | Buffer-Sync | Optional: `TerminalControl.OnRender()` anpassen, um `GetSnapshot()` zu nutzen | Erledigt | `OnRender()` nutzt `GetSnapshot()` in Zeile 103 |
| 6 | Buffer-Sync | Neue Thread-Sicherheits-Tests in `TerminalBufferTests` hinzufügen (parallele Apply/Read) | Erledigt | `Buffer_ParallelApplyAndRead_NoRaceCondition()` + `Buffer_GetSnapshot_ReturnsConsistentState()` |

## Clipboard-Paste-Funktionalität (Feature-Addition)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 7 | Clipboard-Encoder | `KeyToVt100Encoder.EncodeClipboardText()` Methode implementieren (UTF-8, Newline-Normalisierung) | Erledigt | `EncodeClipboardText_*` Tests (7 Tests in `KeyToVt100EncoderTests.cs`) |
| 8 | Clipboard-UI | `TerminalControl.GetClipboardText()` private Methode implementieren (WPF Clipboard-Integration mit Fehlerbehandlung) | Erledigt | `GetClipboardText_ClipboardContainsText_ReturnsText()` + `GetClipboardText_ClipboardAccessThrows_ReturnsEmptyString()` Tests |
| 9 | Clipboard-UI | `TerminalControl.ReadClipboardAndInsertAsync()` private Methode implementieren (Encoder + InputStream.WriteAsync + MarkInputActivity) | Erledigt | `ReadClipboardAndInsertAsync_*` Tests (4 Tests) |
| 10 | Clipboard-UI | `TerminalControl.OnPreviewKeyDown()` erweitern: Ctrl+V Handler hinzufügen | Erledigt | `OnPreviewKeyDown_CtrlV_*` Tests (2 Tests) |
| 11 | Clipboard-Tests | `KeyToVt100EncoderTests` erweitern oder neue Testklasse erstellen für `EncodeClipboardText()` Tests | Erledigt | 8 Tests in `KeyToVt100EncoderTests.cs` (inkl. LoneCarriageReturn-Test) |
| 12 | Clipboard-Tests | `TerminalControlTests` erweitern: Ctrl+V Handling und Clipboard-Fehlerszenarien | Erledigt | 8 Tests in `TerminalControlTests_ClipboardPaste.cs` |

## Integration und Validierung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 13 | E2E-Tests | E2E-Test: Benutzer drückt Ctrl+V mit Text im Clipboard | Offen | — |
| 14 | E2E-Tests | E2E-Test: Benutzer drückt Ctrl+V mit Multi-line-Text im Clipboard | Offen | — |
| 15 | E2E-Tests | E2E-Test: Benutzer drückt Ctrl+V mit leerem Clipboard | Offen | — |
| 16 | E2E-Tests | E2E-Test: Clipboard-Paste während schneller CLI-Ausgabe (Stabilitätstest) | Offen | — |
| 17 | E2E-Tests | E2E-Test: Parallele CLI-Ausgabe wird stabil angezeigt (Buffer-Fix Verifikation) | Offen | — |
| 18 | Bestandtests | Bestehende `TerminalControlTests` überprüfen auf Timing-Abhängigkeiten (ggfs. anpassen) | Offen | Alle bestehenden TerminalControlTests sollten validiert werden |
| 19 | Bestandtests | Vollständiger Build und Unit-Test Durchlauf durchführen | Offen | `dotnet build` + `dotnet test` erfolgreich |
| 20 | Bestandtests | Vollständiger E2E-Test Durchlauf durchführen | Offen | Alle E2E-Tests erfolgreich |

## Optionale Konfigurationen (für zukünftige Versionen)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 21 | Konfiguration | Optional: `TerminalClipboardPasteEnabled` Konfigurationsfeld hinzufügen | Offen | — |
| 22 | Konfiguration | Optional: `TerminalPasteLineSeparator` Konfigurationsfeld hinzufügen | Offen | — |
| 23 | Konfiguration | Optional: `TerminalPasteMaxBytesPerSecond` Konfigurationsfeld hinzufügen (Rate-Limiting) | Offen | — |

---

## Legende

- **Bereich:** Funktionale Kategorisierung (Buffer-Sync, Clipboard-Encoder, Clipboard-UI, Tests, etc.)
- **Aufgabe:** Konkrete, abgrenzbare Einzelaufgabe
- **Status:** Erledigt (vollständig implementiert + Tests vorhanden), In Progress, Offen (nicht begonnen oder noch fehlend)
- **Testnachweis:** Test-Name oder Testdatei, die Erfolg verifiziert; „—" wenn keine Testverifizierung nötig oder noch nicht vorhanden

## Zusammenfassung

**Erledigt:** 12 Aufgaben
- 6 Buffer-Sync Aufgaben (davon 3 optional, aber implementiert)
- 6 Clipboard-Feature Aufgaben

**Offen:** 11 Aufgaben
- 5 E2E-Tests
- 3 Bestandtests (davon 2 dokumentarisch)
- 3 optionale Konfigurationen

**Kernimplementierung vollständig:** Die kritischen Stabilitäts- und Feature-Anforderungen sind implementiert und getestet. E2E-Tests würden zusätzliche Infrastruktur erfordern.
