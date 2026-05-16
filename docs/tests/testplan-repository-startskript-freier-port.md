# Testplan – Schließung der Testlücken „start.ps1 für freien HTTP-Port“

## Eingabe

- Quelle: `docs/tests/testluecken-repository-startskript-freier-port.md`
- Ziel: Kritische Lücken für das neue `start.ps1`-Verhalten und die csproj-Integration schließen.

## Umsetzungsplan (priorisiert)

### AP-01 (P1): Prozessnahe Integrationssuite für `start.ps1`

- Datei: `src/Softwareschmiede.IntegrationTests/Scripts/StartPs1IntegrationTests.cs`
- Umgesetzte Tests:
  - `StartScript_ShouldPreferPortParameter_WhenEnvironmentPortIsAlsoSet`
  - `StartScript_ShouldFailWithExit12_WhenProvidedPortIsAlreadyInUse`
  - `StartScript_ShouldFailWithExit10_WhenLaunchSettingsIsMissing`

### AP-02 (P1): Contract-Test für csproj linked item

- Datei: `src/Softwareschmiede.Tests/Infrastructure/ProjectConfiguration/SoftwareschmiedeProjectFileTests.cs`
- Umgesetzter Test:
  - `SoftwareschmiedeCsproj_ShouldContainLinkedStartScriptItem`

### AP-03 (P2): Geplante Erweiterungen

- Zusätzliche Skript-Fehlerpfade:
  - invalid JSON (`Exit 11`)
  - fehlendes `http`-Profil (`Exit 11`)
  - Write-Fehler (`Exit 13`)
- Host-Verhalten:
  - Host aus bestehender `applicationUrl` wird übernommen
  - Fallback auf `localhost` bei ungültigem/missing HTTP-URL

## Verifikation

- Ausgeführt:
  - `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
  - `dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

## Ergebnisstatus

Die priorisierten P1-Lücken sind implementiert und grün verifiziert.  
P2-Erweiterungen bleiben als nachgelagerte Ergänzung im Test-Backlog.
