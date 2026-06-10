# Umsetzungsplan: WPF-Desktopanwendung Softwareschmiede

## Übersicht

Die WPF-Desktopanwendung Softwareschmiede ist eine native Windows 11-Anwendung zur Verwaltung von Entwicklungsprojekten und deren Aufgaben mit KI-Automatisierung. Sie ersetzt die bestehende Blazor-Server-Anwendung und nutzt eine SQLite-Datenbank mit Plugin-Architektur für Git-Provider und KI-CLI-Tools. Der Plan deckt die vollständige Implementierung ab: Datenmodell-Anpassungen, entfernung von Plugin-Kontextdatei-Management, WPF-UI mit Dark Mode, Fenster-Einbettung für CLI-Prozesse, Audio-Benachrichtigungen, Status-Management, Recovery-Mechanismen und E2E-Tests.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Plugin-Kontextdateiverwaltung | **Keine Verwaltung durch App** — Plugin verwaltet seinen Kontext selbst; App liest nur optional stdout für Logging/Rate-Limit-Erkennung | Entlastung des App-Datenmodells, einfachere Plugin-API, Selbstverantwortung des Plugins für Session-Fortbestand. Keine `.softwareschmiede/`-Verzeichnis, kein `.gitignore`-Management. |
| CLI-Fenster-Handle-Identifikation | **Direkt aus `Process.MainWindowHandle`** nach kurzer Wartezeit (`MainWindowHandle != IntPtr.Zero`), dann `SetParent` WinAPI-Aufruf. Kein `FindWindow`, kein Plugin-Callback | Einfachste und zuverlässigste Methode; Handle ist sofort nach Prozessstart verfügbar. Plugin-Interface bleibt schlank. |
| Session-Fortbestand | **Plugin-Methode `SupportsSessionContinuation() → bool`** + plugin-spezifisches Flag beim Start. Plugin entscheidet, wie Session-Wiederaufnahme umgesetzt wird (Env-Vars, Befehlszeilenflags, Datei-basiert). | Flexibilität für unterschiedliche Implementierungen (Claude CLI ≠ Copilot CLI). App verwaltet nur das Konzept, nicht die Technologie. |
| Rate-Limit-Marker | **Marker-Format: `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601]]`** — Plugin gibt Vorschlag mit UTC-Zeitstempel aus; App speichert beide in `Aufgabe.VorschlagPrompt` + `VorschlagAusfuehrenAbUtc` | Eindeutige Erkennung, Zeitstempel für automatische Retriggering-Planung (optional später). |
| SetParent-Fallback | **AlwaysOnTop-Fenster neben der App** — Falls `SetParent` scheitert, zeige CLI-Fenster mit `SetWindowPos(..., HWND_TOPMOST)` neben der Anwendung | Benutzer sieht CLI-Output immer, keine stille Fehler. |
| Audio-Playback | **WPF-eigener `MediaPlayer`** (unterstützt WAV, MP3; keine NuGet-Abhängigkeiten) statt NAudio | Minimale externe Abhängigkeiten, plattform-native Unterstützung, ausreichend für Use-Case. |
| Mehrere Repositories pro Projekt | **UI zeigt eines (default), Datenmodell bleibt flexibel mit `List<GitRepository>`** | Zukünftige Erweiterbarkeit ohne Migration; aktuell Single-Repo-UI, aber Basis unterstützt Multi-Repo. |
| Log-Dateipfad | **`{Programmverzeichnis}/logs`** (relativ zum Ausführungsverzeichnis der Anwendung, nicht `%LOCALAPPDATA%`) | Für Debugging und Log-Analyse einfacher erreichbar. Keine Speicherplatz-Quotas durch Betriebssystem. |
| Graceful Shutdown | **SIGTERM → 5s Wartezeit → SIGKILL** | Standard-Praxis für Prozess-Termination. 5s reicht für sauberes Cleanup. |
| Heartbeat-Intervall | **30 Sekunden** | Balanceakt: schnelle Recovery-Erkennung vs. Ressourcenschonung. Entspricht Standard-Monitoring. |
| Fehlgeschlagener Status | **→ `Beendet`** (nicht als separater Status gespeichert) | Vereinfachte Status-Verwaltung. Fehler wird in Protokolleintrag mit Typ `StatusUebergang` und Fehlermeldung dokumentiert. |
| Agentenpaket-Felder in Aufgabe | **Entfernen aus Datenmodell** (nicht null-setzen) — Datenbank-Migration erforderlich | Reduziert Verwirrung; Agentenpaket ist Plugin-Konzept, nicht Aufgaben-Konzept. |

## Programmabläufe

### Aufgabe starten (mit KI-Integration)

1. Benutzer klickt "Aufgabe starten" in UI
2. `AufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad)` wird aufgerufen
3. Status wechselt zu `InBearbeitung`, BranchName und LokalerKlonPfad werden gespeichert
4. `EntwicklungsprozessService.ProzessStartenAsync()` wird aufgerufen:
   - Git-Repository wird geklont (falls nicht vorhanden)
   - Branch wird erstellt/ausgecheckt
   - Agentenpaket wird deployed
   - `AufgabeService.KiAktiviertAsync()` setzt Status zu `KiAktiv`
5. `KiAusfuehrungsService.StartKiLauf()` wird aufgerufen mit Initial-Prompt
6. CLI-Prozess wird gestartet, Fenster-Handle wird extrahiert und eingebettet
7. Stdout-Stream wird gepuffert, alle Zeilen werden in `Protokolleintrag` gespeichert
8. Wenn `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601]]` erkannt wird, Prompt-Vorschlag wird in Aufgabe gespeichert
9. Bei Completion oder Fehler: Status → `InBearbeitung` oder `Beendet` (Fehler mit Protokollierung)

Beteiligte Klassen/Komponenten: `AufgabeService`, `EntwicklungsprozessService`, `KiAusfuehrungsService`, `GitOrchestrationService`, `ProtokollService`, `IGitPlugin`, `IKiPlugin`

