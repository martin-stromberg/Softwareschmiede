# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Enum-Änderungen
- [x] `AufgabeStatus` – Enum komplett aktualisiert (Neu, ArbeitsverzeichnisEingerichtet, Gestartet, InArbeit, Wartend, Beendet, Archiviert)
- [x] `BenachrichtigungsModus` – Enum aktualisiert (Deaktiviert, Banner, Ton)
- [x] `BenachrichtigungsKanal` – Enum aktualisiert (Banner, Ton)
- [x] `ProtokollTyp` – Erweitert um CliOutput und RateLimit

### Neue Klassen – WPF UI-Komponenten
- [x] `MainWindow` (XAML) – Hauptfenster mit Navigation und Inhaltsbereich
- [x] `MainWindowViewModel` – State Management für Navigation und Dark Mode Toggle
- [x] `NavigationViewModel` – Verwaltung des Menü-Status (expandiert/eingeklappt)
- [x] `DashboardView` (XAML + CodeBehind) – Dashboard mit Projekt- und Aufgabenzähler
- [x] `DashboardViewModel` – Logic für Dashboard
- [x] `ProjectListView` (XAML + CodeBehind) – Tabelle aller Projekte
- [x] `ProjectListViewModel` – Logic für Projektliste
- [x] `ProjectDetailView` (XAML + CodeBehind) – Projekt bearbeiten und Aufgabenliste
- [x] `ProjectDetailViewModel` – Logic für Projektdetail
- [x] `TaskListView` (XAML + CodeBehind) – Aufgabenliste mit Filterung nach Status
- [x] `TaskListViewModel` – Logic für Aufgabenliste
- [x] `TaskDetailView` (XAML + CodeBehind) – Aufgabendetails, Statusübergänge, Protokoll, CLI-Fenster
- [x] `TaskDetailViewModel` – Logic für Aufgabendetail und CLI-Management
- [x] `SettingsView` (XAML + CodeBehind) – Tabs für Plugin-Konfiguration
- [x] `SettingsViewModel` – Logic für Einstellungen
- [x] `PluginSettingsView` (XAML + CodeBehind) – Automatisch generierte UI-Felder für Plugin-Einstellungen
- [x] `PluginSettingsViewModel` – Logic für Plugin-Einstellungen

### Neue Klassen – Services
- [x] `DarkModeService` – Verwaltung Dark Mode (Theme-Wechsel, Persistierung)
- [x] `ProcessWindowEmbedder` – Win32 SetParent API-Wrapper für Fenster-Einbettung
- [x] `CliProcessManager` – Verwaltung von CLI-Prozessen: Start, Stop, Heartbeat-Tracking
- [x] `BenachrichtigungsService` – Audio-Support, Toast-Integration, Dispatch-Logging
- [x] `WpfAudioService` – WPF-spezifischer Audio-Service für WAV/MP3

### Neue Klassen – UI Controls
- [x] `ProcessWindowHost` (WPF Control + CodeBehind) – Host-Container für eingebettete CLI-Prozesse
- [x] `StatusIndicatorControl` (XAML + CodeBehind) – Visuelle Anzeige des Task-Status
- [x] `RecoveryBannerControl` (XAML + CodeBehind) – Banner für Recovery-Kandidaten
- [x] `ProcessWindowEmbedder` – Win32 SetParent für Fenster-Einbettung

### Änderungen an bestehenden Klassen

#### `IKiPlugin` Interface
- [x] Entfernte Methoden: `StartDevelopmentAsync`, `GetAvailableAgentsAsync`, `IsAgentPackageCompatibleAsync`, `DeployAgentPackageAsync`
- [x] Neue Methode: `StartCliAsync(localRepoPath, parameters, ct)` – Startet CLI und gibt ProcessStartInfo zurück
- [x] Neue Methode: `GetProcessWindowTitle(aufgabeId)` – Hilft bei Prozess-Fenster-Identifikation
- [x] Neue Methode: `SupportsSessionContinuation()` – Gibt an, ob Plugin Session-Fortsetzung unterstützt
- [x] Neue Methode: `CheckHealthAsync(ct)` – Prüft ob Plugin verfügbar ist

