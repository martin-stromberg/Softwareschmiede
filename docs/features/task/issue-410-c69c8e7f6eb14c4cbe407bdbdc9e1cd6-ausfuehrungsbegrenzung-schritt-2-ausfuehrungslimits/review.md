# Umsetzungs-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Gegenstand: Review der Umsetzung gegen [requirement.md](requirement.md) und
[plan.md](plan.md). Prüfbasis: Quellcode-Inspektion des Branch-Diffs sowie die
dokumentierten Läufe in [test-results.md](test-results.md).

## Umgesetzte Planelemente

- [x] **Globales Limit** — `ScmEventExecutionLimitService.GlobalLimitKey`
  (`scm.execution.maxparallel`), gelesen via `AppEinstellungService`; leer/unset =
  unbegrenzt, gültig sind Ganzzahlen ≥ 1 (`ParseLimit`,
  `ScmEventExecutionLimitService.cs:82`).
- [x] **Limit je KI-Plugin** — `PluginLimitKey(prefix)`
  (`plugins.maxparallel.<prefix>`); denselben Wertbereich wie das globale Limit.
- [x] **Zählung laufender Ausführungen** — `ICliExecutionLimitSource` auf dem
  Singleton `KiAusfuehrungsService`: `GetLaufendeAufgabenIds()` liefert Aufgaben
  mit nachweislich laufendem Prozess-Handle (`!HasExited`, exception-tolerant).
  Attribution über `Aufgabe.KiPluginPrefix`; bei lebender SCM-Sitzung gewinnt das
  tatsächlich gestartete Plugin (`IScmPromptSessionAdapter.PluginPrefix`,
  `ScmEventExecutionLimitService.cs:97-100`).
- [x] **Restriktivstes Limit gewinnt** — `GetStartBlockReason` prüft global vor
  Plugin-Limit; beide Grenzen gelten gleichzeitig
  (`ScmEventExecutionLimitService.cs:72-80`).
- [x] **Startprüfung unmittelbar vor dem CLI-Start** — im gemeinsamen
  Dispatch-Pfad `ScmEventOrchestrator.cs:184-193`: Bei erreichtem Limit wird eine
  Systemmeldung protokolliert (nur bei Grundwechsel), das Ereignis bleibt mit
  ehrlichem Wartegrund über `WaitAsync` queued; kein Start, kein Abbruch laufender
  Ausführungen. Da alle Startpfade (`CheckStatusCommand`, `OpenFromQueueAsync`,
  `RetryAsync`, Polling) über `DispatchAsync` laufen, gilt die Kontrolle überall.
- [x] **Reconciliation/Host-only unbegrenzt** — die Limitprüfung steht hinter dem
  Reconcile-Attributionspfad und betrifft nur echte CLI-Neustarts; Delivery-/
  Outbox-/Reconcile-Aktionen bleiben unbegrenzt (Integrationstests
  `Dispatch_ReconcileAttempt_IgnoresLimit`, `Dispatch_HostActions_UnlimitedWhileSlotOccupied`).
- [x] **Priorisierte Auswahl** — `ScmDispatchPrioritaet` mit Klassen
  1 = Fortsetzung (pausiert oder Lauf beendet bei aktiver Aufgabe),
  2 = `Verzögert` aufsteigend nach `NextAttemptUtc`, 3 = übrige nach `Sequence`;
  `Beendet`/`Archiviert` sind von Klasse 1 ausgeschlossen und bleiben über den
  bestehenden `BlockReason`-Mechanismus dauerhaft gesperrt.
  Angewendet je Polling-Batch in `ScmEventMonitoringService.cs:240-253`
  (Batch weiterhin per `Sequence` begrenzt auf 100, danach innerhalb des
  Fensters priorisiert).
- [x] **Persistenz/UI** — globales Feld `MaxParalleleAusfuehrungen` im Expander
  „Aufgaben ausführen“; hostseitiges `ParallelLimit`-Feld in der Gruppe
  „Ausführungsbegrenzung" jedes KI-Plugin-Panels (`HostParallelLimitField`,
  `SettingsViewModel.cs:27-38`, Injektion in `LadePluginEinstellungen`,
  `SettingsViewModel.cs:894-898`). Plugin-Werte werden in `AppEinstellung`
  gespeichert (nicht im Credential-Store), SCM-Plugins erhalten die Gruppe nicht.
- [x] **Validierung** — global über `ValidiereMaxParalleleAusfuehrungen`
  (`SettingsViewModel.cs:988-1000`), pluginbezogen über
  `ValidierePflichtfelderFuerSettings` mit `HostParallelLimitField`-Sonderfall
  (Integer ≥ 1 oder leer), jeweils mit deutschsprachiger Fehlermeldung.
- [x] **Tests** — 12 Limit-Service-Unit-Tests, 10 Priorisierungs-Unit-Tests,
  11 SettingsViewModel-Tests, 10 Integrations-Testfälle (Theorien expandiert),
  1 konsolidiertes FlaUI-E2E-Szenario in `General_BasisUndEinstellungen_E2E`.

## Geprüfte Risiken / Befunde

- **Batch-Fenster der Priorisierung:** Die Reihenfolge gilt innerhalb des
  100er-Selektionsfensters; fällige Fortsetzungen jenseits des Fensters warten
  bis zum Folgebatch. Entspricht dem Plan (fensterbasierte Ordnung statt globaler
  Re-Sortierung) und ist mit `Take(100)` abwärtskompatibel.
- **Parallele Starts außerhalb des Pollings:** UI-getriggerte Starts laufen über
  denselben `DispatchAsync`-Pfad und dieselbe Zulassungskontrolle; die ehrliche
  Wartegrund-Anzeige greift auch dort.
- **Race beim Slot-Nachfüllen:** Die Prüfung zählt live über Prozess-Handles;
  ein zwischen Zählung und Start endender Prozess kann den Start höchstens um
  einen Polling-Takt verzögern — bewusst konservativ, nie zu aggressiv.
- **Magic String `„Verzögert"`:** Etablierte Konvention (gleicher Literal in
  `LaufendeAutomatisierungenZaehlung`, `ScmEventOrchestrator`, `ScmEventStore`);
  keine neue Divergenz eingeführt.
- **Belegungs-Dictionary** ist case-insensitive (`OrdinalIgnoreCase`), sodass
  Prefix-Varianten in `Aufgabe.KiPluginPrefix`/`PluginPrefix` zusammengezählt
  werden (`FuerPlugin_IgnoriertGrossKleinschreibung`).

## Hinweise

- Kein Befund mit Korrekturbedarf; verbleibende Punkte sind dokumentierte
  Entwurfsentscheidungen (Fenster-Priorisierung, konservative Zählung).
