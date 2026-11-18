# Bestandsaufnahme: Ausführungsbegrenzung mit Priorisierung (Schritt 2, #410 Teil 1)

Ist-Stand der SCM-Ereignis-Dispatch-Schleife, der CLI-Ausführungszählung, der Einstellungs-Infrastruktur und der Test-Infrastruktur, bezogen auf `requirement.md` im selben Verzeichnis. Projektweite Vorgeschichte: `docs/projects/task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung/inventory.md` sowie die Schritt-1-Inventur (Plugin-Fallback, #411, bereits im Basisbranch gemergt).

## Zusammenfassung

- **Dispatch-Auswahl** (`ScmEventMonitoringService.RunOnceAsync`, Z. 229-248): Alle fälligen Ereignisse (`Status != "Verarbeitet"` und `NextAttemptUtc` fällig ODER offene Outbox-Aktion) werden in reinen `Sequence`-Batches à 100 sequenziell an `ScmEventOrchestrator.DispatchAsync` übergeben — keine Priorisierung, keine Begrenzung.
- **Dispatch-Kern** (`ScmEventOrchestrator.DispatchCoreAsync`, Z. 74-281): Ablauf — Abgebrochen-Guard → Plugin/Responder-Auflösung → Hostaktionen (`receipt`, `waiting`/`attachments`-Outbox) → `Verarbeitet`-Return → `DeliveryAttemptId`-Reconcile (Z. 107-123, **kein neuer CLI-Start**) → `NextAttemptUtc`-Guard → „älteres offenes Ereignis derselben Aufgabe"-Sperre → Aufgabenladen + `BlockReason` (Beendet/Archiviert bzw. `PausiertBisUtc` in Zukunft) → Remote-Validierung → Sitzungsprüfung → bei fehlender lebender SCM-Sitzung (`adapter is null || !adapter.IsAlive`, Z. 162-193): Prefix-Auflösung (explizit oder aktivierungsbewusst aus Schritt 1) → `ResolveDevelopmentAutomationPluginStrictAsync` → `CheckHealthAsync` → `ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync`. **Der einzige neue CLI-Start im gesamten Pfad ist dieser Block** — die natürliche Stelle für die Startfreigabe.
- **Wartezustand** existiert (`WaitAsync`/`WaitWithCommentAsync`, Z. 552-566; `Status = "Wartend"` + `Reason`), inkl. deterministischer „waiting"-Outbox-Kommentaraktion und Protokolleintrag-Muster bei geändertem Wartegrund (Schritt 1, Z. 174-178). Die Ereignisübersicht zeigt `Reason` bereits an (`ScmEventQueueViewModel` Z. 376+ bzw. 396-404) — **keine UI-Änderung an der Übersicht nötig**.
- **Zählquelle** (`KiAusfuehrungsService`, Singleton): `_handles` (ConcurrentDictionary AufgabeId→`CliProcessHandle`), `GetRunningCount`/`GetLaufendeAufgabenIds` (nur `!Process.HasExited`), `GetScmPromptSession(aufgabeId)` liefert `IScmPromptSessionAdapter` mit `PluginPrefix` + `IsAlive`. `CliProcessHandle` trägt **kein** PluginPrefix — Attribution über `Aufgabe.KiPluginPrefix` bzw. lebende `ScmPromptSessionAdapter.PluginPrefix` (tatsächlich gestartetes Plugin, inkl. Schritt-1-Fallback; wird beim erfolgreichen Start in `Aufgabe.KiPluginPrefix` zurückgeschrieben). Das Handle wird synchron vor Methodenende in `_handles` eingetragen — sequenzielles Dispatchen sieht neue Läufe sofort.
- **Verzögerungs-Mechanismus** existiert: `ScmDelayDirective`/`ScmEventStore` setzen `Status = "Verzögert"` + `NextAttemptUtc` + `PausiertBisUtc` der Aufgabe. „Verzögert" ist die eindeutige Markierung für Prioritätsklasse 2.
- **Aufgabe** trägt alle Prioritätsfelder: `Status` (Neu/Gestartet/Wartend/Beendet/Archiviert), `AusfuehrungsStatus` (NichtGestartet/Aktiv/Beendet), `PausiertBisUtc`, `KiPluginPrefix`, `ProjektId`.
- **Schlüssel-Kollision beachten:** `plugins.sessionlimit.<prefix>` ist **bereits belegt** durch `KiPluginLimitService.SessionLimitKeyPrefix` — dort werden **Rate-Limit-Reset-Zeitpunkte** (DateTimeOffset "O") persistiert, keine Parallelitätsgrenzen. Der Hinweis der Planprüfung auf dieses Muster ist daher nicht umsetzbar; ein eigenes Präfix ist nötig.
- **Einstellungs-Persistenz:** `AppEinstellungService` Key-Value (`GetIntSettingAsync`/`SetSettingAsync`/`GetSettingsAsync`); Plugin-Felder laufen dagegen über `PluginSettingsService` in den `ICredentialStore` — für die `AppEinstellung`-Persistenz der Plugin-Limits wird ein Host-deklariertes Feld in `LadePluginEinstellungen` eingespeist und beim Speichern abgefangen.
- **Einstellungs-UI:** `SettingsView.xaml` Automatisierungs-Tab → Expander „Aufgaben ausführen" (AutomationName `AufgabenAusfuehrenExpander`); Plugins-Tab → `PluginDetailPanel` mit plugindeklarierten `PluginSettingGroupEntry`-Gruppen (Integer-Template existiert, trägt aber **keinen** `AutomationProperties.Name` — für E2E anzufügen). `SettingsViewModel` validiert in `ValidierePflichtfelder*` (nur das aktuell selektierte Plugin-Panel wird validiert/gespeichert).
- **Lücke:** Es existiert kein Parallelitäts-Limit, keine Pro-Plugin-Attribution der laufenden Handles und keine priorisierte Dispatch-Auswahl.

Test-Ausgangszustand: siehe [Tests](inventory/tests.md) (Baseline-Lauf der stabilen Lane).

## Details

- [Datenmodell und Einstellungsschlüssel](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [UI](inventory/ui.md)
- [Tests](inventory/tests.md)
