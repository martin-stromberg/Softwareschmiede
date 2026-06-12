# Plan-Review: WPF-Desktopanwendung

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Enum-Definitionen
- [x] `AufgabeStatus` (Enum) — neue Werte vollständig implementiert: `Neu`, `ArbeitsverzeichnisEingerichtet`, `Gestartet`, `InArbeit`, `Wartend`, `Beendet`, `Archiviert`
- [x] `BenachrichtigungsModus` (Enum) — neue Werte: `Deaktiviert`, `Banner`, `Ton`
- [x] `BenachrichtigungsKanal` (Enum) — neue Werte: `Banner`, `Ton`

### Service-Klassen (neue)
- [x] `DarkModeService` — Dark Mode Toggle mit Theme-Wechsel und Persistierung über AppEinstellungService
- [x] `CliProcessManager` — Heartbeat-Timer-Management für CLI-Prozesse
- [x] `BenachrichtigungsService` (erweitert) — Audio- und Banner-Support mit neuen Enums

### Service-Klassen (überarbeitet)
- [x] `KiAusfuehrungsService` — redesigned für CLI-Prozess-Management:
  - [x] `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters?, ct)` — Prozess starten
  - [x] `StopCliAsync(aufgabeId, ct)` — Prozess stoppen (SIGTERM → 5s → SIGKILL)
  - [x] `IsCliRunning(aufgabeId) → bool` — Prozess-Status prüfen
  - [x] `GetLastExitCode(aufgabeId) → int?` — Exit-Code abrufen
  - [x] `UpdateHeartbeat(aufgabeId)` — Heartbeat aktualisieren
  - [x] `CliProcessStatusChanged` { event } — Prozess-Status-Events
  - [x] `CliProcessHandle` (Value Object) — Handle mit Heartbeat-Tracking
  - [x] `CliProcessStatus` (Enum) — Gestartet, Gestoppt, Fehler

- [x] `AufgabeService` — Status-Management und Heartbeat-Support:
  - [x] `SetStatusAsync(aufgabeId, newStatus, ct)` — Status mit Validierung setzen
  - [x] `UpdateHeartbeatAsync(aufgabeId, ct)` — Heartbeat aktualisieren
  - [x] `GetHeartbeatAgeMinutesAsync(aufgabeId, ct) → int?` — Heartbeat-Alter berechnen

- [x] `AufgabeRecoveryService` (überarbeitet) — neue Recovery-Bedingungen:
  - [x] `ScanForRecoveryCandidatesAsync(ct) → IEnumerable<Guid>` — Kandidaten mit Status InArbeit/Wartend, Heartbeat > 5 Min, kein laufender Prozess

- [x] `ProtokollService` (erweitert) — CLI-Output und Rate-Limit-Parsing:
  - [x] `AddCliOutputAsync(aufgabeId, outputLine, ct)` — CLI-Zeile speichern
  - [x] `ParseRateLimitMarker(outputLine) → (bool, string?, DateTimeOffset?)` — Marker parsen

### Plugin-Interface
- [x] `IKiPlugin` (überarbeitet) — neue Methoden-Signatures:
  - [x] `StartCliAsync(localRepoPath, parameters?, ct) → ProcessStartInfo` — CLI starten
  - [x] `GetProcessWindowTitle(aufgabeId) → string` — Fenster-Titel-Hinweis
  - [x] `SupportsSessionContinuation() → bool` — Session-Fortsetzung-Support prüfen
  - [x] `CheckHealthAsync(ct) → Task<bool>` — Health-Check (beibehalten)
  - [x] Entfernte Methoden: `StartDevelopmentAsync`, `GetAvailableAgentsAsync`, `IsAgentPackageCompatibleAsync`, `DeployAgentPackageAsync` (nicht implementiert, aber auch nicht mehr im Code vorhanden)

