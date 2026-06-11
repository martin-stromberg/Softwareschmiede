# Plan-Review: WPF-Desktopanwendung Softwareschmiede

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Die Implementierung deckt die meisten Service- und Backend-Komponenten ab, hat aber noch Lücken bei WPF UI-Komponenten und einigen optionalen Features.

## Umgesetzte Planelemente

### Enums
- [x] `AufgabeStatus` — vollständig angepasst mit neuen Werten (Neu, ArbeitsverzeichnisEingerichtet, Gestartet, InArbeit, Wartend, Beendet, Archiviert)
- [x] `BenachrichtigungsModus` — angepasst (Deaktiviert, Banner, Ton)
- [x] `BenachrichtigungsKanal` — angepasst (Banner, Ton)
- [x] `ProtokollTyp` — keine Änderungen erforderlich
- [x] `CliProcessStatus` — neue Enum (Gestartet, Gestoppt, Fehler)

### Interfaces
- [x] `IKiPlugin` — angepasst: alte Methoden (StartDevelopmentAsync, GetAvailableAgentsAsync, DeployAgentPackageAsync) entfernt; neue Methoden vorhanden (StartCliAsync, GetProcessWindowTitle, SupportsSessionContinuation, CheckHealthAsync)
- [x] `IBenachrichtigungsAudioService` — neu implementiert (PlayAudioAsync)

### Abstrakte Basis-Klassen
- [x] `CliKiPluginBase` — angepasst: BuildProcessStartInfo, StartCliAsync, GetProcessWindowTitle, SupportsSessionContinuation, ExtractWindowTitleFromProcess vorhanden
- [x] `InvalidStatusTransitionException` — neu implementiert

### Services
- [x] `KiAusfuehrungsService` — vollständig redesignt: StartCliAsync, StopCliAsync, IsCliRunning, GetLastExitCode, UpdateHeartbeat, CliProcessStatusChanged-Event vorhanden; CliProcessHandle-Klasse mit LastHeartbeat implementiert
- [x] `AufgabeService` — erweitert: SetStatusAsync, UpdateHeartbeatAsync, GetHeartbeatAgeMinutesAsync vorhanden
- [x] `AufgabeRecoveryService` — angepasst: ScanForRecoveryCandidatesAsync, RecoverManuellAsync mit neuen Status-Bedingungen
- [x] `EntwicklungsprozessService` — vereinfacht: nur noch Repository-Setup und Rate-Limit-Marker-Erkennung; ProzessStartenAsync vorhanden
- [x] `ProtokollService` — erweitert: AddCliOutputAsync, TryParseRateLimitMarker vorhanden
- [x] `BenachrichtigungsService` — erweitert: ShowBannerAsync, PlayAudioAsync, DispatchAsync mit neuen Modi vorhanden
- [x] `AppEinstellungService` — erweitert: Fenstergeometrie-Keys (WindowPositionXKey, WindowPositionYKey, WindowWidthKey, WindowHeightKey), DarkModeEnabledKey, DefaultKiPluginKey, LogLevelKey; GetWindowGeometryAsync, SetWindowGeometryAsync vorhanden
- [x] `CliProcessManager` — neu implementiert: StartHeartbeat, StopHeartbeat, OnCliProcessStatusChanged, reagiert auf CliProcessStatusChanged-Event
- [x] `ProcessWindowEmbedder` — neu implementiert: EmbedProcessWindowAsync, WaitForMainWindowHandleAsync, ActivateAlwaysOnTopFallback

