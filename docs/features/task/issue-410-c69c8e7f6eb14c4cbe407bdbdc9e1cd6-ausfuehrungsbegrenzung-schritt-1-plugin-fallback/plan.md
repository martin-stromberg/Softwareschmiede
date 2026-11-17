# Umsetzungsplan: Plugin-Fallback für SCM-Ereignisausführungen (Schritt 1, #411)

**Basisbranch:** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`

## Übersicht

Die Plugin-Auflösung im SCM-Dispatch-Pfad (`ScmEventOrchestrator.DispatchCoreAsync`) wird aktivierungsbewusst: `PluginSelectionService` erhält eine neue, strikt auf ausführbare (installierte, ladbare **und** aktivierte) KI-Plugins filternde Auflösungskette „Aufgaben-Prefix → Projekt-Default → Global-Default (`plugins.default.*`) → `ki.plugin.default`". Der Orchestrator nutzt diese Kette nur für den impliziten (nicht-expliziten) Prefix; findet sie kein ausführbares Plugin, wird das Ereignis mit ehrlichem Wartegrund und Protokolleintrag wartend gehalten statt mit `NextAttemptUtc = long.MaxValue` gesperrt. Die explizite Benutzerwahl bleibt unverändert streng.

## Scope-Klärung

|| Feld | Festlegung |
||------|------------|
|| Verarbeitungseinheit | Ein einzelnes `ScmIncomingEvent` im Dispatch-Pfad unmittelbar vor dem CLI-/Sitzungsstart (nur wenn keine lebende `ScmPromptSessionAdapter`-Sitzung besteht) |
|| Auslösende Ereignisse | Jeder `DispatchCoreAsync`-Durchlauf ohne expliziten Prefix: Monitoring-Poll (`ScmEventMonitoringService`), `RetryAsync`, `CheckStatusCommand` (`MutateAndDispatchAsync`), automatischer Wiederversuch nach `NextAttemptUtc` |
|| Erwartete Wirkung | Fallback-Fall: Ausführung startet mit dem globalen Standard-Plugin (sichtbar: `Status` → `Zugestellt`, `Aufgabe.KiPluginPrefix` = Fallback-Prefix). Verweigerungs-Fall: `Status = "Wartend"` mit verständlichem `Reason`, Protokolleintrag (`ProtokollTyp.SystemMeldung`), **kein** `NextAttemptUtc = long.MaxValue`, kein willkürliches Ersatz-Plugin; Wiederaufnahme automatisch über den Poll oder manuell via `RetryAsync`/`CheckStatusCommand` |
|| Umgang mit fehlenden Objekten | Gespeicherte Zuordnung zeigt auf nicht ausführbares Plugin → Zuordnung für diesen Start **ignorieren** (gespeicherter Wert bleibt unverändert), nächste Stufe der Kette prüfen. Kein ausführbares Plugin und nichts konfiguriert → bestehende Wartemeldung „Keine Standard-CLI konfiguriert". Konfiguriert, aber nicht ausführbar → neue ehrliche Wartemeldung + Protokolleintrag |

### Scope-Ausschlüsse

|| # | Einschränkung / Ausschluss | Begründung | Status |
||---|----------------------------|------------|--------|
|| 1 | Kein Fallback bei fehlgeschlagenem `CheckHealthAsync` — ein aktiviertes, installiertes, aber „nicht startbereites" Plugin bleibt im bisherigen Fehlerpfad (`InvalidOperationException` → `NextAttemptUtc = long.MaxValue`). | „Nicht verfügbar" im Sinne der Anforderung = deaktiviert/nicht installiert/nicht ladbar (Konfigurationszustand). Ein transienter Health-Check-Fehler soll kein stilles Plugin-Umschalten auslösen; der Pfad ist bewusst „deliberate retry after correction". | bestätigt (Annahme, dokumentiert 2026-11-17 — automatisierte Umgebung, keine Rückfrage möglich) |
|| 2 | Die aktivierungsblinde Methode `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` bleibt für die UI-Aufrufer (`TaskDetailViewModel` Z. 966/1393/1495/1599) unverändert. | Manuelle UI-Starts besitzen eigene Dialog-/Fehlerpfade; die Anforderung betrifft ausschließlich SCM-ereignisgesteuerte Ausführungen. | bestätigt (Annahme, dokumentiert 2026-11-17) |
|| 3 | Keine ereignisgesteuerte Wiederaufnahme bei Konfigurationsänderung — die Wiederaufnahme erfolgt über den bestehenden Poll (Wartend ohne `NextAttemptUtc` wird jeden Zyklus erneut dispatcht) oder manuelle Retry-Aktionen. | Entspricht dem heutigen Verhalten aller Wartegründe; kein neuer Mechanismus gefordert. | bestätigt (Annahme, dokumentiert 2026-11-17) |
|| 4 | Keine UI-Änderung — die Ereignisübersicht zeigt `Status`/`Reason` bereits an. | Sichtbarkeit des Wartegrunds ist durch den Bestand abgedeckt. | bestätigt (Annahme, dokumentiert 2026-11-17) |

## Designentscheidungen

|| Komponente / Bereich | Gewählter Ansatz | Begründung |
||----------------------|-----------------|------------|
|| Auflösungskette | **Neue Methode** `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` in `PluginSelectionService`, die die bestehende Kette gegen `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync` prüft; Ergebnis als Record `KiPluginResolution(string? PluginPrefix, IReadOnlyList<string> IgnoredPluginPrefixes)` | Die bestehende Methode wird von 4 UI-Stellen verwendet, deren Dialogverhalten nicht geändert werden soll; ein Ergebnis-Record statt `string?` erlaubt dem Orchestrator eine ehrliche Wartemeldung, die die ignorierten Zuordnungen benennt |
|| Verweigerte Ausführung | `WaitWithCommentAsync` **ohne** `NextAttemptUtc` + einmaliger `Protokolleintrag` (`SystemMeldung`), nur wenn sich der `Reason` ändert | Wartend ohne `NextAttemptUtc` folgt dem Bestandsmuster (z. B. „Keine Standard-CLI") und macht die Ausführung nach Konfigurationskorrektur automatisch wieder startbar; die Reason-Änderungsprüfung verhindert Protokoll-Spam bei jedem 60-s-Poll; die „waiting"-Outbox-Aktion ist per deterministischer ID von sich aus idempotent (`ScmEventOutbox.EnsureAsync`) |
|| „Nicht ausführbar" | Nicht-Element der `GetEnabledDevelopmentAutomationPluginsAsync`-Liste | Deckt deaktiviert (`plugins.enabled.*`), nicht installiert und nicht ladbar (nicht von `IPluginManager` geladen) in einer Prüfung ab |

## Programmabläufe

### Implizite Auflösung mit Fallback (SCM-Dispatch)

1. `DispatchCoreAsync` erreicht den CLI-Start-Block (`adapter is null || !adapter.IsAlive`, Z. 162).
2. `explicitPluginPrefix` gesetzt → bisheriger Pfad unverändert (`ResolveDevelopmentAutomationPluginStrictAsync`; `InvalidOperationException` → `long.MaxValue`-Sperre).
3. Ohne expliziten Prefix: `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(task.KiPluginPrefix, task.ProjektId, ct)`:
   - Lädt einmalig die aktivierten KI-Plugins.
   - Stufe 1: `task.KiPluginPrefix` — aktiviert → Treffer; konfiguriert, aber nicht aktiviert → in `IgnoredPluginPrefixes` aufnehmen und weiter.
   - Stufe 2: `plugins.default.project.<projektId>.DevelopmentAutomation` — analog.
   - Stufe 3: `plugins.default.DevelopmentAutomation` (global) — analog.
   - Stufe 4: `ki.plugin.default`-Einstellung — Match auf `PluginName` **oder** `PluginPrefix` gegen die **aktivierte** Liste — analog.
   - Jede ignorierte Zuordnung wird per `ILogger` protokolliert.
4. `PluginPrefix != null` → bisheriger Startpfad (`ResolveDevelopmentAutomationPluginStrictAsync` zur abschließenden Prüfung, `CheckHealthAsync`, `strictPluginSelection: true` beim Start). Die interne Strikt-Auflösung in `EntwicklungsprozessService` besteht, da der Prefix aktiviert ist; der tatsächlich verwendete Prefix wird wie bisher per `UpdateAsync` in `Aufgabe.KiPluginPrefix` persistiert.
5. `PluginPrefix == null` und `IgnoredPluginPrefixes` leer → bisherige Meldung „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." via `WaitWithCommentAsync`.
6. `PluginPrefix == null` und `IgnoredPluginPrefixes` nicht leer → neue Meldung (sinngemäß: „Die gespeicherte Standard-CLI-Zuordnung (‚X'[, ‚Y']) ist nicht mehr installiert oder aktiviert, und es ist kein ausführbares globales Standard-Plugin konfiguriert. Plugin in den Einstellungen aktivieren oder Ereignis öffnen und CLI auswählen.") — Protokolleintrag `SystemMeldung` nur bei geändertem `Reason`, dann `WaitWithCommentAsync`. `NextAttemptUtc` bleibt ungesetzt → Ereignis wird beim nächsten Poll erneut versucht und startet nach Konfigurationskorrektur automatisch.

Beteiligte Klassen/Komponenten: `ScmEventOrchestrator`, `PluginSelectionService`, `PluginActivationService`, `PluginDefaultSettingsService`, `AppEinstellungService`, `ProtokollService`, `ScmEventOutbox`.

## Neue Klassen

|| Klasse | Typ | Zweck |
||--------|-----|-------|
|| `KiPluginResolution` | Record (in `PluginSelectionService.cs`, analog `ScmActionIdCollisionException` in `ScmEventOutbox.cs`) | Ergebnis der aktivierungsbewussten Auflösung: `PluginPrefix` (aufgelöst oder `null`) und `IgnoredPluginPrefixes` (konfigurierte, nicht ausführbare Zuordnungen) |

## Änderungen an bestehenden Klassen

### `PluginSelectionService` (Service)

- **Neue Methoden:** `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync(string? aufgabenPluginPrefix, Guid projektId, CancellationToken ct = default)` → `Task<KiPluginResolution>` — aktivierungsbewusste Kette gemäß „Programmabläufe"; jede konfigurierte, nicht ausführbare Stufe landet dedupliziert (ordinal-ignore-case) in `IgnoredPluginPrefixes` und wird per `ILogger.LogInformation`/`LogWarning` protokolliert.
- **Geänderte Methoden:** keine — `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` und `ResolveDevelopmentAutomationPluginStrictAsync` bleiben unverändert.

### `ScmEventOrchestrator` (Service)

- **Geänderte Methoden:** `DispatchCoreAsync` — im Start-Block (Z. 162-176): ohne `explicitPluginPrefix` neue aktivierungsbewusste Auflösung; bei `PluginPrefix == null` Unterscheidung „nichts konfiguriert" (bisherige Meldung) vs. „konfiguriert, nicht ausführbar" (neue Meldung + Protokolleintrag `ProtokollTyp.SystemMeldung` bei Reason-Änderung); kein `NextAttemptUtc = long.MaxValue` für diesen Fall.
- **Neue Methoden:** keine (Hilfslogik inline/klein; ggf. private Hilfsmethode für die Wartemeldung, wenn es die Lesbarkeit erfordert).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — keine neuen Eingaben; die Auflösung filtert ausschließlich bestehende Konfigurationswerte.

## Konfigurationsänderungen

Keine — bestehende Schlüssel `plugins.enabled.*`, `plugins.default.*`, `plugins.default.project.*`, `ki.plugin.default` werden genutzt.

## Seiteneffekte und Risiken

- **`Aufgabe.KiPluginPrefix`-Rückschreibung:** Nach erfolgreichem Fallback-Start persistiert `EntwicklungsprozessService` den Fallback-Prefix als neue Aufgaben-Zuordnung (bisheriges Verhalten des Startpfads). Folgewirkung: Die deaktivierte Zuordnung wird bei Erfolg „geheilt" — fachlich konsistent (die Aufgabe läuft ja mit diesem Plugin), aber bewusst zu vermerken.
- **Wiederholte Dispatches:** Wartende Ereignisse ohne `NextAttemptUtc` werden bei jedem Poll-Zyklus erneut dispatcht (Bestandsverhalten) — inkl. Repository-Revalidierung; keine neue Last gegenüber dem heutigen „Keine Standard-CLI"-Fall. Protokolleinträge werden durch die Reason-Änderungsprüfung auf Änderungen begrenzt.
- **`EntwicklungsprozessService` strikte Wiederauflösung:** Der Fallback-Prefix ist garantiert aktiviert; die interne `ResolveDevelopmentAutomationPluginStrictAsync`-Prüfung (Z. 163/286) schlägt daher nicht fehl. Kein Code-Eingriff nötig.
- **`TestDbContextFactory`/`ScmDispatchFixture`:** Die Fixture registriert nur ein KI-Plugin; für Fallback-Tests mit deaktiviertem Zweitplugin ist eine Erweiterung nötig (s. Tests).

## Umsetzungsreihenfolge

1. **`KiPluginResolution`-Record + `ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` in `PluginSelectionService`**
   - Voraussetzungen: keine (alle Abhängigkeiten bereits injiziert)
   - Beschreibung: Record am Dateiende (Muster `ScmActionIdCollisionException`); Methode implementiert die aktivierungsbewusste Kette mit Ignoriert-Liste und Logging.
2. **`ScmEventOrchestrator.DispatchCoreAsync` auf die neue Auflösung umstellen**
   - Voraussetzungen: Schritt 1
   - Beschreibung: Verzweigung explizit/implizit; neue Wartemeldung für „konfiguriert, nicht ausführbar"; Protokolleintrag `SystemMeldung` bei Reason-Änderung; `long.MaxValue`-Pfad bleibt für sonstige `InvalidOperationException`/`IOException`/`UnauthorizedAccessException` unverändert.
3. **`ScmDispatchFixture` um optionales zweites KI-Plugin erweitern**
   - Voraussetzungen: keine
   - Beschreibung: Optionaler Ctor-Parameter (z. B. `ScmKiProfile? ki2`), der ein zweites `Mock<IKiPlugin>` mit eigenem Prefix/Startspec/Health in `GetDevelopmentAutomationPlugins()` einreiht; Zugriff auf den Mock (z. B. `Ki2`) und das `Mock<IPluginManager>` bzw. eine Registrierungsmöglichkeit bereitstellen.
4. **Unit-Tests `PluginSelectionServiceTests` für die neue Auflösungskette**
   - Voraussetzungen: Schritt 1
   - Beschreibung: alle Fallback-Zweige — Aufgabe deaktiviert→Projekt, Projekt deaktiviert→Global, Global deaktiviert→`ki.plugin.default`, nicht installiert, nichts konfiguriert, explizit-nichts-ignoriert-Liste.
5. **Dispatch-Tests `ScmEventOrchestratorTests`**
   - Voraussetzungen: Schritte 2, 3
   - Beschreibung: Fallback-Start (deaktiviertes/nicht installiertes Aufgaben-Plugin → globaler Default liefert Zustellung), verweigerter Start mit Wartegrund + Protokolleintrag + Wiederaufnahme nach Aktivierung, expliziter Prefix bleibt streng (`long.MaxValue`), Anpassung `UnresolvableSettingsDefaultCli_KeepsWaitingState` auf die neue Meldung.

## Tests

### Neue Tests

|| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
||--------------------|------------|-------------------------------------|
|| `ResolveEnabled..._TaskPrefixEnabled_Wins` | `PluginSelectionServiceTests` | Aktiviertes Aufgaben-Prefix schlägt Projekt-/Global-Default |
|| `ResolveEnabled..._TaskPrefixDisabled_FallsBackToProjectDefault` | `PluginSelectionServiceTests` | Deaktiviertes Aufgaben-Prefix → Projekt-Default; Ignoriert-Liste enthält Aufgaben-Prefix |
|| `ResolveEnabled..._TaskPrefixNotInstalled_FallsBack` | `PluginSelectionServiceTests` | Nicht installiertes Aufgaben-Prefix → nächste Stufe |
|| `ResolveEnabled..._ProjectDefaultDisabled_FallsBackToGlobalDefault` | `PluginSelectionServiceTests` | Deaktivierter Projekt-Default → Global-Default |
|| `ResolveEnabled..._GlobalDefaultDisabled_FallsBackToSettingsDefault` | `PluginSelectionServiceTests` | Deaktivierter Global-Default → `ki.plugin.default` (Match per PluginName und PluginPrefix) |
|| `ResolveEnabled..._AllConfiguredDisabled_ReturnsNullWithIgnored` | `PluginSelectionServiceTests` | Alle Stufen konfiguriert, aber deaktiviert → `PluginPrefix == null`, `IgnoredPluginPrefixes` vollständig/dedupliziert |
|| `ResolveEnabled..._NothingConfigured_ReturnsNullEmptyIgnored` | `PluginSelectionServiceTests` | Nichts konfiguriert → `null` + leere Ignoriert-Liste |
|| `ResolveEnabled..._SettingsDefaultDisabled_ReturnsNull` | `PluginSelectionServiceTests` | `ki.plugin.default` zeigt auf deaktiviertes Plugin → `null`, Einstellungswert in Ignoriert-Liste |
|| `DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers` | `ScmEventOrchestratorTests` | Aufgabe mit deaktiviertem Plugin + aktiviertem Global-Default → `Zugestellt`, `Aufgabe.KiPluginPrefix` = Fallback |
|| `UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix` | `ScmEventOrchestratorTests` | Alles deaktiviert → `Wartend` + verständlicher `Reason` + `NextAttemptUtc == null` + `SystemMeldung`-Protokolleintrag + keine Sitzung; nach Reaktivierung `RetryAsync` → `Zugestellt` |
|| `ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable` | `ScmEventOrchestratorTests` | `DispatchAsync(eventId, deaktivierterPrefix)` → `InvalidOperationException`-Pfad, `NextAttemptUtc = long.MaxValue`, kein Fallback |
|| `ScmDispatchFixture`-Erweiterung (zweites KI-Plugin `Ki2`/`ScmKiProfile`-Parameter) | `ScmDispatchFixture` | Bereitstellung zweier KI-Plugins für Aktiviert/Deaktiviert-Kombinationen |

### Betroffene bestehende Tests

|| Test / Testklasse | Grund der Anpassung |
||-------------------|---------------------|
|| `UnresolvableSettingsDefaultCli_KeepsWaitingState` (`ScmEventOrchestratorTests`) | Konfigurierter, nicht auflösbarer `ki.plugin.default` erzeugt nun die neue „konfiguriert, nicht ausführbar"-Meldung statt „Keine Standard-CLI konfiguriert"; Erwartung an neue Meldung anpassen |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich: Die Änderung betrifft ausschließlich interne Auflösungs-/Fehlerlogik des SCM-Dispatchs — kein neuer oder geänderter Benutzerfluss, keine neue Benutzerinteraktion. Der Wartegrund erscheint über den bestehenden `Reason`-Mechanismus in der Ereignisübersicht, dessen Anzeige unverändert ist. Die geforderten Funktionsnachweise liegen auf Dispatch-Ebene (`ScmDispatchFixture`: echte Services, SQLite, deterministische Plugin-/Terminal-Grenze) sowie Service-Unit-Tests; FlaUI würde keine zusätzliche Abdeckung eines neuen Benutzerflusses liefern. Bestehende E2E-Tests (`E2E_ScmEvents` u. a.) sind nicht betroffen, da sie keine deaktivierte Plugin-Zuordnung konfigurieren.

## Akzeptanzszenarien

|| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
||---|----------|----------|------------------|----------|-------------------|----------|
|| 1 | Happy Path Fallback (deaktiviert) | Dispatch eines SCM-Ereignisses (Poll/`RetryAsync`) | Aufgabe mit `KiPluginPrefix` = deaktiviertes Plugin; Global-Default `plugins.default.DevelopmentAutomation` = aktiviertes Plugin | Dispatch → Aufgaben-Zuordnung wird ignoriert → Global-Default aufgelöst → CLI-Start `strictPluginSelection` | Ereignis `Zugestellt`, Prompt geschrieben, `Aufgabe.KiPluginPrefix` = Global-Default | `DisabledTaskPlugin_FallsBackToGlobalDefault_Delivers` |
|| 2 | Happy Path Fallback (nicht installiert) | Dispatch | `Aufgabe.KiPluginPrefix` zeigt auf nicht vorhandenes Plugin; Global-Default aktiviert | wie 1 | wie 1 | `ScmDispatchFixture`-Test / Unit-Kette (`NotInstalled`) |
|| 3 | Verweigerter Start | Dispatch | Aufgaben-/Projekt-/Global-Zuordnung zeigen auf deaktivierte/fehlende Plugins; kein ausführbares Plugin konfiguriert | Dispatch → alle Stufen ignoriert → `PluginPrefix == null` mit Ignoriert-Liste | `Status = "Wartend"`, `Reason` benennt die ignorierte(n) Zuordnung(en) und den Konfigurationshinweis, `NextAttemptUtc == null`, `SystemMeldung`-Protokolleintrag, keine CLI-Sitzung | `UnavailablePlugins_EventWaitsWithoutHardLock_ResumesAfterFix` (Teil 1) |
|| 4 | Wiederaufnahme nach Konfigurationskorrektur | `RetryAsync`/Poll nach Aktivierung | Zustand aus Szenario 3; Plugin wurde zwischenzeitlich aktiviert | Aktivierung → `RetryAsync` | Ereignis wird zugestellt, kein manuelles Entsperren nötig | `UnavailablePlugins_..._ResumesAfterFix` (Teil 2) |
|| 5 | Strenge bei expliziter Auswahl | `DispatchAsync(eventId, explicitPrefix)` mit deaktiviertem Prefix (UI: `OpenFromQueueAsync`) | Deaktiviertes Plugin wird explizit gewählt | Dispatch → strikte Auflösung wirft | `Wartend` + Fehlermeldung, `NextAttemptUtc = long.MaxValue` (bisheriges Verhalten unverändert), kein stiller Fallback | `ExplicitPluginPrefix_RemainsStrict_LocksOnUnavailable` |
|| 6 | Nichts konfiguriert (Regression) | Dispatch | Keine Zuordnung an keiner Stufe | Dispatch | Bisherige Meldung „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." | `NoDefault_ActivatesTaskBeforeWaitingForSelection` (bestehend) |

## Offene Punkte

Keine.
