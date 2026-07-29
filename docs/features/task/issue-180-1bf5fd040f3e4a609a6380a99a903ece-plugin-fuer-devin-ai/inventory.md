# Bestandsaufnahme: Devin-CLI-Plugin

## Ergebnis

Die Anwendung lädt Plugins dynamisch als DLL aus dem `plugins`-Verzeichnis. Ein neues Devin-Plugin kann daher dem bestehenden Muster der CLI-Plugins folgen, ohne eine eigene Registrierung im Anwendungscode zu benötigen. Die fachliche Einordnung ist `PluginType.DevelopmentAutomation` und damit die vorhandene KI-Plugin-Auswahl.

Die engsten Referenzimplementierungen sind `CodexPlugin` und `GitHubCopilotPlugin`: Beide erben von `CliKiPluginBase`, verwenden `ICredentialStore` für `ExecutablePath` und `CommandLineParameters`, bauen ein `ProcessStartInfo` für das lokale Repository und nutzen die Basisklasse für Hilfe- und Health-Aufrufe.

## Relevante Komponenten

| Bereich | Bestand | Bedeutung für Devin |
|---|---|---|
| Plugin-Vertrag | `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs` und `Domain/Interfaces/IKiPlugin.cs` | Verbindlicher Vertrag für Start, Hilfe, Health-Check, Session-Fortsetzung und Einstellungen |
| CLI-Plugin-Muster | `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`, `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs` | Vorlage für Metadaten, Pfadkonfiguration und Prozessstart |
| Plugin-Discovery | `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` | Lädt alle exportierten Plugin-Typen aus Plugin-DLLs; keine manuelle DI-Registrierung nötig |
| Plugin-Auswahl | `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` | Verwendet alle geladenen `DevelopmentAutomation`-Plugins und deren `PluginPrefix` |
| Credential-/Einstellungswerte | `IPlugin` und `ICredentialStore` | Felder werden als `<PluginPrefix>.<FieldKey>` gespeichert; für Devin kein Token-Feld vorsehen |
| Prozessausführung | `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs` und `src/Softwareschmiede/Infrastructure/Services/CliRunner.cs` | `ProcessStartInfo`, stdin/stdout/stderr, Parameterweitergabe und Prozesslebenszyklus |
| Tests | `src/Softwareschmiede.Tests/Infrastructure/Plugins/*PluginTests.cs` und `PluginManagerTests.cs` | Bestehende Teststruktur für Metadaten, Einstellungen, Pfadauflösung und Discovery |

## Vorgesehene Artefakte für die Umsetzung

- Neues Projekt `plugins/Softwareschmiede.Plugin.Devin` mit `DevinPlugin.cs` und Referenz auf `Softwareschmiede.Plugin.Contracts`.
- Neuer Eintrag in `Softwareschmiede.slnx`.
- Referenz des neuen Plugin-Projekts im Testprojekt.
- Aktualisierung des Testmodus-Filters in `PluginManager.IsAllowedInTestMode`.
- `DevinPluginTests` analog zu den vorhandenen CLI-Plugin-Tests.
- Bei abweichender Devin-CLI-Syntax gezielte Anpassung von `GetCliHelpTextAsync`, `CheckHealthAsync`, `SupportsSessionContinuation` oder dem Prozessstart.

## Offene technische Erkenntnisse

- Der vorhandene `ResolveExecutablePath`-Mechanismus der Basisklasse verwendet nur den konfigurierten Credential-Store-Pfad oder den übergebenen Befehlsnamen. Er durchsucht nicht selbst den `PATH`; die eigentliche Prozessauflösung erfolgt zusätzlich in `CliRunner`.
- `RunHelpCommandAsync` und `CheckHealthWithVersionCommandAsync` erwarten derzeit `--help` bzw. `--version`. Ob Devin diese Parameter unterstützt, muss vor der Implementierung anhand der offiziellen CLI-Spezifikation oder einer lokalen CLI geprüft werden.
- `CliRunner` schließt stdin unmittelbar nach dem Prozessstart. Ein Devin-Plugin muss deshalb mit dem bestehenden nicht-interaktiven Ausführungsmodell kompatibel sein oder einen klar begrenzten Sonderweg benötigen.
- `SupportsSessionContinuation` muss anhand des Devin-Aufrufvertrags festgelegt werden; aus dem Bestand ist kein allgemeiner Session-Mechanismus ableitbar.
- Der korrekte Devin-Anzeigename, Dateipräfix und Plugin-Präfix sind noch nicht im Repository festgelegt und müssen konsistent gewählt werden.

## Detaildokumente

- [Plugin-Architektur und Discovery](inventory/plugin-architecture.md)
- [CLI-Plugin-Vertrag und Prozessstart](inventory/cli-contract.md)
- [Konfiguration und Executable-Auflösung](inventory/configuration.md)
- [Tests und betroffene Projektdateien](inventory/testing.md)

## Abweichung vom Lifecycle-Ablauf

Für diesen Lauf stand kein delegierbarer Unteragent mit `/inventory`-Ausführung zur Verfügung. Die Bestandsaufnahme wurde deshalb direkt anhand des Repository-Codes erstellt; die erwarteten Artefakte und die Struktur des Lifecycle-Schritts wurden beibehalten.
