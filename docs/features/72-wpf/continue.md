# Offene Aufgaben

Erstellt am: 2026-06-13
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 2: 10 Befunde → Iteration 3: ebenfalls 10 Befunde; Code-Review findet neue Folgebefunde nach jeder Korrektur)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

(Kein Plan-Review durchgeführt – kein plan.md für diesen Fortsetzungslauf)

## Code-Review-Befunde

- [ ] **Hoch** `WpfAudioService.cs:54` — `args.ErrorException` im `MediaFailed`-Handler ohne Null-Check; bei bestimmten WPF-Medienfehlern ist `ErrorException` null → `NullReferenceException` im Dispatcher → `TaskCompletionSource` bleibt unresolved → `PlayAudioAsync` hängt für immer
- [ ] **Hoch** `AufgabeDetail.razor.cs:219` — `_streamingLines` wird nie befüllt; neue `KiAusfuehrungsService`-Implementierung verwaltet nur Process-Handles ohne Ausgabe-Callbacks → `IsStreamingContainerVisible` dauerhaft `false`, Live-Ausgabe-Panel permanent ausgeblendet (Regression)
- [ ] **Hoch** `CliSessionService.cs:130` — `ICliSessionService` hat kein `StopAsync`/`Dispose`; Background-Loops und Child-Prozess werden beim Host-Shutdown nicht aufgeräumt
- [ ] **Mittel** `AufgabeService.AbschliessenAsync` — löscht `BranchName` und `LokalerKlonPfad` nicht mehr aus der DB; nach Abschluss zeigt Entität auf gelöschtes Verzeichnis
- [ ] **Mittel** `WpfAudioService.cs:72` — Race: `Aborted`-Event wird nach `InvokeAsync()` subscribed; bei Dispatcher-Shutdown zwischen den beiden Zeilen bleibt `tcs` unresolved
- [ ] **Mittel** `WpfBannerService.cs:44` — AppId `"Softwareschmiede"` nicht als Windows-AUMID registriert → Toast-Feature schlägt auf allen nicht-paketierten Installationen lautlos fehl
- [ ] **Niedrig** `AufgabeDetail.razor.cs` — 18-zeiliger Prompt-Lade-Block doppelt in zwei Lifecycle-Methoden
- [ ] **Niedrig** `AufgabeDetail.razor.cs:ResolveCliName` — Plugin-Namen weiterhin via fragile Substring-Checks statt Plugin-seitigem `ProviderDateiPraefix` (Fix aus Iteration 2 unvollständig)
- [ ] **Niedrig** `KiAusfuehrungsService` — CLI-Prozess-Exit mit Fehlercode persistiert keinen `Fehlgeschlagen`-Status in der DB
- [ ] **Niedrig** `ProcessWindowHost.SetAlwaysOnTopFallback` — irreführender Kommentar und hardcodierte 800×600

## Fehlgeschlagene Tests

(Keine – alle 474 Tests bestehen, 5 E2E-Tests korrekt mit Skip markiert)
