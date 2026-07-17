# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden (alle vier nachweislich umgebungsbedingt, siehe `continue.md`)

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.ServiceIntegration

- **CliEmbeddingServiceIntegrationTests.StartWithPseudoConsoleAsync_StartetProzess_UndSetztPseudoConsoleSession** — System.InvalidOperationException: Cannot process request because the process has exited.

### Softwareschmiede.Tests.App.Controls

- **TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue** — System.Runtime.InteropServices.COMException: OpenClipboard fehlgeschlagen (0x800401D0 (CLIPBRD_E_CANT_OPEN))

### Softwareschmiede.Tests.E2E

- **E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E** — System.TimeoutException: Element "EditTitel" wurde nicht innerhalb von 20s gefunden.
- **E2E_WorkingDirectory.RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E** — System.UnauthorizedAccessException: Access to the path denied (Directory.Delete).

## Zusammenfassung

- Gesamt: 997
- Bestanden: 992
- Fehlgeschlagen: 4
- Übersprungen: 1

Kommando: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug`, Dauer 6 m 30 s.

Alle vier Fehlschläge sind gemäß Analyse in `continue.md` unabhängig von den in dieser Anforderung geänderten Dateien (`CliUpdateSafetyService.cs`, `AppConverters.cs`, `AufgabeLaufAktivitaet.cs`, zugehörige Tests) und stammen aus bekannten Sandbox-/Umgebungseinschränkungen (ConPTY-Isolation, Zwischenablage ohne interaktiven Desktop, Datei-Locking, UI-Timing unter Volllast).
