# Testplan – Issue-Auswahl, Branch-Verknüpfung und PR Auto-Close

## Ziel

Vollständige Absicherung des Feature-Scope:
- Issue wird ausgewählt
- Prozessstart erzeugt/verknüpft Branch
- PR enthält Closing-Direktive für Auto-Close beim Merge

## Status

Alle geplanten Testfälle sind umgesetzt und grün.

## Verifikation

1. Fokussierter Lauf

```powershell
dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo --filter "FullyQualifiedName~NeueAufgabeBunitTests|FullyQualifiedName~EntwicklungsprozessServiceTests|FullyQualifiedName~GitOrchestrationServiceTests"
```

Ergebnis: **62/62 erfolgreich**

2. Gesamtlauf

```powershell
dotnet test .\Softwareschmiede.slnx --nologo
```

Ergebnis: **406/406 erfolgreich**

## Fazit

Die Testlücken für Issue/Branch/PR-Linking sind vollständig geschlossen.