### WPF UI-Komponenten
- [x] `MainWindow` (XAML + CodeBehind) — vorhanden
- [x] `MainWindowViewModel` — vorhanden
- [x] `DashboardView` (XAML + CodeBehind) — vorhanden
- [x] `DashboardViewModel` — vorhanden
- [x] `ProjectListView` (XAML + CodeBehind) — vorhanden
- [x] `ProjectListViewModel` — vorhanden
- [x] `ProjectDetailView` (XAML + CodeBehind) — vorhanden
- [x] `ProjectDetailViewModel` — vorhanden
- [x] `TaskDetailView` (XAML + CodeBehind) — vorhanden
- [x] `TaskDetailViewModel` — vorhanden
- [x] `SettingsView` (XAML + CodeBehind) — vorhanden
- [x] `SettingsViewModel` — vorhanden
- [x] `ProcessWindowHost` — neu implementiert (HwndHost-basiert, EmbeddedHandle-Dependency-Property)
- [x] `DarkModeService` — neu implementiert: InitializeAsync, ToggleAsync, SetDarkModeAsync, DarkModeChanged-Event, Theme-Wechsel-Logik
- [x] `WpfAudioService` — neu implementiert (IBenachrichtigungsAudioService)
- [x] `App.xaml` mit Theme-ResourceDictionaries (LightTheme.xaml, DarkTheme.xaml) — vorhanden
- [x] `Themes/DarkTheme.xaml` — vorhanden
- [x] `Themes/LightTheme.xaml` — vorhanden

### Value Objects & Records
- [x] `CliProcessHandle` — neu: AufgabeId, Process, LastHeartbeat
- [x] `WindowGeometrySettings` — neu: X, Y, Width, Height (record)

### Datenbankmigrationen
- [x] `UpdateAufgabeStatusEnum` — Status-Mapping implementiert
- [x] `UpdateBenachrichtigungsModus` — Enum-Wert-Mappings vorhanden
- [x] `UpdateBenachrichtigungsKanal` — Enum-Wert-Mappings vorhanden
- [x] `AddWindowGeometrySettings` — Schlüssel-Wert-Paare für Fenstergeometrie
- [x] `AddCliProcessTrackingFields` — bestehende Felder umgewidmet für CLI-Prozess-Tracking

## Offene Aufgaben

### WPF UI-Komponenten (teilweise fehlend)
- [ ] `TaskListView` (XAML + CodeBehind) — fehlt vollständig; nur ViewModel vorhanden
- [ ] `NavigationViewModel` — fehlt vollständig; Menü-State-Verwaltung nicht implementiert
- [ ] `PluginSettingsView` (XAML + CodeBehind) — fehlt vollständig; auto-generierte UI-Felder nicht implementiert
- [ ] `PluginSettingsViewModel` — fehlt vollständig
- [ ] `DiffViewControl` (optional) — fehlt vollständig
- [ ] `DiffViewViewModel` (optional) — fehlt vollständig
- [ ] `StatusIndicatorControl` — fehlt vollständig; Visuelle Status-Anzeige mit Animationen nicht implementiert
- [ ] `RecoveryBannerControl` — fehlt vollständig; Recovery-Banner-UI nicht implementiert

### Fehlerbehandlung & Validierung
- [ ] Status-Übergänge-Validierung — SetStatusAsync muss explizite Validierung der erlaubten Übergänge durchführen (aktuell nur implizit). `InvalidStatusTransitionException` muss bei ungültigen Übergängen geworfen werden.
- [ ] Konkrete Validierungsregeln für Aufgabe.Titel, AnforderungsBeschreibung, Projekt.Name fehlen in Services (nur DB-Constraints)

### Migration & Datenmodell
- [ ] Alte Felder `AgentenpaketName`, `AgentenName` in Aufgabe — Plan sagt "entfernen oder null setzen"; aktuell noch in Code vorhanden (keine explizite Migration sichtbar für Entfernung)

### Tests
- [ ] `TestNewStatusEnum()` — Enum-Werte-Tests fehlend oder nicht spezifisch für neue Werte dokumentiert
- [ ] `TestStatusTransitions()` — Spezifische Transitions-Validierungstests fehlend (Neu → ArbeitsverzeichnisEingerichtet → Gestartet → InArbeit → Beendet/Wartend)
- [ ] `TestCliStartAsync()` — CLI-Start-Tests fehlend
- [ ] `TestProcessWindowEmbedding()` — Win32-SetParent-Tests fehlend
- [ ] `TestHeartbeatUpdate()` — Heartbeat-Update-Tests fehlend
- [ ] `TestRecoveryCandidates()` — Recovery-Kandidaten-Erkennung mit neuen Status fehlend
- [ ] `TestRateLimitMarkerParsing()` — Rate-Limit-Marker-Parsing-Tests fehlend
- [ ] `TestBenachrichtigungNewEnum()` — Neue Benachrichtigungs-Enum-Tests fehlend
- [ ] `TestAudioPlayback()` — Audio-Playback-Tests fehlend
- [ ] `TestDarkModeToggle()` — Dark-Mode-Toggle und Persistierung-Tests fehlend
- [ ] `TestWindowGeometryPersistence()` — Fenstergeometrie-Speicherung-Tests fehlend
- [ ] `TestStatusValidation()` — Ungültige Status-Übergänge-Tests fehlend
- [ ] `TestPluginHealthCheck()` — Plugin-Health-Check-Tests fehlend
- [ ] E2E-Tests — Keine E2E-Tests für UI-Workflows sichtbar (TaskStartupE2ETests, CliEmbeddingE2ETests, RateLimitDetectionE2ETests, RecoveryE2ETests, DarkModeE2ETests, NotificationE2ETests, PluginSettingsE2ETests, WindowGeometryE2ETests)

