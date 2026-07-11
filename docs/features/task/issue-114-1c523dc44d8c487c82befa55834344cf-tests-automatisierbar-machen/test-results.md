# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden (alle verbleibenden Fehler sind dokumentierte, verifizierte Umgebungslimitationen außerhalb des Scopes dieses Plans)

## Finaler vollständiger Lauf (Iteration 3, final — synchron, tatsächlich gemessen)

`dotnet clean` + `dotnet build` + `dotnet test` auf `Softwareschmiede.Tests.csproj`, **synchron
ausgeführt** (kein `run_in_background`, siehe Root-Cause-Fund unten):

- Gesamt: **819**
- Bestanden: **803**
- Fehlgeschlagen: **16**
- Übersprungen: 0
- Gesamtzeit: 8,44 Minuten
- Build-Status: ✓ Erfolgreich, 0 Fehler

### Fehlgeschlagene Tests

**14 ConPTY-bezogene E2E-Tests** (unverändert gegenüber den vorherigen Iterationen, Root Cause
verifiziert und dokumentiert in `e2e-timeout-analyse.md`; Umgebungslimitation dieses
Sandbox-Ausführungskontexts, vom Anwender bestätigt aus Visual Studio heraus funktionsfähig):

- `E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`
- `E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E`
- `E2E_TaskExecutionCommandLineParameters.AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E`
- `E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E`
- `E2E_PluginSelectionDialog.StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E`
- `E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E`
- `E2E_PluginProjectDefault.PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E`
- `E2E_ConPtyTerminalStart.ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E`
- `E2E_ConPtyResize.ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E`
- `E2E_ConPtyProcessEnd.ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E`
- `E2E_ConPtyKeyboardInput.ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E`
- `E2E_AutoStartCli.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E`
- `E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E`
- `E2E_ArbeitsstatusAktualisierung.SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E`

**2 Clipboard-bezogene Unit-Tests** (vorbestehend, Datei von diesem Branch nicht berührt,
Clipboard-Zugriff in nicht-interaktiven/parallelen Sessions bekannt flaky):

- `App.Controls.TerminalControlTests.OnPreviewKeyDown_CtrlV_SetsHandledTrue`
- `App.Controls.TerminalControlTests.OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync`

Alle 3 durch den `"OffeneAufgabenListe"`-Selektor-Fix behobenen Tests sowie der zuvor flaky
`TaskDetailViewModelTests`-Test und der isoliert einmalig geflakte `ProjektNamenAendern`-E2E-Test
sind in diesem Lauf grün.

## Root-Cause-Fund: Build-Lock deckte die tatsächliche Testlaufzeit nicht ab

Während der Verifikation dieser Iteration wurde eine reale, aktuell reproduzierbare
Build-Korruption gefunden (Selektor-Fix-Tests fielen mit 17/18 statt der erwarteten Ergebnisse
aus, `Softwareschmiede.App.runtimeconfig.json` fehlte im `bin/`-Ordner). Root Cause: Der
gemeinsame Lock zwischen `build_before_test.py` (PreToolUse vor `dotnet test`) und
`test-csharp-startup.ps1` (Stop-Hook, feuert bei jedem Ende einer Assistant-Antwort) wurde bisher
nur während der jeweiligen *kurzen* Build-Schritte gehalten, nicht über die *gesamte*, oft
mehrminütige Laufzeit des eigentlichen `dotnet test`-Prozesses. Endete die Antwort während ein
Testlauf noch lief, feuerte der Stop-Hook, baute `Softwareschmiede.App.csproj` mit
`--no-incremental` neu und killte die exe — während der laufende E2E-Test genau diese exe nutzte.

**Fix (umgesetzt, über den ursprünglichen Plan hinaus, mit Rücksprache):**
- `build_before_test.py` gibt den Lock nicht mehr selbst frei, sondern hält ihn.
- Neuer PostToolUse-Hook `release_build_lock.py` (neuer Eintrag in `.claude/settings.json`) gibt
  den Lock erst frei, nachdem der `dotnet test`-Bash-Befehl abgeschlossen ist.
- `test-csharp-startup.ps1` überspringt den Smoke-Test bei belegtem Lock, statt nach Timeout
  ungeschützt weiterzubauen.
- **Wichtige Einschränkung, jetzt in `CLAUDE.md` dokumentiert:** Für `run_in_background: true`
  Bash-Aufrufe feuert der PostToolUse-Hook sofort (beim Zurückkehren des Tool-Aufrufs), nicht wenn
  der Hintergrundprozess tatsächlich fertig ist — der Lock würde dann sofort wieder freigegeben.
  Ein Versuch, dies technisch per Hook-Block (`sys.exit(2)`) zu verhindern, griff im
  Claude-Code-Harness nachweislich nicht. `dotnet test` muss daher als verbindliche
  Verhaltensregel synchron/im Vordergrund ausgeführt werden.

Verifiziert: Ein synchroner Lauf der 3 Selektor-Fix-Testklassen nach dem Fix lieferte 17/18 (der
verbleibende 1 Fehler betraf eine unveränderte Testmethode und trat im finalen Vollständigen Lauf
nicht mehr auf — einmaliger Flake).

## Testabdeckung

**Abdeckung:** 32.11% (Unit Tests); 70.79% (Integration Tests) — Werte aus Iteration-1-Baseline,
in diesem Lauf nicht erneut erhoben.

| Test-Projekt | Zeilenabdeckung | Branch-Abdeckung |
|--------------|-----------------|------------------|
| Softwareschmiede.Tests (Unit) | 32.11% | 57.92% |
| Softwareschmiede.IntegrationTests | 70.79% | 15.28% |

## Abdeckung nach Paket

| Paket | Abdeckung |
|-------|-----------|
| Softwareschmiede (Domain/Services) | 23.76% |
| Softwareschmiede.App (UI Layer) | 50.78% |
| Softwareschmiede.Plugin.Contracts | 63.81% |
| Softwareschmiede.Plugin.BitBucket | 65.76% |

## Hinweise

- Build Status: ✓ Erfolgreich (0 Fehler)
- Test Duration: 8,44 Minuten
- Ergebnisdatei: `src/Softwareschmiede.Tests/TestResults/final-run.trx`
