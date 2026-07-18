# Offene Aufgaben

Erstellt am: 2026-07-18
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] `WpfTestBase.cs` — Doppelter Code: `SetupProjectMitNeuerAufgabe` (Zeilen 549–553) dupliziert den Inline-FlaUI-Block, der jetzt in `NeueAufgabeAnlegen` (Zeilen 667–673) gekapselt ist. Ersetzen durch Aufruf von `NeueAufgabeAnlegen(mainWindow);`.
- [ ] `WpfTestBase.cs` — Verhalten/Kommentar-Mismatch: `ProjektNamenAendernUndSpeichern` (Zeilen 748–757) wartet mit `WaitForElement(... "Speichern", Short)` auf ein „Wiedererscheinen", das im UpdateAsync-Pfad nie eintritt (Button verschwindet nicht) — keine echte Synchronisation auf Speicherabschluss, potentieller Race in nachfolgenden Asserts (z. B. `ProjectDetailE2ETests.cs` Zeilen 92–93). Auf ein Element warten, das den Abschluss tatsächlich signalisiert, oder Doku an reales Verhalten anpassen.
- [ ] `WpfTestBase.cs` — Inkonsistente Helper-API / fehlende Vorbedingungsvalidierung: `ErsteOffeneAufgabeOeffnen(AutomationElement[] items)` (Zeilen 723–726) arbeitet auf anderer Abstraktionsebene als `AufgabeAusListeOeffnen(mainWindow, titel)` und wirft bei leerem Array eine kontextlose `IndexOutOfRangeException`. Entweder `mainWindow`-basierte Überladung ergänzen oder Vorbedingung fail-fast prüfen.
- [ ] `E2E_PluginSelectionDialog.cs` — Doppelter Code: „Phase OK" (Zeilen 56–64) implementiert den Ablauf von `StartenUndPluginWaehlen` (WpfTestBase.cs:578–589) inline nach, obwohl der Plan diesen Helfer ausdrücklich vorsieht. Ersetzen durch `StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");`.
- [ ] `E2E_TaskWechselUeberMenue.cs` — Irreführende Kommentare (Copy-Paste-Fehler): Zeilen 99 und 104 beschreiben noch „Aufgabe B", obwohl der Code auf Aufgabe A prüft (`pidA`/`TitelA`). Kommentare korrigieren.
- [ ] Mehrere Testklassen — Immer-wahre Assertions: `Assert.NotNull(WaitForElement(...))` kann nie fehlschlagen, da `WaitForElement` bei Nichtfinden wirft statt `null` zu liefern. Betroffen u. a.: `E2E_CreateNewTaskNavigation.cs` Zeilen 37, 41; `E2E_TaskDetailNavigation.cs` Zeilen 38, 47; `E2E_PluginSelectionDialog.cs` Zeilen 49, 68; `E2E_PluginProjectDefault_NextTask.cs` Zeile 68; `E2E_AutoStartCli.cs` Zeilen 47, 58; `ProjectDetailE2ETests.cs` Zeilen 48, 83, 88, 243, 251, 262. Entfernen oder durch fachlich aussagekräftige Prüfung ersetzen.
- [ ] `ProjectDetailE2ETests.cs` — Fehlende Wiederverwendung: `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E` (Zeilen 154–157) ermittelt offene Aufgaben inline statt über den neuen Basishelfer `OffeneAufgabenItems(mainWindow)`. Umstellen auf `var offeneItems = OffeneAufgabenItems(mainWindow);`.

## Fehlgeschlagene Tests

Keine.
