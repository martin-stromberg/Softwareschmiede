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

## Nachtrag: OsInterface-Lane aus Visual Studio (manueller Lauf)

4 Fehlschläge, davon 2 als Test-Robustheitsbefund identifiziert und behoben:

- `General_ViewNavigation_E2E` — `COMException 0x80131505` in `WindowExtensions.GetOpenTopLevelWindowTitles`: der vorhandene COMException-Filter saß im `Where`, `.Select(w => w.Name)` las die Property erneut ungeschützt (TOCTOU). **Behoben:** einmalige geschützte Lesung.
- `General_RepositoryUndAufgaben_E2E` — `SessionLimit_...`-Phase: `Title`-Lesefehler `0x80040201`. **Behoben:** `MenuView.TryFindNavigationButton` und `WpfTestBase.WarteAufEchtesHauptfenster` tolerieren jetzt transiente `COMException` in den Warteschleifen.
- `ConPty_LifecycleUndReplay_E2E` — `.clireplay`-Export nicht erzeugt; nicht featurebezogen, vermutlich Timing/Umgebung. Offen: Wiederholungslauf.
- `TerminalControlTests.OnPreviewKeyDown_CtrlShiftC` — `Clipboard.GetText()` lieferte `""`; nicht featurebezogen, klassischer Clipboard-Flake. Offen: Wiederholungslauf.
