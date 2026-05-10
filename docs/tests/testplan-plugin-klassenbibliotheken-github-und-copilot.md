# Testplan – Plugin-Klassenbibliotheken für GitHub und GitHub Copilot

## Ziel
Nachweis der Testabdeckung für das Feature „Plugin-Klassenbibliotheken für GitHub und GitHub Copilot“.

## Abgedeckte Testbereiche

1. **Plugin-Discovery und Fehlerverhalten**
   - `PluginManagerTests`: fehlender Plugin-Ordner, ungültige DLL, Laden gültiger Plugin-DLLs, Default-Plugin-Fehlerfall, keine Duplikatregistrierung.
2. **GitHub-SCM-Plugin**
   - `GitHubPluginTests`: Metadaten, Health-Check, Issue-Verarbeitung sowie zentrale Git-Operationen über den CLI-Runner.
3. **GitHub-Copilot-Plugin**
   - `GitHubCopilotPluginTests`: Agenten-Erkennung, Paket-Kompatibilität, Deploy von `.github`-Inhalten, Prompt-Datei/CLI-Parameter, Testausführung und Health-Check.
4. **Build-/Bereitstellungsmechanik**
   - Verifiziert über Projektkonfiguration in `src/Softwareschmiede/Softwareschmiede.csproj`:
     - `CopyPluginsToHostOutput`
     - `CopyPluginsToPublishOutput`

## Validierungskriterien

- `dotnet build .\Softwareschmiede.slnx --nologo` läuft erfolgreich.
- `dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo` läuft erfolgreich.
- Plugin-Feature ist über Unit-Tests für Discovery, Laufzeitverhalten und Integrationspunkte dokumentiert.

## Verknüpfte Dokumentation

- [API – Plugin-Interfaces](../api/plugin-interfaces.md)
- [Flow – Plugin-Discovery und Laden](../flows/plugin-discovery-load-flow.md)
- [Business – F010 Plugin-Prinzip für Integrationen](../business/features/F010-plugin-prinzip-integrationen.md)