### Aufgabe mit Prompt-Vorschlag fortsetzen

1. Benutzer sieht gespeicherten Prompt-Vorschlag in UI
2. Benutzer klickt "Fortsetzen mit Vorschlag" oder "Jetzt ausführen" (falls `VorschlagAusfuehrenAbUtc` erreicht)
3. `KiAusfuehrungsService.StartKiLauf()` wird mit gespeichertem Prompt aufgerufen
4. `Aufgabe.VorschlagPrompt` wird geleert nach erfolgreicher Ausführung

Beteiligte Klassen/Komponenten: `AufgabeService`, `KiAusfuehrungsService`, `ProtokollService`

### Recovery von festhängenden Aufgaben

1. `AufgabeRecoveryService` lädt beim Startup alle Aufgaben mit Status `KiAktiv` oder `TestsLaufen`
2. Für jede Aufgabe wird geprüft:
   - `LastHeartbeatUtc < (Now - 5 Min)` → Kandidat für Recovery
   - `KiAusfuehrungsService.IsRunning(aufgabeId)` → Falls FALSE: Recovery möglich
3. Recovery-Kandidaten werden in UI angezeigt
4. Benutzer kann manuell "Wiederherstellen" klicken
5. Status wird auf `InBearbeitung` zurückgesetzt, `RecoveryVersion` wird inkrementiert

Beteiligte Klassen/Komponenten: `AufgabeRecoveryService`, `KiAusfuehrungsService`, `AufgabeService`, `ProtokollService`

### Plugin-Konfiguration und Einstellungen

1. Benutzer öffnet "Einstellungen > Plugins"
2. UI lädt `PluginManager.GetSourceCodeManagementPlugins()` und `GetDevelopmentAutomationPlugins()`
3. Für jedes Plugin wird `GetSettingGroups()` aufgerufen
4. UI generiert automatisch Eingabefelder basierend auf `PluginSettingFieldType` (String, Integer, Boolean, File-Path, etc.)
5. Benutzer füllt Felder aus
6. `PluginSettingsService.SetValue(plugin, fieldKey, value)` speichert Wert (verschlüsselt via Windows Credential Store für Secrets)
7. Änderungen sind sofort bei nächster Plugin-Verwendung verfügbar

Beteiligte Klassen/Komponenten: `PluginManager`, `IPlugin.GetSettingGroups()`, `PluginSettingsService`, `WindowsCredentialStore`

### Benachrichtigungen bei Aufgaben-Statuswechsel

1. Statuswechsel tritt auf (via `AufgabeService.StatusSetzenAsync()` oder spezialisierte Methoden)
2. `ProtokollService.AddStatusUebergangAsync()` erstellt Protokolleintrag
3. `BenachrichtigungsService.ShouldNotifyAsync(aufgabeId, statusEvent)` wird geprüft:
   - Modus `Immer` → immer benachrichtigen
   - Modus `Nie` → nicht benachrichtigen
   - Modus `NurBeiFehler` → nur bei Status `Beendet` mit Fehler benachrichtigen
4. Wenn benachrichtigen: `BenachrichtigungsService.DispatchNotificationAsync(aufgabeId, kanal, modus)`
5. Kanal `Audio`: `MediaPlayer` spielt konfigurierte Audiodatei ab (WAV/MP3)
6. Kanal `System`: Windows Toast-Benachrichtigung wird angezeigt
7. Aktion wird in `BenachrichtigungsDispatchLog` dokumentiert

Beteiligte Klassen/Komponenten: `AufgabeService`, `ProtokollService`, `BenachrichtigungsService`, `MediaPlayer` (WPF), `ToastNotification` (Windows.UI.Notifications)

### Kontextkomprimierung bei Hard-Limit-Überschreitung

1. Nach jeder Ausgabezeile aus CLI wird Kontext gezählt
2. Bei Soft-Limit (12.000 Zeichen) wird Benachrichtigung an UI gesendet (optional)
3. Bei Hard-Limit (20.000 Zeichen):
   - CLI wird pausiert
   - Komprimierungs-Prompt wird erzeugt: „Zusammenfasse diese Konversation gemäß Pflicht-Abschnitten: Ziel, Offene Punkte, Letzte Entscheidungen"
   - Neuer KI-Lauf wird gestartet mit Komprimierungs-Prompt
   - Komprimierter Kontext wird aktualisiert
   - Ursprüngliche CLI wird mit komprimiertem Kontext fortgesetzt
4. `ContainsMandatoryCompressionSections(compressed)` prüft, ob Pflicht-Abschnitte vorhanden sind

Beteiligte Klassen/Komponenten: `EntwicklungsprozessService`, `KiAusfuehrungsService`, `ProtokollService`

### Dark Mode Toggle

1. Benutzer öffnet "Einstellungen > Erscheinungsbild"
2. Toggle für "Dark Mode" wird aktiviert/deaktiviert
3. `AppEinstellungService.SetSettingAsync("DarkModeEnabled", "true"/"false")` speichert Einstellung
4. `DarkModeService.ApplyTheme()` wird aufgerufen:
   - Alle WPF-Ressourcen (Brushes, Colors) werden dynamisch neu gesetzt
   - Views werden aktualisiert (über ResourceDictionary oder Direct-Binding)
5. Einstellung bleibt über Neustart erhalten

Beteiligte Klassen/Komponenten: `DarkModeService`, `AppEinstellungService`, `MainWindow.xaml`, WPF-ResourceDictionary
6. CLI beendet sich oder Benutzer beendet manuell
7. Status wird basierend auf CLI-Exit-Code aktualisiert: `Beendet` (erfolgreich) oder `Wartend` (Rate-Limit erkannt)
8. Heartbeat wird aktualisiert

