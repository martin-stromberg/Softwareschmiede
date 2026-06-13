# Offene Aufgaben

Erstellt am: 2026-06-13
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [x] E2E-Test: Projekt bearbeiten und speichern — `ProjectDetailE2ETests.ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E`
- [x] E2E-Test: Projekt löschen — `ProjectDetailE2ETests.ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E`
- [x] E2E-Test: Aufgabe neu anlegen — `ProjectDetailE2ETests.AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E`
- [x] E2E-Test: Aufgaben filtern — `ProjectDetailE2ETests.AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E`
- [x] E2E-Test: Repository zuweisen — `ProjectDetailE2ETests.RepositoryZuweisen_DialogOeffnetUndSchliessbarPerAbbrechen_E2E`
- [x] E2E-Test: Repository öffnen — `ProjectDetailE2ETests.RepositoryOeffnen_ButtonExistiertInDetailansicht_E2E`
- [x] E2E-Test: Zurück zur Übersicht — `ProjectDetailE2ETests.ZurueckZurUebersicht_SchliesstOverlayUndZeigtListe_E2E`

## Code-Review-Befunde

- [ ] `ProjectDetailViewModel.Dispose()` setzt `_selectedTaskViewModel` und `_ladenCts` nicht auf `null` nach dem Entsorgen → doppeltes Dispose möglich (`ObjectDisposedException`)
- [ ] `ProjektSpeichernAsync`: Nach Erstellung bleibt Formular offen, zweiter Speichern-Klick trifft Update-Pfad → stille Doppelspeicherung; Empfehlung: `ZurueckAction?.Invoke()` nach Erstellung aufrufen oder Felder zurücksetzen
- [ ] `RepositoryOeffnenAsync`: Als `async Task` deklariert mit `CancellationToken`, enthält aber nur synchronen Code; `catch (OperationCanceledException)` ist unerreichbar — Methode auf synchrones `void` umstellen oder toten Block entfernen
- [ ] `RepositoryZuweisenAsync`: Nach `ShowDialog()` wird in Felder eines potenziell bereits disposed ViewModels geschrieben — Disposed-Flag prüfen vor Folgeoperationen
- [ ] `LadenCommand = new AsyncRelayCommand(ct => LadenAsync(ct))` — redundante Lambda, direkt `new AsyncRelayCommand(LadenAsync)` verwenden
- [ ] `NeuesProjektHinzufuegen` ist `async void` ohne `CancellationToken` — auf `Func<CancellationToken, Task>` umstellen
- [ ] `ZeigeDetailErstellungsFormularAsync` und `ZeigeDetailAsync` sind synchrone `void`-Methoden mit falschem `Async`-Suffix — umbenennen
- [ ] `RepositoryAssignDialog`: Öffentlicher parameterloser Konstruktor lässt `DataContext = null` — auf `private` setzen
- [ ] `AsyncRelayCommand._isExecuting`-Guard nicht atomar — `volatile int` + `Interlocked.CompareExchange` oder Migration auf `CommunityToolkit.Mvvm.Input.AsyncRelayCommand`

## Fehlgeschlagene Tests

(Keine — alle 483 Tests bestanden, 8 E2E-Tests als erwartet übersprungen)
