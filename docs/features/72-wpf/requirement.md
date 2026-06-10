# Technische Anforderungsspezifikation – WPF-Desktopanwendung Softwareschmiede

## Fachliche Zusammenfassung

Die Anwendung Softwareschmiede ist eine WPF-Desktopanwendung für Windows 11, die Entwicklungsprojekte und deren Aufgaben mit KI-Automatisierung verwaltet. Sie ersetzt eine bestehende Webanwendung und läuft ausschließlich lokal als Single-User-Anwendung mit einer SQLite-Datenbank. Das System bietet ein Plugin-Architektur mit pluggablen Git-Providern (`IGitProvider`) und KI-CLI-Providern (`IAiCliProvider`), um verschiedene Git-Plattformen (GitHub, BitBucket, lokale Verzeichnisse) und KI-Tools (GitHub Copilot CLI, Claude CLI) zu integrieren. Die UI strukturiert sich in ein einklappbares Navigationsmenü (Dashboard, Projekte, Einstellungen), ein zentrales Inhaltsbereich mit Dark-Mode-Unterstützung, und unterstützt komplexe Statusübergänge von Aufgaben mit automatischer Prozesswiederherstellung und Protokollierung.

## Betroffene Klassen und Komponenten

### Datenmodell

#### Neue / erweiterte Entitäten

- **`Projekt`**: Name, Beschreibung, ErstellungsDatum, Status (Aktiv/Archiviert), 1:n Repositories
- **`GitRepository`**: PluginTyp, RepositoryUrl, RepositoryName, Aktiv, StartKonfiguration
- **`Aufgabe`**: Titel, AnforderungsBeschreibung, Status (Neu, Arbeitsverzeichnis eingerichtet, Gestartet, In Arbeit, Wartend, Beendet, Archiviert), BranchName, LokalerKlonPfad, AgentenpaketName, AgentenName, KiPluginPrefix, ErstellungsDatum, AbschlussDatum, AktiveRunId, LastHeartbeatUtc, RecoveryVersion, VorschlagPrompt, VorschlagAusfuehrenAbUtc
- **`Protokolleintrag`**: AufgabeId, Zeitstempel, Typ (Prompt, KiAntwort, GitAktion, StatusUebergang, TestErgebnis), Inhalt, AgentName
- **`IssueReferenz`**: AufgabeId, IssueNummer, Titel, IssueUrl
- **`AppEinstellung`**: Schlüssel-Wert-Paare (WorkDir, DefaultScmPlugin, DefaultKiPlugin)
- **`PluginKonfiguration`**: PluginPrefix, FieldKey, Value (verschlüsselt)
- **`BenachrichtigungsEinstellung`**: Modus (Immer/Nie/NurBeiFehler), Kanal (Audio/System)
- **`BenachrichtigungsAudioDatei`**: Pfad, Aktiv
- **`BenachrichtigungsDispatchLog`**: AufgabeId, Zeitstempel, Entscheidung (Gesendet/Unterdrückt), Kanal

#### Enums

- **`AufgabeStatus`**: `Offen`, `InBearbeitung`, `KiAktiv`, `Abgeschlossen`, `Fehlgeschlagen`, `Archiviert`
- **`ProtokollTyp`**: `Prompt`, `KiAntwort`, `GitAktion`, `StatusUebergang`, `TestErgebnis`
- **`ProjektStatus`**: `Aktiv`, `Archiviert`
- **`PluginType`**: `SourceCodeManagement`, `DevelopmentAutomation`
- **`BenachrichtigungsModus`**: `Immer`, `Nie`, `NurBeiFehler`
- **`BenachrichtigungsKanal`**: `Audio`, `System`
- **`BenachrichtigungsEntscheidung`**: `Gesendet`, `Unterdrückt`

### Plugin-System & Interfaces

