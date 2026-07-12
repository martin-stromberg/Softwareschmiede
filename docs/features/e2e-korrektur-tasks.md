# Tasks: ConPTY-E2E-Tests von echtem Kindprozess entkoppeln

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `IPseudoConsoleHandle`-Interface anlegen (`src/Softwareschmiede/Infrastructure/Terminal/`) | Offen | — |
| 2 | Logik | `NullPseudoConsoleHandle`-Klasse (No-Op-Implementierung, `Instance`-Singleton) anlegen | Offen | — |
| 3 | Logik | `PseudoConsole` um `: IPseudoConsoleHandle` erweitern | Offen | — |
| 4 | Logik | `PseudoConsoleSession`-Konstruktoren auf `IPseudoConsoleHandle` statt `PseudoConsole` umstellen | Offen | — |
| 5 | Logik | `IPseudoConsoleProcessLauncher`-Interface anlegen | Offen | — |
| 6 | Logik | `Win32PseudoConsoleProcessLauncher` anlegen (bisherige `StartPseudoConsoleProcess`-Logik verschieben) | Offen | — |
| 7 | Logik | `SimulatedPseudoConsoleProcessLauncher` anlegen (Process.Start mit umgeleiteten Streams) | Offen | — |
| 8 | Logik | `KiAusfuehrungsService`-Konstruktor um optionalen `IPseudoConsoleProcessLauncher? launcher = null`-Parameter erweitern | Offen | — |
| 9 | Logik | `KiAusfuehrungsService.StartPseudoConsoleProcess`-Aufruf durch Delegation an `_launcher.Start(...)` ersetzen | Offen | — |
| 10 | Konfiguration | `App.xaml.cs`: bedingte DI-Registrierung von `IPseudoConsoleProcessLauncher` (Testmodus-Erkennung analog `PluginManager.IsTestMode()`) | Offen | — |
| 11 | Tests | `SimulatedPseudoConsoleProcessLauncherTests.Start_LiefertLaufendenProzessUndSession` schreiben | Offen | — |
| 12 | Tests | `SimulatedPseudoConsoleProcessLauncherTests.Start_GesendetesKommandoWirdAusgefuehrt` schreiben | Offen | — |
| 13 | Tests | `SimulatedPseudoConsoleProcessLauncherTests.Start_ProzessBeendetSichAufKillEntireProcessTree` schreiben | Offen | — |
| 14 | Tests | `KiAusfuehrungsServiceTests.StartWithPseudoConsoleAsync_MitInjiziertemFakeLauncher_ErreichtGestartet` schreiben | Offen | — |
| 15 | E2E-Tests | 14 betroffene E2E-Tests unter `dotnet test` ohne `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS` verifizieren | Offen | — |
| 16 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_WorkingDirectory.cs` entfernen | Offen | — |
| 17 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_TaskWechselUeberMenue.cs` entfernen | Offen | — |
| 18 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_TaskExecutionCommandLineParameters.cs` entfernen | Offen | — |
| 19 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_PluginWechsel.cs` entfernen | Offen | — |
| 20 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_PluginSelectionDialog.cs` entfernen | Offen | — |
| 21 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_PluginProjectDefault.cs` entfernen | Offen | — |
| 22 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_PluginProjectDefault_NextTask.cs` entfernen | Offen | — |
| 23 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_ConPtyTerminalStart.cs` entfernen | Offen | — |
| 24 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_ConPtyResize.cs` entfernen | Offen | — |
| 25 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_ConPtyProcessEnd.cs` entfernen | Offen | — |
| 26 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_ConPtyKeyboardInput.cs` entfernen | Offen | — |
| 27 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_AutoStartCli.cs` entfernen | Offen | — |
| 28 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_AufgabeStarten.cs` entfernen | Offen | — |
| 29 | E2E-Tests | `SkipWennConPtyNichtVerfuegbar()`-Aufruf aus `E2E_ArbeitsstatusAktualisierung.cs` entfernen | Offen | — |
