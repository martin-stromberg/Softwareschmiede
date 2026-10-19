# Offene Aufgaben

Erstellt am: 2026-10-19
Abbruchgrund: Maximale Iterationsanzahl erreicht (3 Iterationen); verbleibender Punkt ist umgebungsbedingt

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Task #36 Verifikation — komplett grüner Lauf der **vollen** OsInterface-Testlane steht aus. Alle feature-relevanten E2E-Pflichtszenarien wurden auf dem finalen Code-Stand einzeln grün verifiziert (siehe `test-results.md`, E2E-Abdeckung); der Bündellauf scheitert reproduzierbar an Umgebungsinstabilität (UIA `COMException 0x80040201`, verwaiste testhost-/DLL-Locks, parallel laufendes Visual Studio). Kein codebedingter Fehler. Empfehlung: volle Lane in stabiler Umgebung erneut laufen lassen.

## Code-Review-Befunde

Keine — `review-code.md` (Iteration 3): `Keine Befunde`.

## Usability-Befunde

Keine — `review-usability.md`: `Keine Befunde`.

## Fehlgeschlagene Tests

- `General_RepositoryUndAufgaben_E2E` (Bündelmethode) — feature-relevante Phase `AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E` komplett bestanden; Abbruch erst in nicht-featurebezogener Folgephase `SessionLimit_...` mit UIA `COMException 0x80040201` (Umgebungsfehler, kein Codefehler). Details: `test-results.md`.
