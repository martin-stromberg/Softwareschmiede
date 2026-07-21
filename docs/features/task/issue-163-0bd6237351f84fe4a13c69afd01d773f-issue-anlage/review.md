# Plan-Review

Datum: 2026-07-21
Status: Vollstaendig umgesetzt

## Gepruefte Punkte

- Die Korrektur beschraenkt sich auf den in `continue-done.2.md` benannten PR-Test `AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning`.
- Der Test synchronisiert den ersten Refresh deterministisch bis in eine kontrolliert blockierende Test-Datenquelle, bevor der zweite parallele Aufruf gestartet wird.
- Die Absicherung prueft die Re-Entrancy-Semantik direkt: Der zweite Aufruf wird uebersprungen und loest keinen zweiten Abruf der verzögerten Datenquelle aus.
- Das vorherige absolute Laufzeitlimit von 250 ms wurde entfernt. Verbleibende 5-Sekunden-Timeouts dienen nur als Deadlock-/Fehlergrenze fuer den Testablauf.
- Die Produktivlogik wurde nicht veraendert; `MainWindowViewModel.AktiveAufgabenAktualisierenAsync()` verwendet weiterhin `SemaphoreSlim.WaitAsync(0, ct)`. Ergaenzt wurde nur die kleine Schnittstelle `IAktiveAufgabenService`, damit der Test den Datenquellenabruf direkt zaehlen kann.

## Offene Aufgaben

Keine.
