# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

|| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
||--------------------|-------------------|----------------------|--------|
|| Deaktiviertes/nicht mehr vorhandenes Projekt- bzw. Aufgaben-Standard-Plugin → SCM-Ausführung startet mit globalem Standard-Plugin | Neue Methode `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` (aktivierungsbewusste Kette Aufgabe→Projekt→Global→`ki.plugin.default`); `DispatchCoreAsync` nutzt sie für den impliziten Prefix; Ablaufschritte 1–4; Szenarien 1–2 | `DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers` (Dispatch), Unit-Tests `...TaskPrefixDisabled_FallsBackToProjectDefault`, `...TaskPrefixNotInstalled_FallsBack`, `...ProjectDefaultDisabled_FallsBackToGlobalDefault` | Abgedeckt |
|| Globales Standard-Plugin ebenfalls nicht ausführbar/aktiv → kein Start; ehrlicher Wartegrund + Protokolleintrag; kein willkürliches Ersatz-Plugin; keine `long.MaxValue`-Sperre ohne Wiederaufnehmbarkeit | Ablaufschritt 6: neue Wartemeldung + `SystemMeldung`-Protokolleintrag + `WaitWithCommentAsync` ohne `NextAttemptUtc`; kein Ersatz-Plugin außerhalb der konfigurierten Kette; Szenarien 3–4 | `UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix` (Status/Reason/`NextAttemptUtc == null`/Protokolleintrag/keine Sitzung + Wiederaufnahme via `RetryAsync`), `...AllConfiguredDisabled_ReturnsNullWithIgnored`, `...NothingConfigured_ReturnsNullEmptyIgnored` | Abgedeckt |
|| „Nicht verfügbar" deckt deaktiviert, nicht installiert und nicht ladbar ab; ausführbarer explizit gewählter Prefix verhält sich wie bisher (kein Fallback bei expliziter Wahl) | Prüfung gegen `GetEnabledDevelopmentAutomationPluginsAsync` (deckt alle drei Zustände); expliziter Prefix bleibt auf `ResolveDevelopmentAutomationPluginStrictAsync`-Pfad; Szenario 5 | `ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable` (`long.MaxValue` unverändert), Unit-Tests deaktiviert/nicht installiert/unset | Abgedeckt |
|| Unit-Tests für die neue Auflösungskette (alle Fallback-Zweige, deaktiviert/nicht installiert/unset) sowie Dispatch-Tests über `ScmDispatchFixture` für Fallback-Start und verweigerten Start mit Wartegrund | 8 Unit-Testmethoden in `PluginSelectionServiceTests`, 3 Dispatch-Tests in `ScmEventOrchestratorTests`, Fixture-Erweiterung (zweites KI-Plugin) als eigener Umsetzungsschritt 3 | Vollständige Tabelle in `plan.md` § Tests | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

|| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
||------------------------------------|--------------------|--------|
|| Kein neuer oder geänderter Benutzerfluss — interne Auflösungs-/Fehlerlogik des SCM-Dispatchs; Wartegrund erscheint über den bestehenden `Reason`-Mechanismus | Keine neuen E2E-Tests (Begründung im Plan) | Nicht erforderlich mit Begründung |
|| Bestehende E2E-Tests | Keine Anpassung nötig (keine geänderte UI, keine geänderte Nutzeraktion) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- Die vier Scope-Ausschlüsse tragen den Status `bestätigt` mit dokumentierter Annahme (automatisierte Umgebung ohne Anwender-Rückfrage); sie sind fachlich eng an der Anforderung („explizite Wahl streng", „kein willkürliches Ersatz-Plugin", „keine UI-Anforderung") ausgerichtet.
- Der betroffene Bestandstest `UnresolvableSettingsDefaultCli_KeepsWaitingState` ist benannt; `NoDefault_ActivatesTaskBeforeWaitingForSelection` bleibt unverändert gültig (keine Konfiguration → alte Meldung).
