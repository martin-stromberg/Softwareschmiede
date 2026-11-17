# Code-Review

## Ergebnis

**Status:** Keine Befunde mit Korrekturbedarf

Geprüft wurde der Branch-Diff (Produktions- und Testcode) gegen die
Projekt-Konventionen. Die Änderungen sind klein, fachlich gebündelt und folgen
den bestehenden Mustern (AppEinstellung-Schlüssel, Service-Scopes, Magic-String-
Statuskonvention, Fluent-Teststil).

## Geprüfte Aspekte

### `ScmEventExecutionLimitService.cs` (neu)

- Klare Verantwortung: Lesen der Grenzen + Zulassungsauswertung; die reine
  Auswertung ist als `internal static` vom DB-/Service-Zugriff getrennt und
  direkt unit-testbar (`GetStartBlockReason_PrueftGlobalesVorPluginLimit`).
- `GetBelegungAsync` zählt live aus den Prozess-Handles
  (`GetLaufendeAufgabenIds`), lädt die Aufgaben-Prefixe in **einer** Abfrage
  (`AsNoTracking`, `Contains`-IN-Liste) und überlagert sie mit dem tatsächlich
  gestarteten Plugin der lebenden SCM-Sitzung — exakt die geforderte
  Attribution (live PluginPrefix > Aufgabe.KiPluginPrefix).
- Case-insensitives `OrdinalIgnoreCase`-Dictionary verhindert Prefix-Duplikate;
  leere Prefixe werden übersprungen statt gezählt.
- `ParseLimit` behandelt ungültige Werte als unbegrenzt — konsistent mit der
  UI-Validierung (die das Speichern ungültiger Werte ohnehin verhindert) und
  defensiv gegen manuell gesetzte Schlüssel.

### `ScmDispatchPrioritaet.cs` (neu)

- Statische, zustandslose Auswertung mit sprechenden Lightweight-Records;
  Klassenlogik in `PrioritaetsKlasse` exakt nach Vorgabe (Fortsetzung nur bei
  aktiver Aufgabe; `Beendet`/`Archiviert` explizit ausgenommen).
- `OrderForDispatch` ist stabil (`Sequence` als letzter Tie-Breaker) und sortiert
  Klasse 2 zeitaufsteigend mit `long.MaxValue`-Fallback für fehlende Zielzeit.
- Literal `„Verzögert"`: Magic String, aber etablierte Codebase-Konvention
  (drei weitere Vorkommen in `LaufendeAutomatisierungenZaehlung`,
  `ScmEventOrchestrator`, `ScmEventStore`); kein neuer Divergenzpunkt.

### `ScmEventMonitoringService.cs` (Diff)

- Fensterbasierte Priorisierung: weiterhin `OrderBy(Sequence).Take(100)` für die
  Kandidatenmenge, danach `OrderForDispatch` innerhalb des Fensters — korrekte
  Paginierung (`batch[^1].Sequence` bleibt der Maximalwert, da das Ordering vor
  der Projektion steht) und abwärtskompatibles Laufzeitverhalten.
- Zustandslookup der Aufgaben erfolgt gebündelt pro Batch (eine `Contains`-
  Abfrage, `AsNoTracking`), kein N+1.

### `ScmEventOrchestrator.cs` (Diff)

- Zulassungskontrolle steht nach der Prefix-Auflösung und **vor** Plugin-Auflösung,
  Health-Check und CLI-Start — der einzige Punkt, an dem ein Slot verbraucht würde;
  Reconciliation bereits gestarteter Versuche läuft daran vorbei (unbegrenzt).
- Systemmeldung nur bei Grundwechsel (`item.Reason`-Vergleich) — kein
  Protokoll-Spam bei wiederholtem Polling desselben Wartegrunds.
- Der Konstruktor wächst auf 12 DI-Parameter — folgt dem dort etablierten
  Primärkonstruktor-Muster; kein neues Strukturproblem (Bemerkung, kein Befund).

### `KiAusfuehrungsService.cs` / `ICliExecutionLimitSource.cs`

- Schmale Schnittstelle (2 Methoden), implementiert am bestehenden Singleton —
  kein zweiter Wahrheitskanal für laufende Handles; `HasExited`-Zugriff ist
  exception-tolerant wie `GetRunningCount`.
- `GetScmPromptSession` liefert den an den tatsächlichen Start gebundenen
  Adapter (Alt-/Shellstarts ohne sicheren Vertrag liefern `null`) — die
  `IsAlive`-Prüfung bleibt beim Aufrufer und ist dort korrekt umgesetzt.

### `SettingsViewModel.cs` / `SettingsView.xaml` (Diff)

- Host-Feld als statischer `PluginSettingField` mit `ReferenceEquals`-Erkennung
  im Validierungs- und Speicherpfad — eine klar markierte, einzige Stelle für die
  Host-Eigenheit; Persistenz läuft bewusst über `AppEinstellungService`
  (Host-Setting), nicht über den Plugin-Credential-Store.
- Speicherpfad schreibt idempotent pro Speichern; `NullWennLeer` deckt
  Leerstrings auf `null` ab — „leer = unbegrenzt“ ist in beide Richtungen
  konsistent.

### Tests

- Neue Suites decken die geforderte Matrix vollständig ab (Grenzen global/plugin/
  kombiniert/unbegrenzt/ungültig, Attribution inkl. Live-Session-Fallback,
  Host-only-/Reconcile-Bypass, alle drei Prioritätsklassen plus
  Beendet/Archiviert-Ausschluss, ViewModel-Laden/Speichern/Validierung, E2E).
- `SettingsViewModelTests.KiPluginSelectedCommand_…` wurde fachlich korrekt auf
  die zweite, hostseitige Gruppe angepasst (SCM-Pendant beweist unverändertes
  Verhalten für SCM-Plugins).

## Hinweise

- `E2E_AusfuehrungsLimits.cs` enthält im `finally` einen verschachtelten
  `try/catch` mit stillem Fallback auf direkte DB-Bereinigung — bewusstes
  Cleanup-Hardening für Folgeszenarien, dokumentiert im Kommentar; kein Befund.