- **`IPlugin`**: Basis-Interface mit `PluginName`, `PluginPrefix`, `PluginType`, `GetSettingGroups()`
- **`IGitProvider` : `IPlugin`**: 
  - `CloneRepositoryAsync(repositoryUrl, localPath, ct)`
  - `CreateBranchAsync(localPath, branchName, ct)`
  - `CheckoutRemoteBranchAsync(localPath, branchName, ct)`
  - `GetDefaultBranchAsync(repositoryUrl, ct)`
  - `GetRemoteBranchesAsync(repositoryUrl, ct)`
  - `CommitAsync(localPath, message, ct)`
  - `PushBranchAsync(localPath, branchName, ct)`
  - `PullAsync(localPath, ct)`
  - `CreatePullRequestAsync(repositoryId, branchName, title, body, ct) → Task<PullRequest>`
  - `ResetAsync(localPath, resetType, targetRef?, ct)`
- **`IAiCliProvider` : `IPlugin`**:
  - `GetAvailableAgentsAsync(agentPackagePath, ct) → IEnumerable<AgentInfo>`
  - `IsAgentPackageCompatibleAsync(agentPackagePath, ct) → bool`
  - `DeployAgentPackageAsync(agentPackagePath, localRepoPath, ct)`
  - `StartDevelopmentAsync(prompt, agent, localRepoPath, model?, ct) → IAsyncEnumerable<string>`
  - `RunTestsAsync(localRepoPath, ct) → Task<TestResult>`
  - `CheckHealthAsync(ct) → bool`
- **`CliKiPluginBase`**: Abstrakte Basis für CLI-basierte KI-Plugins mit `ProviderDateiPraefix`, `BuildContextFilePath()`, `GetLatestContextFilePath()`, `ClearContextFiles()`, `MarkPromptToIncludeContextFile()`, `UnwrapPromptContextMarker()`, `EnsureGitignoreEntries()`
- **`PluginSettingGroup`**: Beschreibt Einstellungsgruppe mit Feldtyp, Label, Hilfetext

#### Value Objects

- **`AgentInfo`**: Name, Beschreibung, Pfad
- **`PullRequest`**: Nummer, Titel, Url
- **`TestResult`**: Bestanden, Ergebnisse (`IEnumerable<TestErgebnisInfo>`)

### Services & Logik

- **`ProjektService`**: CRUD-Operationen auf Projekte (Create, Read, Update, Delete)
- **`AufgabeService`**: 
  - CRUD-Operationen auf Aufgaben
  - `SavePromptVorschlagAsync(aufgabeId, prompt, executeAfterUtc)`
  - `UpdateStatusAsync(aufgabeId, newStatus)`
  - `ArchiveAsync(aufgabeId)`
- **`GitService`**: 
  - Repository-Verwaltung
  - Integration mit `IGitProvider`
  - Branch-Management
- **`KiAusfuehrungsService`**:
  - `StartKiLauf(aufgabeId, prompt, agentName) → IAsyncEnumerable<string>`
  - `IsRunning(aufgabeId) → bool`
  - `StopAsync(aufgabeId)`
  - Exklusiver Lauf pro Aufgabe (Guard gegen doppelte Ausführung)
- **`EntwicklungsprozessService`**:
  - `ProzessStartenAsync(aufgabeId, basisBranchName?, ct)`
  - `EnsureContextWithinLimitsAsync(aufgabeId, context, ct)`
  - `TryParseRateLimitSuggestion(outputLine) → (bool, SuggestionInfo?)`
  - `DeleteDirectoryForce(path)` (Windows-spezifische Bereinigung schreibgeschützter Dateien)
  - `CompressContextAsync(context, ct) → string`
  - `ContainsMandatoryCompressionSections(compressed) → bool`
- **`AufgabeRecoveryService`**:
  - `CanRecoverAsync(aufgabeId) → bool`
  - `RecoverAsync(aufgabeId) → Task`
  - Recovery-Bedingungen prüfen (Status, Heartbeat > 5 Min, kein aktiver Lauf)
- **`PluginService`**:
  - Plugin-Discovery (Laden aus `/plugins`-Verzeichnis)
  - Plugin-Instanziierung
  - `GetPluginByPrefix(prefix) → IPlugin`
  - `GetAllPluginsOfType(type) → IEnumerable<IPlugin>`
