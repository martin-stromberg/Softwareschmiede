# Übersetzte Anforderung: Ausführungsbegrenzung mit Priorisierung (Entwicklungsschritt 2, Issue #410 Teil 1)

**Quelle:** Projektplan `docs/projects/task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung/project-plan.md`, „Schritt 2" + Vorgehensentscheidung 2; GitHub-Issue #410.
**Basisbranch (Diff-Basis):** `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`

## Fachliche Zusammenfassung

Die heute unbegrenzte Sofort-Ausführung fälliger SCM-Ereignisse wird um eine konfigurierbare Parallelitätsbegrenzung ergänzt: ein globales Limit gleichzeitig laufender CLI-Ausführungen und je KI-Plugin ein eigenes Limit (ganzzahlig ≥ 1, leer = unbegrenzt). Ist ein Limit erreicht, bleibt ein fälliges Ereignis mit ehrlichem Wartegrund eingereiht statt zu starten; bei freiem Slot wird das nächste Ereignis nach definierter Priorität (Fortsetzungsereignisse → fällige Verzögerungs-Ereignisse zeitaufsteigend → Eingangsreihenfolge) gewählt. Die Freigabeprüfung sitzt unmittelbar vor dem CLI-Start im `ScmEventOrchestrator`, sodass auch UI-ausgelöste Starts dieselbe Prüfung durchlaufen; die Dispatch-Auswahl des `ScmEventMonitoringService` wird von reiner `Sequence`-Reihenfolge auf priorisierte, slotbegrenzte Auswahl umgestellt.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein einzelnes `ScmIncomingEvent`, das zur CLI-Ausführung einer Aufgabe dispatcht werden soll — konkret (a) die Auswahl fälliger Ereignisse im Poll-Zyklus und (b) die Startfreigabe unmittelbar vor dem CLI-Start.
- **Auslösende Ereignisse:**
  - Ereigniseingang/Fälligkeit über den Poll-Zyklus des `ScmEventMonitoringService` (`RunOnceAsync`).
  - UI-ausgelöste Starts über denselben Dispatch-Pfad (`CheckStatusCommand`, `OpenFromQueueAsync`, `RetryAsync`).
  - (Explizit NICHT Teil dieses Schritts: der ereignisgesteuerte Trigger bei Beendet/Pausiert — das ist Schritt 3; hier genügt der Poll als Auslöser.)
- **Erwartete Wirkung:**
  - In den Einstellungen setzbar/persistiert: globales Limit im Automatisierungs-Abschnitt, Plugin-Limit auf der jeweiligen Plugin-Konfigurationsfläche (`AppEinstellung`-Key-Value-Muster); leer/ungesetzt = unbegrenzt.
  - Bei erreichtem globalem **oder** pluginbezogenem Limit startet keine neue Ausführung; das Ereignis bleibt im Status „Wartend" mit verständlichem Wartegrund (sichtbar in der Ereignisübersicht) und startet später bei freiem Slot — nichts wird verworfen, laufende Ausführungen werden nie unterbrochen.
  - Rein zustellungs-/abschlussbezogene Hostaktionen (Empfangsreaktionen, Outbox-Aktionen, Reconcile begonnener Versuche) laufen unbegrenzt weiter — sie stellen keine neue CLI-Ausführung dar.
  - Bei freiem Slot exakte Priorität: (1) Fortsetzungsereignisse für Aufgaben mit angehaltenem/beendetem Lauf bei aktivem `Aufgabe.Status` (`PausiertBisUtc` gesetzt **oder** `AusfuehrungsStatus == Beendet` bei `Status` `Neu`/`Gestartet`/`Wartend`), (2) fällige Verzögerungs-Ereignisse aufsteigend nach `NextAttemptUtc`, (3) sonstige Ereignisse in `Sequence`-Reihenfolge.
  - Ereignis an Aufgabe mit `AufgabeStatus.Beendet`/`Archiviert` bleibt permanent via `BlockReason` gesperrt — kein Reaktivierungsmechanismus.
  - Pro-Plugin-Zählung attributiert korrekt: über `Aufgabe.KiPluginPrefix` der laufenden Handles; bei lebender SCM-Sitzung das tatsächlich gestartete Plugin via `ScmPromptSessionAdapter.PluginPrefix`.
- **Umgang mit fehlenden Objekten:**
  - Ungesetztes/leeres Limit → **unbegrenzt** behandeln (kein Objekt erzeugen, kein Fehler).
  - Ungültiger persistierter Wert (< 1, nicht numerisch) → wie unbegrenzt behandeln (robuste Lesefunktion), keine Fehlersperre.
  - Laufendes Handle ohne auflösbare Plugin-Attribution (Aufgabe ohne `KiPluginPrefix`, keine lebende SCM-Sitzung) → zählt nur ins globale Limit, nicht in ein Plugin-Limit (Annahme, dokumentiert).
