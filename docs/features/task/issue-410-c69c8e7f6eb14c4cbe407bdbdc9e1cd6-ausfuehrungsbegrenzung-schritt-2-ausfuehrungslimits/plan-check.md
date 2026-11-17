# Plancheck: Ausführungsbegrenzung mit Priorisierung (Schritt 2)

Prüfung von `plan.md` gegen `requirement.md` und die Inventur vor der Implementierung.

| Prüfpunkt | Ergebnis |
|-----------|----------|
| Vollständigkeit der Akzeptanzszenarien A1-A8 | ✅ Alle Szenarien sind auf konkrete Umsetzungsschritte und Tests abgebildet (Dienst + Orchestrator-Freigabe = A2/A4/A6; `ScmDispatchPrioritaet` + `RunOnceAsync` = A3; ViewModel/XAML + `AppEinstellung` = A1/A8; bestehende `BlockReason` unverändert = A5; Testmatrix = A7). |
| Startfreigabe an der richtigen Stelle | ✅ Einziger neuer CLI-Start liegt im Block `adapter is null || !IsAlive` (Inventur logic.md); die Freigabe greift dort für Poll **und** alle UI-Pfade (`DispatchAsync`, `MutateAndDispatchAsync`, `OpenFromQueueAsync`). Reconcile (`DeliveryAttemptId`) liegt davor → unbegrenzt. |
| Priorisierungs-Semantik | ✅ Klassen 1-3 exakt wie gefordert; Beendet/Archiviert können nie Klasse 1 sein; Verzögerungen per `Status="Verzögert"` + `NextAttemptUtc`-Sortierung; „priorisiert nur unter den Startbaren" realisiert durch Freigabe im Dispatch (blockierte Kandidaten warten ehrlich). Trade-off dokumentiert (Annahme 3). |
| Ehrlicher Wartegrund / kein Hard-Lock | ✅ `WaitAsync` setzt `Status="Wartend"` + `Reason` ohne `NextAttemptUtc`-Sperre; Wiederaufnahme über Poll. Ereignisübersicht zeigt `Reason` (kein UI-Umbau nötig). |
| Persistenz | ✅ `AppEinstellung`-Muster; **Kollision `plugins.sessionlimit.*` erkannt** (belegt durch `KiPluginLimitService`-Reset-Zeitpunkte) → `plugins.maxparallel.<prefix>` / `scm.execution.maxparallel`, Abweichung vom Plan-Hinweis begründet. |
| Wertebereich | ✅ `>= 1`-Validierung in ViewModel (global + Host-Feld); Dienst behandelt `< 1`/unparsebar defensiv als unbegrenzt. |
| Attribution | ✅ `ScmPromptSessionAdapter.PluginPrefix` bei lebender Sitzung, sonst `Aufgabe.KiPluginPrefix`; leerer Prefix zählt nur global. Case-insensitiv. |
| Kein Interrupt / Unbegrenztheit | ✅ Reine Startfreigabe; Hostaktionen (`receipt`/`waiting`/`attachments`/`ai:*`-Outbox) und Reconcile laufen vor/außerhalb der Freigabe. |
| Risiken | Akzeptiert: (a) UI-`OpenFromQueueAsync` mit explizitem Prefix prüft Limit auf den gewählten Prefix — korrekt (tatsächlich zu startendes Plugin); (b) Pro-Plugin-Dictionary fasst Prefixe case-insensitiv; (c) Belegungszählung lädt Aufgaben je Freigabe — kleine Batch-Kosten, akzeptabel; (d) gleiche Aufgabe/ältere-Ereignis-Sperre bleibt vor der Freigabe → keine Mehrfachstarts. |
| Testbarkeit | ✅ `ICliExecutionLimitSource` entkoppelt Belegung; `ScmDispatchPrioritaet` pur/statisch; Fixture-Integration real SQLite. |

**Status: erfolgreich** — Der Plan deckt alle Anforderungen ab; die dokumentierten Abweichungen (Schlüsselpräfix, Auswahl-vs.-Freigabe, Kapazität ohne Host-Kommentar, Subagenten-Abweichung) sind begründet.
