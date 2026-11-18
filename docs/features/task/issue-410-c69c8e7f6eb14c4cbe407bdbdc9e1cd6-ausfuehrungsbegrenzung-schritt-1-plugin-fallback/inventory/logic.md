# Logik – Plugin-Auflösung und SCM-Dispatch

## `PluginSelectionService`
Datei: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs`

Abhängigkeiten: `IPluginManager`, `PluginDefaultSettingsService`, `PluginActivationService`, `ILogger`, optional `AppEinstellungService` (Z. 13-17, Konstruktor Z. 20-32).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetStoredDefaultPluginPrefixAsync` | public | Liest `plugins.default.<PluginType>` (Delegat, Z. 35-36) |
| `SaveDefaultPluginPrefixAsync` | public | Schreibt globalen Default (Z. 39-40) |
| `SaveProjectDefaultPluginPrefixAsync` | public | Schreibt Projekt-Default (Z. 43-44) |
| `ResolveSourceCodeManagementPluginAsync` | public | SCM-Plugin: explizit → Default → Sortier-Fallback (Z. 47-59) |
| `GetAvailableKiPluginPrefixesAsync` | public | Prefixe aller **aktivierten** KI-Plugins (Z. 62-70) |
| `ResolveDevelopmentAutomationPluginAsync` | public | KI-Plugin: explizit → Default → Copilot-bevorzugter Sortier-Fallback; **prüft Aktivierung nicht** (Z. 73-85) |
| `ResolveDevelopmentAutomationPluginStrictAsync` | public | Nur expliziter Prefix, nur wenn **aktiviert** (`GetEnabledDevelopmentAutomationPluginsAsync`); sonst `InvalidOperationException` (Z. 88-94) |
| `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` | public | Prefix-Kette Aufgabe → Projekt-Default → Global-Default → `ki.plugin.default`; **prüft Aktivierung/Installation nicht** (Z. 103-126) |
| `ResolveConfiguredDefaultKiPluginPrefixAsync` | private | `ki.plugin.default`-Einstellung → Prefix via PluginName/PluginPrefix-Match auf **ungefilterte** `GetDevelopmentAutomationPlugins()`; kein Treffer → `null` + Warnung (Z. 134-157) |
| `ResolveIdePluginAsync` / `ResolveAlleKompatiblenIdePluginsAsync` | public | IDE-Auflösung, nicht betroffen (Z. 166-231) |
| `ResolvePluginAsync` / `TryResolveByPrefix` | private | Gemeinsame Auflösungshilfen (Z. 267-318) |

Aufgerufen von: `ScmEventOrchestrator.DispatchCoreAsync` (Z. 164, 167), `EntwicklungsprozessService` (Z. 162-164, 180-182, 285-287), `ScmEventQueueViewModel` u. a.

## `PluginActivationService`
Datei: `src/Softwareschmiede/Application/Services/PluginActivationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsPluginEnabledAsync` | public | `plugins.enabled.<prefix>`; fehlender/ungültiger Eintrag = aktiviert (Z. 30-34, `IsEnabledValue` Z. 82-88) |
| `SetPluginEnabledAsync` | public | Schreibt `plugins.enabled.<prefix>` (Z. 40-48) |
| `GetEnabledDevelopmentAutomationPluginsAsync` | public | Filtert `_pluginManager.GetDevelopmentAutomationPlugins()` auf aktivierte (Z. 59-60 via `FilterEnabledAsync` Z. 68-80) |

„Nicht verfügbar" im Sinne der Anforderung = nicht in der Rückgabe von `GetEnabledDevelopmentAutomationPluginsAsync` (deckt deaktiviert, nicht installiert und nicht ladbar ab, da `IPluginManager` nur geladene Plugins liefert).

## `PluginDefaultSettingsService`
Datei: `src/Softwareschmiede/Application/Services/PluginDefaultSettingsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDefaultPluginPrefixAsync` | public | `plugins.default.<PluginType>` (Z. 27-37, `BuildKey` Z. 109) |
| `SaveDefaultPluginPrefixAsync` | public | Upsert globaler Default (Z. 40-65) |
| `GetProjectDefaultPluginPrefixAsync` | public | `plugins.default.project.<guid>.<PluginType>` (Z. 68-78, `BuildProjectKey` Z. 111) |
| `SaveProjectDefaultPluginPrefixAsync` | public | Upsert Projekt-Default (Z. 81-107) |

