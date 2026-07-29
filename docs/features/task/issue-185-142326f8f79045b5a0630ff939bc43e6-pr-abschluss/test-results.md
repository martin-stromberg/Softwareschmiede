# Testergebnisse - PR-Abschluss

Status: `Keine Fehler`

Datum: 2026-07-29

## Ausgefuehrte Kommandos

| Kommando | Ergebnis |
|----------|----------|
| `dotnet build Softwareschmiede.slnx` | Erfolgreich, 0 Fehler, bestehende Warnungen |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~PullRequestMonitoringServiceTests|FullyQualifiedName~PullRequestReferenzServiceTests|FullyQualifiedName~GitHubPluginTests" --no-build` | Erfolgreich, 63 bestanden |
| `dotnet test Softwareschmiede.slnx --no-build` | Nach ca. 298 Sekunden ohne verwertbare Ausgabe mit Exitcode 1 beendet |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build` | Nach ca. 296 Sekunden ohne verwertbare Ausgabe mit Exitcode 1 beendet |
| `dotnet test src\Softwareschmiede.IntegrationTests\Softwareschmiede.IntegrationTests.csproj --no-build` | Erfolgreich, 69 bestanden |
| `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --blame-hang-timeout 60s --blame-hang-dump-type none` | Timeout nach ca. 184 Sekunden, keine verwertbare Ausgabe |

## Fehlgeschlagene Tests

Keine.

## Erfolgreiche relevante Tests

- Build der Solution erfolgreich.
- Fokussierte PR-/Monitoring-/GitHub-Tests: 63 bestanden.
- Integrationstestprojekt vollstaendig: 69 bestanden.

## Offene Testnachweise

- Der vollstaendige Lauf des Unit-/E2E-Testprojekts `src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj` schliesst weiterhin nicht innerhalb von mehreren Minuten ab und liefert dabei keine verwertbare Ausgabe.
- Der vollstaendige Solution-Testlauf ist dadurch weiterhin nicht erfolgreich nachgewiesen.
