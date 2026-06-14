# Plugin-Interfaces und ValueObjects

## `IPlugin` (Interface)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs`

Gemeinsame Basis aller Plugins. Definiert Name und konfigurierbare Einstellungsfelder.

| Methode / Eigenschaft | Typ | Zweck |
|---|---|---|
| `PluginName` | `string` (Property) | Eindeutiger Anzeigename des Plugins (z.B. "GitHub", "GitHub Copilot") |
| `PluginPrefix` | `string` (Property) | Präfix für alle Credential-Store-Schlüssel (z.B. "Softwareschmiede.GitHub"). Einzelne Felder werden als `<PluginPrefix>.<FieldKey>` gespeichert |
| `GetSettingGroups()` | `IReadOnlyList<PluginSettingGroup>` (Methode) | Gibt die Einstellungsgruppen mit ihren Feldern zurück. Die Reihenfolge bestimmt die Anzeigereihenfolge in der UI |
| `PluginType` | `PluginType` (Property) | Plugin-Typ zur automatischen Zuordnung im PluginManager |

Einstellungswerte werden vom `ICredentialStore` unter dem Schlüssel `<PluginPrefix>.<FieldKey>` gespeichert.

---

## `IGitPlugin` (Interface)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

Git-Provider Plugin Interface. Erbt von `IPlugin`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRepositoryLinkFields()` | - | `IReadOnlyList<PluginSettingField>` | Liefert die Felder für die projektbezogene Repository-Verknüpfung |
| `GetIssuesAsync(repositoryId, ct)` | `string repositoryId`, `CancellationToken ct` | `Task<IEnumerable<Issue>>` | Ruft Issues aus dem Repository ab |
| `CloneRepositoryAsync(repositoryUrl, targetPath, ct)` | URLs, Pfade | `Task` | Klont ein Repository in das Zielverzeichnis |
| `CreateBranchAsync(localPath, branchName, ct)` | Pfad, Name | `Task` | Legt einen neuen Branch im lokalen Klon an |
| `PushBranchAsync(localPath, branchName, ct)` | Pfad, Name | `Task` | Pusht den Branch auf den Remote |
| `PullAsync(localPath, ct)` | Pfad | `Task` | Holt Änderungen vom Remote |
| `CreatePullRequestAsync(repositoryId, branchName, title, body, ct)` | IDs, Texte | `Task<PullRequest>` | Erstellt einen Pull Request |
| `CommitAsync(localPath, message, ct)` | Pfad, Nachricht | `Task` | Führt einen Commit durch |
| `ResetAsync(localPath, resetType, targetRef, ct)` | Pfad, Optionen | `Task` | Setzt Commits zurück |
| `CheckHealthAsync(ct)` | `CancellationToken` | `Task<bool>` | Prüft ob das Plugin verfügbar ist (CLI installiert, Token gültig) |
| `GetRemoteBranchesAsync(repositoryUrl, ct)` | URL | `Task<IEnumerable<string>>` | Listet alle Remote-Branches auf |
| `GetDefaultBranchAsync(repositoryUrl, ct)` | URL | `Task<string>` | Ermittelt den Standard-Branch eines Repositories |
| `CheckoutRemoteBranchAsync(localPath, branchName, ct)` | Pfad, Name | `Task` | Wechselt zu einem Remote-Branch |
| `GetGitActionCapabilitiesAsync(localPath, ct)` | Optionaler Pfad | `Task<GitActionCapabilities>` | Liefert verfügbare Git-Aktionen für die UI |
| `MergeToSourceAsync(localPath, ct)` | Pfad | `Task` | Übernimmt lokale Änderungen ins Quellverzeichnis |
| `GetAvailableRepositoriesAsync(ct)` | `CancellationToken` | `Task<IEnumerable<AvailableRepository>>` | Liefert verfügbare Repositories aus der externen Quelle |

---

## `IKiPlugin` (Interface)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IKiPlugin.cs`

KI-Plugin Interface. Startet CLI-Prozesse. Erbt von `IPlugin`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartCliAsync(localRepoPath, parameters, ct)` | `string localRepoPath`, `string? parameters`, `CancellationToken ct` | `Task<ProcessStartInfo>` | Startet den CLI-Prozess mit optionalen Parametern |
| `GetProcessWindowTitle(aufgabeId)` | `Guid aufgabeId` | `string` | Gibt einen Hinweis auf den erwarteten Fenstertitel des CLI-Prozesses |
| `SupportsSessionContinuation()` | - | `bool` | Gibt an, ob das Plugin Session-Fortsetzung unterstützt |
| `CheckHealthAsync(ct)` | `CancellationToken` | `Task<bool>` | Prüft ob das Plugin verfügbar ist |

---

## `PluginSettingGroup` (ValueObject)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingGroup.cs`

Gruppiert mehrere `PluginSettingField`-Einträge unter einem gemeinsamen Anzeigenamen.

```csharp
public sealed record PluginSettingGroup(
    string GroupName,
    IReadOnlyList<PluginSettingField> Fields);
```

---

## `PluginSettingField` (ValueObject)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`

Beschreibt ein einzelnes konfigurierbares Einstellungsfeld eines Plugins.

```csharp
public sealed record PluginSettingField(
    string Key,
    string Label,
    PluginSettingFieldType FieldType = PluginSettingFieldType.Text,
    string? Placeholder = null,
    string? Description = null,
    bool IsRequired = false,
    IReadOnlyList<string>? EnumOptions = null);
```

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Key` | `string` | Eindeutiger Schlüssel innerhalb des Plugins (wird mit Plugin-Prefix kombiniert) |
| `Label` | `string` | Anzeigename des Feldes |
| `FieldType` | `PluginSettingFieldType` | Datentyp und Darstellung des Feldes |
| `Placeholder` | `string?` | Beispieltext für das Eingabefeld (z.B. "ghp_...") |
| `Description` | `string?` | Optionale Beschreibung / Hinweistext unterhalb des Feldes |
| `IsRequired` | `bool` | Gibt an ob das Feld Pflicht ist |
| `EnumOptions` | `IReadOnlyList<string>?` | Zulässige Optionen für Enum-Felder |

---

## `PluginSettingFieldType` (Enum)
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

Datentyp eines Plugin-Einstellungsfeldes.

| Wert | Bedeutung |
|------|-----------|
| `Text` | Einzeiliger Text |
| `Secret` | Maskiertes Passwort- oder Token-Feld |
| `Url` | URL-Eingabe |
| `Integer` | Ganzzahl |
| `Boolean` | Wahrheitswert (Checkbox) |
| `Enum` | Auswahl über feste Werte (Select) |
| `FilePath` | Dateipfad mit Datei-Auswahl-Dialog |

---

## `IPluginManager` (Interface)
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

Verwaltet Discovery und Zugriff auf geladene Plugins.

| Methode | Rückgabewert | Zweck |
|---------|--------------|-------|
| `GetSourceCodeManagementPlugins()` | `IReadOnlyList<IGitPlugin>` | Gibt alle geladenen SCM-Plugins zurück |
| `GetDevelopmentAutomationPlugins()` | `IReadOnlyList<IKiPlugin>` | Gibt alle geladenen Development-Automation-Plugins zurück |
| `GetDefaultSourceCodeManagementPlugin()` | `IGitPlugin` | Gibt das erste verfügbare SCM-Plugin zurück |
| `GetDefaultDevelopmentAutomationPlugin()` | `IKiPlugin` | Gibt das priorisierte Development-Automation-Plugin zurück |
