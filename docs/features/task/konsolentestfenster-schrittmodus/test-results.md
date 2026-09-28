# Test-Ergebnisse

Testrunde 3 — Konsolentestfenster Schrittmodus, nach Iteration-3-Nacharbeiten (`_schleifeBeendenAngefordert`-Terminations-Flag, `WendeChunkAnUnterLock`-Extraktion, `WarteBisAsync`-Timeout-Erhöhung, neuer Regressionstest `SchrittZurueck_ImExitedHandler_TerminiertAufgeweckteSchleife`). Ausführung mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` (Sandbox-Vorgabe), alle Läufe synchron/foreground, voller `dotnet build Softwareschmiede.slnx` vorausgehend (0 Fehler, 0 Warnungen).

## Ergebnis

**Status:** Keine Fehler

Alle vier Lanes waren im ersten Durchlauf vollständig grün — keine transienten Einzelfehler in dieser Runde. Insbesondere ist `KonsolenTestViewModelTests.Wiedergabe_PausierenUndFortsetzen` (in Runde 2 einmaliger `WarteBisAsync`-Deadline-Flake) nach der Timeout-Erhöhung bestanden; `RunGeneralTests` mit dem erweiterten Konsolentestfenster-Szenario ist grün.

## Fehlgeschlagene Tests

Keine.

## E2E-Abdeckung

Alle geplanten Pflicht-Szenarien laufen im konsolidierten FlaUI-Szenario `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (Aufruf in `End2EndTest.RunGeneralTests`, `src/Softwareschmiede.Tests/E2E/MainTest.cs:36`, Kategorie `[OsInterface]`). `RunGeneralTests` wurde in der OsInterface-Lane ausgeführt und ist bestanden (8 m 29 s; Gesamtlane 9 min 58 s) — damit sind alle enthaltenen Schritt-Phasen bestanden.

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Laden → 2× `SchrittVor` → „Chunk 2/3" + Quell-Selektion → `SchrittZurueck` → „Chunk 1/3"/„Chunk 0/3" + leere Selektion + `SchrittZurueck` deaktiviert + `Neu starten` aus dem Schrittmodus erreichbar | Schrittmodus-Phase 1 in `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (`E2E_KonsolenTestfenster.cs`) | Bestanden |
| `SchrittVor` bis Ende → „Wiedergabe beendet." + `SchrittVor` deaktiviert → `SchrittZurueck` → „Chunk 2/3" → `StartWiedergabe` (Zeitraffer 0) setzt an Position fort → erneut „beendet" | Schrittmodus-Phase 2 dto. | Bestanden |
| `pausePfad` (3-s-Pause): `StartWiedergabe` → „Chunk 1/2" → `PausierenToggle` → `SchrittZurueck` → „Chunk 0/2" → Fortsetzen → Position läuft erneut über „Chunk 1/2" bis „Wiedergabe beendet." (inkl. Assert: Schritt-Buttons während unpausierter Wiedergabe deaktiviert) | Schrittmodus-Phase 3 dto. | Bestanden |

## Zusammenfassung

- Gesamt: 2009
- Bestanden: 2006
- Fehlgeschlagen: 0
- Übersprungen: 3

| Lane | Befehl | Gesamt | Bestanden | Übersprungen | Fehlgeschlagen |
|------|--------|--------|-----------|--------------|----------------|
| Build | `dotnet build Softwareschmiede.slnx` | — | — | — | 0 Fehler, 0 Warnungen |
| Tests (ohne OsInterface) | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | 1876 | 1875 | 1 | 0 (1 min 21 s) |
| Tests (OsInterface) | `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category=OsInterface"` | 55 | 53 | 2 | 0 (9 min 58 s) |
| IntegrationTests (ohne OsInterface) | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category!=OsInterface"` | 69 | 69 | 0 | 0 |
| IntegrationTests (OsInterface) | `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "Category=OsInterface"` | 9 | 9 | 0 | 0 |

