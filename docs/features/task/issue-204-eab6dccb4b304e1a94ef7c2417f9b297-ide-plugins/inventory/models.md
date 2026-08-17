# Datenmodelle

## `IdeEntryPoint`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/IdeEntryPoint.cs`

Ein unveränderlicher Record, der einen Einstiegspunkt darstellt, den ein IDE-Plugin öffnen kann.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Path` | `string` | Pfad des Einstiegspunkts (z. B. Solution-Datei `.sln` oder Repository-Verzeichnis) |
| `DisplayName` | `string?` | Optional: Anwenderfreundliche Bezeichnung für die UI-Anzeige |

**Notizen:**
- Es ist ein Record mit zwei Parametern im Constructor.
- `DisplayName` ist nullable und wird bei der Übergabe optional mit `null` initialisiert.
- Der Einstiegspunkt wird von `IIdePlugin.FindEntryPointsAsync` ermittelt und zurückgegeben.
