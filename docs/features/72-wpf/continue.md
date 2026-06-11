# Offene Aufgaben

Erstellt am: 2026-06-11
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] ProtokollService: Methode `ParseRateLimitMarker(outputLine) → (bool, string?, DateTimeOffset?)` optional als öffentliche Methode extrahieren (aktuell inline in `EntwicklungsprozessService`)
- [ ] EntwicklungsprozessService: Kontextkomprimierung mit Soft-Limit (12.000 Zeichen) und Hard-Limit (20.000 Zeichen) implementieren
- [ ] DarkModeService: ViewModels müssen `DarkModeChanged`-Event abonnieren und UI aktualisieren
- [ ] Audio-Dateipfad-Konfiguration: `PluginSettingsView` soll Audio-Datei-Auswahl ermöglichen
- [ ] Fenster-Geometrie-Persistierung: `MainWindow.xaml.cs` muss beim Laden Geometrie via `GetWindowGeometryAsync()` restaurieren und beim Schließen via `SetWindowGeometryAsync()` speichern
- [ ] PluginSettingsView: Prüfen, ob alle `PluginSettingFieldType`-Werte (String, Integer, Boolean, File-Path etc.) in der automatischen UI-Generierung unterstützt werden
- [ ] NavigationViewModel: Menu-Toggle-Logik (Einklappbarkeit des Seitenmenüs) prüfen und ggf. implementieren

## Code-Review-Befunde

- [ ] `src/Softwareschmiede.App/App.xaml.cs:88` — PluginManager ist als konkreter Typ registriert, nicht als IPluginManager; PluginSelectionService wirft InvalidOperationException beim DI-Auflösen. Außerdem fehlen IGitPlugin, IBenutzerkontextService, IArbeitsverzeichnisResolver und PluginDefaultSettingsService im Container.
- [ ] `src/Softwareschmiede/Application/Services/CliProcessManager.cs:38` — Timer-Callback ist async void; unkontrollierte Exceptions aus UpdateHeartbeatAsync reißen den Prozess ab.
- [ ] `src/Softwareschmiede.App/App.xaml.cs:25` — OnStartup ist async void; Exceptions aus `_host.StartAsync()` landen als unhandledException ohne Fehlerdialog.
- [ ] `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:45` — TOCTOU-Race zwischen IsRunning-Prüfung und `_handles[aufgabeId]`-Zugriff bei gleichzeitigem Dispose oder Doppelstart.
- [ ] `src/Softwareschmiede/Application/Services/BenachrichtigungsService.cs:207` — Temporäre Audiodateien (`softwareschmiede-audio-<guid>.mp3`) werden niemals gelöscht; dauerhaftes Leck im Temp-Verzeichnis.
- [ ] `src/Softwareschmiede.App/Controls/ProcessWindowHost.cs:97` — `ResizeEmbeddedWindow` setzt `SWP_SHOWWINDOW` ohne `SWP_NOACTIVATE`; jede Größenänderung stiehlt den Tastaturfokus vom WPF-Bereich.
- [ ] `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:91` — Nach CLI-Start via `KannCliStarten` wird der Aufgabenstatus nicht auf `InArbeit` gesetzt; Status läuft auseinander und `AufgabeRecoveryService` findet die Aufgabe nicht.
- [ ] `src/Softwareschmiede.App/Services/WpfAudioService.cs:29` — `Dispatcher.InvokeAsync`-Rückgabewert wird verworfen; bei Shutdown-Zustand des Dispatchers wartet der Aufrufer unendlich auf `tcs.Task`.
- [ ] `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs:104` — N+1-Datenbankabfragen in `LadenAsync`: eine Query pro Projekt für aktive Aufgaben; bei 50 Projekten = 51 Rundtrips.
- [ ] `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:68` — `DarkModeChanged`-Event wird im Konstruktor abonniert, aber nie abgemeldet; Singleton hält Referenz auf transiente ViewModel-Instanzen.

## Fehlende E2E-Tests (aus Plan, Pflicht)

Die folgenden E2E-Testklassen sind im Plan als Pflicht definiert, aber nicht implementiert:

- [ ] `ProjectE2ETests` — Projekt erstellen und Aufgabe hinzufügen
- [ ] `TaskStartupE2ETests` — Aufgabe starten → Repository klonen → Branch erstellen; Status-Übergang `ArbeitsverzeichnisEingerichtet` → `Gestartet`
- [ ] `CliEmbeddingE2ETests` — CLI-Prozess starten und Fenster in WPF-Control einbetten
- [ ] `ProtocolLoggingE2ETests` — Stdout-Streaming und Protokoll-Eintrag in UI
- [ ] `RateLimitDetectionE2ETests` — Marker erkennen, Vorschlag speichern, Status → `Wartend`
- [ ] `RecoveryE2ETests` — App startet, erkennt `InArbeit`/`Wartend`-Aufgaben, zeigt Recovery-Banner
- [ ] `DarkModeE2ETests` — Dark Mode aktivieren, persistieren, beim Neustart wiederherstellen
- [ ] `NotificationE2ETests` — Status-Wechsel triggert Banner/Audio basierend auf Modus
- [ ] `PluginSettingsE2ETests` — Plugin-Einstellung speichern, verschlüsseln, beim Reload laden
- [ ] `WindowGeometryE2ETests` — Fensterposition verschieben/skalieren und beim Neustart wiederherstellen

## Fehlgeschlagene Tests

Keine fehlgeschlagenen Tests.