- **`PluginSettingsService`**:
  - `GetPluginSettingsAsync(pluginPrefix) → IReadOnlyList<PluginSetting>`
  - `SavePluginSettingAsync(pluginPrefix, fieldKey, value)` (verschlüsselt)
- **`AppEinstellungService`**:
  - `GetSettingAsync(key) → string?`
  - `SetSettingAsync(key, value)`
- **`BenachrichtigungsService`**:
  - `ShouldNotifyAsync(aufgabeId, statusEvent) → bool`
  - `DispatchNotificationAsync(aufgabeId, kanal, modus)`
  - Audio-Support für `.mp3`, `.wav`, `.ogg`
- **`ProtokollService`**:
  - `LogAsync(aufgabeId, typ, inhalt, agentName?)`
  - Abrufen von Protokolleinträgen mit Filter
- **`DatenbankService`**: SQLite-Datenbankinitialisierung und Migration

### UI-Komponenten (WPF)

#### Hauptfenster

- **`MainWindow.xaml`**: Struktur mit Titelleiste, einklappbarem Menübereich (Links), zentralem Inhaltsbereich
- **`NavigationViewModel`**: Verwaltung Menü-State (einklappbar/expandiert)
- **`DarkModeService`**: Verwaltung Dark Mode (aktivierbar in Einstellungen)

#### Navigation & Seiten

- **`Dashboard.xaml`**: Anzeige Projektanzahl, Aufgabenanzahl, zuletzt geänderte Aufgaben (mit Anklickbarkeit)
- **`ProjectListView.xaml`**: Tabelle aller Projekte mit CRUD-Buttons
- **`ProjectDetailView.xaml`**: Projekt bearbeiten, Git-Integration, Aufgabenliste
- **`TaskListView.xaml`**: Aufgabenliste im Projekt
- **`TaskDetailView.xaml`**: Aufgabendetails, Statusübergänge, Protokoll, eingebettetes CLI-Fenster
- **`SettingsView.xaml`**: Tabs für Quellcodeverwaltung, KI-Ausführung, Logging
- **`PluginSettingsView.xaml`**: Automatisch generierte UI-Felder basierend auf `PluginSettingGroup`-Typen

#### Status-Management UI

- Task-Status-Anzeige mit visuellen Indikatoren
- Buttons für Zustandsübergänge (Beenden, Archivieren, Recovery)
- In-Task-Seiten-Anzeige für aktive/wartende Aufgaben im Menü (sortiert nach letzter Statusänderung, absteigend)
- Info-Overlay für Aufgabenbeschreibung (ab Status != "Neu")

### Tests

- **`Softwareschmiede.Tests`**: Unit-Tests
  - Statusübergänge (AufgabeStatus-Transitionslogik)
  - Plugin-Ladeprozess
  - Prozessverwaltung (KI-CLI)
  - Git-Operationen (mit Mocking)
  - Einstellungen & Validierungen
  - Datenbankzugriffe (SQLite In-Memory)
- **`Softwareschmiede.E2E`**: End-to-End-Tests
  - UI-Rendering-Tests
  - Aufgaben CRUD
  - Statuswechsel sichtbar
  - Plugin-Laden & Initialisierung
  - KI-CLI-Prozess-Start & -Einbettung
  - SQLite-Echtdatenbank
  - Echtdateisystem (Testverzeichnisse)
  - Test-Dummies für CLI-Prozesse (keine reinen Mocks)

## Implementierungsansatz

### Architektur-Übersicht

1. **Layering**:
   - **Präsentation (WPF)**: XAML-Views + ViewModels (MVVM)
   - **Anwendungslogik (Core)**: Services (Business-Rules, Orchestrierung)
   - **Datenzugriff**: EF Core mit SQLite
   - **Plugin-System**: Reflection-basiertes Laden aus `/plugins`-DLLs mit `IPlugin`-Interface

2. **Plugin-Lifecycle**:
   - **Startup**: `PluginService` scannt `/plugins`-Verzeichnis, lädt alle `.dll`-Dateien
   - **Instantiation**: Reflection-basierte Instanziierung (Konstruktor ohne Parameter oder mit DI-Container)
   - **Settings**: Pro Plugin `GetSettingGroups()` → UI generiert automatisch Eingabefelder
   - **Configuration**: Werte in `PluginKonfiguration`-Tabelle gespeichert (verschlüsselt via Windows Credential Store für Secrets)
   - **No Hot-Reload**: Neustart erforderlich für Plugin-Änderungen

