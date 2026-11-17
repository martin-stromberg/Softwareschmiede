# Tasks: Plugin-Fallback für SCM-Ereignisausführungen (Schritt 1, #411)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `KiPluginResolution`-Record in `PluginSelectionService.cs` anlegen (`PluginPrefix`, `IgnoredPluginPrefixes`) | Offen | — |
| 2 | Logik | `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` implementieren (aktivierungsbewusste Kette Aufgabe→Projekt→Global→`ki.plugin.default`, Ignoriert-Liste, Logging) | Offen | — |
| 3 | Logik | `ScmEventOrchestrator.DispatchCoreAsync`: impliziten Prefix über neue Auflösung wählen; „konfiguriert, nicht ausführbar" → neue Wartemeldung + `SystemMeldung`-Protokolleintrag bei Reason-Änderung, ohne `NextAttemptUtc`-Sperre | Offen | — |
| 4 | Testinfrastruktur | `ScmDispatchFixture` um optionales zweites KI-Plugin erweitern | Offen | — |
| 5 | Tests | Unit-Tests `PluginSelectionServiceTests` für alle Fallback-Zweige der neuen Auflösung | Offen | — |
| 6 | Tests | Dispatch-Tests `ScmEventOrchestratorTests`: Fallback-Start, verweigerter Start + Wiederaufnahme, strenge explizite Auswahl | Offen | — |
| 7 | Tests | `UnresolvableSettingsDefaultCli_KeepsWaitingState` an neue Wartemeldung anpassen | Offen | — |
