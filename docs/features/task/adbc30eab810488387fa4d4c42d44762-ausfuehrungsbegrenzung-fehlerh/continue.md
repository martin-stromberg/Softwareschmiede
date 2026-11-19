# Offene Aufgaben

Erstellt am: 2026-11-19
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — Plan-Review meldet `Vollständig umgesetzt`.

## Code-Review-Befunde

- [x] `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (Testqualität / Testisolation, Zeile 201): `ScmEventExecutionLimitService.ReservierungenZuruecksetzen()` in `InitializeAsync` leert den prozessweit statischen Reservierungsbestand — im `Softwareschmiede.IntegrationTests`-Projekt (keine Parallelisierungs-Serialisierung, ~20 Fixture-Klassen parallel) kann der Reset auch lebende Reservierungen parallel laufender Tests leeren. Verwundbar insb. `Dispatch_ConcurrentStartDuringHealthCheck_GlobalLimitOne_ExactlyOneStarts`. Empfehlung: Fixture-basierte Integrationstestklassen serialisieren (gemeinsame xUnit-Collection oder `ParallelMode.None`), alternativ Reset auf nicht-lebende Reservierungen einschränken. **Gelöst:** Statischer Bestand in den DI-Singleton `ScmStartPlatzReservierungsPool` ausgelagert (neu: `src/Softwareschmiede/Application/Services/ScmStartPlatzReservierungsPool.cs`); jede Fixture erhält über ihren eigenen Container einen eigenen Bestand — `ReservierungenZuruecksetzen()` entfernt.

## Fehlgeschlagene Tests

- [ ] `Softwareschmiede.Tests.App.ViewModels.KonsolenTestViewModelTests.WiedergabeNeustarten_AusSchrittmodus_SpieltAbPosition0` — `Expected sut.IstWiedergabeAktiv to be True, but found False` (dokumentierter Umgebungs-Flake, isoliert grün, kein Bezug zur Änderung)
- [ ] `Softwareschmiede.Tests.App.Controls.TerminalControlTests.OnPreviewKeyDown_MausankerDannShiftRechts_ZeichnetAuswahlUndKopiertText` — `COMException: OpenClipboard fehlgeschlagen (CLIPBRD_E_CANT_OPEN)` (Umgebungsproblem der Sandbox, deterministisch, kein Bezug zur Änderung)
