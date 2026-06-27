# Bestandsaufnahme

## Kurzfazit
Das Repository besitzt bereits eine belastbare Plugin-Struktur mit klar getrennten Contracts, dynamischer Discovery und einer kleinen Menge an KI-Plugins, die als direkte Referenzen fuer ein neues Codex-KI-Plugin dienen koennen.

## Bestehende KI-Plugins

### GitHub Copilot
- Pfad: `plugins/Softwareschmiede.Plugin.GitHubCopilot/`
- Projektdaten: `Softwareschmiede.Plugin.GitHubCopilot.csproj`, `GitHubCopilotPlugin.cs`
- Verhalten:
  - `PluginType.DevelopmentAutomation`
  - `ProviderDateiPraefix = "copilot"`
  - `PluginPrefix = "Softwareschmiede.GitHubCopilot"`
  - keine Session-Fortsetzung
  - optional konfigurierbarer CLI-Pfad ueber Credential-Key `Softwareschmiede.GitHubCopilot.ExecutablePath`
  - setzt bei vorhandenem Token `GH_TOKEN`
- Bedeutung fuer das neue Plugin:
  - ist das aktuell bevorzugte KI-Plugin in der Host-Priorisierung
  - zeigt das Muster fuer CLI-basierte KI-Integration mit Einstellungsgruppen und Authentifizierung

### Claude CLI
- Pfad: `plugins/Softwareschmiede.Plugin.ClaudeCli/`
- Projektdaten: `Softwareschmiede.Plugin.ClaudeCli.csproj`, `ClaudeCliPlugin.cs`
- Verhalten:
  - `PluginType.DevelopmentAutomation`
  - `ProviderDateiPraefix = "claude"`
  - `PluginPrefix = "Softwareschmiede.ClaudeCli"`
  - Session-Fortsetzung aktiv
  - eine Einstellungsgruppe fuer Authentifizierung
  - setzt bei vorhandenem Token `ANTHROPIC_API_KEY`
  - sucht das CLI ueber `PATH`, sonst Fallback auf `claude`
- Bedeutung fuer das neue Plugin:
  - ist das Dialog-/Assistenzmuster fuer eine allgemein nutzbare KI-Interaktion
  - zeigt das minimale Setup fuer ein CLI-gestuetztes KI-Plugin

### KI Simulator
- Pfad: `plugins/Softwareschmiede.Plugin.KiSimulator/`
- Projektdaten: `Softwareschmiede.Plugin.KiSimulator.csproj`, `KiSimulatorPlugin.cs`
- Verhalten:
  - `PluginType.DevelopmentAutomation`
  - `ProviderDateiPraefix = "simulator"`
  - `PluginPrefix = "Softwareschmiede.KiSimulator"`
  - keine Session-Fortsetzung
  - keine Plugin-Settings
  - Health-Check liefert immer `true`
  - startet fuer Tests einen kontrollierten `cmd.exe`-Prozess
- Bedeutung fuer das neue Plugin:
  - ist das lokale, deterministische Referenzplugin fuer Tests und Entwicklung
  - zeigt, wie ein KI-Plugin ohne externen Anbieter in die Plugin-Landschaft passt

## Plugin-Contracts und Discovery

### Contracts
- Gemeinsame Basis: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`
- KI-Contract: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`
- Git-Contract: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- Basisklassen:
  - `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
  - `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
- Weitere relevante Typen:
  - `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`
  - `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingGroup.cs`
  - `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`

### Wesentliche Beobachtungen
- `IPlugin` definiert Name, Prefix, Settings und Typ als gemeinsame Plugin-Basis.
- `IKiPlugin` koppelt KI-Plugins an CLI-Start, Fenstertitel, Session-Fortsetzung und Health-Check.
- `CliKiPluginBase` liefert die gemeinsame Startlogik fuer CLI-basierte KI-Plugins.
- `IGitPlugin` ist deutlich umfangreicher und trennt SCM- und KI-Plugins sauber ueber `PluginType`.
- `PluginSettingGroup` und `PluginSettingField` sind der vorhandene Mechanismus fuer plugin-spezifische Konfiguration.

