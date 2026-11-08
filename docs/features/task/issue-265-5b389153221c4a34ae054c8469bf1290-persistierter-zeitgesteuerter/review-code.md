# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PromptZeitVersandService.cs / ZeitgesteuerterPromptStore.cs (PromptZeitVersandService, ZeitgesteuerterPromptStore)

- **Thread-Safety / Lost-Update** — `UpsertWennUebereinstimmendAsync` (Store Zeilen 78–103) blockiert legitime Writes, wenn `erwartet` `null` ist und dennoch ein Datensatz existiert. Die Annahme „vorhandener Datensatz ohne ersetzten Eintrag = neuerer Plan" ist falsch: `ersetzterEintrag == null` tritt auch auf, wenn der Datensatz einem **bereits entfernten** Vorgänger gehört — konkret (a) einem in-flight-Versand, dessen Eintrag in `HandleTimerElapsedAsync` (Zeile 337) bzw. `ZustellungNachSessionStartAsync` (Zeile 323) bereits vor dem Versand entfernt wurde, oder (b) einem in-flight-`CancelScheduledPromptAsync` (Eintrag entfernt Zeile 149, `LoeschenAsync` läuft noch). Konkrete Verlustfolge: P0-Versand in-flight (Datensatz P0) → Nutzer plant P1 mit vergangener Zielzeit → `ersetzterEintrag` ist `null` → P1-Versand schlägt fehl → `MarkiereWartetAufSession` legt P1 zurück → CAS verweigert wegen Datensatz P0 → P0-Versand schlägt danach fehl oder sein `LoeschenNachVersandAsync` (Match-Zweig löscht unbedingt) entfernt den Datensatz. Endstand: P1 nur im Speicher (bzw. veralteter P0-Datensatz überlebt) — nach Neustart geht P1 verloren bzw. wird P0 zugestellt. Dieselbe Verweigerung tritt dauerhaft auf, wenn der Datensatz von `ersetzterEintrag.Info` **abgedriftet** ist (ein früherer `UpdateZielZeitpunktAsync`/`UpsertAsync`-Fehlschlag wird in `MitStoreAsync` nur geloggt): `erwartet` ist dann nicht `null`, stimmt aber nicht mit dem Datensatz überein — der CAS verweigert fortan jeden Schreibzugriff im Sofortversand-Fehlerpfad, obwohl der Datensatz tatsächlich der Vorgänger ist.

  Empfehlung: Die CAS-Erwartung darf sich nicht allein auf den ersetzten Eintrag stützen. Entweder In-Flight-Versände/Löschungen pro Aufgabe unter `_lock` mitführen und ihre `ScheduledPromptInfo` als zulässige Vorgänger beim CAS mitgeben — oder die weitergehende Lösung aus dem nächsten Befund (Serialisierung pro Aufgabe) umsetzen, dann kann zwischen Eintrag-Entfernung und Store-Zugriff kein fremder Datensatz mehr auftauchen, der nicht Vorgänger ist.

- **Thread-Safety / Lost-Update** — `LoescheDatensatzWennAktuellAsync` (Zeilen 525–537) liest `neuererPlanAktiv` aus `_scheduledPrompts`, bevor `MitStoreAsync` die Store-Operation ausführt; die Momentaufnahme ist zum Löschzeitpunkt bereits veraltet. Zwei Folgefenster: (a) Wird zwischen Flag-Lesezugriff und `LoeschenNachVersandAsync` ein neuer Plan persistiert (`SchedulePromptAsync` → `UpsertDatensatzAsync` vor Eintrag-Einfügung, Zeilen 120–139), trifft der Mismatch-Zweig mit veraltetem `neuererPlanAktiv == false` den **neuen** Datensatz und löscht ihn — der neue Plan ist armiert, aber nicht persistiert und überlebt keinen Neustart. (b) Die Heuristik „kein In-Memory-Eintrag ⇒ abweichender Datensatz ist eine Leiche" ignoriert in-flight-Versände: Löscht ein erfolgreicher P1-Sofortversand den Datensatz des noch laufenden P0-Versands (Mismatch, kein Eintrag), schlägt P0 danach fehl und wird per `MarkiereWartetAufSession` nur noch im Speicher vorgehalten — P0s Persistenz ist weg. Analog löscht `CancelScheduledPromptAsync` → `LoeschenAsync` (Zeile 150) unbedingt und kann einen zwischenzeitlich von `SchedulePromptAsync` persistierten neueren Datensatz entfernen. Wurzel: In-Memory-Zustand und Store-Operationen sind pro Aufgabe nicht serialisiert; alle drei Schutzmechanismen entscheiden auf Snapshots.

  Empfehlung: Den kompletten Plan-Lebenszyklus pro Aufgabe serialisieren (z. B. `ConcurrentDictionary<Guid, SemaphoreSlim>` bzw. einen keyed Async-Lock um `SchedulePromptAsync`, `CancelScheduledPromptAsync`, `HandleTimerElapsedAsync`, `ZustellungNachSessionStartAsync` inklusive der Store-Zugriffe), sodass Planen/Stornieren/Versand-Finish pro Aufgabe nicht überlappen. Minimalvariante: `neuererPlanAktiv`- und In-Flight-Prüfung in denselben kritischen Abschnitt wie die Store-Entscheidung verlegen — das erfordert, dass `SchedulePromptAsync` seinen Eintrag/Unterbrechungspunkt gegenüber der Prüfung atomar macht.

