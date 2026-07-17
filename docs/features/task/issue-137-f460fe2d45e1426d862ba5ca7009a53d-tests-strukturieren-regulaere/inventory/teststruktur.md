# Teststruktur und Kategorien

## Vorhandene Testprojekte

- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
- `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`

Beide Projekte werden vom Einzeltest-Skript ueber `Microsoft.NET.Test.Sdk` entdeckt. Der CI-Workflow baut und testet aktuell nur `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`.

## Vorhandene Kategorien

| Kategorie | Fundstellen | Bedeutung |
|---|---:|---|
| `E2E` | viele Klassen unter `src/Softwareschmiede.Tests/E2E/` | WPF-/FlaUI-nahe End-to-End-Tests, brauchen Desktop-/App-Zustand |
| `ConPTY` | `src/Softwareschmiede.Tests/ServiceIntegration/CliEmbeddingServiceIntegrationTests.cs` | echter ConPTY-Integrationstest |
| `OsInterface` | keine | Zielkategorie aus der Anforderung |

Aktueller CI-Filter:

```powershell
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build -c Debug --filter "Category!=E2E&Category!=ConPTY"
```

Der geforderte Filter `Category!=OsInterface` kann derzeit keine OS-nahen Tests ausschliessen.

## E2E-Struktur

Die E2E-Klassen liegen gebuendelt unter `src/Softwareschmiede.Tests/E2E/`. Die meisten Dateien tragen einen Kommentar mit lokalem/CI-Filterhinweis und ein Klassen-Trait:

```csharp
[Trait("Category", "E2E")]
```

Mehrere ConPTY-bezogene E2E-Klassen tragen nur `E2E`, nicht zusaetzlich `ConPTY` oder `OsInterface`, zum Beispiel:

- `E2E_ConPtyTerminalStart.cs`
- `E2E_ConPtyResize.cs`
- `E2E_ConPtyProcessEnd.cs`
- `E2E_ConPtyKeyboardInput.cs`

## Fehlende zentrale Testattribute

Es gibt keine zentrale Datei fuer projektspezifische xUnit-Attribute wie `OsInterfaceFactAttribute` oder `OsInterfaceTheoryAttribute`. Falls solche Attribute eingefuehrt werden, bietet sich ein Test-Infrastrukturordner im Testprojekt an, zum Beispiel:

- `src/Softwareschmiede.Tests/Infrastructure/Testing/`
- alternativ `src/Softwareschmiede.Tests/Helpers/`

Wichtig: Ein von `FactAttribute` abgeleitetes Attribut setzt nicht automatisch xUnit-Traits. Fuer `dotnet test --filter Category=OsInterface` braucht es entweder direkte `[Trait("Category", "OsInterface")]`-Verwendung oder eine xUnit-kompatible Trait-Discoverer-Implementierung.
