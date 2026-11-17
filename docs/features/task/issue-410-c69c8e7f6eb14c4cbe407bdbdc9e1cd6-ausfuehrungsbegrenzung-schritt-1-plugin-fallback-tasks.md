# Tasks: Plugin-Fallback für SCM-Ereignisausführungen (Schritt 1, #411)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `KiPluginResolution`-Record in `PluginSelectionService.cs` anlegen (`PluginPrefix`, `IgnoredPluginPrefixes`) | Erledigt | `PluginSelectionServiceTests.ResolveEnabledPlugin_*` |
| 2 | Logik | `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` implementieren (aktivierungsbewusste Kette Aufgabe→Projekt→Global→`ki.plugin.default`, Ignoriert-Liste, Logging) | Erledigt | `PluginSelectionServiceTests.ResolveEnabledPlugin_*` (8 Tests) |
| 3 | Logik | `ScmEventOrchestrator.DispatchCoreAsync`: impliziten Prefix über neue Auflösung wählen; „konfiguriert, nicht ausführbar" → neue Wartemeldung + `SystemMeldung`-Protokolleintrag bei Reason-Änderung, ohne `NextAttemptUtc`-Sperre | Erledigt | `ScmEventOrchestratorTests.UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix`, `DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers`, `ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable` |
| 4 | Testinfrastruktur | `ScmDispatchFixture` um optionales zweites KI-Plugin erweitern | Erledigt | `ScmEventOrchestratorTests.DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers` |
| 5 | Tests | Unit-Tests `PluginSelectionServiceTests` für alle Fallback-Zweige der neuen Auflösung | Erledigt | 8 neue Tests, alle bestanden |
| 6 | Tests | Dispatch-Tests `ScmEventOrchestratorTests`: Fallback-Start, verweigerter Start + Wiederaufnahme, strenge explizite Auswahl | Erledigt | 3 neue Tests, alle bestanden |
| 7 | Tests | `UnresolvableSettingsDefaultCli_KeepsWaitingState` an neue Wartemeldung anpassen | Erledigt | Test besteht im regulären Lauf |
