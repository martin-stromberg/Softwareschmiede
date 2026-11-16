# Bestandsaufnahme

## Betroffener Code

- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` — Discovery in `DiscoverPlugins()`:
  - scannt `plugins/*.dll` (TopDirectoryOnly) plus `plugins/*/` eine Ebene tief (`*.dll`).
  - `LoadPluginsFromDll(dllPath)` lädt via `AssemblyLoadContext.Default.LoadFromAssemblyPath` und scannt exportierte Typen auf `IGitPlugin`/`IKiPlugin`/`IIdePlugin`.
  - `IsAllowedInTestMode()` existiert bereits als Namensfilter, greift aber nur wenn `_applyTestModeFilter && IsTestMode()` — und listet nur 6 von 8 Plugins (kein BitBucket, kein LocalDirectory? doch LocalDirectory ist enthalten; fehlt: BitBucket).
- `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs` — Interface bleibt unverändert.

## Plugin-Bestand (8 First-Party-Plugins, Ordner `plugins/`)

| Assembly-Name | Typ |
|---|---|
| Softwareschmiede.Plugin.BitBucket | SCM |
| Softwareschmiede.Plugin.ClaudeCli | KI |
| Softwareschmiede.Plugin.Codex | KI |
| Softwareschmiede.Plugin.Devin | KI |
| Softwareschmiede.Plugin.GitHub | SCM |
| Softwareschmiede.Plugin.GitHubCopilot | KI |
| Softwareschmiede.Plugin.KiSimulator | KI |
| Softwareschmiede.Plugin.LocalDirectory | SCM |

## Deployment-Layout

`Softwareschmiede.App.csproj` (Target `CopyPluginsToOutput`) kopiert je Plugin den gesamten Build-Output
(`bin\<Config>\net10.0\*.dll`) nach `$(OutDir)plugins\<AssemblyName>\`. Ein Unterordner enthält daher
neben der Entry-DLL auch Dependency-DLLs (z. B. `Softwareschmiede.Plugin.Contracts.dll`,
`Microsoft.Extensions.Logging.Abstractions.dll`). Diese sind keine Ladekandidaten im Sinne der
Plugin-Discovery.

## Signatur-Status

- Keine Strong-Name-Signierung im Repo (`SignAssembly`/`*.snk` nicht vorhanden).
- .NET (Core) verifiziert Strong-Names beim Laden ohnehin nicht; eine Strong-Name-Prüfung wäre nur
  als manueller Public-Key-Token-Vergleich möglich und erfordert vorherige Signierung aller Assemblies
  — außerhalb des Rahmens dieser Aufgabe (Issue: „und/oder").

## Bestehender Schutz (unverändert)

- Test-Modus-Filter (`IsAllowedInTestMode`) bleibt als zusätzlicher Filter bestehen.
- `BadImageFormatException`/`FileLoadException`/allgemeine Fehler beim Laden → Warn-Log + Skip (besteht).

## Tests

- `src/Softwareschmiede.Tests/Infrastructure/Plugins/PluginManagerTests.cs` — kopiert echte
  Plugin-Assemblies in ein Temp-Verzeichnis (Top-Level), erwartet 2 SCM + 5 KI-Plugins.
  Whitelist muss diese Namen enthalten, damit die Tests weiterlaufen.
- Logger-Assertions via Moq `Mock<ILogger<T>>` etabliert (vgl. `GitHubPluginNetrcCleanupTests`).

## Relevante Docs

- `docs/help/plugins/beschreibung.md`, `ablauf-technisch.md`, `business-rules.md`, `installation.md`