3. **Event-Hooks & Erweiterungspunkte**:
   - Task-Status-Wechsel triggert `StatusWechselEvent` → UI aktualisiert, Benachrichtigungen gesendet
   - KI-CLI-Output wird gestreamt durch `IAsyncEnumerable<string>` → Real-Time-UI-Update
   - Plugin-Konfiguration-Änderung triggert `PluginSettingsChangedEvent` → evtl. UI-Refresh
   - Recovery-Kandidaten: Service scannt `InBearbeitung`/`KiAktiv` Aufgaben mit Heartbeat > 5 Min

4. **Prozessverwaltung (KI-CLI)**:
   - Fensterhandle wird bereitgestellt: Prozess-Fenster wird in WPF-Control eingebettet
   - Stdout/Stderr werden durch Streaming-Interface konsumiert
   - Pro Aufgabe nur eine CLI gleichzeitig (Guard im `KiAusfuehrungsService`)
   - Parallele Einschränkung global konfigurierbar (Deaktiviert / Pro Aufgabe / Pro KI-Anbieter / Insgesamt)

5. **Git-Integration**:
   - `IGitProvider` abstrahiert Git-Operationen
   - Lokale Repositories werden in Arbeitsverzeichnis geklont
   - Branch-Strategie: Neuer `task/*`-Branch wenn kein Basis-Branch oder Basis = Hauptbranch; sonst Checkout existierender Branch
   - Pull Request-Erstellung durch Plugin implementiert

6. **Kontext- & Protokoll-Management**:
   - Aufgabenprotokoll in `Protokolleintrag`-Tabelle speichert alle CLI-Output
   - Kontextkomprimierung bei Soft-Limit (12k Zeichen) / Hard-Limit (20k Zeichen)
   - Komprimierung startet separaten KI-Lauf mit Komprimierungs-Prompt
   - Rate-Limit-Erkennung: Ausgabezeile mit `[[SOFTWARESCHMIEDE_RATE_LIMIT]]`-Marker → Vorschlag speichern

7. **Konfiguration & Persistierung**:
   - Anwendungseinstellungen: `AppEinstellung`-Tabelle
   - Plugin-Einstellungen: `PluginKonfiguration`-Tabelle (verschlüsselt)
   - Dark Mode-Setting: UI-Thema wird aus Einstellung geladen
   - Logging: Datei + optionale Datenbank-Logs (konfigurierbar)

### Abhängigkeiten

- **Framework**: .NET 9+ (oder .NET 8+)
- **UI**: WPF (Windows Presentation Foundation)
- **DB**: Entity Framework Core + SQLite
- **DI**: Microsoft.Extensions.DependencyInjection
- **Plugins**: Reflection, keine externen NuGet-Abhängigkeiten pro Plugin (außer Standard-.NET)
- **Crypto**: Windows Credential Store (dpapi) für sensitive Plugin-Settings

## Konfiguration

### Anwendungsebene (`AppEinstellung`)

- `WorkDir`: Lokal Arbeitsverzeichnis für Repository-Klons
- `DefaultScmPlugin`: Plugin-Prefix des Standard-Git-Plugins (z.B. `GitHub`)
- `DefaultKiPlugin`: Plugin-Prefix des Standard-KI-Plugins (z.B. `Claude`)
- `LoggingEnabled`: Bool, ob Logging aktiv
- `DarkModeEnabled`: Bool, ob Dark Mode aktiv
- `NotificationMode`: `Immer` / `Nie` / `NurBeiFehler`
- `NotificationChannel`: `Audio` / `System`
- `CustomAudioFilePath`: Pfad zu Benachrichtigungs-Audiodatei
- `MaxParallelKiProcesses`: Enum (Deaktiviert / Pro Aufgabe / Pro KI-Anbieter / Insgesamt)
- `AutoCloneRepository`: Bool, „Arbeitsverzeichnis automatisch verwalten"

