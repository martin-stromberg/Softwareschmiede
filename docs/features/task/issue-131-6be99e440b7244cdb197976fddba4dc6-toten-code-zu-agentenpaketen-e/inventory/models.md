# Datenmodell

## `AgentPackageInfo`

Datei: `src/Softwareschmiede/Domain/ValueObjects/AgentPackageInfo.cs`

Ein Record, das Informationen über ein Agentenpaket enthält. Es wird als Rückgabewert von `IAgentPackageService` und `IAgentPackageFileService` verwendet.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Name des Pakets |
| `Pfad` | `string` | Dateisystempfad des Pakets (absoluter Pfad) |
| `Agenten` | `IReadOnlyList<AgentInfo>` | Verfügbare Agenten in diesem Paket. `AgentInfo` wird definiert in `Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/AgentInfo.cs` und wird auch in `CliKiPluginBase.cs` verwendet (nicht zu löschen) |
| `Dateien` | `IReadOnlyList<string>` | Liste aller Dateien im Paket (relative Pfade) |

### Hinweise

- Das Record ist ein einfaches Datenträger-Objekt ohne Geschäftslogik
- `AgentInfo` ist eine externe Abhängigkeit; das Entfernen von `AgentPackageInfo` bedeutet nicht, dass `AgentInfo` gelöscht werden kann
- Beide Properties `Agenten` und `Dateien` sind read-only Collections