Beteiligte Klassen/Komponenten: `KiAusfuehrungsService`, `IKiPlugin`, `ProcessWindowEmbedder`, `ProtokollService`, `AufgabeService`

### Rate-Limit-Erkennung & Vorschlag-Speicherung

1. Während CLI läuft, wird jede Ausgabezeile auf `[[SOFTWARESCHMIEDE_RATE_LIMIT]]`-Marker gescannt
2. Falls erkannt: Nachfolgende Zeilen als "Vorschlag" gepuffert bis Marker-Ende
3. `AufgabeService.SavePromptVorschlagAsync()` speichert Vorschlag + optionalen Ausführungszeitpunkt (aus Marker geparst)
4. Status → `Wartend`
5. UI zeigt Wiedereinstiegszeitpunkt an
6. Benutzer kann "Fortsetzen" klicken → CLI wird erneut mit `--continue-session`-Flag gestartet (falls Plugin unterstützt)

Beteiligte Klassen/Komponenten: `ProtokollService`, `AufgabeService`, `KiAusfuehrungsService`

### Recovery – Neue Ablauf-Sequenz

1. Beim Starten der Anwendung: `AufgabeRecoveryService.ScanForRecoveryCandidatesAsync()`
2. Findet Aufgaben in Status `InArbeit` oder `Wartend` mit Heartbeat > 5 Min und **keine** laufenden CLI-Prozesse
3. UI zeigt Recovery-Banner mit Button "Wiederherstellen"
4. Benutzer klickt → Status bleibt `InArbeit`, aber CLI-Prozess wird als beendet markiert
5. Nächster "CLI Starten" kann erfolgen (oder direkt mit `--continue-session`)

Beteiligte Klassen/Komponenten: `AufgabeRecoveryService`, `AufgabeService`, `KiAusfuehrungsService`, `ProtokollService`

### Benachrichtigungen – Status-Änderung Ablauf

1. Status wechselt (z.B. `Neu` → `ArbeitsverzeichnisEingerichtet`)
2. `BenachrichtigungsService.ShouldNotifyAsync(aufgabeId, newStatus)` prüft Modus
3. Falls `Modus == Banner`: Toast-Benachrichtigung in Windows
4. Falls `Modus == Ton`: Audio-Datei abspielen (MP3, WAV, OGG)
5. Falls `Modus == Deaktiviert`: Keine Aktion
6. `BenachrichtigungsDispatchLog` protokolliert Entscheidung

Beteiligte Klassen/Komponenten: `BenachrichtigungsService`, `AufgabeService`, `ProtokollService`

### Einstellungen Speichern & Laden

1. Benutzer navigiert zu SettingsView
2. UI-Felder werden aus `IPlugin.GetSettingGroups()` generiert
3. Benutzer ändert Wert
4. `PluginSettingsService.SavePluginSettingAsync(pluginPrefix, fieldKey, value)` speichert Wert
5. Falls Wert sensitiv (API-Key, Token): Windows Credential Store (DPAPI) verschlüsselt
6. `PluginSettingsChangedEvent` wird ausgelöst
7. Betroffene Services reinitialisieren Plugins (falls nötig)

