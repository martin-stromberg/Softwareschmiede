# Übersetzte Anforderung: Plugin-Fallback für SCM-Ereignisausführungen (Entwicklungsschritt 1, Issue #411)

**Quelle:** Projektplan `docs/projects/task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung/project-plan.md`, „Schritt 1" + Vorgehensentscheidung 1; GitHub-Issue #411.
**Basisbranch (Diff-Basis):** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`

## Fachliche Zusammenfassung

Die Plugin-Auflösung für SCM-ereignisgesteuerte Ausführungen wird aktivierungsbewusst: Eine gespeicherte Plugin-Zuordnung (aufgaben- bzw. projektbezogener Standard) gilt nur, wenn das zugeordnete KI-Plugin tatsächlich ausführbar ist. Ist sie es nicht, wird die Zuordnung ignoriert und auf das globale Standard-Plugin zurückgefallen; ist auch dieses nicht ausführbar oder nicht konfiguriert, startet die Ausführung nicht — das Ereignis bleibt mit ehrlichem Wartegrund eingereiht statt hart gesperrt zu werden. Ersetzt wird das heutige Verhalten (`InvalidOperationException` → `NextAttemptUtc = long.MaxValue` in `ScmEventOrchestrator` ~Z. 250-255).

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein einzelnes `ScmIncomingEvent`, das zur CLI-Ausführung einer Aufgabe dispatcht werden soll — konkret der Plugin-Auflösungsschritt unmittelbar vor dem CLI-/Sitzungsstart im `ScmEventOrchestrator`-Dispatch-Pfad.
- **Auslösende Ereignisse:** Der Dispatch eines SCM-Ereignisses, der einen neuen CLI-Start vorbereitet — egal ob über den Poll-Zyklus des `ScmEventMonitoringService` oder über UI-Aktionen (`RetryAsync`, `CheckStatusCommand`, `OpenFromQueueAsync`), die denselben `DispatchAsync`-Pfad nutzen.
- **Erwartete Wirkung:**
  - Deaktiviertes/nicht installiertes/nicht ladbares Aufgaben- oder Projekt-Standard-Plugin → Ausführung startet mit dem globalen Standard-Plugin (sichtbar: gestarteter Lauf verwendet den globalen Prefix).
  - Globales Standard-Plugin ebenfalls nicht ausführbar bzw. nicht konfiguriert → kein Start; das Ereignis verbleibt im Status „Wartend" mit verständlichem Wartegrund in der Ereignisübersicht und einem Protokolleintrag; es wird **kein** willkürliches Ersatz-Plugin gewählt und **keine** nicht-wiederaufnehmbare `NextAttemptUtc = long.MaxValue`-Sperre gesetzt — nach Konfigurationskorrektur ist ein Start möglich.
  - Explizite Plugin-Auswahl des Anwenders in der Ereignisübersicht bleibt unverändert streng (kein stiller Fallback bei expliziter Wahl).
- **Umgang mit fehlenden Objekten:**
  - Gespeicherte Aufgaben-/Projekt-Plugin-Zuordnung zeigt auf nicht ausführbares Plugin → Zuordnung **ignorieren** (nicht erzeugen/ändern), Fallback auf globalen Standard.
  - Globales Standard-Plugin nicht konfiguriert oder nicht ausführbar → Ausführung nicht starten; Ereignis wartend halten (wiederverwendbarer Wartezustand des Bestands, kein Verwerfen).
- **Scope-Ausschlüsse:**
  - Keine UI-Änderungen an der Ereignisübersicht — vorhandener Wartegrund/Status-Mechanismus wird wiederverwendet. *(Annahme, dokumentiert — die Anforderung fordert nur Sichtbarkeit des Wartegrunds, die der Bestand bereits liefert.)*
  - Keine Änderung am Verhalten der expliziten Benutzer-Pluginwahl (bleibt streng, kein Fallback).
  - Kein automatisches erneutes Aktivieren deaktivierter Plugins und keine Korrektur gespeicherter Zuordnungen — die gespeicherte Zuordnung bleibt unverändert und wird nur für diesen Start ignoriert.
  - Keine ereignisgesteuerte Wiederaufnahme nach Konfigurationsänderung — die Wiederaufnahme erfolgt über den bestehenden Retry-/Poll-Mechanismus (Ereignis ist nicht gesperrt).

## Betroffene Klassen und Komponenten

- `PluginSelectionService` (`src/Softwareschmiede/Application/Services/PluginSelectionService.cs`): `ResolveDevelopmentAutomationPluginWithProjectScopeAsync` (~Z. 103-126) prüft Aktivierung nicht → neue/erweiterte aktivierungsbewusste Auflösung für den SCM-Pfad.
- `PluginActivationService` (`PluginActivationService.cs`): `plugins.enabled.<prefix>`-Prüfung (`IsEnabledValue` ~Z. 82-88; fehlender/ungültiger Eintrag = aktiviert).
- `PluginDefaultSettingsService` (`PluginDefaultSettingsService.cs`): `plugins.default.<PluginType>` global, `plugins.default.project.<projektId>.<PluginType>` projektbezogen.
- `PluginManager` (`src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`): `GetDevelopmentAutomationPlugins()` — ungefilterte Liste (installiert/ladbar).
- `ScmEventOrchestrator` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`): Prefix-Auflösung ~Z. 164-167, Fehlerpfad `catch` ~Z. 246-255 (`InvalidOperationException` → `NextAttemptUtc = long.MaxValue`), Wartegrund-Mechanismus (`WaitAsync` → `"Wartend"` ~Z. 535-540).
- `ScmIncomingEvent` (`Domain/Entities/ScmIncomingEvent.cs`): Status/Wartegrund-Felder (Strings, kein Enum).
- Tests: `src/Softwareschmiede.Tests` — neue Unit-Tests für die Auflösungskette sowie Dispatch-Tests über `ScmDispatchFixture`.

## Implementierungsansatz

- Aktivierungsbewusste SCM-Auflösung in `PluginSelectionService`: Kette „Aufgaben-Prefix (gespeicherte Zuordnung) → Projekt-Default → Global-Default → `ki.plugin.default`-Einstellung", wobei jede Stufe nur akzeptiert wird, wenn das Plugin installiert/ladbar **und** aktiviert ist; die explizite Benutzerwahl (`ResolveDevelopmentAutomationPluginStrictAsync`-Semantik) bleibt streng ohne Fallback.
- `ScmEventOrchestrator`: SCM-Pfad nutzt die aktivierungsbewusste Auflösung; liefert sie kein ausführbares Plugin, wird der `long.MaxValue`-Sperrpfad für diesen Fall durch den bestehenden Wartezustand mit ehrlichem Wartegrund + Protokolleintrag ersetzt (kein Verwerfen, keine nicht-wiederaufnehmbare Sperre).
- „Nicht verfügbar" = deaktiviert (`PluginActivationService`), nicht installiert (nicht in `PluginManager`-Liste) oder nicht ladbar — jeder nicht ausführbare Zustand.

## Konfiguration

Keine neue Konfiguration erforderlich — bestehende Schlüssel werden genutzt: `plugins.enabled.*`, `plugins.default.*`, `ki.plugin.default`.

## Offene Fragen

Keine — die verbindlichen Annahmen des Projektplans (Vorschläge 7 und 8 aus der Projektanforderung) gelten: „nicht verfügbar" umfasst jeden nicht ausführbaren Zustand; ein verweigerter Start hinterlässt das Ereignis mit ehrlichem Wartegrund protokolliert in der Warteschlange.