### Übersprungene Tests (keine Fehler)

- `ArbeitsverzeichnisOeffnenServiceTests.Oeffne_AufNichtWindows_WirftPlatformNotSupportedException` — plattformbedingt übersprungen (Test gilt nur für Nicht-Windows; Lauf erfolgte unter Windows).
- `End2EndTest.RunConPtyTests` — ConPTY-gebunden, per `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` übersprungen: **nicht ausführbar in dieser Sandbox** (bestätigte Sandbox-Limitation laut CLAUDE.md, kein Feature-Bezug).
- `E2E_RepositoryInitialisierungAusfuehrungTests.InitialisierungsskriptAusfuehrung` — ConPTY-gebunden, gleiche Begründung: **nicht ausführbar in dieser Sandbox**.

### Feature-relevante Tests (alle bestanden)

Session-Ebene (`TerminalReplaySessionTests`): `SchrittVor_WendetNaechstenChunkZeitunabhaengigAn`, `SchrittVor_FeuertOutputChunkUndBufferChanged`, `SchrittVor_AmEnde_FeuertExited_IstDannNoOp`, `SchrittZurueck_BautPraefixDeterministischNeuAuf`, `SchrittZurueck_SetztParserZustandZurueck`, `SchrittZurueck_BeiPosition0_IstNoOp`, `Pausiert_Schritte_Fortsetzen_SetztAnSchrittpositionFort`, `SchrittVor_BisEndeBeiPausierterSchleife_TerminiertSauber`, `WiedergabeStarten_NachEndeUndSchrittZurueck_SetztAnPositionFort`, `Schritte_NachDispose_SindNoOp`, `SchrittZurueck_ImExitedHandler_TerminiertAufgeweckteSchleife` (neu in Iteration 3 — Regressionstest für das `_schleifeBeendenAngefordert`-Terminations-Flag).

ViewModel-Ebene (`KonsolenTestViewModelTests`): `SchrittVor_SchrittZurueck_SynchronisierenPositionUndQuellAuswahl`, `SchrittCommands_CanExecute_NachZustand`, `SchrittCommands_Execute_WaehrendLaufenderWiedergabe_SindNoOp`, `WiedergabeNeustarten_AusSchrittmodus_SpieltAbPosition0`, `SchrittVor_BisEnde_ZeigtStatusBeendet`, `Pausiert_Schritt_Fortsetzen_ViewModelEbene` (sowie die erweiterte `Commands_OhneAufzeichnung_SindInert`). Der in Runde 2 transient geflake-te `Wiedergabe_PausierenUndFortsetzen` ist nach der `WarteBisAsync`-Timeout-Erhöhung bestanden.

## Testabdeckung

**Abdeckung:** Nicht messbar

Die Lanes wurden exakt gemäß Vorgabe ohne Coverage-Collector ausgeführt; es liegen keine Coverage-Daten vor.

## Fehlende Tests

Quelle: `Dateinamen-Konvention` (Fallback, auf die vom Feature berührten Dateien begrenzt)

Keine — alle berührten Quelldateien haben korrespondierende Tests:

- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` → `TerminalReplaySessionTests` (11 Schrittmodus-Tests inkl. neuem Regressionstest, alle bestanden; Bestandstests als Regressionsschutz unverändert grün)
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs` → `KonsolenTestViewModelTests` (6 Schrittmodus-Tests + erweiterte `Commands_OhneAufzeichnung_SindInert`, alle bestanden)
- `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs` → nur Dokumentationskommentar geändert (1-basierte `#`-Zählung), indirekt über die Positions-/Selektions-Asserts der ViewModel- und E2E-Tests abgedeckt
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml` (WrapPanel-Toolbar mit Buttons `SchrittZurueck`/`SchrittVor`) → über das erweiterte E2E-Szenario abgedeckt
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs` → Test-Infrastruktur, selbst Teil des E2E-Laufs