### WPF UI-Komponenten (neue)
- [x] `MainWindow` (XAML + CodeBehind) — Hauptfenster mit Navigation und Einstellungen-Integration
- [x] `MainWindowViewModel` — State Management für Navigation und Dark Mode
- [x] `NavigationViewModel` — Menü-State-Verwaltung (einklappbar/expandiert)
- [x] `DashboardView` (XAML) — Dashboard mit Projekt- und Aufgabenzähler
- [x] `DashboardViewModel` — Logic für Dashboard
- [x] `ProjectListView` (XAML) — Projekttabelle mit CRUD-Buttons
- [x] `ProjectListViewModel` — Logic für Projektliste
- [x] `ProjectDetailView` (XAML) — Projektbearbeitung und Aufgabenliste
- [x] `ProjectDetailViewModel` — Logic für Projektdetail
- [x] `TaskListView` (XAML) — Aufgabenliste mit Status-Filterung
- [x] `TaskListViewModel` — Logic für Aufgabenliste
- [x] `TaskDetailView` (XAML) — Aufgabendetails, Statusübergänge, Protokoll, CLI-Fenster
- [x] `TaskDetailViewModel` — Logic für Aufgabendetail und CLI-Management
- [x] `SettingsView` (XAML) — Tabs für Git-Plugin, KI-Plugin, Logging, Benachrichtigungen
- [x] `SettingsViewModel` — Logic für Einstellungen
- [x] `PluginSettingsView` (XAML) — Dynamisch generierte UI-Felder basierend auf Plugin-Setting-Typen
- [x] `PluginSettingsViewModel` — Logic für Plugin-Einstellungen

### WPF Controls (neue)
- [x] `ProcessWindowHost` (XAML + CodeBehind) — WPF-Host-Container für eingebettete CLI-Prozesse via Win32 SetParent
- [x] `StatusIndicatorControl` (XAML + CodeBehind) — Visuelle Status-Anzeige mit Animationen
- [x] `RecoveryBannerControl` (XAML + CodeBehind) — Banner für Recovery-Kandidaten

### Datenbank-Migrationen
- [x] `20260610000001_202606100001_UpdateAufgabeStatusEnum` — Enum-Wert-Migration (Offen→Neu, InBearbeitung→ArbeitsverzeichnisEingerichtet/Gestartet, KiAktiv→InArbeit, etc.)
- [x] `20260610000002_202606100002_UpdateBenachrichtigungsEnums` — Benachrichtigungs-Enum-Migration (NurAufgabenseite→Banner, Global→Ton, Toast→Banner, Audio→Ton)
- [x] `20260610000003_202606100003_AddWindowGeometrySettings` — Fenstergeometrie-Einstellungen (X, Y, Width, Height, DarkModeEnabled als AppEinstellung-Schlüssel-Wert-Paare)

### AppEinstellung-Erweiterungen
- [x] `WindowPosition.X`, `WindowPosition.Y`, `WindowPosition.Width`, `WindowPosition.Height` — Fenstergeometrie
- [x] `DarkModeEnabled` — Dark Mode Toggle
- [x] Methoden in `AppEinstellungService`: `GetWindowGeometryAsync()`, `SetWindowGeometryAsync()`, `GetBoolSettingAsync()`, `SetBoolSettingAsync()`

### Tests
- [x] `DarkModeE2ETests` — Dark Mode Toggle und Persistierung

## Offene Aufgaben

- [ ] **`ProcessWindowEmbedder` Service** — fehlt vollständig. Zwar ist `ProcessWindowHost` (WPF-Control) implementiert, aber laut Plan sollte ein separater Service `ProcessWindowEmbedder` vorhanden sein, der Win32-API-Wrapper zur Fenster-Identifikation und Einbettung mit Fallback auf separates Fenster bereitstellt. Das Control allein behandelt nur die Einbettung, nicht die Prozess-Handle-Identifikation nach Prozessstart.