## `ScmEventOrchestrator`
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchAsync` | public | Einstieg unter `ScmTaskCoordinator.Shared`-Aufgabensperre (Z. 23-28) |
| `OpenFromQueueAsync` | public | UI-Öffnen mit expliziter CLI-Auswahl (`select`-Callback → `explicitPluginPrefix`, Z. 31-72) |
| `DispatchCoreAsync` | private | Dispatch-Kern (Z. 74-264). Plugin-Auflösung Z. 164-167: `explicitPluginPrefix ?? ResolveDevelopmentAutomationPluginWithProjectScopeAsync(task.KiPluginPrefix, task.ProjektId)` → `ResolveDevelopmentAutomationPluginStrictAsync(prefix)`; Start Z. 168-174 mit `strictPluginSelection: true` |
| `ReconcileAttemptAsync` | private | Versuchsabgleich (Z. 389-504) |
| `RetryAsync` / `ConfirmReadyAsync` / `ResendAsync` / `CancelAsync` | public | Mutations-Einstiege über `MutateAndDispatchAsync` (Z. 276-358) |
| `BlockReason` | private static | `Beendet`/`Archiviert`/`PausiertBisUtc` → Sperrgrund (Z. 531-533) |
| `WaitAsync` | private | `Status = "Wartend"` + `Reason`, speichert (Z. 535-540) |
| `WaitWithCommentAsync` | private | `WaitAsync` + deterministische „waiting"-Outbox-Kommentaraktion (Z. 542-549) |

Fehlerpfad (Z. 243-255): `ScmProviderException` → Backoff-`NextAttemptUtc` bzw. `long.MaxValue` bei Auth/Permission; `InvalidOperationException`/`IOException`/`UnauthorizedAccessException` → **`NextAttemptUtc = long.MaxValue`** + `WaitWithCommentAsync(ex.Message)` — die heutige harte Sperre für „deaktiviertes/nicht installiertes Plugin".

Wichtig: `item.NextAttemptUtc > now → return` (Z. 126) — jeder gesetzte `NextAttemptUtc` in der Zukunft verhindert Wiederversuche durch den Poll. Ein Wartezustand mit `NextAttemptUtc == null` wird bei jedem Poll-Zyklus erneut dispatcht.

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenUndCliStartenAsync` | public | Klon+CLI-Start; `strictPluginSelection` → `ResolveDevelopmentAutomationPluginStrictAsync(kiPluginPrefix)` (Z. 126-135, 162-164, 180-182) |
| `CliNeustartenAsync` | public | Neustart im vorhandenen Klon; bei `strictPluginSelection` ebenfalls strikte Auflösung (Z. 236-243, 285-287) |
| (beide) | — | Nach erfolgreicher Auflösung wird `AufgabeService.UpdateAsync` mit `kiPlugin.PluginPrefix` aufgerufen → **`Aufgabe.KiPluginPrefix` wird auf den tatsächlich gestarteten Prefix aktualisiert** (Z. 183-188, 288-293) |

Konsequenz: Der vom Orchestrator ermittelte Fallback-Prefix wird — da er aktiviert ist — die interne Strikt-Prüfung bestehen und anschließend als `Aufgabe.KiPluginPrefix` persistiert.

## `ProtokollService`
Datei: `src/Softwareschmiede/Application/Services/ProtokollService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddEintragAsync` | public | Protokolleintrag `(aufgabeId, ProtokollTyp, inhalt, agentName?, ct)` (Z. 37-60) |

`ProtokollTyp`-Werte (`src/Softwareschmiede/Domain/Enums/ProtokollTyp.cs`): `Prompt`, `KiAntwort`, `StatusUebergang`, `TestErgebnis`, `GitAktion`, `CliOutput`, `RateLimit`, … Im Orchestrator genutzt für PR-Verknüpfung (`GitAktion`, Z. 519-523) und übersprungene Screenshots (Z. 473-476).
