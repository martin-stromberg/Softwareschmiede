# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Aufgabe per Klick auf die gesamte Kachel öffnen (Pfeil-Button entfällt) → unauffällig. Die Kachel ist ein vollflächiger `Button` mit `Cursor="Hand"`, Hover-Feedback (Rahmen in `AccentBrush`), Pressed-Feedback (Opacity 0,8) und AutomationProperties-Name `AufgabeNavigieren:{Titel}` — als echtes Button-Element auch per Tastatur erreichbar. Klickbarkeit ist für Laien erkennbar (Hand-Cursor + deutliche Rahmen-Hervorhebung).
- Anzahl offener To-dos ablesen und To-do-Dialog öffnen → unauffällig. Der Button „N Todos" sitzt in der Symbolzeile, ist als eigenständiges, umrahmt beschriftetes Element erkennbar und löst dank `OffeneTodosButton_Click` (`e.Handled = true`) nicht die Kachel-Navigation aus.
- SCM-Plugin, KI-Plugin und Ausführungsstatus anhand von Symbolen erkennen → unauffällig. Drei Glyphen in einer Zeile; die entfallenen Textinformationen bleiben als ToolTip in Klartext erhalten („SCM: …", „KI: …", vollständiger Statustext inkl. Pausiert-Countdown). Kein interner Wert (Id, Prefix o. ä.) muss eingegeben oder gekannt werden; nicht auflösbare Plugins fallen auf den generischen Glyph „◻" mit Prefix als ToolTip zurück.
- Anzeigemodus zwischen „Datum", „Repository" und „KI" umschalten → unauffällig. Drei klar beschriftete `RadioButton`s mit erläuternden ToolTips („Flache Liste, sortiert nach letzter Ausführung" usw.) unter der Überschrift „Aktive Aufgaben" in der Seitenleiste sowie neben der Überschrift auf dem Dashboard; der gewählte Zustand ist über den Radio-Punkt sichtbar. Muster entspricht dem bestehenden `AufgabenFilter` (EnumToBoolConverter + RadioButton-Gruppe) in `ProjectDetailView.xaml`.
- Gruppierte Liste lesen (Repository-/KI-Modus) → unauffällig. Gruppenüberschriften zeigen den Klartext-Gruppennamen (Repository-Name, KI-Plugin-Name); Aufgaben ohne Zuordnung landen in den verständlich benannten Gruppen „Ohne Repository" bzw. „Nicht ausgeführt" am Listenende.
- Pausierte Aufgabe erkennen → unauffällig. Kachel wird abgedimmt (Opacity 0,55), Status-Glyph „⏸", ToolTip enthält den Countdown-Volltext.
- Befund-Verifikation Iteration 1 (Überlappung Modus-Buttons/Titel) → behoben: Titel und Umschalter liegen in getrennten Zeilen (`MainWindow.xaml`); bei eingeklappter Sidebar wird die Umschalterzeile ausgeblendet.
- Befund-Verifikation Iteration 1 (kein Hover-/Pressed-Feedback) → behoben: `ControlTemplate.Triggers` in `ActiveTasksListControl.xaml` liefern Rahmen-Hervorhebung bei Hover und Abblendung beim Drücken.

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/App.xaml` (Converter-Registrierung)
- `src/Softwareschmiede.App/Converters/AppConverters.cs` (`EnumToBoolConverter`, `KiAusfuehrungsStatusSymbolConverter`)
- `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs`
- `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`
- `src/Softwareschmiede.App/ViewModels/AufgabenListenModus.cs`
- `src/Softwareschmiede.App/ViewModels/AufgabenListenAnsicht.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` (Mapping `MapAktiveAufgabePanelItem`, `AufgabenListenModus`)
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/Services/AufgabenListenModusService.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`, `Domain/Abstractions/GitPluginBase.cs`, `Domain/Abstractions/CliKiPluginBase.cs` (`PluginSymbol`-Vertrag mit Fallback-Glyph)
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`, `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`, `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`, `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`, `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`, `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`, `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs` (`PluginSymbol`-Overrides)
