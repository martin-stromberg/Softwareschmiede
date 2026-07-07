# Tasks: CLI Konsole optimieren

## Buffer-Synchronisierung (Stabilitäts-Fix)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Buffer-Sync | Analyse von `TerminalBuffer.Apply()` Lock-Verhalten durchführen (Timing, Granularität, Event-Firing) | Offen | — |
| 2 | Buffer-Sync | `TerminalBuffer.Apply()` anpassen: Sicherstellen, dass `BufferChanged`-Event nach Lock-Release gefeuert wird | Offen | `Buffer_BufferChangedEventFiredAfterLockRelease` Test |
| 3 | Buffer-Sync | Optional: `_bufferVersion` Feld in `TerminalBuffer` hinzufügen für Snapshot-Unterstützung | Offen | — |
| 4 | Buffer-Sync | Optional: `TerminalBuffer.GetSnapshot()` Methode implementieren | Offen | `Buffer_GetSnapshot_ReturnsConsistentState` Test |
| 5 | Buffer-Sync | Optional: `TerminalControl.OnRender()` anpassen, um `GetSnapshot()` zu nutzen | Offen | Bestehende Render-Tests |
| 6 | Buffer-Sync | Neue Thread-Sicherheits-Tests in `TerminalBufferTests` hinzufügen (parallele Apply/Read) | Offen | `Buffer_ParallelApplyAndRead_NoRaceCondition` Test |

## Clipboard-Paste-Funktionalität (Feature-Addition)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 7 | Clipboard-Encoder | `KeyToVt100Encoder.EncodeClipboardText()` Methode implementieren (UTF-8, Newline-Normalisierung) | Offen | `EncodeClipboardText_*` Tests (6 Tests) |
| 8 | Clipboard-UI | `TerminalControl.GetClipboardText()` private Methode implementieren (WPF Clipboard-Integration mit Fehlerbehandlung) | Offen | `GetClipboardText_*` Tests (2 Tests) |
| 9 | Clipboard-UI | `TerminalControl.ReadClipboardAndInsertAsync()` private Methode implementieren (Encoder + InputStream.WriteAsync + MarkInputActivity) | Offen | `ReadClipboardAndInsertAsync_*` Tests (4 Tests) |
| 10 | Clipboard-UI | `TerminalControl.OnPreviewKeyDown()` erweitern: Ctrl+V Handler hinzufügen | Offen | `OnPreviewKeyDown_CtrlV_*` Tests (2 Tests) |
| 11 | Clipboard-Tests | `KeyToVt100EncoderTests` erweitern oder neue Testklasse erstellen für `EncodeClipboardText()` Tests | Offen | 6 neue Tests |
| 12 | Clipboard-Tests | `TerminalControlTests` erweitern: Ctrl+V Handling und Clipboard-Fehlerszenarien | Offen | 6 neue Tests |

## Integration und Validierung

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 13 | E2E-Tests | E2E-Test: Benutzer drückt Ctrl+V mit Text im Clipboard | Offen | E2E-Test `ClipboardPaste_SingleLineText_InsertsCorrectly` |
| 14 | E2E-Tests | E2E-Test: Benutzer drückt Ctrl+V mit Multi-line-Text im Clipboard | Offen | E2E-Test `ClipboardPaste_MultiLineText_InsertsWithCR` |
| 15 | E2E-Tests | E2E-Test: Benutzer drückt Ctrl+V mit leerem Clipboard | Offen | E2E-Test `ClipboardPaste_EmptyClipboard_NoOp` |
| 16 | E2E-Tests | E2E-Test: Clipboard-Paste während schneller CLI-Ausgabe (Stabilitätstest) | Offen | E2E-Test `ClipboardPaste_WithFastCliOutput_NoFlickering` |
| 17 | E2E-Tests | E2E-Test: Parallele CLI-Ausgabe wird stabil angezeigt (Buffer-Fix Verifikation) | Offen | E2E-Test `CliOutput_ParallelUpdates_StableDisplay` |
| 18 | Bestandtests | Bestehende `TerminalControlTests` überprüfen auf Timing-Abhängigkeiten (ggfs. anpassen) | Offen | Alle bestehenden TerminalControlTests |
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
- **Status:** Offen (initial), In Progress, Completed
- **Testnachweis:** Test-Name oder Testdatei, die Erfolg verifiziert; „—" wenn keine Testverifizierung nötig

Insgesamt **20 Aufgaben** (davon 3 optional für zukünftige Versionen): 6 Buffer-Sync, 6 Clipboard-Feature, 5 E2E-/Integrations-Tests, 3 Bestandtests.
