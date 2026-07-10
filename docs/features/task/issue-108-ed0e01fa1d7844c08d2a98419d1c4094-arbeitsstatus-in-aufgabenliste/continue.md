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

- [x] **Neu (2026-07-10):** Der Wechsel "Bereit" → "Läuft" funktioniert jetzt, aber der Rückweg nicht:
  Nach Ausführung wechselt die Fußzeile korrekt auf "Wartet auf Eingabe", dieser Statuswechsel kommt
  aber nicht in der Menü-/Seitenleisten-Kachel an.

  **Behoben am 2026-07-10.** Ursache (verifiziert): `PseudoConsoleSession` ermittelt ihren Laufzeit-
  Substatus (`CliRuntimeStatus.Laeuft`/`WartetAufEingabe`, aus Output-/Input-Aktivität,
  `RefreshRuntimeStatus()`/`CliRuntimeStatusEvaluator.Determine`) rein lokal und löst dabei
  `RuntimeStatusChanged` aus. Dieses Event wurde ausschließlich in `TaskDetailViewModel.AttachCliStatusSession`
  abonniert und speiste nur `CliStatusText` (die Fußzeile) — nirgends wurde der Wert an `Aufgabe`/
  `AufgabeService`/die Datenbank weitergereicht. `KiAusfuehrungsStatusConverter` zeigte "▶ Läuft", solange
  `AktiveRunId != null` und der Heartbeat aktuell war — seit dem Fix vom Vormittag (Commit `e1fa9f9`) galt das
  die **gesamte** Prozesslaufzeit über, unabhängig vom tatsächlichen Warte-/Arbeits-Substatus.

  **Fix:** Neues Domain-Enum `AufgabeLaufStatus` (`Laeuft`/`WartetAufEingabe`,
  `Domain/Enums/AufgabeLaufStatus.cs`) und neues nullable Feld `Aufgabe.LaufStatus` (Migration
  `202607100001_AddAufgabeLaufStatus`, additive Spalte, `HasConversion<string>()`) — bewusst **nicht**
  `AufgabeStatus.Wartend` wiederverwendet, da dieser Wert bereits "CLI hat Rate-Limit erreicht" bedeutet
  (anderer Lebenszyklus-Zustand mit eigener Transitions-Validierung) und eine Vermischung entweder die
  Statusmaschine verletzt oder die Rate-Limit-Semantik verwässert hätte. `CliProcessManager` abonniert beim
  Start (`CliProcessStatus.Gestartet`) jetzt zusätzlich `PseudoConsoleSession.RuntimeStatusChanged` der
  zugehörigen ConPTY-Sitzung (`KiAusfuehrungsService.GetPseudoConsoleSession`) und persistiert jeden
  Statuswechsel über die neue Methode `AufgabeService.AktualisiereLaufStatusAsync`; beim Stopp/Fehler wird
  die Registrierung sauber abgemeldet und `LaufStatus` zusammen mit `AktiveRunId` zurückgesetzt
  (`AufgabeService.AktivenLaufBeendenAsync`). `KiAusfuehrungsStatusConverter` unterscheidet nun: solange
  `AktiveRunId` aktiv ist, liefert `LaufStatus == WartetAufEingabe` "⏸ Wartet", sonst weiterhin "▶ Läuft"
  (auch wenn `LaufStatus` null ist, z. B. beim klassischen Start ohne ConPTY). Der bestehende 5s-
  `DispatcherTimer`-Poll in `MainWindowViewModel` (unverändert) holt den neuen Substatus automatisch mit ab,
  ohne dass an der Hybrid-Aktualisierung selbst etwas geändert werden musste.

  **Tests:** Neue Tests `KiAusfuehrungsStatusConverterTests` (2 neue Fälle: Wartet trotz laufendem Prozess,
  Läuft bei explizitem `Laeuft` bzw. `null`), `AufgabeServiceTests_AktiverLauf` (neue Testklasse, Issue-108-
  Substatus-Tests aus `AufgabeServiceTests` ausgelagert gemäß Testklassen-Struktur-Konvention; 6 Tests inkl.
  vollem Zyklus Bereit→Läuft→Wartet→Bereit) und `CliProcessManagerTests_LaufStatus` (neue Testklasse, 2 Tests
  über die echte Produktionsverdrahtung: injiziert eine reale `PseudoConsoleSession` in
  `KiAusfuehrungsService`, löst `RefreshRuntimeStatus()` deterministisch per Reflection aus und prüft, dass
  `CliProcessManager` den Wechsel korrekt persistiert bzw. nach Stopp keine verspäteten Events mehr
  verarbeitet).

  **Verifikation:** Der volle Solution-Build (`dotnet build Softwareschmiede.slnx`) sowie `Softwareschmiede.
  Tests`/`Softwareschmiede.App` konnten in dieser automatisierten Umgebung nicht gebaut werden, weil eine
  laufende `Softwareschmiede.App.exe`-Instanz (vermutlich die Host-Instanz dieser Claude-Code-Session) ihre
  eigene Build-Ausgabe blockiert hat — diese Instanz wurde auf ausdrückliche Anweisung nicht angerührt.
  Ersatzweise verifiziert: (1) `dotnet build src/Softwareschmiede/Softwareschmiede.csproj` — 0 Fehler (Domain/
  Application/Infrastructure inkl. Migration); (2) `dotnet build`+`dotnet test` für
  `Softwareschmiede.IntegrationTests` — 0 Fehler, 85/85 Tests grün (läuft gegen den echten DbContext mit der
  neuen Spalte); (3) alle neuen/geänderten Testdateien (`KiAusfuehrungsStatusConverterTests`,
  `AufgabeServiceTests_AktiverLauf`, `CliProcessManagerTests_LaufStatus`) wurden zusätzlich in einem isolierten
  Testprojekt kompiliert und ausgeführt, das exakt dieselbe SDK-/TFM-/WPF-/Implicit-Usings-/
  `InternalsVisibleTo`-Konfiguration wie `Softwareschmiede.Tests` nachbildet (gleicher Assembly-Name, um
  `internal`-Zugriff auf `PseudoConsole`/`PseudoConsoleSession` zu erhalten) — alle 15 Tests grün. Volle
  Verifikation von `MainWindowViewModelTests` (unverändert, da `MainWindowViewModel.cs` in diesem Fix nicht
  angefasst wurde) und `E2E_ArbeitsstatusAktualisierung` (nicht erweitert — der bestehende E2E-Test deckt den
  Rückweg Läuft→Wartet nicht ab; eine Erweiterung würde eine echte interaktive Desktop-Session mit FlaUI
  benötigen, die in dieser Umgebung ohnehin schon für den vorherigen Fix nicht verfügbar war) steht auf einer
  Maschine mit freiem Solution-Build noch aus.
