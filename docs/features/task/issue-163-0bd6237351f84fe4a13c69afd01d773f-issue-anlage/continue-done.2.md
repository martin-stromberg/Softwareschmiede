# Offene Aufgaben

Erstellt am: 2026-07-21
Abbruchgrund: Korrekturrueckmeldung aus PR-Testlauf

Die folgenden Aufgaben muessen in einem Lifecycle-Fortsetzungslauf bearbeitet werden.

## Offene Planelemente

- [x] Fehlgeschlagenen PR-Test `AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning` korrigieren. Der Test ist timing-sensitiv und erwartet aktuell, dass der zweite Aufruf unter ca. 250 ms abgeschlossen ist; im GitHub-Runner dauert er etwa 820 ms.
- [x] Die Ursache als Test-Race/CI-Timing-Problem analysieren und den Test robuster machen. Bevorzugt soll nicht nur ein absoluter Grenzwert erhoeht werden, sondern die Re-entrancy-Semantik direkt nachgewiesen werden: Ein paralleler zweiter Aufruf muss uebersprungen werden und darf keine zweite Abfrage der verzoegerten Datenquelle ausloesen.
- [x] Falls ein Zeitlimit weiter noetig ist, soll es CI-tauglich sein und nicht die eigentliche Verhaltensabsicherung ersetzen.
- [x] Den betroffenen Test lokal ausfuehren und, soweit sinnvoll, den passenden fokussierten Testlauf fuer die umgebende ViewModel-/Service-Klasse ergaenzen.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

- [x] `AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning` — timing-sensitive Assertion im PR/GitHub-Runner; zweiter Aufruf dauerte ca. 820 ms statt erwarteter < 250 ms.
