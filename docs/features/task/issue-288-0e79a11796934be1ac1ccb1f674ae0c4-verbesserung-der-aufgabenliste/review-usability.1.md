# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MainWindow.xaml (Seitenleiste, Sektion „Aktive Aufgaben")

- **Erreichbarkeit** — Die drei Modus-Umschalter („Datum", „Repository", „KI") liegen rechtsbündig in derselben Grid-Zelle wie der Abschnittstitel „Aktive Aufgaben", ohne Spaltentrennung oder eigene Zeile (Zeilen 127–159). Bei der Standard-Seitenleistenbreite von 240 px (`BoolToWidthConverter.ExpandedWidth`) ist der Platz rechnerisch nicht ausreichend: Titel (~90 px) plus drei RadioButtons mit Klartext (~170–185 px) ergeben ~260–275 px — der „Datum"-Schalter überlappt das Ende des Titels bzw. wird so dicht an den Titel gerückt, dass beide Texte ineinanderlaufen. Eine nicht-technische Anwenderin kann die Umschalter dann nur noch schwer lesen und zuordnen.

  Empfehlung: Die Modus-Buttons in eine eigene Zeile unter den Abschnittstitel legen oder Titel und Buttonzeile in zwei Grid-Zeilen/Spalten mit klarer Trennung aufteilen. Alternativ die Seitenleisten-Titelleiste wie auf dem Dashboard strukturieren, wo die Buttons wegen der größeren Breite problemlos passen.

### ActiveTasksListControl.xaml (`AufgabenKachelTemplate`)

- **Erreichbarkeit** — Die gesamte Aufgabenkachel ist jetzt ein vollflächiger `Button` zum Öffnen der Aufgabe (anforderungsgemäß entfällt der Pfeil-Button). Das ersetzte `ControlTemplate` (Border + ContentPresenter, Zeilen 91–118) enthält jedoch keinerlei `IsMouseOver`- oder `IsPressed`-Trigger: Weder beim Darüberfahren noch beim Klicken ändert sich irgendein visuelles Merkmal der Kachel — einzige Rückmeldung ist der Hand-Cursor. Eine Anwenderin, die nicht zufällig mit der Maus über die Kachel fährt, kann nicht erkennen, dass die Kachel anklickbar ist, und erhält beim Klicken keine visuelle Bestätigung. Die Kachel sieht damit aus wie ein reines Anzeigeelement.

  Empfehlung: Im `Border.Style` einen `Trigger Property="IsMouseOver"` (bzw. `IsPressed` am Button) ergänzen, der z. B. den Rahmen in `AccentBrush` färbt oder den Hintergrund leicht aufhellt — das übliche Hover-Verhalten klickbarer Karten. Die Funktion selbst (Klick öffnet die Aufgabe) ist korrekt verdrahtet.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Aufgabe durch Klick auf die gesamte Kachel öffnen → Befund vorhanden (kein visuelles Klick-/Hover-Feedback, siehe `ActiveTasksListControl.xaml`)
- SCM-Plugin, KI-Plugin und Ausführungsstatus an der Symbolzeile ablesen → unauffällig (Plugin-Namen und Status-Volltext als ToolTip in Klartext; Pausiert zusätzlich über die abgedimmte Kachel erkennbar, sodass das identische ⏸-Glyph für „Wartet"/„Pausiert" unterscheidbar bleibt)
- Anzahl offener To-Dos ablesen und To-do-Übersicht über den Button öffnen → unauffällig (Beschriftung „N Todos", eigenständig klickbar, löst keine Kachel-Navigation aus)
- Anzeigemodus über die Buttons in der Titelleiste umschalten (Datum / Repository / KI) → Befund vorhanden (verständliche Klartext-Beschriftung mit ToolTips, aber Überlappung mit dem Sektionstitel in der 240-px-Seitenleiste, siehe `MainWindow.xaml`; im Dashboard unproblematisch)
- Gruppierte Liste lesen (Gruppenüberschriften „Repository-Name", „KI-Name", Fallback-Gruppen „Ohne Repository" / „Nicht ausgeführt" am Listenende) → unauffällig (Klartext-Gruppentitel; bei deinstalliertem KI-Plugin erscheint der gespeicherte Prefix als Gruppenname — verständliche Degradierung)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`
- `src/Softwareschmiede.App/Controls/AufgabenListenAnsicht.cs`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`
- `src/Softwareschmiede.App/ViewModels/AufgabenListenModus.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/Services/AufgabenListenModusService.cs`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`, `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`, `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`, `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`, `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`, `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`, `plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs` (PluginSymbol-Overrides)
