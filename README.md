# 🔨 Softwareschmiede

> **KI-gestützter Softwareentwicklungs-Workflow — lokal, strukturiert und erweiterbar**

[![.NET](https://img.shields.io/badge/.NET-10%2B-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)](https://blazor.net/)
[![SQLite](https://img.shields.io/badge/SQLite-EF%20Core-003B57?logo=sqlite)](https://www.sqlite.org/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## Inhaltsverzeichnis

1. [Projektbeschreibung](#-projektbeschreibung)
2. [Features](#-features)
3. [Screenshots](#-screenshots)
4. [Voraussetzungen](#-voraussetzungen)
5. [Schnellstart](#-schnellstart)
6. [Usage](#-usage)
7. [Konfiguration & Plugin-Setup](#️-konfiguration--plugin-setup)
8. [Agentenpakete](#-agentenpakete)
9. [Projektstruktur](#-projektstruktur)
10. [Architektur](#-architektur)
11. [Tests](#-tests)
12. [Deployment](#-deployment)
13. [Changelog](#-changelog)
14. [Roadmap](#-roadmap)
15. [Dokumentation](#-dokumentation)
16. [Beitragen](#-beitragen)
17. [Lizenz](#-lizenz)

---

## 📖 Projektbeschreibung

**Softwareschmiede** ist eine webbasierte **Einzelnutzer-Anwendung** auf Basis von **Blazor Server (.NET 10+)**, die den vollständigen Workflow der **KI-gestützten Softwareentwicklung** in einer einheitlichen Oberfläche verwaltet.

Die Anwendung läuft vollständig **lokal unter Windows**, erfordert **keinen Login** und verbindet Projektmanagement, Git-Integration, Aufgabenverwaltung und KI-Steuerung an einem zentralen Ort.

### Geschäftsziele

| # | Ziel |
|---|------|
| Z-1 | Verwaltung mehrerer Softwareprojekte an einem zentralen Ort |
| Z-2 | Strukturierte Erfassung von Anforderungen je Aufgabe |
| Z-3 | Automatisierte Umsetzung von Anforderungen durch KI-Plugins |
| Z-4 | Nachvollziehbarer Verlauf jeder KI-gesteuerten Entwicklungsaufgabe |
| Z-5 | Erweiterbarkeit für weitere Git-Provider und KI-Systeme ohne Kernänderungen |

---

## 🚀 Features

### 📁 Projektmanagement
- Beliebig viele Softwareprojekte anlegen, bearbeiten, archivieren und löschen
- Repositories aus dem Git-Provider verknüpfen und Issues automatisch laden
- Projektübersicht mit Status und aktiven Aufgaben
- Konfigurierbares Basis-Arbeitsverzeichnis für lokale Repository-Klone (persistiert als `repositories.workdir`, inkl. Runtime-Fallback)

### 🔗 Git-Integration (Plugin-System)
- **Plugin-Architektur** über `IGitPlugin`-Interface – austauschbar für verschiedene Git-Provider
- **GitHub-Plugin** (erstes Plugin): vollständige GitHub-Integration via `gh` CLI
- Klonen, Branch anlegen, Committen, Pushen, Pullen und Pull Requests erstellen
- Aufgabenspezifische Branches (`task/<aufgaben-id>-<kurzname>`)
- Commit-Verwaltung inkl. Rollback (soft / mixed / hard)

### ✅ Aufgabenverwaltung
- Aufgaben aus GitHub Issues anlegen (Titel, Body, Labels, Milestone werden übernommen)
- Freie Aufgaben ohne Issue-Referenz anlegen
- Statusmodell: `Offen` → `In Bearbeitung` → `KI aktiv` / `Tests laufen` → `Abgeschlossen` / `Fehlgeschlagen`
- Automatisches Aufräumen (Branch & Klon löschen) nach Abschluss oder Abbruch

### 🤖 KI-Steuerung (Plugin-System)
- **Plugin-Architektur** über `IKiPlugin`-Interface – austauschbar für verschiedene KI-Systeme
- **GitHub Copilot-Plugin** (erstes Plugin): KI-Integration via `copilot` CLI
- Prompt-Persistenz pro Lauf in `{executionId}.copilot-task.md` mit dateibasierter CLI-Übergabe (`--prompt @...`)
- Automatische, idempotente `.gitignore`-Konsolidierung auf `*.copilot-task.md`
- Einheitlicher `StartDevelopmentAsync`-Vertrag ohne test-spezifischen Kurz-Overload (konsistente Service-/Test-Aufrufe)
- Echtzeit-Streaming der KI-Ausgabe (< 500 ms Latenz pro Stream-Chunk)
- Iterative Entwicklung durch Folge-Prompts direkt aus dem Protokoll
- Agentenpaket-Auswahl und Agenten-Auswahl pro Prompt
- Test-Ausführung und strukturierte Auswertung der Ergebnisse

### 📋 Aufgabenprotokoll
- Lückenloses, chronologisches Protokoll aller Prompts, KI-Antworten und Zeitstempel
- Status-Übergänge und Git-Aktionen werden protokolliert
- Volltextsuche über alle Protokolleinträge einer Aufgabe
- Test-Ergebnisse strukturiert: Testname, Status, Fehlermeldung, Dauer

### 📦 Agentenpakete
- Verzeichnisbasierte Pakete mit `.agent.md`-Dateien
- Deployment ins Repository-Verzeichnis beim Start eines KI-Laufs
- Vorschau von Dateiliste, Beschreibung und verfügbaren Agenten in der Oberfläche

### 🏠 Dashboard
- Projektübergreifende Übersicht aller aktiven Aufgaben
- Farbkodierte Statusanzeige (KI aktiv = blau, Fehlgeschlagen = rot, Abgeschlossen = grün)
- Direktnavigation zum Aufgabenprotokoll per Klick

---

## 📸 Screenshots

![Dashboard](docs/images/dashboard.png)

> *Screenshot-Platzhalter – wird nach erstem Release ergänzt.*

---

## ✅ Voraussetzungen

| Voraussetzung | Version | Hinweis |
|---------------|---------|---------|
| **Windows** | 10 / 11 | Pflicht – Windows Credential Store wird benötigt |
| **.NET SDK** | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| **GitHub CLI** (`gh`) | aktuell | [cli.github.com](https://cli.github.com/) – für GitHub-Operationen |
| **Git** | aktuell | [git-scm.com](https://git-scm.com/) |
| **Copilot CLI** (`copilot`) | aktuell | Für KI-Steuerung im Plugin (`copilot --version`) |
| **GitHub Copilot** | aktives Abo | Wird über die Copilot-CLI genutzt |

**GitHub CLI einrichten:**

```powershell
# GitHub CLI installieren (z. B. via winget)
winget install --id GitHub.cli

# Authentifizieren
gh auth login

# Copilot-CLI prüfen
copilot --version
```

---

## ⚡ Schnellstart

### 1. Repository klonen

```powershell
git clone https://github.com/<your-org>/Softwareschmiede.git
cd Softwareschmiede
```

### 2. Abhängigkeiten wiederherstellen & bauen

```powershell
dotnet restore
dotnet build src/Softwareschmiede/Softwareschmiede.csproj
```

Beim Build des Host-Projekts werden die Plugin-DLLs automatisch nach `bin/<Config>/<TFM>/plugins/` kopiert (inkl. Publish-Ausgabe).

### 3. Anwendung starten

```powershell
dotnet run --project src/Softwareschmiede/Softwareschmiede.csproj
```

Die Anwendung ist danach unter **`https://localhost:5001`** (oder dem konfigurierten Port) erreichbar.

### 4. Erste Schritte

1. **GitHub-Token einrichten** – Credential Manager öffnen und Token speichern (siehe [Konfiguration](#️-konfiguration--plugin-setup))
2. **Projekt anlegen** – Auf der Seite *Projekte* ein neues Projekt mit GitHub-Repository erstellen
3. **Aufgabe anlegen** – Issue aus dem Repository wählen oder freie Anforderung erfassen
4. **Agentenpaket auswählen** – Passendes Agentenpaket aus `agent-packages/` zuweisen
5. **KI-Lauf starten** – Prompt eingeben, Agenten auswählen und den KI-gestützten Prozess starten

---

## 🖥️ Usage

### Typischer Ablauf in der Anwendung

1. **Projekt erstellen oder öffnen** und ein Repository verknüpfen.
2. **Aufgabe anlegen** (frei oder aus GitHub-Issue).
3. **Entwicklungsprozess starten** (lokaler Klon + Aufgaben-Branch).
4. **KI-Lauf ausführen** (Prompt + Agent aus Agentenpaket wählen).
5. **Ergebnis prüfen**, optional weitere Folge-Prompts senden.
6. **Commits/Push/PR durchführen** und Aufgabe abschließen oder abbrechen.

Details zu den einzelnen Schritten:
- [Benutzerleitfaden](docs/user-guide.md)
- [Feature-Dokumentation](docs/business/features.md)

---

## ⚙️ Konfiguration & Plugin-Setup

### Plugin-Architektur (kurz)

- **Contracts:** `src/Softwareschmiede.Plugin.Contracts` definiert `IPlugin`, `IGitPlugin`, `IKiPlugin`, `PluginType`
- **Plugin-Projekte:** liegen als eigenständige Klassenbibliotheken unter `plugins/`
- **Host-Referenzen:** `src/Softwareschmiede/Softwareschmiede.csproj` referenziert Plugin-Projekte mit `ReferenceOutputAssembly="false"`
- **Build/Publish-Kopie:** MSBuild-Targets kopieren Plugin-Artefakte nach `$(OutDir)plugins` bzw. `$(PublishDir)plugins`
- **Discovery zur Laufzeit:** `PluginManager` lädt alle `*.dll` aus `AppContext.BaseDirectory/plugins` und registriert sie nach `PluginType`

### GitHub-Token im Windows Credential Store speichern

Softwareschmiede speichert API-Tokens **ausschließlich im Windows Credential Store** – kein Klartext in Konfigurationsdateien oder der Datenbank.

**Option A – Credential Manager UI:**

1. Startmenü → *Windows-Anmeldeinformationsverwaltung* (Credential Manager) öffnen
2. *Windows-Anmeldeinformationen* → *Generische Anmeldeinformation hinzufügen*
3. Felder ausfüllen:
   - **Internetadresse oder Netzwerkadresse:** `Softwareschmiede.GitHub.Token`
   - **Benutzername:** *(beliebig, z. B. `github`)*
   - **Kennwort:** Dein GitHub Personal Access Token (PAT) mit den Scopes `repo`, `read:org`

**Option B – Kommandozeile (`cmdkey`):**

```powershell
cmdkey /generic:Softwareschmiede.GitHub.Token /user:github /pass:<DEIN_TOKEN>
```

**Token entfernen:**

```powershell
cmdkey /delete:Softwareschmiede.GitHub.Token
```

### Credential-Schlüssel (Referenz)

| Schlüssel | Inhalt |
|-----------|--------|
| `Softwareschmiede.GitHub.Token` | GitHub Personal Access Token |

### Weitere Plugin-Konfiguration

Alle weiteren Plugin-Einstellungen (Repository-URL, Organisations-URL) werden direkt in der Oberfläche unter *Projekte → Repository verknüpfen* konfiguriert und sicher in der lokalen SQLite-Datenbank gespeichert.

### Arbeitsverzeichnis für lokale Klone

Das Basis-Arbeitsverzeichnis für lokale Repository-Klone ist in den Einstellungen konfigurierbar und wird als globale App-Einstellung `repositories.workdir` in SQLite gespeichert.  
Wenn keine Einstellung gesetzt ist oder der konfigurierte Pfad zur Laufzeit nicht nutzbar ist, verwendet die Anwendung automatisch den Fallback auf Basis von `Path.GetTempPath()`.

Der finale Klonpfad wird immer unterhalb von:

`<Basispfad>/softwareschmiede/<aufgabeId>`

gebildet, z. B.:

- Konfiguriert: `D:/Repos` → `D:/Repos/softwareschmiede/<aufgabeId>`
- Fallback: `Path.GetTempPath()` → `<temp>/softwareschmiede/<aufgabeId>`

### Copilot-Task-Datei und `.gitignore`

Beim Start eines KI-Laufs schreibt das GitHub-Copilot-Plugin den Prompt in:

```
<lokalerKlonPfad>/{executionId}.copilot-task.md
```

Anschließend wird in `<lokalerKlonPfad>/.gitignore` die Regel `*.copilot-task.md` idempotent sichergestellt. Ältere Varianten (`/.copilot-task.md`, `.copilot-task.md`) werden dabei konsolidiert.

---

## 📦 Agentenpakete

### Was sind Agentenpakete?

Agentenpakete sind **Verzeichnisse mit `.agent.md`-Dateien**, die KI-Agenten und deren Instruktionen definieren. Sie werden beim Start eines KI-Laufs automatisch in das Arbeitsverzeichnis des Branches kopiert und vom KI-Plugin (z. B. GitHub Copilot) ausgewertet.

### Speicherort

```
<Programmverzeichnis>/agent-packages/
```

Das Verzeichnis wird beim App-Start automatisch angelegt, falls es noch nicht existiert.

### Struktur eines Agentenpakets

```
agent-packages/
├── mein-agentenpaket/               # Unterordner = ein Agentenpaket
│   ├── planner.agent.md             # Agent: Planer
│   ├── implementer.agent.md         # Agent: Implementierer
│   ├── reviewer.agent.md            # Agent: Reviewer
│   └── README.md                    # Optionale Beschreibung des Pakets
└── weiteres-paket/
    └── ...
```

### Aufbau einer `.agent.md`-Datei

Eine `.agent.md`-Datei beschreibt einen einzelnen Agenten mit seinen Instruktionen, Werkzeugen und Rollen. Das Format folgt dem Standard für GitHub Copilot Custom Agents.

### Agentenpakete in der Oberfläche

- Seite *Agentenpakete*: Liste aller verfügbaren Pakete mit Vorschau
- Beim Anlegen einer Aufgabe kann ein Agentenpaket zugewiesen werden
- Vor jedem KI-Lauf kann der Anwender den gewünschten Agenten aus dem Paket auswählen

---

## 🗂️ Projektstruktur

```
Softwareschmiede/                            # Solution Root
├── src/
│   ├── Softwareschmiede/                    # Blazor Server Hauptprojekt (Host)
│   │   ├── Application/
│   │   │   └── Services/                    # EntwicklungsprozessService, ProjektService,
│   │   │                                    # AufgabeService, ProtokollService, ...
│   │   ├── Domain/
│   │   │   ├── Entities/                    # Projekt, Aufgabe, Protokolleintrag, ...
│   │   │   ├── Interfaces/                  # IPluginManager, ...
│   │   │   ├── ValueObjects/
│   │   │   └── Enums/                       # AufgabeStatus, ProtokolleintragTyp, ...
│   │   ├── Infrastructure/
│   │   │   ├── Data/                        # EF Core DbContext, Migrations
│   │   │   ├── Plugins/                     # PluginManager (Discovery/Loading)
│   │   │   └── Services/                    # CliRunner, WindowsCredentialStore, ...
│   │   ├── Components/
│   │   │   └── Pages/                       # Blazor Razor Pages
│   │   │       ├── Home.razor               # Dashboard
│   │   │       ├── Projekte/
│   │   │       ├── Aufgaben/
│   │   │       └── Agentenpakete/
│   │   └── wwwroot/                         # Statische Assets (CSS, JS, Bilder)
│   ├── Softwareschmiede.Client/             # Blazor WebAssembly Client Assembly
│   ├── Softwareschmiede.IntegrationTests/   # Integrations-Tests
│   ├── Softwareschmiede.Plugin.Contracts/   # IPlugin, IGitPlugin, IKiPlugin, PluginType
│   └── Softwareschmiede.Tests/              # Unit-Tests (xUnit, FluentAssertions, Moq)
├── plugins/                                 # Plugin-Projekte (separate Klassenbibliotheken)
│   ├── Softwareschmiede.Plugin.GitHub/      # Git-Provider Plugin
│   └── Softwareschmiede.Plugin.GitHubCopilot/ # KI-Plugin
├── docs/                                    # Planungsdokumente und Architektur
│   ├── requirements/
│   │   └── requirements-analysis.md
│   ├── architecture/
│   │   ├── architecture-blueprint.md
│   │   └── entity-relationship-model.md
│   └── improvements/
└── Softwareschmiede.slnx               # Solution-Datei
```

---

## 🏗️ Architektur

Softwareschmiede folgt einer **Clean Architecture** mit vier klar getrennten Schichten:

```mermaid
graph TB
    subgraph Presentation["Presentation Layer (Blazor Server)"]
        PRL1[Razor Pages / Components]
        PRL2[ViewModels]
        PRL3[SignalR – Echtzeit-Streaming]
    end

    subgraph Application["Application Layer (Services / Use Cases)"]
        APL1[ProjektService]
        APL2[AufgabeService]
        APL3[ProtokollService]
        APL4[KiOrchestrationService]
        APL5[GitOrchestrationService]
        APL6[AgentPackageService]
    end

    subgraph Domain["Domain Layer (Kern – keine äußeren Abhängigkeiten)"]
        DOL1[Entitäten: Projekt · Aufgabe · Protokolleintrag]
        DOL2[IPlugin + PluginType]
        DOL3[IGitPlugin / IKiPlugin]
        DOL4[Value Objects · Enums · Domänenregeln]
    end
 
    subgraph Infrastructure["Infrastructure Layer"]
        INL1[EF Core / SQLite]
        INL2[PluginManager (lädt DLLs aus plugins/)]
        INL3[GitHubPlugin / GitHubCopilotPlugin als Plugin-Projekte]
        INL4[Windows Credential Store]
        INL5[AgentPackage FileSystem Reader]
    end

    Presentation -->|ruft auf| Application
    Application -->|verwendet| Domain
    Application -->|orchestriert| Infrastructure
    Infrastructure -->|implementiert| Domain
```

| Schicht | Verantwortung |
|---------|---------------|
| **Presentation** | UI-Rendering, Benutzerinteraktion, ViewModel-Binding, Echtzeit-Updates via SignalR |
| **Application** | Anwendungsfalllogik, Koordination von Domain und Infrastructure, Plugin-Aufruf |
| **Domain** | Fachentitäten, Domänenregeln, Plugin-Interfaces – **keine** äußeren Abhängigkeiten |
| **Infrastructure** | DB-Zugriff, CLI-Prozesse, Credential Store, Dateisystem |

### Plugin-Interfaces

```csharp
// Basisvertrag für alle Plugins
public interface IPlugin
{
    string PluginName { get; }
    string PluginPrefix { get; }
    PluginType PluginType { get; } // SourceCodeManagement | DevelopmentAutomation
}

public interface IGitPlugin : IPlugin { /* Git operations */ }
public interface IKiPlugin : IPlugin { /* AI/Copilot operations */ }
```

`IPluginManager` lädt Plugin-DLLs aus `plugins/` dynamisch und ordnet sie über `PluginType` den Kategorien zu.

### Discovery- und Build-Flow

1. Plugin-Projekte unter `plugins/*` referenzieren nur `Softwareschmiede.Plugin.Contracts`
2. Das Host-Projekt baut Plugins als Projekt-Referenzen (ohne statisches Linken in den Host)
3. Nach Build/Publish kopieren MSBuild-Targets die Plugin-DLLs in den `plugins`-Unterordner der Ausgabe
4. `PluginManager` scannt beim ersten Zugriff den Ordner `AppContext.BaseDirectory/plugins` (`*.dll`, TopDirectoryOnly)
5. Gefundene Typen werden per `ActivatorUtilities` instanziiert und anhand von `PluginType` als Git- oder KI-Plugin registriert

---

## 🧪 Tests

Das Projekt enthält Unit-Tests im Projekt `Softwareschmiede.Tests`:

```powershell
# Alle Tests ausführen
dotnet test

# Tests mit Coverage-Report
dotnet test --collect:"XPlat Code Coverage"
```

**Test-Stack:**
- [xUnit](https://xunit.net/) – Test-Framework
- [FluentAssertions](https://fluentassertions.com/) – Lesbare Assertions
- [Moq](https://github.com/moq/moq4) – Mocking von Plugin-Interfaces und Services

---

## 🚀 Deployment

Softwareschmiede ist für den **lokalen Betrieb unter Windows** ausgelegt.

- **Development:** `dotnet run --project src/Softwareschmiede/Softwareschmiede.csproj`
- **Publish:** `dotnet publish src/Softwareschmiede/Softwareschmiede.csproj -c Release`
- Das Publish-Output enthält automatisch den Ordner `plugins/` mit den Plugin-DLLs.

Für die Inbetriebnahme müssen `gh`, `git` und `copilot` auf dem Zielsystem verfügbar sein.

---

## 📝 Changelog

Es gibt aktuell keine separate Changelog-Datei. Änderungen werden über Git-Historie und Pull Requests nachvollzogen.

Letzte dokumentierte Feature-Ergänzung:
- GUID-präfixierte Prompt-Datei `{executionId}.copilot-task.md` inkl. optionaler `executionId`, robustem Cleanup und `.gitignore`-Konsolidierung im `GitHubCopilotPlugin`
- Konsolidierung auf die kanonische `StartDevelopmentAsync(..., model, executionId, ct)`-Signatur (Entfernung des test-spezifischen Overloads bei unverändertem Laufzeitverhalten)

---

## 🗺️ Roadmap

### v1.0 – MVP (vollständig implementiert ✅)
- [x] Anforderungsanalyse und Architektur-Blueprint
- [x] Domänenmodell und EF Core Datenbankschema
- [x] GitHub-Plugin (gh CLI) – vollständige Git-Integration
- [x] GitHub Copilot-Plugin (copilot CLI) – KI-Steuerung mit Echtzeit-Streaming
- [x] Blazor UI: Dashboard, Projekte, Aufgaben, Protokoll, Agentenpakete
- [x] Windows Credential Store Integration

### v1.x – Erweiterungen
- [ ] GitLab-Plugin
- [ ] Azure DevOps-Plugin
- [ ] Weiteres KI-Plugin (z. B. OpenAI / Claude)
- [ ] Export des Aufgabenprotokolls (PDF / Markdown)
- [ ] Erweiterte Agentenpaket-Verwaltung (Upload, Bearbeitung in der UI)

---

## 📚 Dokumentation

| Dokument | Beschreibung |
|----------|-------------|
| [Anforderungsanalyse](docs/requirements/requirements-analysis.md) | Funktionale und nicht-funktionale Anforderungen, Use Cases, Domänenmodell |
| [Architektur-Blueprint](docs/architecture/architecture-blueprint.md) | Schichtenarchitektur, Plugin-System, Sequenzdiagramme, Technologieentscheidungen |
| [Entity-Relationship-Modell](docs/architecture/entity-relationship-model.md) | Datenbankstruktur und Entitäten-Beziehungen |
| [Planungsübersicht](docs/planning-overview.md) | Projektplanung und Meilensteine |
| [Benutzerleitfaden](docs/user-guide.md) | Schritt-für-Schritt-Anleitung für Endanwender |
| [Feature-Dokumentation](docs/business/features.md) | Fachliche Beschreibung aller Features für nicht-technische Stakeholder |
| [Feature F009: Arbeitsverzeichnis konfigurieren](docs/business/features/F009-arbeitsverzeichnis-konfigurieren.md) | Fachliche Beschreibung des konfigurierbaren Arbeitsverzeichnisses inkl. Fallback und Migration |
| [Feature F010: Plugin-Prinzip für Integrationen](docs/business/features/F010-plugin-prinzip-integrationen.md) | Fachliche Beschreibung der ausgelagerten GitHub-/Copilot-Plugins |
| [Feature F011: GUID-präfixierte Copilot-Task-Datei](docs/business/features/F011-copilot-task-datei-bindung.md) | Fachliche Beschreibung von `{executionId}.copilot-task.md`, wildcard-Ignore-Regel und Korrelation pro Lauf |
| [Plugin-Interfaces](docs/api/plugin-interfaces.md) | Technische Dokumentation der Plugin-Schnittstellen für Plugin-Entwickler |
| [Copilot-Task-Binding (technisch)](docs/api/copilot-task-binding.md) | Technische Detaildokumentation für `executionId`, `{executionId}.copilot-task.md`, `.gitignore`-Konsolidierung und CLI-Parameter |
| [Workdir-Konfiguration (technisch)](docs/api/workdir-configuration.md) | Technische Umsetzung von Settings, Resolver, Klonpfadbildung und Reason-Codes |
| [Programmablaufpläne](docs/flows/development-process-flow.md) | Grafische Ablaufpläne und technische Prozessbeschreibungen |
| [Flow: Copilot-Task-Datei und Gitignore-Sync](docs/flows/copilot-task-binding-flow.md) | Sequenz- und Entscheidungsablauf für Prompt-Datei-Erstellung und idempotente Ignore-Regel |
| [Flow: Arbeitsverzeichnis-Auflösung](docs/flows/workdir-resolution-flow.md) | Sequenzablauf für Konfiguration, Laufzeit-Auflösung und Fallback-Verhalten |
| [Flow: Plugin-Discovery und Laden](docs/flows/plugin-discovery-load-flow.md) | Ablauf der dynamischen Plugin-Erkennung und robusten Registrierung |
| [Testplan: Plugin-Klassenbibliotheken](docs/tests/testplan-plugin-klassenbibliotheken-github-und-copilot.md) | Abgedeckte Testbereiche für Plugin-Discovery, Build-Kopie und Laufzeitverhalten |
| [Testlücken: Plugin-Klassenbibliotheken](docs/tests/testluecken-plugin-klassenbibliotheken-github-und-copilot.md) | Aktueller Stand der offenen Testlücken für das Plugin-Feature |

---

## 🤝 Beitragen

Beiträge zum Projekt sind willkommen! Bitte beachte die folgenden Konventionen.

### Branch-Konvention

| Typ | Muster | Beispiel |
|-----|--------|---------|
| Neues Feature | `feature/<kurz-beschreibung>` | `feature/gitlab-plugin` |
| Fehlerbehebung | `bugfix/<id>` | `bugfix/42-stream-timeout` |
| Refactoring | `refactor/<bereich>` | `refactor/ki-plugin-interface` |

### Commit-Format (Conventional Commits)

Commits folgen dem **[Conventional Commits](https://www.conventionalcommits.org/)**-Standard:

```
feat:     Neues Feature
fix:      Fehlerbehebung
refactor: Code-Umstrukturierung ohne Verhaltensänderung
test:     Tests hinzufügen oder korrigieren
docs:     Nur Dokumentationsänderungen
chore:    Build, Abhängigkeiten, CI-Konfiguration
```

**Beispiele:**
```
feat: GitHub Copilot Streaming-Unterstützung hinzugefügt
fix: Token-Speicherung im Credential Store repariert
refactor: KiOrchestrationService in kleinere Methoden aufgeteilt
```

### Pull Requests

- Jeder PR benötigt **mindestens 1 Approver** (Code-Review-Pflicht)
- Branch muss aktuell mit `main` sein (rebase oder merge vor dem PR)
- Alle Tests müssen bestehen (`dotnet test`)
- PR-Beschreibung enthält: Kontext, Änderungen, Testnachweis

### Coding-Guidelines

- **Naming:** PascalCase für Klassen/Methoden, camelCase für Parameter/Variablen, Präfix `I` für Interfaces
- **Async:** Alle I/O-Operationen konsequent `async`/`await` – keine `.Result`- oder `.Wait()`-Aufrufe
- **Logging:** `ILogger<T>` in allen Services – strukturiertes Logging mit aussagekräftigen Nachrichten und Parametern
- **Plugin-Erweiterungen:** Neue Plugins implementieren `IPlugin` + `IGitPlugin`/`IKiPlugin`, setzen `PluginType` und werden als eigenes Projekt unter `plugins/` eingebunden (Discovery via `PluginManager`, keine direkte `AddScoped<...>`-Bindung)

---

## 📄 Lizenz

MIT License *(Platzhalter – wird vor erster Veröffentlichung festgelegt)*

---

*Softwareschmiede – KI-gestützter Entwicklungsworkflow, lokal und unter Ihrer Kontrolle.*

