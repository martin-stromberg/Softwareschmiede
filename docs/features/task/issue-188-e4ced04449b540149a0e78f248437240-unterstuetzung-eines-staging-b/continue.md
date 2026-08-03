# Offene Aufgaben

Erstellt am: 2026-08-03
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` bestätigt Status "Vollständig umgesetzt".

## Code-Review-Befunde

Keine — `review-code.md` (Iteration 3) bestätigt Status "Keine Befunde". Alle Befunde aus
Iteration 1 (6 Stück) und Iteration 2 (3 Stück) wurden behoben und jeweils unabhängig
verifiziert (siehe `review-code.1.md`, `review-code.2.md`).

## Fehlgeschlagene Tests

- [ ] `Softwareschmiede.Tests.E2E.E2E_PluginAktivierung.PluginAktivierung_ValidierungPersistenzUndSinglePluginVerhalten_E2E` — `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.` in `WpfTestBase.WaitForElement`, ausgelöst über `E2E_PluginAktivierung.DeaktivierenVonDreiKiPlugins_PersistiertUndBlendetAuswahlAus_E2E` (Zeile 129). Reproduziert in zwei unabhängigen Testläufen mit identischem Fehlerbild. Die Testdatei `E2E_PluginAktivierung.cs` wurde in diesem Branch nicht verändert; die einzige Änderung an der gemeinsam genutzten Basisklasse `WpfTestBase.cs` ist rein additiv (zwei neue Hilfsmethoden `OpenRepositoryAssignDialog`/`WaitForFirstRepositoryItem`, keine Änderung an `WaitForElement` oder anderen von diesem Test genutzten Codepfaden). Deutet auf vorbestehende Instabilität dieses konkreten E2E-Tests in dieser Sandbox hin, nicht auf eine Regression durch die Basis-Branch-Implementierung — sollte dennoch isoliert auf einem sauberen `main`-Checkout gegengeprüft werden, bevor der PR abgeschlossen wird.

- [x] `Softwareschmiede.Tests.E2E.E2E_WorkingDirectory.RepositoryZuweisung` — schlug in einem Lauf mit `System.TimeoutException` in `StartenUndPluginWaehlen`/`SelectComboBoxItemByClick` fehl, lief bei isoliertem Re-Run direkt danach fehlerfrei durch. Bestätigte Flakiness, kein Handlungsbedarf.
