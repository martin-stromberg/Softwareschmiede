# Bestandsaufnahme: Korrektur der Aufgabenliste im Programmmenue

## Zusammenfassung

Die linke Aufgabenliste wird in der WPF-Shell ueber `MainWindowViewModel.AktiveAufgabenListe` gespeist und mit `ActiveTasksListControl` gerendert. Aktuell werden nur Titel, Projektname und KI-Ausfuehrungsstatus angezeigt. Eine aktive Aufgaben-ID, Plugin-Anzeigenamen und ein dedizierter Zeitstempel "Letzter Start" existieren nicht.

Die Sortierung aktiver Aufgaben erfolgt derzeit in `AufgabeService.GetAktiveAufgabenAsync` nach `LastHeartbeatUtc ?? ErstellungsDatum`. Das widerspricht der Anforderung, weil Heartbeats und laufzeitnahe Aktualisierungen keine echten CLI-Neustarts sind und dadurch die Reihenfolge waehrend laufender Aufgaben instabil wirken kann.

## Detaildokumente

- [UI und Navigation](inventory/ui-und-navigation.md)
- [Domainmodell und Persistenz](inventory/domainmodell-und-persistenz.md)
- [CLI-Start und Laufzeitstatus](inventory/cli-start-und-laufzeitstatus.md)
- [Plugin-Metadaten](inventory/plugin-metadaten.md)
- [Tests und Risiken](inventory/tests-und-risiken.md)

## Relevante Codebereiche

| Bereich | Dateien | Bedeutung |
|---|---|---|
| Seitenleistenliste | `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`, `src/Softwareschmiede.App/Views/MainWindow.xaml`, `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml(.cs)` | Befuellung, Navigation und Darstellung der Aufgabenpanels |
| Aufgabenmodell | `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`, `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`, `src/Softwareschmiede/Migrations/*` | Persistierte Aufgabenfelder, EF-Konfiguration und Migrationen |
| Aktive Aufgaben | `src/Softwareschmiede/Application/Services/AufgabeService.cs` | Query, Include-Strategie und Sortierung der aktiven Aufgaben |
| CLI-Lauf | `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`, `src/Softwareschmiede/Application/Services/CliProcessManager.cs`, `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Echte Starts, Neustarts, vorhandene Session wieder anzeigen |
| Plugins | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`, `src/Softwareschmiede/Domain/Entities/GitRepository.cs`, `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` | Plugin-Prefixe und Anzeigenamen |

## Festgestellter Ist-Zustand

- `MainWindowViewModel.CurrentView` kennt die aktuell angezeigte View, aber `AktiveAufgabenListe` enthaelt rohe `Aufgabe`-Entities ohne zusaetzliche UI-Information zur aktiven Aufgabe.
- `ActiveTasksListControl` hat Dependency Properties fuer `ItemsSource`, `NavigateCommand` und `ShowNavigationButton`, aber keine Property fuer `ActiveAufgabeId` oder vergleichbare Selektion.
- Die aktuelle Kachel verwendet konstant `SurfaceBrush` und `BorderBrush`; es gibt keine aktive visuelle Variante.
- `Aufgabe` enthaelt `KiPluginPrefix`, `LastHeartbeatUtc`, `AktiveRunId` und `LaufStatus`, aber kein Feld fuer "Letzter Start".
- `GetAktiveAufgabenAsync` sortiert nach `LastHeartbeatUtc ?? ErstellungsDatum` und laedt nur `Projekt`, nicht `GitRepository`.
- Echte CLI-Starts werden durch `KiAusfuehrungsService.StartCliAsync` und `StartWithPseudoConsoleAsync` ausgeloest; beide feuern `CliProcessStatusChanged(..., Gestartet)`.
- `CliProcessManager` reagiert auf `CliProcessStatus.Gestartet` und persistiert aktuell `AktiveRunId`, `LastHeartbeatUtc` und `LaufStatus`.
- Das SCM-Plugin ist pro Repository als `GitRepository.PluginTyp` gespeichert. Das KI-Plugin ist pro Aufgabe als `Aufgabe.KiPluginPrefix` gespeichert. Beide Werte sind Prefixe/Typ-Strings, waehrend die anzuzeigenden Namen ueber `IPlugin.PluginName` verfuegbar sind.

## Erwartete Aenderungspunkte fuer die Umsetzung

- Neues persistiertes Feld in `Aufgabe`, z. B. `LetzterStartUtc` oder `LetzterCliStartUtc`, inklusive EF-Konfiguration, Migration und Snapshot.
- Service-Methode zum Setzen dieses Felds beim echten CLI-Start. Naheliegender Ort ist `AufgabeService.AktivenLaufSetzenAsync`, weil sie zentral durch `CliProcessManager` bei `CliProcessStatus.Gestartet` aufgerufen wird.
- `GetAktiveAufgabenAsync` sollte absteigend nach neuem Startzeitstempel sortieren und fuer Aufgaben ohne Wert deterministisch fallen backen, z. B. `ErstellungsDatum` plus `Id`/Titel als Tie-Breaker.
- Die Query fuer die Seitenleiste muss die fuer SCI-Plugin-Anzeige notwendige Repository-Information laden (`GitRepository`) oder ein eigenes Sidebar-DTO liefern.
- Das UI sollte nicht mehr direkt rohe `Aufgabe`-Entities binden, wenn Plugin-Anzeigenamen und Aktivstatus berechnet werden muessen. Ein schmales ViewModel/DTO fuer Aufgabenpanels waere stabiler.
- `ActiveTasksListControl` braucht eine aktive ID bzw. eine `IsActive`-Eigenschaft im Item sowie Styles/Automation-Namen fuer die aktive Kachel.

## Offene Punkte fuer Planung

- Soll bei bestehenden Aufgaben ohne "Letzter Start" das Fallback `ErstellungsDatum` verwenden oder ein bewusst niedriger Wert, damit Aufgaben mit echtem Start immer vor Altbestand stehen? Beide Varianten sind deterministisch.
- Soll im Panel der Plugin-Prefix oder der `PluginName` angezeigt werden, wenn das konkrete Plugin aktuell nicht geladen/verfuegbar ist? Empfohlen: `PluginName`, Fallback auf gespeicherten Prefix.
- Der Begriff "SCI-Plugin" entspricht im Code sehr wahrscheinlich "SCM-/Source-Code-Management-Plugin" (`IGitPlugin`, `PluginType.SourceCodeManagement`).