### Plugin-Ebene (`PluginKonfiguration`)

Jedes Plugin definiert via `GetSettingGroups()` eigene Einstellungsfelder:
- Typ-basierte Validierung (String, Integer, Boolean, Enum, File-Path, etc.)
- Automatische UI-Generierung durch Typ
- Speicherung verschlüsselt (Windows Credential Store für API-Keys, Tokens)

### Projekt-Ebene

- Git-Repository-Auswahl pro Projekt
- Start-Konfiguration (optionales Startup-Skript)

### Aufgaben-Ebene

- Agentenpaket-Auswahl
- Agentenname
- KI-Plugin-Auswahl

## Offene Fragen & Klärungsbedarf

1. **UI-Framework & XAML-Struktur**:
   - Sollen ViewModels MVVM-Toolkit, Prism oder manuelles INotifyPropertyChanged verwenden?
   - Datenein-Binding: One-way, Two-way oder reaktive Patterns (ReactiveUI)?

2. **Plugin-Konfiguration – Credential Store**:
   - Windows Credential Store (DPAPI) für alle Secrets oder selektiv?
   - Fallback für non-Windows-Umgebungen (nur Windows 11 vorgegeben, aber trotzdem prüfen)?

3. **Prozess-Einbettung (CLI-Fenster)**:
   - Windows-Handle-Capture: Realisierung via `SetParent` WinAPI? Oder Container-Fenster?
   - Unterstützung für Multi-Monitor-Layouts?

4. **KI-CLI-Ausführung – Session-Fortbestand**:
   - Wie wird "Fortsetzen einer vorherigen Session" konkret umgesetzt? (Datei-basiert, Umgebungsvariablen, CLI-Befehl?)
   - Gilt dies für beide Claude-CLI und Copilot-CLI oder nur eine?

5. **Status-Modell – Semantik**:
   - Aktuelle Spec hat: Offen, InBearbeitung, KiAktiv, Abgeschlossen, Fehlgeschlagen, Archiviert
   - Lastenheft spricht von: Neu, Arbeitsverzeichnis eingerichtet, Gestartet, In Arbeit, Wartend, Beendet, Archiviert
   - Mapping erforderlich: Welcher Lastenheft-Status → welcher Code-Status?

6. **Kontextkomprimierung – Pflicht-Abschnitte**:
   - "Ziel", "Offene Punkte", "Letzte Entscheidungen": Exakte Überschriften oder regex-Match?
   - Markdown-Struktur für Validierung?

7. **Git-Plugin "Lokales Verzeichnis" – Branches**:
   - "Arbeitsbranch erstellt (nur organisatorisch)": Wird dieser in Git tatsächlich erstellt oder nur in der DB verwaltet?
   - Wie wird "Pull" (Dateikopie zurück) implementiert ohne Merge-Handling?

8. **Mehrere Repositories pro Projekt**:
   - Spezifikation sagt "1:1" zwischen Projekt und Git-Repo
   - Datenmodell zeigt aber `List<GitRepository>` in Projekt
   - Ist Mehrfach-Zuordnung geplant oder Datenmodell-Fehler?

9. **Rate-Limit-Vorschlag – Automatisches Retriggern**:
   - Wird der gespeicherte Prompt automatisch zum konfigurierten Zeitpunkt neu gesendet oder nur in UI vorausgefüllt?
   - Wer triggert die Ausführung (Benutzer manuell, Timer, Cloud-Job)?

10. **Testautomatisierung – CI/CD**:
    - GitHub Actions als Standard? Welche Event-Trigger (Push, PR, Nightly)?
    - Welche Git-Plattform für Repo selbst? (GitHub wird angenommen, aber nicht explizit genannt)

11. **Logging-Granularität**:
    - Logdatei-Format (Plain-Text, JSON, strukturiert)?
    - Rotation-Policy (Größe, Anzahl, Alter)?

12. **Build-Integration der Plugins**:
    - Welcher Build-Runner (.csproj-Struktur, MSBuild, Cake, etc.)?
    - Sind Plugin-DLLs versioniert (z.B. GitHub-Plugin-v1.0.dll)?
