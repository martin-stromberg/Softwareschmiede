# Interfaces: GitHub-PAT-Authentifizierung

## `ICredentialStore`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetCredential(string target)` | `target` – Schlüssel/Zielname | `string?` – Wert oder null | Liest einen Credential-Wert aus dem Windows Credential Store (oder äquivalent). **Wird von `GitHubPlugin.GetGhEnvironment()` aufgerufen, um den Token `Softwareschmiede.GitHub.Token` zu holen** |
| `SetCredential(string target, string value)` | `target` – Schlüssel, `value` – Wert | void | Speichert einen Credential-Wert im Windows Credential Store |
| `DeleteCredential(string target)` | `target` – Schlüssel | void | Löscht einen Credential-Eintrag |

**Beschreibung:** Sicherer Speicher für sensitive Daten (API-Tokens, Passwörter). Token wird unter dem Schlüssel `Softwareschmiede.GitHub.Token` gespeichert und ist nicht in Dateien/Logs sichtbar.

---

## `ICliRunner`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync(string command, IEnumerable<string> args, string? workingDirectory, IDictionary<string, string>? environmentVariables, CancellationToken ct)` | `command` – CLI-Programm (z.B. `"git"`, `"gh"`), `args` – Argumente, `workingDirectory` – Arbeitsverzeichnis, `environmentVariables` – Umgebungsvariablen (z.B. für Token), `ct` – CancellationToken | `Task<CliResult>` | Führt CLI-Befehle synchron aus. **Wird von `GitHubPlugin` für alle `git` und `gh` Befehle verwendet. Umgebungsvariablen wie `GH_TOKEN` werden hier übergeben** |
| `StreamAsync(string command, IEnumerable<string> args, string? workingDirectory, IDictionary<string, string>? environmentVariables, CancellationToken ct)` | Wie oben | `IAsyncEnumerable<string>` | Führt CLI-Befehle aus und streamt stdout zeilenweise (aktuell nicht in `GitHubPlugin` verwendet) |

**Beschreibung:** Abstrahiert CLI-Ausführung. Unterstützt Umgebungsvariablen-Übergabe, kritisch für sichere Token-Übergabe.

---

## `IPlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs` (nicht vollständig gelesen, aber referenziert)

| Eigenschaft/Methode | Typ | Zweck |
|-------------|-----|-------|
| `PluginPrefix` | string | Präfix für Konfigurationsschlüssel (für GitHub: `"Softwareschmiede.GitHub"`) |
| `PluginName` | string | Display-Name (für GitHub: `"GitHub"`) |
| `GetSettingGroups()` | Method | Gibt konfigurierbare Einstellungsgruppen zurück (einschl. Token-Feld) |

---

## Verbindungen zwischen Interfaces

```
┌─────────────────────────┐
│    GitHubPlugin         │
│  (Implementierung)      │
└────────┬────────────────┘
         │
    ┌────┴──────┐
    │            │
    ▼            ▼
┌──────────────┐ ┌────────────────────┐
│ICredentialStore│ │    ICliRunner      │
│ - Token Speicher│ │- git/gh Befehle  │
│ - Secure       │ │- Env-Variablen   │
└────────────────┘ └────────────────────┘
```

### `PluginSettingsService` als Vermittler

`PluginSettingsService` nutzt `ICredentialStore`, um Plugin-Einstellungen zu verwalten:
- Schlüssel-Format: `{plugin.PluginPrefix}.{field.Key}`
- Für GitHub-Token: `Softwareschmiede.GitHub.Token`
