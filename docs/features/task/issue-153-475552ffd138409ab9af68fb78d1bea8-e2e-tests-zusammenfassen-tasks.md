# Tasks: E2E-Test-Struktur zur Reduktion von Testlaufzeiten optimieren

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Hilfsmethoden | `NeueAufgabeAnlegen(mainWindow)` in `WpfTestBase` anlegen (klickt `AufgabeNeu`, wartet auf `EditTitel`, liefert TextBox) | Offen | — |
| 2 | Hilfsmethoden | `AufgabeTitelSetzen(mainWindow, titel)` in `WpfTestBase` anlegen (Ctrl+A + Tippen in `EditTitel`) | Offen | — |
| 3 | Hilfsmethoden | `AufgabeDetailSpeichern(mainWindow)` in `WpfTestBase` anlegen (klickt `Speichern`, wartet auf `ProjektName`) | Offen | — |
| 4 | Hilfsmethoden | `AufgabeDetailZurueck(mainWindow)` in `WpfTestBase` anlegen (klickt `Zurück`, wartet auf `ProjektName`) | Offen | — |
| 5 | Hilfsmethoden | `OffeneAufgabenItems(mainWindow)` in `WpfTestBase` anlegen (liefert `ListItem`-Kinder der `OffeneAufgabenListe`) | Offen | — |
| 6 | Hilfsmethoden | `ErsteOffeneAufgabeOeffnen(mainWindow)` in `WpfTestBase` anlegen (öffnet erstes Item per Doppelklick) | Offen | — |
| 7 | Hilfsmethoden | `AufgabeAusListeOeffnen(mainWindow, titel)` in `WpfTestBase` anlegen (öffnet benannte Aufgabe per Doppelklick, wartet auf `Zurück`) | Offen | — |
| 8 | Hilfsmethoden | `ProjektNamenAendernUndSpeichern(mainWindow, neuerName)` in `WpfTestBase` anlegen (Ctrl+A + Tippen in `ProjektName`, speichern) | Offen | — |
| 9 | E2E-Tests | `E2E_CreateNewTaskNavigation`: beide `[Fact]` zu einer konsolidierten Methode zusammenführen; Listenlängen-Assert auf „≥ 2" | Offen | — |
| 10 | E2E-Tests | `E2E_TaskDetailNavigation`: drei `[Fact]` zu einer konsolidierten Methode zusammenführen | Offen | — |
| 11 | E2E-Tests | `E2E_PluginSelectionDialog`: beide Methoden zu einer `[SkippableFact]` (Abbrechen + OK) zusammenführen | Offen | — |
| 12 | E2E-Tests | `ProjectDetailE2ETests`: vier Navigations-/Zurück-Tests zu einer Methode „Projekt-Navigation" zusammenführen | Offen | — |
| 13 | E2E-Tests | `ProjectDetailE2ETests`: zwei Umbenennungs-/Bearbeiten-Tests zu einer Methode „Projekt bearbeiten" zusammenführen (nutzt `ProjektNamenAendernUndSpeichern`) | Offen | — |
| 14 | E2E-Tests | `ProjectDetailE2ETests`: `AufgabeNeuAnlegen_...` + `AufgabenFiltern_...` zu einer Methode „Aufgaben in Projektdetail" zusammenführen | Offen | — |
| 15 | E2E-Tests | `ProjectDetailE2ETests`: vier Repository-Dialog-Tests zu einer Methode „Repository-Dialog" zusammenführen | Offen | — |
| 16 | E2E-Tests | `ProjectDetailE2ETests`: `ProjektLoeschen_...` und `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E` eigenständig belassen | Offen | — |
| 17 | E2E-Tests | `E2E_TaskWechselUeberMenue`: `ErstelleUndStarteAufgabe` auf Basishelfer umstellen, `OeffneAufgabeAusListe` entfernen | Offen | — |
| 18 | E2E-Tests | `E2E_PluginProjectDefault_NextTask`: Inline-Blöcke auf `NeueAufgabeAnlegen`/`AufgabeDetailZurueck` umstellen | Offen | — |
| 19 | E2E-Tests | `E2E_AutoStartCli`: Inline-Blöcke auf `AufgabeDetailZurueck`/`OffeneAufgabenItems`/`ErsteOffeneAufgabeOeffnen` umstellen | Offen | — |
| 20 | Verifikation | Vollständiger Build + OS-Interface-/E2E-Lane (`--filter "Category=OsInterface"`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`) grün; reduzierte Methodenanzahl bestätigen | Offen | — |