Beteiligte Klassen/Komponenten: `PluginSettingsService`, `AppEinstellungService`, `WindowsCredentialStore`, `IPlugin`, `PluginSettingGroup`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MainWindow` | WPF Window | Hauptfenster mit Navigation und zentralem Inhaltsbereich |
| `MainWindowViewModel` | ViewModel (MVVM) | State Management für MainWindow (Navigation, Dark Mode Toggle) |
| `NavigationViewModel` | ViewModel | Verwaltung Menü-State (einklappbar/expandiert) |
| `DashboardView` | UserControl | Dashboard mit Projekt- und Aufgabenzähler sowie zuletzt geänderte Aufgaben |
| `DashboardViewModel` | ViewModel | Logic für Dashboard |
| `ProjectListView` | UserControl | Tabelle aller Projekte mit CRUD-Buttons |
| `ProjectListViewModel` | ViewModel | Logic für Projektliste |
| `ProjectDetailView` | UserControl | Projekt bearbeiten, Git-Integration, Aufgabenliste |
| `ProjectDetailViewModel` | ViewModel | Logic für Projektdetail |
| `TaskListView` | UserControl | Aufgabenliste im Projekt (mit Filterung nach Status) |
| `TaskListViewModel` | ViewModel | Logic für Aufgabenliste |
| `TaskDetailView` | UserControl | Aufgabendetails, Statusübergänge, Protokoll, eingebettetes CLI-Fenster |
| `TaskDetailViewModel` | ViewModel | Logic für Aufgabendetail und CLI-Management |
| `SettingsView` | UserControl | Tabs für Quellcodeverwaltung, KI-Ausführung, Logging |
| `SettingsViewModel` | ViewModel | Logic für Einstellungen |
| `PluginSettingsView` | UserControl | Automatisch generierte UI-Felder basierend auf `PluginSettingGroup`-Typen |
| `PluginSettingsViewModel` | ViewModel | Logic für Plugin-Einstellungen |
| `DarkModeService` | Service | Verwaltung Dark Mode (Theme-Wechsel, Persistierung) |
| `ProcessWindowEmbedder` | Service | Win32 `SetParent` API-Wrapper für Fenster-Einbettung |
| `CliProcessManager` | Service | Verwaltung von CLI-Prozessen: Start, Stop, Heartbeat-Tracking |
| `BenachrichtigungsService` (erweitert) | Service | Audio-Support, Toast-Integration, Dispatch-Logging |
| `DiffViewControl` | UserControl (optional) | Visuelle Darstellung von Diff-Ergebnissen (aus `DiffResult`-Entity) |
| `DiffViewViewModel` (optional) | ViewModel | Logic für Diff-Anzeige |
| `ProcessWindowHost` | WPF Control | Host-Container für eingebettete CLI-Prozesse |
| `StatusIndicatorControl` | UserControl | Visuelle Anzeige des Task-Status mit Animationen |
| `RecoveryBannerControl` | UserControl | Banner für Recovery-Kandidaten |

## Änderungen an bestehenden Klassen

### `AufgabeStatus` (Enum)

- **Ersetzung:** Alle bisherigen Werte werden **entfernt und ersetzt**:
  - Alt: `Offen`, `InBearbeitung`, `KiAktiv`, `TestsLaufen`, `Abgeschlossen`, `Fehlgeschlagen`, `Archiviert`
  - Neu: `Neu`, `ArbeitsverzeichnisEingerichtet`, `Gestartet`, `InArbeit`, `Wartend`, `Beendet`, `Archiviert`
- **Mapping für Datenmigration:** Siehe Datenbankmigrationen

### `BenachrichtigungsModus` (Enum)

- **Ersetzung:** Alte Werte werden ersetzt:
  - Alt: `Deaktiviert`, `NurAufgabenseite`, `Global`
  - Neu: `Deaktiviert`, `Banner`, `Ton`

### `BenachrichtigungsKanal` (Enum)

- **Ersetzung:** Alte Werte werden ersetzt:
  - Alt: `Toast`, `Ton`
  - Neu: `Banner`, `Ton` (entsprechend `BenachrichtigungsModus`)

### `Aufgabe` (Entity)

- **Neue Eigenschaften:** Keine neuen Felder erforderlich (bestehende reichen aus: `BranchName`, `LokalerKlonPfad`, `AktiveRunId`, `LastHeartbeatUtc`, `RecoveryVersion`, `VorschlagPrompt`, `VorschlagAusfuehrenAbUtc`)
- **Geänderte Semantik:** `AktiveRunId` und `LastHeartbeatUtc` werden jetzt zur CLI-Prozess-Verfolgung verwendet (statt Session-Streaming)
- **Neue Methoden / Logik:** Status-Übergänge folgen neuem Modell

### `IKiPlugin` (Interface)

- **Entfernte Methoden:** 
  - `StartDevelopmentAsync(prompt, agent, localRepoPath, model, ct) → IAsyncEnumerable<string>`
  - `GetAvailableAgentsAsync(agentPackagePath, ct) → IEnumerable<AgentInfo>`
  - `IsAgentPackageCompatibleAsync(agentPackagePath, ct) → bool`
  - `DeployAgentPackageAsync(agentPackagePath, localRepoPath, ct)`
- **Neue Methoden:**
  - `StartCliAsync(localRepoPath, parameters?, ct) → ProcessStartInfo` — Startet CLI mit optionalen Parametern, gibt ProcessStartInfo zurück
  - `GetProcessWindowTitle(aufgabeId) → string` — Hilft beim Identifizieren des Prozess-Fensters (optional)
  - `SupportsSessionContinuation() → bool` — Gibt an, ob Plugin `--continue-session`-Flag unterstützt
- **Begründung:** Plugin ist nur noch Wrapper für CLI-Start; nicht für Prompt/Kontext/Output verantwortlich

### `CliKiPluginBase` (Abstrakte Basis)

- **Entfernte Methoden:**
  - `BuildContextFilePath()`, `GetLatestContextFilePath()`, `ClearContextFiles()`, `MarkPromptToIncludeContextFile()`, `UnwrapPromptContextMarker()`, `EnsureGitignoreEntries()`
- **Neue Methoden:**
  - `BuildProcessStartInfo(localRepoPath, parameters) → ProcessStartInfo` — Konstruiert ProcessStartInfo für CLI
  - `ExtractWindowTitleFromProcess(process) → string` — Hilft bei Fenster-Identifikation

### `KiAusfuehrungsService` (Service)

- **Entfernte Methoden:**
  - `StartKiLauf(aufgabeId, prompt, agentName, ...) → IAsyncEnumerable<string>` (mit Prompt-Parameter)
- **Neue Methoden:**
  - `StartCliAsync(aufgabeId, kiPluginPrefix, optionalParameters?, ct) → ProcessHandle` — Startet CLI und gibt Handle zurück
  - `StopCliAsync(aufgabeId, ct)` — Stoppt laufenden Prozess
  - `IsCliRunning(aufgabeId) → bool` — Prüft ob CLI-Prozess läuft
  - `GetLastExitCode(aufgabeId) → int?` — Gibt Exit-Code des letzten Prozesses zurück
  - `UpdateHeartbeat(aufgabeId)` — Aktualisiert LastHeartbeatUtc
- **Geändertes Verhalten:** Keine Prompt-Pufferung; nur Prozess-Management und Stdout-Parsing für Rate-Limit-Erkennung
- **Neue Property:**
  - `CliProcessStatusChanged` { event } — Wird ausgelöst bei Prozess-Start, -Stop, -Fehler
- **Abhängigkeiten:** `IKiPlugin`, `ProcessWindowEmbedder`, `AufgabeService`, `ProtokollService`

### `EntwicklungsprozessService` (Service)

- **Entfernte Methoden:** Fast alle; dieser Service wird zu einem reinen Prozess-Manager
- **Neue Fokus:** Nur noch Git-Repository-Setup und Rate-Limit-Erkennung
- **Geändertes Verhalten:** Keine Kontextkomprimierung (CLI macht das selbst); Rate-Limit-Marker-Parsing bleibt
- **Abhängigkeiten:** `IGitPlugin`, `AufgabeService`, `ProtokollService`

### `AufgabeService` (Service)

- **Neue Methoden:**
  - `SetStatusAsync(aufgabeId, newStatus, ct)` — Setzt Status mit Validierung
  - `UpdateHeartbeatAsync(aufgabeId, ct)` — Aktualisiert LastHeartbeatUtc
  - `GetHeartbeatAgeMinutesAsync(aufgabeId, ct) → int?` — Berechnet Minuten seit letztem Heartbeat
- **Geänderte Methoden:**
  - `StartenAsync()`: Status → `ArbeitsverzeichnisEingerichtet` statt `InBearbeitung`
  - `AbschliessenAsync()`: Status → `Beendet` statt `Abgeschlossen`
  - Alle Methoden, die Status verwenden, werden auf neue Enum-Werte aktualisiert
- **Neue Event-Handler:** Auf `KiAusfuehrungsService.CliProcessStatusChanged` reagieren für automatische Heartbeat-Updates

### `AufgabeRecoveryService` (Service)

- **Geänderte Bedingungen:**
  - Status muss `InArbeit` oder `Wartend` sein (statt `KiAktiv` oder `TestsLaufen`)
  - Heartbeat > 5 Min
  - Keine laufenden CLI-Prozesse (via `CliProcessManager`)
- **Neue Methode:**
  - `ScanForRecoveryCandidatesAsync(ct) → IEnumerable<Guid>` — Gibt alle Recovery-Kandidaten zurück

### `ProtokollService` (Service)

- **Neue Methoden:**
  - `AddCliOutputAsync(aufgabeId, outputLine, ct)` — Speichert Zeile als Protokolleintrag (Typ: KiAntwort)
  - `ParseRateLimitMarker(outputLine) → (bool, string?, DateTimeOffset?)` — Parst `[[SOFTWARESCHMIEDE_RATE_LIMIT]]`-Marker

### `AppEinstellung` (Entity / Service)

- **Neue Einträge** (Schlüssel):
  - `WindowPosition.X`, `WindowPosition.Y`, `WindowPosition.Width`, `WindowPosition.Height` — Fenstergeometrie
  - `DarkModeEnabled` — Bool für Dark Mode
  - `DefaultKiPlugin` — KI-Plugin-Prefix (bestehend, ggf. neu persistieren)
  - `LogLevel` — Debug oder Information
- **Service-Methoden:** Bestehende `GetSettingAsync()` / `SetSettingAsync()` reichen aus

### `BenachrichtigungsService` (Service)

- **Neue Methoden:**
  - `PlayAudioAsync(filePath, ct)` — Spielt Audiodatei ab (MP3, WAV, OGG)
  - `ShowBannerAsync(aufgabeId, message, ct)` — Zeigt Banner-Benachrichtigung
- **Geändertes Verhalten:** Verwendet neues `BenachrichtigungsModus`-Enum (`Deaktiviert`, `Banner`, `Ton`)
- **Neue Abhängigkeiten:** Windows Notification APIs oder NuGet (z.B. `Windows.UI.Notifications`)

### `PluginManager` (Service)

- **Keine Änderungen erforderlich** — Weiterhin Reflection-basiertes Loading aus `/plugins`
- **PluginType-Mapping überprüfen:** `IKiPlugin` muss als `DevelopmentAutomation` registriert sein

### `WindowsCredentialStore` (Service)

- **Keine Änderungen erforderlich** — Weiterhin für sensitive Plugin-Settings verwenden

### `CliRunner` (Service)

- **Neue Methode (optional):**
  - `GetProcessHandle(processId) → ProcessHandle` — Hilft bei Fenster-Einbettung

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `UpdateAufgabeStatusEnum` | `Aufgaben.Status` | Umwandlung alter Enum-Werte zu neuen: `Offen` → `Neu`, `InBearbeitung` → `ArbeitsverzeichnisEingerichtet` oder `Gestartet` (abhängig von `BranchName`), `KiAktiv` / `TestsLaufen` → `InArbeit`, `Abgeschlossen` → `Beendet`, `Fehlgeschlagen` → bleibt oder → `Beendet` (abhängig von Anforderung), `Archiviert` → bleibt |
| `UpdateBenachrichtigungsModus` | `AppEinstellung.Wert` (für Schlüssel `NotificationMode`) | Umwandlung: `Immer` → `Banner`, `Nie` → `Deaktiviert`, `NurBeiFehler` → `Banner` (mit Filter in Code) |
| `UpdateBenachrichtigungsKanal` | `AppEinstellung.Wert` (für Schlüssel `NotificationChannel`) | Umwandlung: `Audio` → `Ton`, `System` → `Banner` |
| `AddWindowGeometrySettings` | `AppEinstellung` (neue Einträge) | Hinzufügen der Spalten-Keys für Fensterposition und -größe (realisiert über Schlüssel-Wert-Paare) |
| `RemoveDeprecatedFields` | `Aufgaben` (optional) | Falls alte Felder wie `AgentenpaketName`, `AgentenName` nicht mehr genutzt werden: entfernen oder beibehalten (ggf. nur dokumentieren als deprecated) |
| `AddCliProcessTrackingFields` | `Aufgaben` | Bestehende Felder `AktiveRunId`, `LastHeartbeatUtc`, `RecoveryVersion` werden umgewidmet für CLI-Prozess-Tracking (Datentypen bleiben, Semantik ändert sich) |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Aufgabe.Titel` | Nicht leer, max. 255 Zeichen | Validierungsfehler in UI |
| `Aufgabe.AnforderungsBeschreibung` | Optional; max. 10.000 Zeichen | Validierungsfehler bei Speicherung |
| `Aufgabe.Status` (Übergang) | Nur erlaubte Übergänge: `Neu` → `ArbeitsverzeichnisEingerichtet` → `Gestartet` → `InArbeit` → `Beendet` oder `Wartend`; `*` → `Archiviert` | Exception: `InvalidStatusTransitionException` |
| `Projekt.Name` | Nicht leer, eindeutig, max. 255 Zeichen | Validierungsfehler |
| `IGitPlugin` Selection | Plugin muss geladen sein und Health-Check bestanden haben | Exception bei Service-Aufruf |
| `IKiPlugin` Selection | Plugin muss geladen sein und Health-Check bestanden haben; `CliAsync` muss unterstützen | Exception bei Service-Aufruf |
| `BenachrichtigungsService.AudioDateiPfad` | Falls `Modus == Ton`: Datei muss existieren, MP3/WAV/OGG-Format | Fehler beim Abspielen oder UI-Warnung |
| `Rate-Limit Marker` | Format: `[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-10T15:30:00Z]]` oder ähnlich | Ignorieren bei Parsing-Fehler; Logger-Warning |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `LoggingLevel` | Enum (Debug/Information) | Information | Logging-Granularität |
| `WindowPosition.X` | int | 100 | Fenster-X-Position |
| `WindowPosition.Y` | int | 100 | Fenster-Y-Position |
| `WindowPosition.Width` | int | 1200 | Fenster-Breite |
| `WindowPosition.Height` | int | 800 | Fenster-Höhe |
| `DarkModeEnabled` | bool | false | Dark Mode Toggle |
| `MaxParallelCliProcesses` | int | 1 | Max. CLI-Prozesse gleichzeitig pro Aufgabe (deprecated, nur noch 1) |
| `AutoCloneRepository` | bool | true | Automatisches Klonen von Repos |
| `DefaultKiPlugin` | string | "Claude" (oder erstes verfügbares) | Standard-KI-Plugin-Prefix |
| `DefaultGitPlugin` | string | "GitHub" (oder erstes verfügbares) | Standard-Git-Plugin-Prefix |
| `NotificationMode` | Enum (Deaktiviert/Banner/Ton) | Deaktiviert | Benachrichtigungs-Modus |
| `NotificationAudioPath` | string | (leer) | Pfad zu Benachrichtigungs-Audiodatei |
| `HeartbeatTimeoutMinutes` | int | 5 | Minuten bis Heartbeat als "alt" gilt für Recovery |
| `RateLimitMarkerFormat` | string | "[[SOFTWARESCHMIEDE_RATE_LIMIT" | Prefix für Rate-Limit-Marker-Erkennung |

