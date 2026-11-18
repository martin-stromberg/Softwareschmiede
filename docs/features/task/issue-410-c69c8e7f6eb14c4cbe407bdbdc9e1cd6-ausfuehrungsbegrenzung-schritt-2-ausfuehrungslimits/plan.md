# Umsetzungsplan: Ausführungsbegrenzung mit Priorisierung (Schritt 2, #410 Teil 1)

Umgesetzt wird die Ausführungsbegrenzung gemäß `requirement.md`: ein globales und je-KI-Plugin konfigurierbares Parallel-Limit für **neue CLI-Ausführungen** über den SCM-Ereignis-Dispatch, mit deterministischer Priorisierung (Fortsetzungen → fällige Verzögerungen zeitaufsteigend → Eingangsreihenfolge), ehrlichem Wartegrund und ununterbrochenen laufenden Ausführungen.

## Umsetzungsidee

### 1. Neuer Dienst `ScmEventExecutionLimitService` (`Application/Services`, scoped)

- Schlüssel: `GlobalLimitKey = "scm.execution.maxparallel"`, `PluginLimitKeyPrefix = "plugins.maxparallel."` (+ `PluginLimitKey(prefix)`).
  - Begründung: `plugins.sessionlimit.<prefix>` ist durch `KiPluginLimitService` belegt (Rate-Limit-**Reset-Zeitpunkte**, keine Integers) — das Präfix aus dem Plan-Hinweis ist nicht verwendbar (s. inventory/models.md).
