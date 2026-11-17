# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `KiPluginResolution` (Record) — angelegt in `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` (Z. 416-419) mit `PluginPrefix` und `IgnoredPluginPrefixes`
- [x] Methode `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` in `PluginSelectionService` — vorhanden (Z. 137-210): aktivierungsbewusste Kette Aufgaben-Prefix → Projekt-Default → Global-Default → `ki.plugin.default` (Match auf `PluginName`/`PluginPrefix` gegen die aktivierte Liste), deduplizierte `IgnoredPluginPrefixes`, `ILogger`-Warnungen je ignorierter Zuordnung
- [x] `ScmEventOrchestrator.DispatchCoreAsync` — erweitert (Z. 162-183): expliziter Prefix unverändert; implizite Auflösung über die neue Methode; bei `PluginPrefix == null` + nicht leerer Ignoriert-Liste neue Wartemeldung mit benannten Zuordnungen, `ProtokollTyp.SystemMeldung`-Eintrag nur bei geändertem `Reason`, `WaitWithCommentAsync` ohne `NextAttemptUtc`; bei leerer Ignoriert-Liste bisherige „Keine Standard-CLI"-Meldung
- [x] `ScmDispatchFixture`-Erweiterung — vorhanden: optionaler Ctor-Parameter `ScmKiProfile? ki2`, `Ki2`-Mock mit Prefix/Name/Health/Startspec, `GetDevelopmentAutomationPlugins()` liefert beide Plugins
- [x] Unit-Tests `PluginSelectionServiceTests` — alle 8 geplanten Methoden vorhanden: `ResolveEnabledPlugin_TaskPrefixEnabled_Wins`, `..._TaskPrefixDisabled_FallsBackToProjectDefault`, `..._TaskPrefixNotInstalled_FallsBack`, `..._ProjectDefaultDisabled_FallsBackToGlobalDefault`, `..._GlobalDefaultDisabled_FallsBackToSettingsDefault`, `..._AllConfiguredDisabled_ReturnsNullWithIgnored`, `..._NothingConfigured_ReturnsNullEmptyIgnored`, `..._SettingsDefaultDisabled_ReturnsNull`
- [x] Dispatch-Tests `ScmEventOrchestratorTests` — vorhanden: `DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers`, `UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix`, `ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable`
- [x] Angepasster Bestandstest `UnresolvableSettingsDefaultCli_KeepsWaitingState` — erwartet nun die neue „konfiguriert, nicht ausführbar"-Meldung

## Offene Aufgaben

Keine.

## Hinweise

- `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` und `ResolveDevelopmentAutomationPluginStrictAsync` blieben plan- und scope-gemäß unverändert (UI-Aufrufer, explizite Wahl).
- Der `long.MaxValue`-Fehlerpfad besteht weiterhin für `InvalidOperationException`/`IOException`/`UnauthorizedAccessException` (explizite Wahl, Health-Check, Start-/Workspace-Fehler) — entspricht den dokumentierten Scope-Ausschlüssen 1 und „explizite Wahl streng".