#### `KiAusfuehrungsService` Service
- [x] Neue Methode: `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct)` – Startet CLI und gibt Handle zurück
- [x] Neue Methode: `StopCliAsync(aufgabeId, ct)` – Stoppt laufenden Prozess (SIGTERM → 5s → SIGKILL)
- [x] Neue Methode: `IsCliRunning(aufgabeId)` – Prüft ob CLI-Prozess läuft
- [x] Neue Methode: `GetLastExitCode(aufgabeId)` – Gibt Exit-Code des letzten Prozesses zurück
- [x] Neue Methode: `UpdateHeartbeat(aufgabeId)` – Aktualisiert LastHeartbeatUtc (in Handle)
- [x] Neues Event: `CliProcessStatusChanged` – Wird ausgelöst bei Prozess-Start, -Stop, -Fehler
- [x] Neue Klasse: `CliProcessHandle` – Wrapper für laufende Prozesse mit Heartbeat-Tracking
- [x] Neues Enum: `CliProcessStatus` (Gestartet, Gestoppt, Fehler)

#### `AufgabeService` Service
- [x] Neue Methode: `SetStatusAsync(aufgabeId, newStatus, ct)` – Setzt Status mit Validierung
- [x] Neue Methode: `UpdateHeartbeatAsync(aufgabeId, ct)` – Aktualisiert LastHeartbeatUtc in DB
- [x] Neue Methode: `GetHeartbeatAgeMinutesAsync(aufgabeId, ct)` – Berechnet Minuten seit letztem Heartbeat
- [x] Aktualisierte Methode: `StartenAsync()` – Status → ArbeitsverzeichnisEingerichtet statt InBearbeitung
- [x] Validierungslogik für Status-Übergänge – Nur erlaubte Übergänge (Neu → ArbeitsverzeichnisEingerichtet → Gestartet → InArbeit → Beendet/Wartend; * → Archiviert)

#### `AufgabeRecoveryService` Service
- [x] Neue Methode: `ScanForRecoveryCandidatesAsync(ct)` – Gibt alle Recovery-Kandidaten zurück
- [x] Aktualisierte Bedingungen: Status muss InArbeit oder Wartend sein; Heartbeat > 5 Min; kein laufender CLI-Prozess

#### `ProtokollService` Service
- [x] Neue Methode: `AddCliOutputAsync(aufgabeId, outputLine, ct)` – Speichert Ausgabezeile als Protokolleintrag
- [x] Neue Methode: `ParseRateLimitMarker(outputLine)` – Parst Rate-Limit-Marker Format: `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601_DATETIME]]`
- [x] Interne Methode: `TryParseRateLimitMarker(outputLine, out resetUtc)` – Parst Marker mit Zeitstempel

#### `AppEinstellungService` Service
- [x] Neue Einträge: WindowPosition.X, Y, Width, Height – Fenstergeometrie
- [x] Neue Einträge: DarkModeEnabled – Bool für Dark Mode
- [x] Neue Methode: `GetWindowGeometryAsync()` – Lädt Fenstergeometrie
- [x] Neue Methode: `SetWindowGeometryAsync(geometry)` – Speichert Fenstergeometrie
- [x] Neue Methode: `GetBoolSettingAsync(key)` – Lädt Bool-Einstellung
- [x] Neue Methode: `SetBoolSettingAsync(key, value)` – Speichert Bool-Einstellung

#### `BenachrichtigungsService` Service
- [x] Neue Methode: `PlayAudioAsync(filePath, ct)` – Spielt Audiodatei ab
- [x] Neue Methode: `ShowBannerAsync(aufgabeId, message, ct)` – Zeigt Banner-Benachrichtigung
- [x] Aktualisiertes Verhalten: Verwendet neue Enums (Deaktiviert, Banner, Ton)

#### `CliProcessManager` Service
- [x] Neue Methode: `StartHeartbeat(aufgabeId)` – Startet Heartbeat-Timer (30 Sekunden Intervall)
- [x] Neue Methode: `StopHeartbeat(aufgabeId)` – Stoppt Heartbeat-Timer
- [x] Event-Handler: `OnCliProcessStatusChanged` – Reagiert auf CliProcessStatusChanged vom KiAusfuehrungsService

### Datenbank-Migrationen
- [x] `UpdateAufgabeStatusEnum` – Umwandlung alter Enum-Werte zu neuen
- [x] `UpdateBenachrichtigungsModus` – Umwandlung von Immer/Nie/NurBeiFehler → Deaktiviert/Banner/Ton
- [x] `UpdateBenachrichtigungsKanal` – Umwandlung von Audio/System → Ton/Banner
- [x] `AddWindowGeometrySettings` – Fensterposition und -größe (über AppEinstellung Schlüssel-Wert-Paare)
- [x] `AddCliProcessTrackingFields` – Semantische Umwidmung von AktiveRunId, LastHeartbeatUtc, RecoveryVersion für CLI-Prozess-Tracking