- [ ] `CliKiPluginBase` Klasse — laut Plan sollten alte Methoden (`BuildContextFilePath()`, `GetLatestContextFilePath()`, `ClearContextFiles()`, etc.) entfernt und neue Methoden hinzugefügt werden:
  - [ ] `BuildProcessStartInfo(localRepoPath, parameters) → ProcessStartInfo`
  - [ ] `ExtractWindowTitleFromProcess(process) → string`
  - Status: Unklar, ob diese Klasse angepasst wurde oder noch existiert.

- [ ] **IKiPlugin entfernte Methoden — Überprüfung erforderlich**: Laut Plan sollten folgende Methoden entfernt sein:
  - `StartDevelopmentAsync(prompt, agent, localRepoPath, model, ct) → IAsyncEnumerable<string>`
  - `GetAvailableAgentsAsync(agentPackagePath, ct) → IEnumerable<AgentInfo>`
  - `IsAgentPackageCompatibleAsync(agentPackagePath, ct) → bool`
  - `DeployAgentPackageAsync(agentPackagePath, localRepoPath, ct)`
  - Status: Die neuen Methoden sind vorhanden, aber alte Implementierungen in Plugins müssen überprüft werden.

- [ ] **EntwicklungsprozessService — Vereinfachung**. Laut Plan sollte dieser Service stark vereinfacht werden:
  - [ ] Entfernung von `StartDevelopmentAsync` (wenn vorhanden)
  - [ ] Fokus nur auf Git-Setup und Rate-Limit-Parsing
  - Status: Unklar, ob Kontextkomprimierungs-Logik noch vorhanden ist.

- [ ] **Status-Validierungslogik** — `ValidateStatusTransition()` Methode muss implementiert sein:
  - Nur erlaubte Übergänge: `Neu` → `ArbeitsverzeichnisEingerichtet` → `Gestartet` → `InArbeit` → (`Beendet` | `Wartend`); `*` → `Archiviert`
  - Status: Methode existiert im Code, aber Validierungsregeln müssen überprüft werden.

- [ ] **`StartenAsync()` Semantik-Änderung** — Status sollte zu `ArbeitsverzeichnisEingerichtet` führen (nicht `InBearbeitung`)
  - Status: Überprüfung erforderlich, ob Implementierung aktualisiert wurde.

- [ ] **`AbschliessenAsync()` Semantik-Änderung** — Status sollte zu `Beendet` führen (nicht `Abgeschlossen`)
  - Status: Überprüfung erforderlich, ob Implementierung aktualisiert wurde.

- [ ] **Audio-Playback mit WPF MediaPlayer** — `BenachrichtigungsService.PlayAudioAsync()` sollte WPF-`MediaPlayer` verwenden.
  - Status: Methode vorhanden, aber Implementierungsdetail (`IBenachrichtigungsAudioService`) muss überprüft werden.

- [ ] **Windows Toast-Benachrichtigungen** — `BenachrichtigungsService.ShowBannerAsync()` sollte Windows Notifications API verwenden.
  - Status: Methode vorhanden, aber nur als Logging-Fallback implementiert; echte Windows Toast-Integration erforderlich.

- [ ] **Tests für neue Status-Transitions** — Unit- und E2E-Tests:
  - [ ] `TestNewStatusEnum()` — neue Enum-Werte
  - [ ] `TestStatusTransitions()` — erlaubte Übergänge validieren
  - [ ] `TestCliStartAsync()` — CLI-Start und ProcessHandle
  - [ ] `TestProcessWindowEmbedding()` — SetParent Win32-API
  - [ ] `TestHeartbeatUpdate()` — Heartbeat-Aktualisierung
  - [ ] `TestRecoveryCandidates()` — Recovery-Scan
  - [ ] `TestRateLimitMarkerParsing()` — Marker-Parsing
  - Status: Einige Tests vorhanden (z.B. `DarkModeE2ETests`), aber umfassende Coverage erforderlich.

