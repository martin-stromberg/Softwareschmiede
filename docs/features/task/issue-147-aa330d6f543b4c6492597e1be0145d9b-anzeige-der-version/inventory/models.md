# Datenmodelle

## `InstalledVersionInfo`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`

Sealed Record mit folgenden Properties:

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Version` | `string` | Semantische Version ohne führendes `v` (z. B. "1.2.3") |
| `TagName` | `string?` | Optionaler Release-Tag aus `version.json` |
| `Commit` | `string?` | Optionaler Commit-SHA |
| `CreatedAtUtc` | `DateTimeOffset?` | Optionaler Erstellungszeitpunkt der Version |

**Verwendung:** Wird von `ApplicationVersionProvider.GetInstalledVersionAsync()` zurückgegeben und enthält die lokal installierte Versionsinformation.