- **Scope-Ausschlüsse:**
  - Kein Unterbrechen, Drosseln oder Neuanordnen laufender CLI-Ausführungen — die Begrenzung wirkt ausschließlich auf neue Starts. *(Status: bestätigt — verbindliche Annahme des Projektplans, Vorschlag 4/Nicht-Ziel.)*
  - Keine Begrenzung zustellungsbezogener Hostaktionen (Empfangsreaktionen, Outbox, Reconcile). *(Status: bestätigt — Nicht-Ziel des Projektplans.)*
  - Keine ereignisgesteuerte sofortige Startprüfung bei Laufende/Pausierung — folgt in Schritt 3; hier bleibt der Poll-Zyklus der Auslöser. *(Status: bestätigt — explizite Schrittabgrenzung des Projektplans.)*
  - Kein Reaktivierungsmechanismus für Aufgaben mit `AufgabeStatus.Beendet`/`Archiviert`. *(Status: bestätigt — Projektplan.)*
  - Keine Änderung der expliziten Benutzer-Pluginwahl und der Schritt-1-Fallback-Semantik. *(Status: bestätigt.)*

## Betroffene Klassen und Komponenten

- `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`): Dispatch-Auswahl in `RunOnceAsync` (~Z. 229-248) — heute alle fälligen Ereignisse nach `Sequence`; Umstellung auf priorisierte, slotbegrenzte Auswahl.
- `ScmEventOrchestrator` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`): Startfreigabe unmittelbar vor CLI-Start; Wartegrund bei Limit-Sättigung (`WaitAsync`-Muster aus Schritt 1 wiederverwenden).
- `KiAusfuehrungsService` (`src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`): `_handles`, `GetRunningCount`, `GetLaufendeAufgabenIds`, `RunningCountChanged` — Zählquelle; `CliProcessHandle` trägt kein `PluginPrefix`.
- `ScmPromptSessionAdapter` (`PluginPrefix`): Attribution des tatsächlich gestarteten Plugins bei lebender SCM-Sitzung.
- `Aufgabe` (Domain-Entity): `KiPluginPrefix`, `PausiertBisUtc`, `AusfuehrungsStatus`, `Status` — für Attribution und Prioritätsklasse 1.
- `AppEinstellungService`/`AppEinstellung`: neue Schlüssel (Hinweis Planprüfung: Muster `plugins.sessionlimit.<prefix>` näherliegend als plugindeklarierte Felder — im Code zu prüfen; globaler Schlüssel analog, z. B. `automatisierung.maxparallelausfuehrungen` oder konsistentes Bestandsmuster).
- `SettingsViewModel`/`SettingsView.xaml`: globaler Wert im Automatisierungs-Abschnitt; Plugin-Limits auf der jeweiligen Plugin-Konfigurationsfläche.
- `ScmEventQueueViewModel` / Ereignisübersicht: Wartegrund-Anzeige (vorhandener Mechanismus, ggf. keine Änderung nötig).
- Tests: `src/Softwareschmiede.Tests` — `ScmDispatchFixture`-Dispatch-Tests, `SettingsViewModel`-Tests, FlaUI-E2E (konsolidiert).

## Implementierungsansatz

- **Zählung:** Zentrale Wahrheit „CLI läuft" = `KiAusfuehrungsService` (nur Handles mit `!Process.HasExited`). Pro-Plugin-Attribution über `Aufgabe.KiPluginPrefix` des Handles; Überschreibung durch `ScmPromptSessionAdapter.PluginPrefix`, wenn eine lebende SCM-Sitzung das tatsächlich gestartete Plugin kennt (Fallback-Fall aus Schritt 1).
- **Startfreigabe:** Unmittelbar vor dem CLI-Start im `ScmEventOrchestrator.DispatchCoreAsync`: globales Limit und Plugin-Limit des aufgelösten Plugins prüfen; bei Sättigung bestehender Wartezustand mit ehrlichem Wartegrund + Protokolleintrag (kein `NextAttemptUtc`-Hardlock, kein Verwerfen).
- **Priorisierte Auswahl:** Im Monitoring-Zyklus werden nur so viele startbare Ereignisse gewählt, wie Slots frei sind; Ordnung: Klasse 1 (Fortsetzung: `PausiertBisUtc` gesetzt oder `AusfuehrungsStatus.Beendet` bei aktivem `Status`) → Klasse 2 (`NextAttemptUtc` fällig, zeitaufsteigend) → Klasse 3 (`Sequence`). Bestehende Sperren (`BlockReason`, `NextAttemptUtc` in Zukunft, ältere offene Ereignisse derselben Aufgabe) bleiben Voraussetzung für „startbar" — die Priorität ordnet nur unter den Startbaren.
- **Einstellungen:** Neue `AppEinstellung`-Schlüssel nach Bestandsmuster; `SettingsViewModel`-Eigenschaften mit Validierung (ganzzahlig ≥ 1 oder leer); UI-Bindung in `SettingsView.xaml` (Automatisierungs-Abschnitt) und Plugin-Konfigurationsfläche.

## Konfiguration

- Globaler Grenzwert: Anwendungseinstellung (`AppEinstellung`-Key-Value), Automatisierungs-Abschnitt der Einstellungen.
- Plugin-Grenzwert: Anwendungseinstellung je Plugin-Prefix (`plugins.sessionlimit.<prefix>`-Muster, vorbehaltlich Code-Prüfung), Plugin-Konfigurationsfläche.
- Beide optional; leer = unbegrenzt.

## Offene Fragen

Keine — die verbindlichen Annahmen des Projektplans (Vorschläge 1–6 aus der Projektanforderung) gelten; die Schrittabgrenzung zu Schritt 3 (ereignisgesteuerter Trigger) ist im Projektplan verbindlich festgelegt.
