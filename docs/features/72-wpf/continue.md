# Offene Aufgaben

Erstellt am: 2026-06-13
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 2 durch Session-Limit unterbrochen; Befunde stammen aus Altlasten im Branch-Diff)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

(Kein Plan-Review durchgeführt – kein plan.md vorhanden für diesen Fortsetzungslauf)

## Code-Review-Befunde

- [ ] **Kritisch** `AufgabeDetail.razor.cs:68` — Referenzen auf entfernte Enum-Werte (Offen, InBearbeitung, KiAktiv, …) und nicht mehr existierende Service-Methoden (KiAbgeschlossenAsync, UpdateAsync mit 6 Parametern) → CS0117/CS1061-Kompilierfehler, Blazor-Projekt startet nicht
- [ ] **Kritisch** `AufgabeDetail.razor.cs:1703` — Aufruf von `EntwicklungsprozessService.AbbrechenAsync`, das vollständig entfernt wurde → CS1061, Abbrechen-Button defekt
- [ ] **Hoch** `KiAusfuehrungsService.cs:73` — `_handles[aufgabeId] = handle` wird vor `process.Start()` gesetzt; schlägt Start fehl, verbleibt ein ungültiger Handle im Dictionary
- [ ] **Hoch** `KiAusfuehrungsService.cs:63` — Exited-Handler emittiert immer `Gestoppt` unabhängig vom Exit-Code; Abstürze sind von sauberem Stop ununterscheidbar
- [ ] **Hoch** `CliSessionService.cs:62` — `!_process.HasExited` als Schleifenbedingung statt EOF → gepufferte Ausgabe-Zeilen gehen bei schnellem Prozessende verloren
- [ ] **Hoch** `CliSessionService.cs:62` — Stdout ohne Prozessende geschlossen → `ReadLineAsync()` gibt dauerhaft null → 100 % CPU-Spin
- [ ] **Mittel** `server.js:23` — `["-NoExit"]` auf allen Plattformen; auf Linux/macOS kein Shell-Flag → PTY startet nicht
- [ ] **Mittel** `AufgabeService.cs:220` — `DeleteAsync` ohne Status-Guard → aktive Aufgaben (InArbeit, Gestartet) löschbar ohne Prozessstopp
- [ ] **Mittel** `ProcessWindowHost.cs:106` — `GetWindowLong` Fehlerbehandlung fehlt; `SetWindowLong` wird bedingungslos mit potentiell invalidem Wert aufgerufen
- [ ] **Niedrig** `TaskDetailViewModel.cs:169` — Event-Subscription auf Singleton-Service nur in `Dispose` abgemeldet → Memory Leak bei WPF-Navigation

## Fehlgeschlagene Tests

- [ ] **ProduktErstellenUndAufgabeHinzufuegen_E2E** — Softwareschmiede.App.exe wurde nicht gefunden. App-Projekt muss vor dem Testlauf gebaut werden.
- [ ] **AufgabeStarten_RepositoryKlonen_BranchErstellen_E2E** — Softwareschmiede.App.exe wurde nicht gefunden.
- [ ] **CliProzessStartenUndFensterEinbetten_E2E** — Softwareschmiede.App.exe wurde nicht gefunden.
- [ ] **DarkModeAktivierenUndPersistieren_E2E** — Softwareschmiede.App.exe wurde nicht gefunden.
- [ ] **RecoveryBannerNachHeartbeatTimeout_E2E** — Softwareschmiede.App.exe wurde nicht gefunden.
