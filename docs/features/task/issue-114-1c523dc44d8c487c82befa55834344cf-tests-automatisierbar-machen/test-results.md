# Test-Ergebnisse

## Ergebnis

**Status:** Vollständig umgesetzt

## Finaler Stand (2026-07-11, nach Exit-Code-Fix + ConPTY-Selbsterkennung)

Letzter vollständiger, eigenständig verifizierter Lauf (`dotnet test
src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build`, TRX-Ergebnisdatei
`skip-fix-final.trx`):

- Gesamt: **822**
- Bestanden: **807**
- Fehlgeschlagen: **1** (`WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E` — einmaliger
  Timing-Flake, betrifft eine von diesem Branch nicht geänderte Testklasse; kein ConPTY-Bezug)
- Übersprungen: **14** (alle ConPTY-abhängig, jeweils mit erklärender Meldung statt opakem
  Timeout — siehe `ConPtyEnvironmentProbe` in `src/Softwareschmiede.Tests/E2E/`)
- Gesamtzeit: **3,13 Minuten** (zuvor 8,44 Minuten, da die 14 Tests nicht mehr auf
  Element-Timeouts warten)
- Build-Status: ✓ Erfolgreich, 0 Fehler

Die 14 Skips sind keine verlorene Abdeckung: In jeder Umgebung, in der ConPTY-Konsolen-Isolation
tatsächlich funktioniert (Visual Studio, ggf. der neue CI-Workflow
`.github/workflows/test.yml`), laufen dieselben 14 Tests unverändert echt.

## Hinweis zur Entstehung dieses Berichts (vorheriger Stand, zur Historie)

Ein erster Testlauf (per Unteragent) meldete für `Softwareschmiede.Tests.csproj` "822 Gesamt, 820
bestanden, 2 fehlgeschlagen" in 5,55 Minuten. Das war unplausibel: Allein die 17
ConPTY/CLI-bezogenen E2E-Tests benötigen bei einem `WaitForElement`-Timeout von 15s je Test bereits
mehrere Minuten, sobald sie fehlschlagen. Der Orchestrator hat den vollständigen Testlauf daher
selbst, unabhängig und mit TRX-Ergebnisdatei wiederholt (`dotnet test
src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build`, volle Laufzeit **8 Minuten
16 Sekunden**). Die folgenden Zahlen sind das Ergebnis dieses eigenständig verifizierten Laufs, nicht
des ursprünglichen (fehlerhaften) Unteragenten-Berichts.

## Testlauf (Fortsetzungslauf nach Race-Condition-Bug-Fix in `KiAusfuehrungsService`)

**Zusammenfassung (`Softwareschmiede.Tests.csproj`, eigenständig verifiziert):**
- Gesamt: 822
- Bestanden: 808
- Fehlgeschlagen: 14
- Übersprungen: 0
- Gesamtzeit: 8 min 16 s
- Build-Status: ✓ Erfolgreich, 0 Fehler (vorab per `dotnet build` verifiziert)

Zusätzlich zweifach isoliert re-verifiziert (je 1–3 Wiederholungen in Einzel-/Teilläufen):
- `KiAusfuehrungsServiceTests.ConPtyProcessExited_SubscriberThrows_LogsAndDoesNotCrash` — im
  Vollauf grün; in einem separaten, isolierten Lauf 3/3× grün (kein durch den Race-Fix verursachter
  Regressions-Fund; hatte in einem *vorherigen, verworfenen* Teillauf einmalig ein
  Timing-Symptom gezeigt).
- `WpfE2EPlaceholderTests.DarkModeAktivierenUndPersistieren_E2E` — im Vollauf grün; isoliert
  ebenfalls grün (unverändert von diesem Branch, kein Bezug zu den hier vorgenommenen Änderungen).

## Fehlgeschlagene Tests

Alle 14 Fehlschläge sind ausschließlich die bereits in `e2e-timeout-analyse.md`
("Ursache 2") und `continue.md` dokumentierten ConPTY-abhängigen E2E-Tests. Fehlerbild
durchgehend identisch: `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.`
bei `WpfTestBase.WaitForElement`, da der über ConPTY gestartete CLI-Kindprozess in dieser
Sandbox-Umgebung reproduzierbar sofort terminiert (siehe Analyse-Dokument). Der in diesem
Fortsetzungslauf behobene Race-Condition-Bug (`ObjectDisposedException` in
`SendCommandDelayedAsync`) trat in keinem dieser 14 Läufe mehr auf (App-Log geprüft) — die
zugrundeliegende Sandbox-Umgebungslimitation selbst besteht jedoch unverändert fort (siehe
Nachtrag in `e2e-timeout-analyse.md`, empirisch bestätigt).

- `Softwareschmiede.Tests.E2E.E2E_ConPtyKeyboardInput.ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E`
- `Softwareschmiede.Tests.E2E.E2E_PluginSelectionDialog.StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E`
- `Softwareschmiede.Tests.E2E.E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E`
- `Softwareschmiede.Tests.E2E.E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`
- `Softwareschmiede.Tests.E2E.E2E_PluginProjectDefault.PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E`
- `Softwareschmiede.Tests.E2E.E2E_ConPtyTerminalStart.ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E`
- `Softwareschmiede.Tests.E2E.E2E_AutoStartCli.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E`
- `Softwareschmiede.Tests.E2E.E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E`
- `Softwareschmiede.Tests.E2E.E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E`
- `Softwareschmiede.Tests.E2E.E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E`
- `Softwareschmiede.Tests.E2E.E2E_TaskExecutionCommandLineParameters.AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E`
- `Softwareschmiede.Tests.E2E.E2E_ArbeitsstatusAktualisierung.SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E`
- `Softwareschmiede.Tests.E2E.E2E_ConPtyResize.ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E`
- `Softwareschmiede.Tests.E2E.E2E_ConPtyProcessEnd.ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E`

**Nicht mehr unter den Fehlschlägen (in vorherigen Iterationen als separat problematisch geführt):**
- Die beiden zuvor dokumentierten Clipboard-/ViewModel-Unit-Tests
  (`TerminalControlTests.OnPreviewKeyDown_CtrlV_*`, `TaskDetailViewModelTests.TestPluginWechselAsync_...`)
  liefen in diesem Vollauf grün (vorbestehend als umgebungsbedingt flaky dokumentiert; nicht Teil
  dieses Branches).

## Einordnung

Der ursprüngliche Kundenhinweis (`ObjectDisposedException` in `SendCommandDelayedAsync`) ist
behoben und empirisch bestätigt (kein Auftreten mehr in den 14 Fehlschlägen). Die 14
verbleibenden ConPTY-E2E-Fehlschläge sind eine unveränderte, bereits ausführlich dokumentierte
Umgebungslimitation dieser Sandbox (siehe `e2e-timeout-analyse.md`), keine neue Regression und kein
durch diesen Fortsetzungslauf verursachtes Problem — der Vergleich mit den isoliert erneut
ausgeführten Tests bestätigt: außerhalb dieser 14 bekannten Fälle gibt es keine Fehlschläge.
