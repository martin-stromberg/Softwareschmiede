# Testlücken – Feature „start.ps1 für freien HTTP-Port in Visual-Studio-Debug“

## Analysebasis

- Geänderter Scope:
  - `start.ps1` (neu, Repo-Root)
  - `src/Softwareschmiede/Softwareschmiede.csproj` (linked file auf `start.ps1`)
- Kontextdokumente:
  - `docs/requirements/start-ps1-visual-studio-freier-http-port-requirements-analysis.md`
  - `docs/architecture/start-ps1-visual-studio-freier-http-port-architecture-blueprint.md`
  - `docs/improvements/start-ps1-visual-studio-freier-http-port-architecture-review.md`
- Testbasis:
  - `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
  - `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

## Priorisierte Lücken und Status

### P1 – Kritisch

1. **Skript-Priorisierung Parameter > Environment**  
   - Ziel: `-Port` überschreibt `SOFTWARESCHMIEDE_FREE_PORT`.  
   - Status: ✅ geschlossen (`StartPs1IntegrationTests.StartScript_ShouldPreferPortParameter_WhenEnvironmentPortIsAlsoSet`)

2. **Deterministischer Fehlerpfad bei belegtem Port**  
   - Ziel: Exit-Code `12`, keine Veränderung von `launchSettings.json`.  
   - Status: ✅ geschlossen (`StartPs1IntegrationTests.StartScript_ShouldFailWithExit12_WhenProvidedPortIsAlreadyInUse`)

3. **Fehlerpfad „launchSettings fehlt“**  
   - Ziel: Exit-Code `10` mit klarer Diagnose.  
   - Status: ✅ geschlossen (`StartPs1IntegrationTests.StartScript_ShouldFailWithExit10_WhenLaunchSettingsIsMissing`)

4. **MSBuild-Integration des Linked Files in csproj**  
   - Ziel: `<None Include="..\..\start.ps1" Link="start.ps1" />` vertraglich abgesichert.  
   - Status: ✅ geschlossen (`SoftwareschmiedeProjectFileTests.SoftwareschmiedeCsproj_ShouldContainLinkedStartScriptItem`)

### P2 – Mittel

5. **Skriptpfade „invalid JSON“, „http-Profil fehlt“, „Write-Fehler“ direkt auf Prozessebene**  
   - Status: 🔶 offen (als nächste Erweiterung der Integrationssuite vorgesehen)

6. **Host-Fallback/Host-Erhalt bei `applicationUrl` in unterschiedlichen Inputformen**  
   - Status: 🔶 offen

## Ergebnis

Die kritischen, feature-nahen Testlücken für `start.ps1` und die csproj-Integration sind durch automatisierte Tests geschlossen.  
Es verbleiben mittelpriorisierte Erweiterungen für zusätzliche Negativ- und Randfälle des Skripts.
