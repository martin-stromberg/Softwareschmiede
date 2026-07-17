# Interfaces

## `ICredentialStore`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`

Kerninterface für sichere Credential-Speicherung. Wird von `WindowsCredentialStore` implementiert und durch `PluginSettingsService` sowie Plugins verwendet.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetCredential` | `target` (string) | `string?` | Gibt den gespeicherten Wert zurück oder null, wenn nicht vorhanden. `target` ist der Schlüssel/Zielname des Credentials (z.B. `"Softwareschmiede.Codex.CommandLineParameters"`). |
| `SetCredential` | `target` (string), `value` (string) | void | Speichert einen Credential-Wert unter dem angegebenen Schlüssel. |
| `DeleteCredential` | `target` (string) | void | Löscht einen Credential-Eintrag. |

### Verwendung in der Codebasis

- **`PluginSettingsService`**: Verwendet das Interface über DI-Konstruktor um Plugin-Einstellungen zu verwalten (Schlüsselformat: `<PluginPrefix>.<FieldKey>`)
- **`CodexPlugin`**: Injiziert via Konstruktor, nutzt `AppendCommandLineParameters()` um `CommandLineParameters` aus dem Store zu lesen
- **`LocalDirectoryPlugin`**: Injiziert via Konstruktor, liest Settings wie `WorkspaceMode`, `SourceDirectory`, `ConfirmGitInitInSourceDirectory`
- **E2E-Tests**: Instantiieren `WindowsCredentialStore` direkt für Test-Setup und Cleanup

