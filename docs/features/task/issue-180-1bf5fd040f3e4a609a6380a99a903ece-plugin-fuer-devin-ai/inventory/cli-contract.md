# CLI-Plugin-Vertrag und Prozessstart

## Basisklasse

`CliKiPluginBase` verlangt Metadaten, Einstellungsgruppen, `BuildProcessStartInfo`, `SupportsSessionContinuation` und `CheckHealthAsync`. Die Basisklasse stellt `StartCliAsync`, `RunHelpCommandAsync`, `CheckHealthWithVersionCommandAsync`, Pfadauflösung über `ICredentialStore` und das Anhängen zusätzlicher Kommandozeilenparameter bereit.

## Referenzablauf

`CodexPlugin` und `GitHubCopilotPlugin` setzen `FileName` auf die konfigurierte Executable oder den CLI-Befehlsnamen, übernehmen das lokale Repository als `WorkingDirectory` und hängen optionale `CommandLineParameters` an. Authentifizierungsvariablen werden nur von Plugins gesetzt, die tatsächlich ein Token verwenden.

## Devin-Anforderungen

- Devin darf kein Token- oder Credential-Feld anbieten und keine Authentifizierungsvariable setzen.
- Der normale Start muss ohne zusätzliche Credential-Argumente funktionieren.
- Hilfe und Health verwenden standardmäßig `--help` und `--version`; Devin-Kompatibilität muss verifiziert werden.
- Session-Fortsetzung ist nur zu aktivieren, wenn der Devin-CLI-Aufruf sie ausdrücklich unterstützt.
- Parameter sollten über `ProcessStartInfo.ArgumentList` oder den bestehenden Parametervertrag sicher übergeben werden; keine Tokenwerte als CLI-Argumente ergänzen.

## Betroffene Dateien

- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
- `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
- neues `DevinPlugin.cs`
