# Bestandsaufnahme: Durchgängiger automatischer Start und Zustellung automatisch angelegter Aufgaben (Schritt 4)

Analyse des Auto-Start-/Zustellungspfads für per `ScmEventTaskResolver` angelegte oder wiederverwendete Aufgaben sowie des Wartend-Verhaltens ohne Standard-CLI. Stand: Branch-HEAD `e076440`.

## Zusammenfassung

- **Der komplette Start- und Zustellungspfad existiert bereits** und läuft für alle Inbox-Einträge mit `AufgabeId` — also auch für die in Schritt 3 eingeführten automatisch angelegten/wiederverwendeten Aufgaben — über `ScmEventMonitoringService.RunOnceAsync` → `ScmEventOrchestrator.DispatchAsync` → `DispatchCoreAsync` im selben Monitorlauf. Kein Parallelmechanismus nötig.
- `DispatchCoreAsync` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs:72-226`) enthält alle geforderten Prüfungen **pro Zustellversuch**: erneute Repository-Identitäts-/`CanWrite`-Prüfung (Z. 124-129), Trigger-/Bearbeiter-Revalidierung (Z. 130-131), Blocker für beendete/archivierte/pausierte Aufgaben (Z. 122-123, 158-159, `BlockReason` Z. 387-389), Status `Neu → Gestartet` (Z. 132, 160), CLI-Auflösung Aufgabe → Projekt-Default → globaler Default (Z. 141), Klon/Branch/CLI-Start über `EntwicklungsprozessService` (Z. 147-151), Zustellung über Exchange-Protokoll (Z. 174-199).
- **Wartend ohne Standard-CLI ist bereits bedienbar**: `WaitWithCommentAsync` mit „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." (Z. 142-143), `NextAttemptUtc` bleibt `null` → automatischer Wiederversuch beim nächsten Poll; zusätzlich UI-Aktionen „Öffnen / CLI auswählen" (`OpenFromQueueAsync`) und „Erneut prüfen" (`RetryAsync`).
- **Anlage erfolgt bereits vor/unabhängig von der CLI-Auflösung** (Resolver läuft in der Aufnahmephase vor `AcceptBatchAsync`) — die empfohlene Entscheidungsvariante ist damit bereits Ist-Zustand und muss nur dokumentiert und testabgesichert werden.
- **Lücke = Nachweis, nicht Implementierung**: Es existiert kein Test, der die Kette „Ereignis ohne Aufgabe → Anlage → Auto-Start → Zustellung" bzw. „keine Standard-CLI → wartend → CLI-Auswahl → Zustellung" für angelegte Aufgaben durchspielt. `Monitor_QualifiedCommentOnUnassignedIssue_CreatesTaskAndInboxEntry` assertiert `Gestartet` implizit, aber ohne CLI-Konfiguration (Ereignis wartet). E2E deckt bisher nur die Anlage ab (E7-Schritt in der konsolidierten Methode).

## Details

- [Dispatch-Pipeline und Startpfade](inventory/pipeline.md): `DispatchCoreAsync`-Reihenfolge, `RunOnceAsync`-Dispatchphase, `EntwicklungsprozessService`-Startmethoden, `ScmTaskCoordinator`-Reentranz, `AufgabeService.SetStatusAsync`/`StartenAsync`.
- [CLI-Auflösung und Wartend-Verhalten](inventory/cli-resolution.md): Auflösungskette, Wartend-Texte, Queue-Aktionen (`OpenFromQueueAsync`, `RetryAsync`, `ActivateForOpenAsync`), UI-Projektion.
- [Tests und Testinfrastruktur](inventory/tests.md): `ScmDispatchFixture`, `ScmE2EFixture`, konsolidierte E2E-Methode, bestehende Abdeckung und Lücken.

## Architektur auf einen Blick

```
RunOnceAsync (60 s Poll)
  ├─ Aufnahmephase: Enumeration + ReadEventsAsync
  │     └─ taskIds == 0 + qualifizierte Beobachtung → ScmEventTaskResolver.ResolveAsync
  │           ├─ Treffer → Wiederverwendung  |  kein Treffer → AufgabeService.CreateFrom*
  │           └─ ScmEventStore.AcceptBatchAsync(aufgabeId) → ScmIncomingEvent "Aufgenommen"
  └─ Dispatchphase: für jedes fällige Inbox-Ereignis → DispatchAsync
        └─ DispatchCoreAsync (unter ScmTaskCoordinator + DB-Lease)
              ├─ Plugin-, FIFO-, Blocker-, Owner-/CanWrite-, Trigger-Prüfungen
              ├─ AufgabeStatus.Neu → Gestartet (AufgabeService.SetStatusAsync)
              ├─ CLI-Auflösung → fehlt → "Keine Standard-CLI konfiguriert. …" (Wartend, bedienbar)
              ├─ ProzessStartenUndCliStartenAsync (Klon+Branch+CLI) oder CliNeustartenAsync
              ├─ Bereitschafts-/Kanalnachweis (ScmPromptSessionAdapter)
              └─ PrepareAsync + WritePromptAsync → "Zugestellt" → Reconcile → "Verarbeitet"
```
