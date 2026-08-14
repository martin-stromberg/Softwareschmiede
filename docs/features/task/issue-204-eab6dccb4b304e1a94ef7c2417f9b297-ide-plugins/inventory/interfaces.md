# Bestandsaufnahme: Interfaces

## `IIdePlugin`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`

**Vererbung:** Erbt von `IPlugin` (aus `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`)

**Erbt von IPlugin:**
- `string PluginName { get; }` — Eindeutiger Anzeigename
- `string PluginPrefix { get; }` — Präfix für Credential-Store-Schlüssel
- `IReadOnlyList<PluginSettingGroup> GetSettingGroups()` — Konfigurierbare Einstellungsfelder
- `PluginType PluginType { get; }` — Plugin-Typ (hier: `PluginType.Ide`)

**Aktuell definierte Methoden in IIdePlugin:**

| Methode | Parameter | Rückgabewert | Beschreibung |
|---------|-----------|--------------|-------------|
| `CheckCompatibilityAsync` | `string repositoryPath`, `CancellationToken ct = default` | `Task<IdePluginCompatibility>` | Prüft die Kompatibilität des Plugins zum angegebenen Repository |
| `OpenRepositoryAsync` | `string repositoryPath`, `CancellationToken ct = default` | `Task` | Öffnet das Repository in der IDE |

**Hinweis:** Es existiert derzeit **keine** `FindEntryPointsAsync`- oder `OpenEntryPointAsync`-Methode; diese sollen mit der vorliegenden Anforderung eingeführt werden.

---

## `IPlugin`

**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

**Beschreibung:** Gemeinsame Basis aller Plugins (nicht nur IDE-Plugins)

| Eigenschaft/Methode | Typ | Beschreibung |
|---|---|---|
| `PluginName` | `string` (Property) | Eindeutiger Anzeigename des Plugins |
| `PluginPrefix` | `string` (Property) | Präfix für Credential-Store-Schlüssel |
| `PluginType` | `PluginType` (Property) | Plugin-Typ zur automatischen Zuordnung im PluginManager |
| `GetSettingGroups()` | Method → `IReadOnlyList<PluginSettingGroup>` | Gibt konfigurierbare Einstellungsfelder zurück |