### Validierungsregeln
- [x] Status-Übergänge validiert in `AufgabeService.ValidateStatusTransition()` – Wirft `InvalidStatusTransitionException`
- [x] Rate-Limit-Marker-Parsing mit Format-Validierung (ISO8601 DateTimeOffset)
- [x] Audiodatei-Existenz-Prüfung in `BenachrichtigungsService.PlayAudioAsync()`

### Konfigurationseinträge
- [x] `LoggingLevel` – Debug oder Information (via Serilog)
- [x] `WindowPosition.X/Y/Width/Height` – Fenstergeometrie
- [x] `DarkModeEnabled` – Bool für Dark Mode Toggle
- [x] `MaxParallelCliProcesses` – Deprecated, nur noch 1
- [x] `AutoCloneRepository` – Bool
- [x] `DefaultKiPlugin` – String Plugin-Prefix
- [x] `DefaultGitPlugin` – String Plugin-Prefix
- [x] `NotificationMode` – Enum (Deaktiviert/Banner/Ton)
- [x] `NotificationAudioPath` – String Pfad zu Audiodatei
- [x] `HeartbeatTimeoutMinutes` – Int (5 Minuten Standard)
- [x] `RateLimitMarkerFormat` – String Prefix für Rate-Limit-Marker

### WPF Themes
- [x] `LightTheme.xaml` – Light Mode Theme mit DynamicResource Definitions
- [x] `DarkTheme.xaml` – Dark Mode Theme mit DynamicResource Definitions

### Tests
- [x] `AufgabeStatusTransitionTests` – Testet Status-Übergänge und Validierungslogik
- [x] `DarkModeE2ETests` – E2E-Test für Dark Mode Aktivierung und Persistierung
- [x] `BenachrichtigungsServiceTests` – Testet neue Audio- und Banner-Methoden
- [x] `KiAusfuehrungsServiceTests` – Angepasst für neue CLI-Prozess-Management-Methoden
- [x] `CliProcessManagerTests` – Testet Heartbeat-Verwaltung
- [x] `AufgabeRecoveryServiceTests` – Angepasst für neue ScanForRecoveryCandidatesAsync-Methode

## Offene Aufgaben

Keine offenen Aufgaben – alle Planelemente sind vollständig umgesetzt.

## Hinweise

### Architektur-Highlights
1. **IKiPlugin Interface Redesign**: Das Interface ist vollständig überarbeitet. Alte Methoden für Prompt-Streaming und Agentenpaket-Management sind entfernt. Das Plugin ist jetzt rein für Prozess-Start verantwortlich.

2. **CLI-Prozess-Management**: `KiAusfuehrungsService` und `CliProcessManager` arbeiten zusammen für robuste Prozess-Verwaltung mit Heartbeat-Tracking und automatischer Recovery-Erkennung.

3. **WPF-Integration**: Vollständige MVVM-Implementierung mit Dependency Injection. Theme-System nutzt WPF ResourceDictionary für dynamischen Dark Mode.

4. **Rate-Limit-Erkennung**: Marker-basiertes Parsing in `ProtokollService` mit ISO8601-Zeitstempel-Unterstützung ermöglicht automatische Wiederaufnahme-Planung.

5. **Status-Transitions**: Strikte Validierungslogik in `AufgabeService` erzwingt korrekte Zustandsübergänge und wirft explizite `InvalidStatusTransitionException`.

6. **Windows Credential Store Integration**: Audio-Benachrichtigungen via WPF `MediaPlayer` ohne externe NuGet-Abhängigkeiten (nur Windows native APIs).

### Abhängigkeiten & Kompatibilität
- Alle Services sind über DI-Container registriert und initialisiert
- Plugin-Interfaces sind abwärts-kompatibel durch Basis-Implementierungen in `GitPluginBase` und `CliKiPluginBase`
- Migrationen sind in korrekter Reihenfolge für EF Core 10.0
- Logging via Serilog mit rollingInterval: daily, retainedFileCountLimit: 14

### Vollständigkeit
Der Plan ist zu 100% umgesetzt. Alle neuen UI-Komponenten, Services, Methoden, Enums und Migrationen sind vorhanden und integriert. Das System ist bereit für E2E-Tests und Produktionsdeployment.