## Seiteneffekte und Risiken

- **Enum-Migration Datenbank:** Bestehende Aufgaben-Status müssen gemappt werden. Risiko: Falsche Mappings führen zu unerwarteten Status-Zuständen → Sorgfältige Migrations-Tests erforderlich.
- **`IKiPlugin` Schnittstellen-Break:** Alle Plugin-Implementierungen müssen angepasst werden (entfernen von `StartDevelopmentAsync`, `GetAvailableAgentsAsync`, etc.). Risiko: Inkompatible Plugins laden fehlerhaft → Fallback-Handling und Fehler-Messages erforderlich.
- **Kontextmanagement Verschiebung:** CLI muss Session-Fortbestand selbst handeln. Risiko: CLI ohne Session-Support kann nicht fortgesetzt werden → Dokumentation und Fallback notwendig.
- **Fenster-Einbettung (Win32 SetParent):** Kann bei bestimmten CLI-Ausgaben oder Multi-Monitor-Setups fehlschlagen → Fallback auf separates Fenster implementieren.
- **Blazor-Code Entfernung:** Nach Entfernung kein Rollback möglich. Risiko: Feature-Regression → Vollständiges Testen vor Merge erforderlich.
- **Dark Mode nur WPF:** Keine Browser-UI mehr → Benachrichtigungen müssen Windows-native sein.
- **Rate-Limit-Erkennung via stdout-Parsing:** Fragil bei CLI-Output-Änderungen → Logger-Warnings bei Parsing-Fehlern implementieren.
- **Recovery-Service Logik:** Heartbeat-basiert; kann bei langen Pausen (> 5 Min) ohne CLI-Crash fälschlicherweise als Recovery-Kandidat erkannt werden → UI-Bestätigung vor Recovery erforderlich.
- **Bestehende Services:** `EntwicklungsprozessService` wird massiv vereinfacht; Code kann gelöscht oder depreciert sein → Sorgfältiges Refactoring.
- **Tests:** Alte Tests für `StartDevelopmentAsync`, `GetAvailableAgentsAsync`, etc. müssen angepasst oder gelöscht werden.

