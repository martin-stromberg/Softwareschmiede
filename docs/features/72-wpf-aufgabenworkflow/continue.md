# Offene Aufgaben

Erstellt am: 2026-06-17
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 1: 6 offene Punkte, Iteration 2: 15 offene Punkte — Anstieg durch erstmaliges Mitlaufen der E2E-Tests)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. Der Plan ist vollständig umgesetzt.

## Code-Review-Befunde

- [ ] **[Hoch] Race Condition: `AbsichtlichGestoppt` zu spät gesetzt in `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:167`** — `StopCliAsync` setzt das Flag erst nach dem `TryGetValue`-Aufruf; der `Exited`-Handler liest `false` und meldet fälschlicherweise `Fehler` statt `Gestoppt`. Fix: Flag vor `HasExited`-Check und vor dem `try`-Block setzen.
- [ ] **[Hoch] `IsCliRunning = true` wird bei Exception in `StartCliAsync` nicht zurückgesetzt in `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:606`** — Wenn `StartCliAsync` wirft, bleibt `IsCliRunning` dauerhaft `true`; Stop-Button aktiv, Start-Button deaktiviert. Fix: `IsCliRunning` nach Fehler im catch-Block auf `false` setzen.
- [ ] **[Mittel] `AbsichtlichGestoppt` ist nicht `volatile` in `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs:341`** — Kein Cross-Thread-Sichtbarkeitsgarantie auf ARM. Fix: `volatile bool` deklarieren oder `Interlocked` verwenden.
- [ ] **[Mittel] `IsCliRunning = false` off-Dispatcher und bedingungslos in `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:338`** — Wird auf Task-Thread gesetzt ohne `_dispatcherInvoke`, auch wenn Stop-Vorgang intern scheitert. Gleiches Problem in `PluginWechselAsync:558`. Fix: Nur setzen wenn Stop erfolgreich; via Dispatcher.
- [ ] **[Mittel] `selectedScmPluginPrefix: null` übergeben in `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:208`** — Repos ohne `PluginTyp` erhalten lautlos den ersten alphabetischen SCM-Plugin. Fix: SCM-Plugin-Auswahl durchreichen oder Fallback explizit machen.
- [ ] **[Niedrig] CancellationToken in `PluginSelectionDialogService` nur am Anfang geprüft in `src/Softwareschmiede.App/Services/PluginSelectionDialogService.cs:17`** — `Dispatcher.Invoke` blockiert ohne Timeout/Cancellation; bei App-Shutdown hängt die Sequenz. Fix: Dispatcher-Timeout oder separaten Cancel-Mechanismus einbauen.
- [ ] **[Niedrig] `TaskDetailViewModelTestFactory` verdrahtet `EntwicklungsprozessService` ohne `KiAusfuehrungsService` in `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs:29`** — Latente Falle: Aufruf von `StartenCommand` wirft `InvalidOperationException`. Fix: `KiAusfuehrungsService`-Instanz auch an `EntwicklungsprozessService` übergeben.

## Fehlgeschlagene Tests

- [ ] `AufgabeRecoveryServiceTests.RecoverManuellAsync_ShouldAllowExactlyOneSuccess_WhenTriggeredInParallel` — Pre-existing Race-Condition-Flake (Expected 1, found 2).
- [ ] `WpfE2ETests.DarkModeAktivierenUndPersistieren_E2E` — Assertion-Fehler: Expected "Light", Actual "Dark". Zustandsüberrest aus vorherigem Testlauf (Dark-Mode wird OS-weit persistiert).
- [ ] `WpfE2ETests.EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E` — Null-Assertion-Fehler. Vermutlich Zustandsüberrest.
- [ ] `E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E` — TimeoutException (15s). Timing-Problem beim Vollsuite-Lauf; in Isolation stabil.
- [ ] `E2E_AutoStartCli.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E` — TimeoutException (15s). Timing-Problem beim Vollsuite-Lauf.
- [ ] `E2E_PluginProjectDefault.PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E` — TimeoutException (15s). Timing-Problem beim Vollsuite-Lauf.
- [ ] `E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E` — TimeoutException (10s). Timing-Problem beim Vollsuite-Lauf.
- [ ] `E2E_PluginSelectionDialog.StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E` — TimeoutException (15s). Timing-Problem beim Vollsuite-Lauf.
- [ ] `E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E` — TimeoutException (15s). Timing-Problem beim Vollsuite-Lauf; in Isolation stabil.
