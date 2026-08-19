# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine. Beide Lanes liefen vollständig fehlerfrei durch — auch ohne Isolationslauf (anders als beim
vorherigen Testlauf dieses Features, bei dem `PseudoConsoleSessionTests.ReadLoopAsync_...` einmalig
timing-bedingt fehlschlug und isoliert erneut verifiziert werden musste; diesmal trat dieser Fehlschlag
gar nicht erst auf).

## Zusammenfassung

- Gesamt: 1328 (Lane 1: 1284, Lane 2/OsInterface: 44)
- Bestanden: 1326 (Lane 1: 1283, Lane 2: 43)
- Fehlgeschlagen: 0
- Übersprungen: 2 (Lane 1: 1, Lane 2: 1 — `RunConPtyTests`, erwartet durch `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`)

Vergleich zum letzten vollen Testlauf (1328 gesamt / 1325 bestanden / 0 fehlgeschlagen (1 als Flakiness
verifiziert) / 2 übersprungen, vor Iteration 2): identische Gesamtzahl (1328) — deckungsgleich mit der
Erwartung aus dem Auftragskontext, dass Iteration 2 nur Refactoring ohne neue Tests enthielt
(`GetOrderedEnabledIdePluginsAsync`-Extraktion, `effectiveWorkdir`-Parameterübergabe, Fehler-Isolierung in
der Aggregationsschleife, `CreateTestIdePluginMock`-Testhelper-Extraktion, Doc-Kommentar). +1 bestanden
gegenüber dem letzten Lauf, da der zuvor als Flakiness verifizierte Test diesmal direkt sauber durchlief.

## Testabdeckung

**Abdeckung:** 35,6 % (gesamtes instrumentiertes Projekt, 13407/37646 Zeilen; Basis: gemergte Cobertura-Reports beider Lanes)

Die Abdeckungszahl ist durch generierten Code (XAML `.g.cs`, EF-Core-Migrations), WPF-View-Code-Behind
(nur per FlaUI/E2E, nicht per Unit-Test erreichbar) und reine XAML-Markup-Dateien nach unten gezogen —
siehe "Fehlende Tests" für die Kategorisierung. Weitgehend deckungsgleich mit dem letzten Testlauf (35,6 %
vs. 35,6 % zuvor), wie bei einem reinen Refactoring-Zyklus ohne neue Tests zu erwarten.

### Feature-relevante Dateien

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | 76,2 % (937/1229 Zeilen) |
| `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` | 97,5 % (156/160 Zeilen) — nicht niedrig, zur Vollständigkeit genannt |

Beide Werte praktisch unverändert gegenüber dem letzten Lauf (`TaskDetailViewModel.cs`: 76,4 % / 932/1220
→ 76,2 % / 937/1229; `PluginSelectionService.cs`: 97,5 % / 155/159 → 97,5 % / 156/160). Die geringfügige
Zeilenzunahme entspricht der `GetOrderedEnabledIdePluginsAsync`-Extraktion aus Iteration 2; keine
Verschlechterung der Abdeckung durch das Refactoring.

### Übrige Dateien unter 80 % (Auszug, nicht feature-relevant)

| Datei | Abdeckung |
|-------|-----------|
| `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs` | 54,4 % |
| `src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs` | 31,1 % |
| `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` | 69,4 % |
| `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs` | 72,7 % |
| `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs` | 60,5 % |
| `src/Softwareschmiede/Application/Services/DiffService.cs` | 61,5 % |
| `src/Softwareschmiede/Application/Services/PullRequestMonitoringService.cs` | 75,6 % |
| `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` | 78,8 % |
| `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` | 79,3 % |
| `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` | 79,8 % |

(Vollständige Liste über 60 weitere Dateien zwischen 0 % und 80 % liegt in den Rohdaten unter
`src/Softwareschmiede.Tests/TestResults/*/coverage.cobertura.xml` vor; hier nur Auszug relevanter Größenordnung.)

## Fehlende Tests

Quelle: `Coverage-Daten` (gemergter Cobertura-Report beider Lanes, 314 instrumentierte Dateien gesamt, 137 davon mit 0 % Zeilenabdeckung)

Kategorische Zusammenfassung der 137 Dateien mit 0 % Abdeckung (unverändert gegenüber dem letzten Lauf):

- **Generierter Code (39 Dateien)** — `obj/Debug/.../*.g.cs`, `*.Designer.cs`; kein Testbedarf (Build-Artefakte).
- **EF-Core-Migrationen (26 Dateien)** — `src/Softwareschmiede/Migrations/*`; werden durch Integrationstests der
  DbContext-Services indirekt abgedeckt, nicht durch Zeilen-Coverage sichtbar; kein gesonderter Testbedarf.
- **XAML-Markup (25 Dateien)** — reine `.xaml`-Dateien ohne Code-Behind-Logik; von Cobertura als 0 % geführt,
  da kein C#-Code vorhanden ist; kein Testbedarf.
- **WPF-View-Code-Behind (24 Dateien)** — z. B. `Views/MainWindow.xaml.cs`, `Views/FileExplorerView.xaml.cs`;
  nur über FlaUI/E2E-Tests erreichbar, nicht über Unit-Tests; teilweise durch bestehende E2E-Suite abgedeckt,
  aber E2E-Coverage wird in dieser Messung nicht separat erfasst.
- **App-Services ohne Unit-Test (13 Dateien)** — z. B. `WpfDialogService.cs`, `WpfAudioService.cs`,
  `WpfBannerService.cs`, `WpfUpdateProgressDialogService.cs`, `PluginSelectionDialogService.cs`; dünne
  WPF-Wrapper um Betriebssystem-/UI-Dialoge, klassischerweise nur per FlaUI/manuell getestet.
- **Controls (2 Dateien)** — `ActiveTasksListControl.xaml.cs`, weitere WPF-Controls ohne Unit-Test.
- **Sonstige (8 Dateien)** — u. a. `PluginSelectionDialogViewModel.cs`, `SolutionSelectionDialogViewModel.cs`
  (Dialog-ViewModels der IDE-Mehrfach-Einstiegspunkt-Auswahl — funktional eng mit der Plugin-Erweiterung
  verwandt, aber ohne direkte Unit-Tests; die Kernlogik der Multi-Plugin-Aggregation selbst liegt in
  `PluginSelectionService.cs`, das mit 97,5 % abgedeckt ist), `StatusUebergangsAnimation.cs`,
  `WorkspaceNodeRow.cs`, `PluginKonfiguration.cs`, `AgentInfo.cs`, `IGitPlugin.cs` (Interface-Datei).

Keine der 0-%-Dateien betrifft die neu implementierte Multi-Plugin-Aggregationslogik selbst
(`PluginSelectionService.cs`, `TaskDetailViewModel.cs`) — diese sind wie oben ausgewiesen mit 97,5 %
bzw. 76,2 % abgedeckt und blieben durch das reine Refactoring in Iteration 2 unverändert gut abgesichert.