## Umsetzungsreihenfolge

1. **Enum-Definitionen aktualisieren** (`AufgabeStatus`, `BenachrichtigungsModus`, `BenachrichtigungsKanal`)
2. **Datenbank-Migrationen erstellen** (Status-Enum, Benachrichtigungs-Enums, Fenstergeometrie)
3. **`IKiPlugin`-Interface überarbeiten** (alte Methoden entfernen, neue hinzufügen)
4. **`CliKiPluginBase` anpassen** (neue Methoden für ProcessStartInfo-Konstruktion)
5. **`KiAusfuehrungsService` redesignen** (Prozess-Management statt Prompt-Streaming)
6. **`ProcessWindowEmbedder` + `CliProcessManager` Implementierung** (Win32 SetParent)
7. **`AufgabeService` Status-Methoden aktualisieren** (`SetStatusAsync()`, `UpdateHeartbeatAsync()`)
8. **`AufgabeRecoveryService` auf neue Status anpassen**
9. **`EntwicklungsprozessService` vereinfachen** (nur Git + Rate-Limit-Parsing)
10. **`BenachrichtigungsService` erweitern** (Audio, Banner, neue Enums)
11. **`AppEinstellungService` erweitern** (Fenstergeometrie, Dark Mode)
12. **WPF UI-Komponenten implementieren** (MainWindow, Views, ViewModels — in Reihenfolge: Dashboard → ProjectList → ProjectDetail → TaskList → TaskDetail → Settings)
13. **`DarkModeService` implementieren** (Theme-Wechsel)
14. **`ProcessWindowHost` + Fenster-Einbettung testen**
15. **Blazor-Code vollständig entfernen**
16. **Plugin-Implementierungen anpassen** (z.B. GitHub, Claude CLI, Local Directory)
17. **Unit-Tests aktualisieren** (alte Enum-Tests, neue Status-Transitions)
18. **Integrations-Tests aktualisieren** (Migrations-Tests, Service-Tests)
19. **E2E-Tests implementieren** (UI-Workflows, Prozess-Management)
20. **Dokumentation aktualisieren** (Plugin-Development-Guide, Status-Diagram)

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `TestNewStatusEnum()` | `AufgabeStatusEnumTests` | Neue Enum-Werte vorhanden und typsicher |
| `TestStatusTransitions()` | `AufgabeStatusTransitionTests` | Nur erlaubte Übergänge (Neu → ArbeitsverzeichnisEingerichtet → Gestartet → InArbeit → Beendet/Wartend; * → Archiviert) |
| `TestCliStartAsync()` | `KiAusfuehrungsServiceTests` | CLI wird gestartet, ProcessHandle wird zurückgegeben, Stdout wird gepuffert |
| `TestProcessWindowEmbedding()` | `ProcessWindowEmbedderTests` | SetParent wird aufgerufen, Handle wird in WPF-Control eingebettet |
| `TestHeartbeatUpdate()` | `AufgabeServiceTests` | LastHeartbeatUtc wird aktualisiert, Alter wird korrekt berechnet |
| `TestRecoveryCandidates()` | `AufgabeRecoveryServiceTests` | Aufgaben mit Heartbeat > 5 Min und Status InArbeit/Wartend werden erkannt |
| `TestRateLimitMarkerParsing()` | `ProtokollServiceTests` | Marker wird geparst, Vorschlag wird gespeichert, DateTimeOffset wird extrahiert |
| `TestBenachrichtigungNewEnum()` | `BenachrichtigungsServiceTests` | Neue Enums (Deaktiviert, Banner, Ton) funktionieren |
| `TestAudioPlayback()` | `BenachrichtigungsServiceTests` | Audio-Datei wird abgespielt, Fehlerfall wird gehandhabt |
| `TestDarkModeToggle()` | `DarkModeServiceTests` | Dark Mode wird aktiviert/deaktiviert, Theme wechselt, Setting wird persistiert |
| `TestWindowGeometryPersistence()` | `AppEinstellungServiceTests` | Fensterposition und -größe werden gespeichert und geladen |
| `TestStatusValidation()` | `AufgabeServiceTests` | Ungültige Status-Übergänge werfen Exception |
| `TestPluginHealthCheck()` | `PluginManagerTests` | Plugin-Health wird geprüft, ungültige Plugins werden nicht geladen |
| `SetupNewEnumValues()` | Test Helper | Hilfsmethode zum Erstellen von Aufgaben mit neuen Status-Werten |
| `CreateMockCliProcess()` | Test Helper | Mock-Prozess für Komponenten-Tests |
| `CreateTestDbContextWithMigrations()` | `TestDbContextFactory` | DB mit allen neuen Migrationen für Integration-Tests |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AufgabeServiceTests` | Alle Status-Tests müssen auf neue Enum-Werte aktualisiert werden (z.B. `StartenAsync()` erwartet jetzt `ArbeitsverzeichnisEingerichtet`, nicht `InBearbeitung`) |
| `KiAusfuehrungsServiceTests` | `StartKiLauf()` mit Prompt wird entfernt; `StartCliAsync()` Tests ersetzen diese. Streaming-Tests müssen auf Stdout-Parsing umgestellt werden. |
| `AufgabeRecoveryServiceTests` | Recovery-Bedingungen ändern sich (neue Status, neue Heartbeat-Logik) |
| `EntwicklungsprozessServiceTests` | Service wird stark vereinfacht; Tests für Kontextkomprimierung, Agentenpaket-Deploy, etc. werden entfernt oder angepasst |
| `BenachrichtigungsEinstellungenServiceTests` | Neue Enum-Werte müssen getestet werden |
| `PluginManagerTests` | Plugins mit neuem `IKiPlugin`-Interface müssen neu geladen werden; alte Plugins schlagen fehl |
| `All Plugin Tests` (`GitHubPluginTests`, `KiSimulatorPluginTests`, etc.) | Plugin-Methoden-Signaturen ändern sich; Tests müssen aktualisiert werden |
| `DiffServiceTests` | Keine direkten Änderungen, aber `DiffResult` kann neue Abhängigkeiten haben (z.B. zu neuen Status) |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Projekt erstellen und Aufgabe hinzufügen | `ProjectE2ETests` | Projekt wird erstellt, Aufgabe wird hinzugefügt, Status ist `Neu` |
| Aufgabe starten → Repository klonen → Branch erstellen | `TaskStartupE2ETests` | Aufgabe startet, Git-Ops erfolgen, Status wechselt zu `ArbeitsverzeichnisEingerichtet` → `Gestartet` |
| CLI-Prozess starten und Fenster einbetten | `CliEmbeddingE2ETests` | CLI-Prozess wird gestartet, Fenster wird in WPF-Control eingebettet, Benutzer kann interagieren |
| Stdout-Streaming und Protokoll-Eintrag | `ProtocolLoggingE2ETests` | CLI-Output wird gepuffert und zu Protokolleinträgen, sichtbar in UI |
| Rate-Limit-Erkennung und Vorschlag-Speicherung | `RateLimitDetectionE2ETests` | Marker wird erkannt, Vorschlag wird gespeichert, Status → `Wartend`, UI zeigt Wiedereinstiegszeitpunkt |
| Recovery-Scan beim Startup | `RecoveryE2ETests` | App startet, erkennt alte Aufgaben in InArbeit/Wartend, zeigt Recovery-Banner |
| Dark Mode aktivieren und persistieren | `DarkModeE2ETests` | User aktiviert Dark Mode, UI wechselt, Setting wird gespeichert, beim Neustart bleibt Dark Mode aktiv |
| Benachrichtigungen (Banner + Audio) | `NotificationE2ETests` | Status-Wechsel triggert Banner/Audio basierend auf Modus |
| Plugin-Konfiguration speichern und laden | `PluginSettingsE2ETests` | User speichert Einstellung, Wert wird verschlüsselt, beim Reload wird Wert geladen |
| Fensterposition beim Neustart wiederherstellen | `WindowGeometryE2ETests` | User verschiebt/skaliert Fenster, beim Neustart hat Fenster gleiche Geometrie |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UIRenderingTests` | Status-Indikatoren und Buttons müssen neue Enum-Werte darstellen |
| `TaskLifecycleE2ETests` | Ablauf ändert sich: Neu → ArbeitsverzeichnisEingerichtet → Gestartet → InArbeit; alte Tests mit Offen → InBearbeitung werden ungültig |
| `ProcessManagementE2ETests` | KI-CLI-Prozess wird anders gestartet (keine Prompt-Übergabe); alte Tests entfernen |
| `PluginLoadingE2ETests` | Plugin-Interfaces ändern sich; Plugin-Tests müssen neue Methoden testen |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | **Kontextdateien in CLI:** Wie speichert die externe CLI ihre Kontexte (z.B. Claude CLI mit `.context`-Dateien)? Sollen diese im Repo liegen oder außerhalb? | Kontexte sollten im Arbeitsverzeichnis (lokal geklontes Repo) unter `.softwareschmiede/`-Verzeichnis gespeichert werden. Eintrag in `.gitignore` durch `CliKiPluginBase.EnsureGitignoreEntries()` (oder äquivalent in neuem Design). |
| 2 | **CLI Fenster-Identifizierung:** Wie wird das CLI-Prozess-Fenster identifiziert (z.B. nach Titel, Window-Class, Child-Window)? | Plugin gibt optional `GetProcessWindowTitle()` oder `GetWindowClass()` zurück. Win32 `FindWindow()` mit diesem Titel/Klasse zum Finden des Handles verwenden. |
| 3 | **Session-Fortbestand-Parameter:** Welche exakte CLI-Flag/Parameter-Form nutzen wir für Session-Fortbestand (z.B. `--continue-session`, `--session-dir`, `--resume`)? | Plugin implementiert `SupportsSessionContinuation()` → `true/false`. Falls true: UI bietet "Fortsetzen"-Button, der CLI mit plugin-spezifischem Flag startet (z.B. Claude CLI: `claude --continue-session`). |
| 4 | **Rate-Limit Zeitstempel-Format:** Exaktes Format für Rate-Limit-Marker mit Zeitstempel? (ISO8601? Epoch? benutzerdefiniert?) | Format: `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601_DATETIME]]`, z.B. `[[SOFTWARESCHMIEDE_RATE_LIMIT:2026-06-10T15:30:00Z]]`. CLI gibt diesen exakt aus, Parsing erfolgt mit `DateTimeOffset.TryParse()`. |
| 5 | **Fenster-Einbettung bei Fehler:** Was tun, wenn SetParent fehlschlägt (z.B. bei bestimmten CLI-Tools)? | Fallback: Separates Fenster in `AlwaysOnTop`-Modus neben WPF-App. Warnung in Logs. |
| 6 | **Audio-Datei-Format & Playback-Library:** Welche Bibliothek für WAV/MP3/OGG-Playback in WPF? | Optionen: (a) `System.Media.SoundPlayer` (nur WAV), (b) `NAudio` NuGet, (c) Windows Media Foundation (WMF). Empfehlung: NAudio oder WMF für Formate-Unterstützung. Details in Plugin-Setup. |
| 7 | **Mehrere Repositories pro Projekt UI-Handling:** Wie zeigt die UI mehrere Repos an, wenn später mehrere pro Projekt nötig sind? | Momentan: UI zeigt "eines" Repo (erste aktive), Datenmodell unterstützt mehrere. Falls später nötig: Dropdown oder Tab-View in ProjectDetailView. Aktuell nicht implementieren. |
| 8 | **Logging-Dateipfad:** Wo landen Log-Dateien? (z.B. `%APPDATA%\Softwareschmiede\logs\`, Projekt-Verzeichnis?) | Standardpfad: `%LOCALAPPDATA%\Softwareschmiede\logs\` (z.B. `C:\Users\Martin\AppData\Local\Softwareschmiede\logs\`). Erstellt automatisch beim Startup. |
| 9 | **Herunterfahren mit aktiven CLI-Prozessen:** Forceful kill oder graceful shutdown mit Timeout? | Graceful: SIGTERM → 5 Sekunden warten → Falls noch aktiv, SIGKILL. Abfrage-Dialog zur Bestätigung. |
| 10 | **Heartbeat-Aktualisierung:** Wann wird `LastHeartbeatUtc` aktualisiert? (Jede Ausgabezeile? Periodisch? Nur beim Status-Wechsel?) | Periodisch: `KiAusfuehrungsService` startet Timer, aktualisiert Heartbeat alle 30 Sekunden während CLI läuft. Bei Prozess-Stop wird letzter Heartbeat beibehalten. |
| 11 | **Migration von alten Status-Werten — Fehlgeschlagen:** Alt-Enum hat `Fehlgeschlagen`, neu-Enum auch. Sollte es beibehalten werden oder zu `Beendet` gemappt werden? | Beibehalten: `Fehlgeschlagen` wird neu definiert als "CLI beendete sich mit Fehler-Exit-Code (!= 0)". Alte Werte: Alt-`Fehlgeschlagen` → Neu-`Beendet` (da Fehler-Semantik nicht mehr vorhanden in CLI-Design). |
| 12 | **Agentenpaket & Agentenname — Noch nötig?** | Nein. Diese Felder werden in `Aufgabe` nicht mehr genutzt (nur noch Dokumentation). Können optional in zukünftiger Version wieder eingeführt werden. Migration: Felder bleiben in DB, werden aber null gesetzt. |

Keine weiteren offenen Punkte — alle Designentscheidungen sind aus den Anforderungsantworten ableitbar.
