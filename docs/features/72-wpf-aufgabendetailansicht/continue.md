# Offene Aufgaben

Erstellt am: 2026-06-15
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. Plan-Review hat Status: Vollständig umgesetzt.

## Code-Review-Befunde

### Feature-Code (durch diese Implementierung eingeführt)

- [ ] `TaskDetailViewModel.cs:343` — `CliStartenAsync` lädt `Aufgabe` via `GetByIdAsync` (shallow, kein `Include`) statt `GetDetailAsync`, wodurch `IssueReferenz`, `GitRepository` und `Protokolleintraege` nach CLI-Start silent wegfallen
- [ ] `TaskDetailView.xaml.cs:25` — `Unloaded`-Handler ruft `vm.Dispose()` auf; `ProjectDetailViewModel.SelectedTaskViewModel`-Setter disposed dieselbe Instanz ebenfalls bei Navigation → Double-Dispose ohne Guard
- [ ] `TaskDetailViewModel.cs:510` — `OnCliProcessStatusChanged` ruft `Application.Current.Dispatcher.Invoke` direkt im ViewModel auf, koppelt das ViewModel an den WPF-Application-Singleton und bricht alle Unit-Tests, die diesen Pfad ausführen
- [ ] `TaskDetailViewModel.cs:358` — Bei unerwartetem Prozessabbruch wird `IsCliRunning` zurückgesetzt, aber `EmbeddedWindowHandle` zeigt weiterhin auf den toten HWND → korrumpierte Einbettungsversuche danach

### Pre-existing (nicht durch dieses Feature eingeführt)

- [ ] `DarkModeService.cs:57` — `ApplyTheme` indexiert `_themeUris[mode]` ohne Guard; ein unbekannter DB-Wert wirft `KeyNotFoundException` beim App-Start
- [ ] `WpfAudioService.cs:37` — `ct.Register` ruft nie `player.Close()` bei Abbruch auf → native Media-Ressourcen akkumulieren im Singleton
- [ ] `MainWindowViewModel.cs:48` — `ToggleDarkModeCommand` wird deklariert aber nie im Konstruktor zugewiesen → Dark-Mode-Toggle dauerhaft wirkungslos
- [ ] `test-csharp-startup.ps1:53` — `--no-incremental` erzwingt bei jedem Stop-Hook einen vollständigen MSBuild-Rebuild (10–30 Min. pro Session)
- [ ] `check_enum_coverage.py:101` — Hook liest bei jedem `.cs`-Speichern die gesamte Solution in den Speicher → O(M×N) blockierender Scan pro Edit

## Fehlgeschlagene Tests

- [ ] `FaviconHammerPickSvgTests.FaviconHammerPickSvg_ShouldExistInWwwroot` — Datei `favicon-hammer-pick.svg` fehlt in `wwwroot/` (pre-existing)
- [ ] `FaviconHammerPickSvgTests.FaviconHammerPickSvg_ShouldContainRequiredMarkers` — Datei `favicon-hammer-pick.svg` fehlt in `wwwroot/` (pre-existing)
- [ ] `ProjectDetailE2ETests` (3 Tests) — Timeout beim Auffinden von UI-Elementen; möglicherweise durch Änderungen in `ProjectDetailViewModel.cs` (Callback-Verdrahtung) beeinflusst — Ursache prüfen