### Discovery/Registry
- Host-Implementierung: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- Discovery-Verhalten:
  - lädt DLLs aus `AppContext.BaseDirectory/plugins`
  - scannt sowohl den Plugin-Ordner selbst als auch direkte Unterordner
  - instanziiert Exported Types ueber `ActivatorUtilities`
  - registriert nur Typen, die `IGitPlugin` oder `IKiPlugin` implementieren
  - trennt intern in SCM- und KI-Listen
  - benutzt im Testmodus einen Filter fuer `LocalDirectory`, `KiSimulator`, `ClaudeCli` und `GitHubCopilot`
  - bevorzugt fuer das Default-KI-Plugin den Prefix `copilot`

## Relevante Projektdateien

### Kernprojekte
- `src/Softwareschmiede/Softwareschmiede.csproj`
  - referenziert nur `Softwareschmiede.Plugin.Contracts`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
  - referenziert alle Plugin-Projekte inklusive Copilot, Claude CLI und KI Simulator
  - nutzt Tests fuers WPF-/Plugin-Umfeld
- `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`
  - referenziert `Softwareschmiede` und `Softwareschmiede.Plugin.LocalDirectory`
  - dient als Integrationsebene fuer Host- und Pluginwechsel

### Plugin-Projekte
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/Softwareschmiede.Plugin.GitHubCopilot.csproj`
- `plugins/Softwareschmiede.Plugin.ClaudeCli/Softwareschmiede.Plugin.ClaudeCli.csproj`
- `plugins/Softwareschmiede.Plugin.KiSimulator/Softwareschmiede.Plugin.KiSimulator.csproj`
- ergaenzend fuer Vergleich und Discovery:
  - `plugins/Softwareschmiede.Plugin.GitHub/Softwareschmiede.Plugin.GitHub.csproj`
  - `plugins/Softwareschmiede.Plugin.BitBucket/Softwareschmiede.Plugin.BitBucket.csproj`
  - `plugins/Softwareschmiede.Plugin.LocalDirectory/Softwareschmiede.Plugin.LocalDirectory.csproj`

### Solution
- Eine `.sln`-Datei wurde im aktuellen Checkout nicht gefunden.
- Die Projektverdrahtung laeuft damit sichtbar ueber direkte `ProjectReference`s.

## Tests

### Unit-Tests fuer KI-Plugins
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/ClaudeCliPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/KiSimulatorPluginTests.cs`

### Discovery- und Registry-Tests
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`
- deckt Plugin-Ladung, Default-Auswahl, Testmodus-Filter und Duplikatfreiheit ab

### Was diese Tests fuer das neue Plugin zeigen
- neue KI-Plugins muessen als eigene DLL im Plugin-Ordner auftauchen
- die Discovery reagiert auf `PluginType` und `PluginPrefix`
- Default-Auswahl und Priorisierung sind bereits implizit hostseitig festgelegt

## Dokumentation

### Vorhandene, direkt relevante Doku
- `README.md`
  - beschreibt Plugin-Architektur, Discovery, KI-Plugins und Testabdeckung auf hoher Ebene
- `docs/features/codex-plugin/requirement.md`
  - fachliche Zielbeschreibung fuer das neue Codex-KI-Plugin
- `docs/features/codex-plugin/todo.md`
  - Lifecycle-Fortschritt und naechste Schritte

### Abgeleitete Dokumentationslage
- Eine separate, detailreiche Dokumentation fuer ein neues Codex-KI-Plugin existiert im aktuellen Checkout noch nicht.
- Die vorhandene Doku beschreibt die Plugin-Plattform ausreichend, aber nicht das neue Codex-Plugin selbst.

## Relevante Schlussfolgerungen
- Ein neues Codex-KI-Plugin kann technisch an den bestehenden KI-Plugins anknuepfen, ohne die Discovery zu erweitern.
- Die wichtigste Integrationsentscheidung ist, ob das neue Plugin eher dem Copilot-Muster, dem Claude-Muster oder einem Mischmodell folgt.
- Der vorhandene Contract-Satz ist bereits geeignet, solange das neue Plugin als eigenes `IKiPlugin` mit sauberem `PluginPrefix` implementiert wird.
- Die Testlandschaft bietet bereits klare Vorlagen fuer Plugin-Metadaten, CLI-Start und Discovery-Verhalten.