- **Fehlerbehandlung / Ressourcen-Lebenszyklus** — `Dispose` (Zeilen 58–73) entschärft armierte Timer und leert die Warteschlange, ist aber nicht „vollständig inert" wie dokumentiert: Ein bereits laufender Callback (`HandleTimerElapsedAsync`/`ZustellungNachSessionStartAsync`), der seinen Eintrag vor dem `Dispose` entfernt hat, durchläuft danach weiter `SendPromptAsync` inkl. `WritePromptAsync`, `PromptSent`/`ScheduledPromptChanged`-Events und Protokolleintrag — eine „beendete" Instanz kann so noch zustellen. Zusätzlich re-populieren `SchedulePromptAsync`, `RestoreScheduledPromptsAsync` und `CancelScheduledPromptAsync` eine disposed Instanz kommentarlos und armen neue Timer. Für den dokumentierten Zweck (simulierter Neustart mit zwei Instanzen am selben `KiAusfuehrungsService`) bleibt damit ein schmaler Doppelzustellungs-/Geisterversand-Pfad offen.

  Empfehlung: `_disposed`-Flag unter `_lock` in `Dispose` setzen; in den öffentlichen Einstiegspunkten sowie unmittelbar vor `SendPromptAsync` in den Callback-Pfaden prüfen (frühzeitig `return` bzw. `ObjectDisposedException`), damit nach `Dispose` weder Einträge entstehen noch Versände ausgelöst werden.

## Verifikation der Befunde aus dem zweiten Review

Alle drei Befunde aus `review-code.2.md` sind behoben; die Verifikation aller Befunde aus `review-code.1.md` wurde dort bereits bestätigt und die Fixes sind weiterhin vorhanden:

- **Konditionale Persistenz im Sofortversand-Fehlerpfad:** `MarkiereWartetAufSession` gibt `bool` zurück (Guard `!_scheduledPrompts.ContainsKey` unter `_lock`, Zeilen 281–301); nur bei Erfolg läuft `UpsertDatensatzWennAktuellAsync` → `UpsertWennUebereinstimmendAsync` mit `ersetzterEintrag?.Info` als Erwartung (Zeilen 110–114). Der Hauptfall aus Befund 1 (neuerer Plan bereits persistiert + eingetragen) ist abgesichert — Restfenster siehe Befund oben. ✔
- **Drift-/Mismatch-Löschung mit Protokollierung:** `LoeschenNachVersandAsync` (Store Zeilen 115–153) loggt beide Mismatch-Äste als Warning und löscht einen abweichenden Datensatz ohne lebenden In-Memory-Plan — der Doppelzustellungs-Fall aus Befund 2 ist geschlossen. Restfenster des `neuererPlanAktiv`-Snapshots siehe Befund oben. ✔
- **Dispose entschärft Timer:** `Dispose` sammelt die Timer unter `_lock`, leert `_scheduledPrompts` und disposed außerhalb des Locks (Zeilen 58–73); `HandleTimerElapsedAsync` kehrt auf geleerter Warteschlange per `Remove`-Miss sofort zurück — kein Timer-Feuern mehr nach Dispose, deadlockfrei (kein `_lock` über `await` oder `Timer.Dispose()` hinweg). Residual: in-flight-Callbacks und Nach-Dispose-Aufrufe, siehe Befund oben. ✔

Neue Tests (Zeilen ~1006–1122 in `PromptZeitVersandServiceTests.cs`): `SofortVersand_Sendefehler_UeberschreibtNichtZwischenzeitlichNeuGeplantenDatensatz` (TCS-gesteuerter in-flight-Sendefehler, prüft CAS-Verweigerung), `Versand_LoeschtAbgedriftetenDatensatz_WennKeinPlanRegistriert` (Drift-Löschung), `Dispose_EntschaerftArmierteTimer` — jeweils ein fachlicher Fall pro Test, saubere Arrange/Act/Assert-Struktur, produktive Einstiegspunkte.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`
- `src/Softwareschmiede/Application/Services/ZeitgesteuerterPromptStore.cs`
- `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Domain/Entities/ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20261107211018_AddZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede/Migrations/20261107211018_AddZeitgesteuerterPrompt.Designer.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
