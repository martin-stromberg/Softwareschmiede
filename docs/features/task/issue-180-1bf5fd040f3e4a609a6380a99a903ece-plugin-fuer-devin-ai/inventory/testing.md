# Tests und betroffene Projektdateien

## Bestehende Testmuster

`CodexPluginTests` und `ClaudeCliPluginTests` prüfen Plugin-Metadaten, Einstellungsgruppen, Session-Fortsetzung, Executable-Pfad, Parameterweitergabe und Authentifizierungsvariablen. `PluginManagerTests` prüft Discovery und die Anzahl geladener KI-Plugins. `CliKiPluginBaseTests` deckt gemeinsame Hilfsfunktionen ab.

## Erwartete Devin-Tests

- Metadaten: Anzeigename, `PluginPrefix`, `ProviderDateiPraefix`, `PluginType`.
- Einstellungen: `ExecutablePath` vorhanden, optional, kein Token-/API-Key-Feld.
- Prozessstart: Standardbefehlsname bei fehlendem Pfad, manueller Pfad bei Konfiguration, Arbeitsverzeichnis und optionale Parameter.
- Sicherheit: keine Authentifizierungsumgebungsvariable und keine Token-Argumente.
- Health/Hilfe: Devin-kompatible Argumente, falls diese vom Standard `--version`/`--help` abweichen.
- Discovery: neue DLL wird geladen und als Development-Automation-Plugin gefunden.

## Projektänderungen

- `plugins/Softwareschmiede.Plugin.Devin/Softwareschmiede.Plugin.Devin.csproj`
- `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/DevinPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- `Softwareschmiede.slnx`

## Testumgebung

Die Plugin- und Repository-Tests zielen auf `net10.0` bzw. `net10.0-windows10.0.17763.0`. Für echte Health-/CLI-Tests darf keine installierte Devin-CLI vorausgesetzt werden; der Prozessstart sollte über reine `ProcessStartInfo`-Assertions getestet werden. Ein echter CLI-Aufruf gehört, falls überhaupt, in einen explizit überspringbaren Integrationstest.
