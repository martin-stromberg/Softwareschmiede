# Testergebnisse

## Ausgefuehrte Tests

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=E2E&Category!=ConPTY" -c Debug --logger "console;verbosity=minimal"
```

Ergebnis: bestanden, 894/894 Tests.

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category!=E2E&Category!=ConPTY" --logger "console;verbosity=minimal"
```

Ergebnis: bestanden, 894/894 Tests.

## Fehlgeschlagene Tests

Keine.
