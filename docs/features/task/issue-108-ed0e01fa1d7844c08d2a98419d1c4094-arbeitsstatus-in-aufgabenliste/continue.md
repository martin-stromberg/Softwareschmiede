# Offene Aufgaben

Erstellt am: 2026-07-10
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Keine

## Rückmeldung des Kunden

- [x] Der Test SeitenleistenKachel_AktualisiertStatusAutomatisch_OhneManuellesNeuladen_E2E verhält sich anscheinend anders als der Ablauf in der Realität passiert. Rufe ich eine Aufgabe auf und die CLI wird gestartet, so zeigt die Fußzeile an, dass die "Ausführug läuft", aber die Kachel im Programmmenü zeigt nur den Status "Bereit".

  **Behoben am 2026-07-10.** Ursache: `Aufgabe.AktiveRunId` — die zentrale Bedingung, unter der
  `KiAusfuehrungsStatusConverter` "▶ Läuft" zurückgibt — wurde im Produktivcode **nirgends** gesetzt.
  `AufgabeService.UpdateHeartbeatAsync` (aufgerufen von `CliProcessManager` alle 30s) aktualisierte nur
  `LastHeartbeatUtc`, nie `AktiveRunId`. Die Fußzeile ("Ausführung läuft") wird komplett unabhängig davon
  aus `PseudoConsoleSession.RuntimeStatus` (Terminal-Ausgabe-Heuristik) gespeist. Dadurch zeigte die
  Seitenleisten-/Dashboard-Kachel real **immer** "✓ Bereit", unabhängig vom tatsächlichen CLI-Zustand.

  Der bisherige E2E-Test deckte das nicht auf, weil er `AktiveRunId`/`LastHeartbeatUtc` direkt per SQL in
  der Test-Datenbank setzte, statt einen echten CLI-Prozess zu nutzen — er testete nur den periodischen
  Timer-Fallback mit einer künstlich vorgetäuschten Datengrundlage, nicht den realen Schreibpfad. Das
  entspricht genau der Kundenbeobachtung ("Test verhält sich anders als die Realität").

  **Fix:** `AufgabeService` um `AktivenLaufSetzenAsync`/`AktivenLaufBeendenAsync` erweitert;
  `CliProcessManager.OnCliProcessStatusChanged` setzt bei `CliProcessStatus.Gestartet` sofort
  `AktiveRunId` + `LastHeartbeatUtc` und entfernt `AktiveRunId` bei `Gestoppt`/`Fehler`
  (`src/Softwareschmiede/Application/Services/AufgabeService.cs`,
  `src/Softwareschmiede/Application/Services/CliProcessManager.cs`).

  **Tests:** Neue Unit-Tests `AufgabeServiceTests.AktivenLaufSetzenAsync_...`/`AktivenLaufBeendenAsync_...`
  sowie `CliProcessManagerTests_AktiverLauf` (3 Tests, mit echtem DI-Scope-Factory + In-Memory-DB, die den
  bisher fehlenden Verdrahtungspfad abdecken) — alle grün. Der E2E-Test
  `E2E_ArbeitsstatusAktualisierung` wurde umgebaut: Er startet/stoppt jetzt einen echten CLI-Prozess
  (KiSimulator-Plugin) statt die Datenbank zu simulieren, und prüft damit den tatsächlichen
  Produktivpfad. Der E2E-Test konnte in dieser automatisierten Umgebung nicht ausgeführt werden (siehe
  `test-results.md` bzw. unten) — Verifikation auf einer Maschine mit interaktiver Desktop-Session steht
  noch aus. Alle 755 nicht-E2E-Tests bestehen.