- API:
  - `Task<string?> GetStartBlockReasonAsync(string pluginPrefix, CancellationToken ct)` → `null` = frei; sonst deutscher Wartegrund („… Platz frei wird.").
  - `GetBelegungAsync` (privat): `KiAusfuehrungsService.GetLaufendeAufgabenIds()` → Aufgaben (`Id`, `KiPluginPrefix`) laden → Attribution je Aufgabe: lebende `GetScmPromptSession(id)?.IsAlive == true` → `session.PluginPrefix`, sonst `aufgabe.KiPluginPrefix` → `(global: n, perPlugin: OrdnerIgnoreCase-Dictionary)`.
  - Grenzen lesen: `GetIntSettingAsync`; `null`/Text-Reste/`< 1` ⇒ unbegrenzt.
  - Bewertung rein/statisch (`GetStartBlockReason`) für deterministische Unit-Tests: globales Limit zuerst, dann Plugin-Limit.
- Injektion: `SoftwareschmiededDbContext`, `AppEinstellungService`, `KiAusfuehrungsService` (konkret, wie im Orchestrator).

### 2. Zählschnittstelle `ICliExecutionLimitSource` (Domain/Interfaces)

Da `KiAusfuehrungsService` konkret ist und Unit-Tests die Belegung fälschen müssen: schmales Interface mit den zwei bereits vorhandenen öffentlichen Methoden `GetLaufendeAufgabenIds()` und `GetScmPromptSession(Guid)`; implementiert von `KiAusfuehrungsService`, registriert in `App.xaml.cs` (Singleton-Weiterleitung wie `IRunningAutomationStatusSource`) und im `ScmDispatchFixture`.

### 3. Startfreigabe im `ScmEventOrchestrator` (Zulassungskontrolle, Kernidee 1)

In `DispatchCoreAsync`, im Block `adapter is null || !adapter.IsAlive` (einziges neues-CLI-Start-Areal), **nach** der Prefix-Auflösung und den Plugin-Wartegründen (Schritt 1), **vor** `ResolveDevelopmentAutomationPluginStrictAsync`/`CheckHealthAsync`/CLI-Start:

```csharp
var limitWartegrund = await _limitService.GetStartBlockReasonAsync(effectivePluginPrefix, ct);
if (limitWartegrund is not null)
{
    if (item.Reason != limitWartegrund) { /* Protokolleintrag-Muster wie Schritt 1 */ }
    return await WaitAsync(item, limitWartegrund, ct);
}
```

- `WaitAsync` (kein Outbox-Kommentar): Kapazitätswarten ist ein interner Scheduler-Zustand; Hostaktionen bleiben unbegrenzt und werden nicht künstlich erzeugt. Wartegrund-Wechsel wird wie bei Schritt 1 protokolliert (deterministischer Kommentar bei geändertem Grund nur, wenn bereits ein Wartezustand dokumentiert wurde — Umsetzung: analoger Block wie Z. 174-178, optional).
- Ereignis bleibt `Wartend` mit `Reason`, **kein** `NextAttemptUtc = long.MaxValue`; nächster Poll erneut startbar. Kein Interrupt laufender Läufe (reine Startfreigabe).
- `DeliveryAttemptId`-Reconcile (Z. 107-123) liegt **vor** dieser Stelle → unbegrenzt. UI-Pfade (`CheckStatusCommand` → `DispatchAsync`, `RetryAsync`/`ResendAsync` → `MutateAndDispatchAsync`, `OpenFromQueueAsync` → Core mit explizitem Prefix) laufen alle durch denselben Block → gleiche Prüfung.
- Bei `OpenFromQueueAsync`: `explicitPluginPrefix` (Benutzerwahl) wird für die Plugin-Limit-Prüfung verwendet — der tatsächlich gestartete Prefix.

### 4. Priorisierte Auswahl im `ScmEventMonitoringService` (Kernidee 2)

- Neue statische Helferklasse `Application/Services/ScmDispatchPrioritaet.cs` (pur, unit-testbar):

```csharp
internal static class ScmDispatchPrioritaet
{
    public sealed record TaskState(AufgabeStatus Status, AufgabeAusfuehrungsStatus AusfuehrungsStatus, DateTimeOffset? PausiertBisUtc);
    public static int PrioritaetsKlasse(ScmIncomingEvent item, TaskState? task); // 1|2|3
    public static IReadOnlyList<ScmIncomingEvent> OrderForDispatch(IReadOnlyList<ScmIncomingEvent> items, IReadOnlyDictionary<Guid, TaskState> taskStates);
}
```

- Klasse 1: `PausiertBisUtc != null` ODER (`AusfuehrungsStatus == Beendet` UND `Status ∈ {Neu, Gestartet, Wartend}`). Beendet/Archiviert-Aufgabenstatus ⇒ nie Klasse 1.
- Klasse 2: `Status == "Verzögert"` (eindeutige Markierung des `ScmDelayDirective`-Mechanismus) — Sortierung `NextAttemptUtc` aufsteigend, `null` zuletzt, Tie-Break `Sequence`.
- Klasse 3: Rest — `Sequence`.
- In `RunOnceAsync`: pro Batch die `AufgabeId`s der Ereignisse → TaskState-Map aus `Aufgaben` laden → `OrderForDispatch` → sequenziell dispatchen. Die eigentliche Platzvergabe bleibt die Orchestrator-Freigabe (lückenlos: gesperrte Kandidaten warten ehrlich, freie Plätze gehen an nachfolgende startbare Ereignisse — identisch zur „Priorität nur unter den Startbaren"-Semantik). Dokumentierte Trade-off-Entscheidung: keine Pro-Zyklus-Budgetkappe in der Auswahl, damit (a) überlange Batches nicht stumm ausgelassen werden und (b) jeder blockierte Kandidat seinen ehrlichen Wartegrund erhält.

### 5. Einstellungen (Kernidee 3)

**`SettingsViewModel`:**
- Neues Feld `MaxParalleleAusfuehrungen` (string, wie `ScmTaskCompletionWaitMinutes`): Laden via `GetIntSettingAsync(GlobalLimitKey)` → `> 0` → String; Speichern `SetSettingAsync(key, trimmed or null)`; `ValidiereMaxParalleleAusfuehrungen` (leer ok, sonst int ≥ 1, dt. Fehlermeldung) in `ValidierePflichtfelder` einhängen.
- Pro-Plugin: In `LadenAsync` für alle `KiPlugins`-Prefixe `GetSettingsAsync(PluginLimitKey(prefix))` → `_kiPluginParallelLimits` (Dictionary prefix→string?). In `LadePluginEinstellungen` wird für `plugin is IKiPlugin` eine Host-Gruppe „Ausführungsbegrenzung" mit einem `PluginSettingEntry` (Feld `PluginSettingField("ParallelLimit", "Maximale parallele Ausführungen", PluginSettingFieldType.Integer, Optional, Placeholder "unbegrenzt", Description "Ganzzahl ≥ 1; leer = unbegrenzt. Zählt laufende CLI-Ausführungen dieses Plugins zusätzlich zum globalen Limit.")`) angehängt — als allgemeine Gruppe vor `Erweiterte Gruppen`.
- Speichern: `SpeicherePluginEinstellungen` → async `SpeicherePluginEinstellungenAsync`; Host-Eintrag (Erkennung `ReferenceEquals(entry.Field, HostParallelLimitField)`) → `SetSettingAsync(PluginLimitKey(prefix), Wert-oder-null)` statt `PluginSettingsService`/CredentialStore.
- Validierung: in `ValidierePflichtfelderFuerSettings` — für das Host-Feld zusätzlich `≥ 1` (nicht nur `TryParse`), mit deutscher Fehlermeldung.

**`SettingsView.xaml`:** Neuer `TextBox`-Block im Expander „Aufgaben ausführen" (nach `DelayKeywordBereich`, vor `PromptAnhaengeBereich`), `AutomationProperties.Name="MaxParalleleAusfuehrungen"`, Label + Beschreibungstext (deutsch, mit ersetztem `cli`-Begriff? — UI nutzt „KI-CLI"; Wortlaut: „Maximale parallele CLI-Ausführungen (global)" + Hilfetext „Leer = unbegrenzt. Zählt alle laufenden KI-CLI-Prozesse; bei Erreichen des Limits warten SCM-Ereignisse bis ein Platz frei wird.").

**`PluginDetailPanel.xaml`:** `AutomationProperties.Name="{Binding Field.Key}"` im `IntegerTemplate` ergänzen (fehlt gegenüber Text/Enum — für E2E-Erreichbarkeit des `ParallelLimit`-Feldes).

### 6. Tests

**Unit (`Softwareschmiede.Tests`, `Application/Services/ScmEventExecutionLimitServiceTests.cs`):**
- Grenzen: ungesetzt/unparsebar/0/-5 → unbegrenzt; 1/2 → wirksam; pro Plugin.
- `GetStartBlockReason`: globales Limit erreicht → Grund; Plugin-Limit erreicht → Grund; beide → globaler Grund zuerst; belegt < Limit → null; Plugin-Limit betrifft nur zählende Aufgaben desselben Prefix (gemischte Plugins); Attribution lebende Sitzung (`PluginPrefix` ≠ `KiPluginPrefix` → Sitzung gewinnt); unlimitiertes Plugin + globales Limit.
- `ScmDispatchPrioritaetTests`: Klassen 1/2/3-Reihenfolge, `NextAttemptUtc`-aufsteigend in Klasse 2, Sequence-Tiebreaks, Beendet/Archiviert nie Klasse 1, `Beendet`+aktiv → Klasse 1, gesetzte `PausiertBisUtc` (auch abgelaufen) → Klasse 1, fehlende Aufgabe → Klasse 3.

**Integration (`Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limits.cs`, `ScmDispatchFixture`):**
- Global-Limit 1 + zwei startbare Ereignisse → genau ein Start, zweites `Wartend` mit Limit-Grund; nach Beenden des ersten Laufes → zweites startet (Slot-Nachrücken über Poll).
- Plugin-Limit: Prefix A Limit 1 — zwei Ereignisse Plugin A + eines Plugin B → B startet, zweites A wartet (gemischte Plugins, restriktives Limit).
- Beide Limits: globales Limit erreicht obwohl Plugin-Limit frei → globaler Grund.
- Hostaktionen/Reconcile: Ereignis mit `DeliveryAttemptId` (laufender Versuch) wird auch bei erreichtem Limit reconciled (kein `Wartend`-Limitgrund, kein neuer Start).
- Priorisierung im Polling: Monitor-Dispatch-Reihenfolge — Fortsetzungsaufgabe (PausiertBisUtc gesetzt, abgelaufen) vor fälligem „Verzögert"-Ereignis vor regulärem Sequence-Event; zwei „Verzögert" mit unterschiedlichem `NextAttemptUtc` in aufsteigender Zielzeit.
- Permanente Sperre: Ereignis an Beendet/Archiviert bleibt `Wartend` „… beendet/archiviert" auch mit freiem Platz.
- `OpenFromQueueAsync` bei erreichtem Limit → `Wartend` mit Limit-Grund (UI-Pfad deckt dieselbe Freigabe ab).

Fixture-Erweiterung: `ICliExecutionLimitSource`-Registrierung (Testdouble oder echter `KiAusfuehrungsService` mit `StartCliAsync`-Ping-Prozessen — Entscheidung bei Implementierung, bevorzugt konfigurierbares Testdouble für Belegung + echte Prozesspfade dort, wo „Start zählt" belegt werden muss).

**SettingsViewModel-Tests** (`SettingsViewModelTests_*`): Laden/Speichern/Validierung (leer, „0", „-1", „abc", „2") des globalen Feldes; Host-Feld erscheint in `SelectedPluginSettings` für KI-Plugins; Speichern legt `plugins.maxparallel.<prefix>` in `AppEinstellung` ab (nicht im CredentialStore); Entfernen bei leerem Wert.

**E2E (FlaUI, eine konsolidierte Methode)** `E2E_AusfuehrungsLimits.cs` + `SettingsView`-Helper (`Get/SetMaxParalleleAusfuehrungen`, `SelectKiPluginEntry`/`SetPluginParallelLimit`): Automatisierung → globalen Wert setzen → speichern → neu laden → Wert persistiert (UI + optional `OpenTestDbContext`-Assertion auf `AppEinstellung`); Plugins-Tab → KI-Plugin-Eintrag → `ParallelLimit` setzen → speichern → persistiert; ungültige Werte („0", „abc") → Fehlerhinweis, kein Persistieren; Aufräumen (Werte wieder leeren) in `finally`.

### 7. Nicht-Ziele / Abgrenzung

- Kein ereignisgesteuertes Nachrücken beim Freiwerden (Schritt 3); Nachrücken erfolgt über den Poll-Zyklus (`ScmEventOptions.PollInterval`).
- Kein Limit für UI-Einzelaktionen außerhalb des Dispatch-Kerns (z. B. manuelles Aufgaben-Starten in anderen Views — Anforderung bindet das Limit an SCM-Ereignis-Dispatch).
- Kein Umbau von `LaufendeAutomatisierungenZaehlung` (fachlicher Auto-Shutdown-Zweck).
- Keine Neu-Verwendung von `plugins.sessionlimit.*` (belegt, s. Inventur).

## Annahmen / offene Punkte (5a)

1. **Schlüssel-Kollision:** `plugins.sessionlimit.<prefix>` ist durch Rate-Limit-Resets belegt → `plugins.maxparallel.<prefix>` + `scm.execution.maxparallel`. Abweichung vom Plan-Hinweis, begründet und dokumentiert.
2. **Kapazitätswarten ohne Host-Kommentar:** „waiting"-Outbox-Kommentare sind Kommentar-Aktionen (Hostaktion); ein Limit-Wartegrund erzeugt keinen SCM-Kommentar — Protokolleintrag nur bei Grundwechsel (Muster Schritt 1).
3. **Auswahl vs. Freigabe:** Die Batch-Auswahl sortiert priorisiert; die autoritative Platzvergabe erfolgt pro Ereignis in der Orchestrator-Freigabe (atomarer, auch für UI-Pfade). „Slotbegrenzte Auswahl" ist damit realisiert: gesperrte Kandidaten starten nicht und belegen keinen Platz.
4. **Plugin-Limit gilt für den aufgelösten Prefix** (`effectivePluginPrefix` inkl. Schritt-1-Fallback/expliziter Wahl) — „das zu startende Plugin".
5. **Subagenten stehen nicht zur Verfügung** — Lifecycle-Schritte werden durch mich selbst ausgeführt (dokumentierte Abweichung).

## Akzeptanzszenarien

- **A1 Einstellungen:** Globales Limit und je-Plugin-Limit in den Einstellungen setzbar, persistiert (`AppEinstellung`-Muster), beim Dispatch wirksam; leer/unset = unbegrenzt.
- **A2 Limit-Wartegrund:** Erreichtes globales oder Plugin-Limit → kein neuer Start; Ereignis bleibt `Wartend` mit Wartegrund; startet bei freiem Platz automatisch.
- **A3 Priorisierung:** Fortsetzung pausierter/beendeter Läufe → fällige Verzögerungen (`NextAttemptUtc` aufsteigend) → Eingangsreihenfolge (`Sequence`).
- **A4 Zählung:** Global = alle `KiAusfuehrungsService`-Aufgaben; pro Plugin = Attribution `Aufgabe.KiPluginPrefix`, bei lebender SCM-Sitzung `ScmPromptSessionAdapter.PluginPrefix`.
- **A5 Permanente Sperre:** Aufgabe Beendet/Archiviert → BlockReason „… beendet/archiviert", keine Reaktivierung.
- **A6 Kein Interrupt / Unbegrenztheit:** Laufende CLI-Ausführungen ungestört; Hostaktionen, Outbox, Reconcile gestarteter Versuche unbegrenzt.
- **A7 Tests:** Unit/Integration für Zählung, Grenzfälle, gemischte Plugins, Priorisierung, Settings-VM; konsolidierter FlaUI-E2E des Einstellungsflusses.
- **A8 UI:** Wartegrund in der Ereignisübersicht sichtbar (via `Reason`, bereits vorhanden).

## Anforderungen an die Implementierung

- Alle neuen Public-APIs mit CS1591-XML-Doku (`<summary>`/`<param>`/`<returns>`).
- Deutsche UI-Texte; Schlüssel-/Log-Texte in bestehendem Stil.
- `dotnet format`-konform; keine `nul`-Umleitungen; kein Kill von `Softwareschmiede.App.exe`.
- Änderungen nur auf dem Feature-Branch; kein Merge.
