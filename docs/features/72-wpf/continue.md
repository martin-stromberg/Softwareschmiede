# Offene Aufgaben

Erstellt am: 2026-06-12
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] `CliKiPluginBase` — neue Methoden implementieren: `BuildProcessStartInfo(localRepoPath, parameters) → ProcessStartInfo` und `ExtractWindowTitleFromProcess(process) → string`; alte Methoden (`BuildContextFilePath`, `GetLatestContextFilePath`, `ClearContextFiles` etc.) entfernen.
- [ ] `IKiPlugin` entfernte Methoden überprüfen — alte Implementierungen in Plugin-Klassen (GitHub-Plugin, Claude CLI Plugin, Local Directory Plugin) auf neues Interface angepasst?
- [ ] `EntwicklungsprozessService` vereinfachen — `StartDevelopmentAsync` entfernen (falls noch vorhanden), Fokus nur auf Git-Setup und Rate-Limit-Parsing; Kontextkomprimierungs-Logik prüfen.
- [ ] Status-Validierungslogik in `AufgabeService.ValidateStatusTransition()` — Validierungsregeln verifizieren: nur Übergänge Neu→ArbeitsverzeichnisEingerichtet→Gestartet→InArbeit→(Beendet|Wartend); * → Archiviert.
- [ ] `StartenAsync()` — Status muss zu `ArbeitsverzeichnisEingerichtet` führen (nicht `InBearbeitung`).
- [ ] `AbschliessenAsync()` — Status muss zu `Beendet` führen (nicht `Abgeschlossen`).
- [ ] `BenachrichtigungsService.ShowBannerAsync()` — echte Windows Notifications API implementieren (aktuell nur Logging-Fallback).
- [ ] Tests für neue Status-Transitions ergänzen: `TestNewStatusEnum`, `TestStatusTransitions`, `TestCliStartAsync`, `TestProcessWindowEmbedding` (via `ProcessWindowHost`), `TestHeartbeatUpdate`, `TestRecoveryCandidates`, `TestRateLimitMarkerParsing`.
- [x] Bestehende Tests anpassen: `AufgabeServiceTests`, `KiAusfuehrungsServiceTests`, `AufgabeRecoveryServiceTests`, `EntwicklungsprozessServiceTests`, Plugin-Tests — auf neue Enums/Methoden-Signaturen aktualisieren.
- [ ] **Echte E2E-Tests implementieren** — Die bisherigen „E2E-Tests" wurden in `ServiceIntegrationTests` umbenannt, weil sie nur Services gegen eine In-Memory-Datenbank testen. Echte E2E-Tests starten die WPF-App als Prozess und steuern die UI über Windows UI Automation (z. B. FlaUI oder Microsoft.TestPlatform.UIAutomation). Abzudeckende Szenarien: Projekt anlegen via UI, Aufgabe starten (CLI sichtbar eingebettet), Dark Mode umschalten und Neustart, Recovery-Banner erscheint nach Heartbeat-Timeout. Voraussetzung: Windows-Desktop im CI, App muss ohne DB-Seiteneffekte startbar sein (Test-Profil).
- [ ] Blazor-Code-Entfernung prüfen — bestehende Blazor-Komponenten aus dem Repo entfernen (falls noch vorhanden).

## Code-Review-Befunde

- [ ] `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` — Double-checked Locking mit Lücke in `StartCliAsync`: Semaphor wird zwischen erster Prüfung und `process.Start()` freigegeben; zwei parallele Aufrufe für dieselbe aufgabeId können beide die erste Prüfung passieren. Semaphor über die gesamte Operation halten.
- [ ] `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` — `IsCliRunning` ist ein reiner Middle-Man-Wrapper auf `IsRunning` ohne eigene Logik. Entfernen, Aufrufer direkt auf `IsRunning` zeigen lassen.
- [ ] `src/Softwareschmiede/Application/Services/CliProcessManager.cs` — `AufgabeService` (Scoped) direkt in Singleton `CliProcessManager` injiziert → Scope-Leak / de facto Singleton-DbContext. `IServiceScopeFactory` injizieren und pro Heartbeat-Tick einen neuen Scope erzeugen und verwerfen.
- [ ] `src/Softwareschmiede/Application/Services/CliProcessManager.cs` — `AktualisierungDurchfuehren` ist redundante Wrapper-Methode um Fire-and-forget. Direkt `_ = AktualisierungAsync(aufgabeId)` im Timer-Callback; Wrapper entfernen.
- [ ] `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs` — `ProjektId`-Setter startet `LadenAsync(CancellationToken.None)` fire-and-forget ohne Abbruchmechanismus. CancellationTokenSource-Muster analog zu `TaskDetailViewModel.AufgabeId` implementieren.
- [ ] `src/Softwareschmiede.App/ViewModels/TaskListViewModel.cs` — `ProjektId`-Setter identisches Fire-and-forget-Problem. CancellationTokenSource-Muster anwenden; IDisposable implementieren.
- [ ] `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` — `IsDarkMode`-Setter ruft `_darkModeService.SetDarkModeAsync(value, CancellationToken.None)` fire-and-forget auf. Toggle ausschließlich über `MainWindowViewModel.ToggleDarkModeCommand` steuern; `IsDarkMode` im SettingsViewModel als read-only Property belassen.
- [ ] `src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs` — Namenskonventionen: `sealed` fehlt, doppelter `using System.Diagnostics`, kein `ICliSessionService`-Interface, hardcodierte CLI-Kommandos in switch-Anweisung. Code bereinigen und Interface extrahieren.
- [ ] `src/Softwareschmiede/Infrastructure/Services/CliSessionService.cs` — `ReadOutputLoop`: keine Exception-Behandlung; `IOException` bei Prozessabbruch terminiert den Loop ohne Log. try/catch mit Logging ergänzen.
- [ ] `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` — `GetWindowGeometryAsync` führt 4 sequenzielle DB-Queries aus. In eine Abfrage zusammenfassen (`WHERE Schluessel IN (...) + ToDictionaryAsync`).
- [ ] `src/Softwareschmiede.App/Controls/ProcessWindowHost.cs` — `SetLastError = true` fehlt bei `SetParent`, `SetWindowPos`, `GetWindowLong`, `SetWindowLong`. Ergänzen und Rückgabewerte nach dem Aufruf prüfen; bei Fehler loggen.

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests.