### Service-Methoden & Logik
- [ ] `ProtokollService.ParseRateLimitMarker()` — Methode heißt `TryParseRateLimitMarker` (mit Try-Pattern); Signatur muss überprüft werden, ob sie den Plan-Anforderungen vollständig entspricht
- [ ] Rate-Limit-Erkennung während CLI-Lauf — Plan erfordert "während CLI läuft, wird jede Ausgabezeile auf [[SOFTWARESCHMIEDE_RATE_LIMIT]] gescannt"; klare Zuordnung zu KiAusfuehrungsService oder ProtokollService erforderlich
- [ ] Heartbeat-Aktualisierung — Plan sagt "Periodisch: KiAusfuehrungsService startet Timer, aktualisiert Heartbeat alle 30 Sekunden"; CliProcessManager stellt Timer bereit, aber Signal-Quelle (wann Timer startet) muss mit KiAusfuehrungsService koordiniert werden

### Optional Features
- [ ] `DiffViewControl` mit Side-by-Side Rendering — optional im Plan, nicht implementiert
- [ ] `DiffViewViewModel` — optional im Plan, nicht implementiert
- [ ] Fenster-Einbettung bei Fehler Fallback (AlwaysOnTop) — implementiert in ProcessWindowEmbedder, aber Integration in UI-Code fehlt

## Hinweise

### Abhängigkeiten
1. **TaskListView Integration:** MainWindow oder ProjectDetailView muss TaskListViewModel instanziieren und TaskListView einbinden (vermutlich als Tab oder untergeordnete View in ProjectDetailView).
2. **PluginSettingsView:** Muss basierend auf `IPlugin.GetSettingGroups()` UI-Felder dynamisch generieren; Abhängigkeit auf PluginSettingField und PluginSettingFieldType zur Feldtyp-Identifikation.
3. **Status-Übergänge-Validierung:** SetStatusAsync in AufgabeService sollte explizit erlaubte Übergänge prüfen und InvalidStatusTransitionException werfen (aktuell nicht sichtbar).

### Hinweise zur Implementierung
- **Rate-Limit-Marker-Parsing:** TryParseRateLimitMarker gibt bool zurück mit out-Parameter für DateTimeOffset; dieser liegt auf dem korrekten Pfad, aber die Integrationsstelle in KiAusfuehrungsService (beim Stdout-Streaming) muss überprüft werden.
- **Dark Mode:** DarkModeService ist gut implementiert; App.xaml.cs muss InitializeAsync aufrufen und UI-Komponenten an DarkModeChanged-Event binden.
- **Fenstergeometrie:** AppEinstellungService hat Get/SetWindowGeometryAsync; MainWindow muss diese beim Startup laden und beim Schließen speichern.
- **Blazor-Code:** Plan erfordert "Blazor-Code vollständig entfernen" — überprüfen, ob noch Blazor-Server-Komponenten im src/ vorhanden sind.

### Potenzielle Probleme
- **PluginSettingsView Fehlende XAML:** Ohne Implementierung können Plugin-Einstellungen nicht konfiguriert werden.
- **TaskListView Fehlende XAML:** Ohne diese View ist die Aufgabenliste in Projekten nicht sichtbar.
- **Validierung unvollständig:** SetStatusAsync sollte InvalidStatusTransitionException werfen, aber aktueller Code nicht einsehbar; muss überprüft werden.