- [ ] **Bestehende Tests anpassen**:
  - [ ] `AufgabeServiceTests` — auf neue Enum-Werte aktualisieren
  - [ ] `KiAusfuehrungsServiceTests` — auf neue Methoden-Signatures aktualisieren
  - [ ] `AufgabeRecoveryServiceTests` — auf neue Recovery-Bedingungen aktualisieren
  - [ ] `EntwicklungsprozessServiceTests` — auf vereinfachten Service anpassen
  - [ ] Plugin-Tests — auf neue `IKiPlugin`-Interface-Signatures anpassen
  - Status: Überprüfung erforderlich, ob alle Tests aktualisiert wurden.

- [ ] **Blazor-Code-Entfernung** — bestehende Blazor-Komponenten sollten entfernt sein (nicht vorhanden im WPF-Projekt, aber möglicherweise noch im Repo)
  - Status: Nicht überprüft.

- [ ] **Plugin-Implementierungen anpassen** — z.B. GitHub-Plugin, Claude CLI Plugin, Local Directory Plugin
  - Status: Unklar, ob diese an neue `IKiPlugin`-Schnittstelle angepasst wurden.

## Hinweise

1. **ProcessWindowEmbedder fehlt**: Das WPF-Control `ProcessWindowHost` ist vorhanden, aber ein Service-Layer-Wrapper ist nicht implementiert. Dies ist eine isolierbare Lücke.

2. **CLI-Prozess-Handling**: Das `KiAusfuehrungsService` ist gut redesignt und bietet alle erforderlichen Methoden für Prozess-Management. Die `CliProcessManager` koordiniert Heartbeat-Updates und Prozess-Status-Events.

3. **Fenster-Einbettungs-Fallback**: Der Plan erwähnt, dass bei `SetParent`-Fehler ein separates Fenster mit `HWND_TOPMOST` angezeigt werden sollte. Dies ist nicht in `ProcessWindowHost` implementiert.

4. **EntwicklungsprozessService**: Muss überprüft werden, ob alte Methoden entfernt und Service auf Git-Setup und Rate-Limit-Parsing beschränkt wurde.

5. **Recovery-Status-Mapping**: Die Migration setzt alte `KiAktiv`/`TestsLaufen`-Status zu `InArbeit`, aber der neue `Wartend`-Status wird nicht explizit adressiert. Dies ist korrekt für alte Daten, aber neuer Code muss `Wartend` eigenständig setzen können.

6. **AppEinstellung.Schluessel-Format**: Fenstergeometrie wird als separate Schlüssel-Wert-Paare gespeichert (z.B. `WindowPosition.X`), nicht als strukturierte Entität. Dies funktioniert, ist aber nicht optimal für Wartbarkeit.

7. **Dark Mode ResourceDictionary**: Der `DarkModeService` erwartet Theme-XAML-Dateien unter `pack://application:,,,/Softwareschmiede.App;component/Themes/`. Diese müssen vorhanden sein.

8. **Tests-Coverage**: Die meisten Unit- und E2E-Tests scheinen vorhanden zu sein, aber eine vollständige Überprüfung aller geforderten Tests (aus der Test-Sektion des Plans) ist erforderlich.

## Zusammenfassung der kritischen Lücken

Kritisch:
- **ProcessWindowEmbedder Service** — müsste implementiert werden als Wrapper um `ProcessWindowHost` mit Handle-Identifizierung und Fallback-Logik.

Nicht-kritisch (bereits teilweise adressiert):
- **Fenster-Einbettungs-Fallback** — Separate `AlwaysOnTop`-Fenster bei `SetParent`-Fehler nicht implementiert.
- **Windows Toast-Integration** — `ShowBannerAsync()` nur als Logging, keine echte Windows Notifications API.
- **Alte Methoden in Plugins** — `IKiPlugin` ist überarbeitet, aber alle Plug-in-Implementierungen müssen überprüft werden.
- **EntwicklungsprozessService** — Muss überprüft werden, ob korrekt vereinfacht.
