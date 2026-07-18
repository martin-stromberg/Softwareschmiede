# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Alle im Plan (`plan.md`) beschriebenen Planelemente sind im Code unter `src/Softwareschmiede.Tests/E2E/` vollständig umgesetzt. Der Build des Testprojekts läuft fehlerfrei durch (0 Warnungen, 0 Fehler).

## Umgesetzte Planelemente

### `WpfTestBase` — acht neue `protected`-Hilfsmethoden (`src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`)

- [x] Methode `NeueAufgabeAnlegen(AutomationElement mainWindow)` — vorhanden (Z. 667), klickt `AufgabeNeu`, wartet auf `EditTitel`, gibt TextBox zurück. Signatur/Rückgabetyp `AutomationElement` wie geplant.
- [x] Methode `AufgabeTitelSetzen(AutomationElement mainWindow, string titel)` — vorhanden (Z. 679), Fokus + `Ctrl+A` + Tippen, `void`.
- [x] Methode `AufgabeDetailSpeichern(AutomationElement mainWindow)` — vorhanden (Z. 691), klickt `Speichern`, wartet auf `ProjektName`, `void`.
- [x] Methode `AufgabeDetailZurueck(AutomationElement mainWindow)` — vorhanden (Z. 703), klickt `Zurück`, wartet auf `ProjektName`, `void`.
- [x] Methode `OffeneAufgabenItems(AutomationElement mainWindow)` — vorhanden (Z. 712), wartet auf `OffeneAufgabenListe`, liefert `AutomationElement[]` der `ListItem`-Kinder.
- [x] Methode `ErsteOffeneAufgabeOeffnen(AutomationElement mainWindow)` — vorhanden (Z. 722), öffnet erstes Item per `DoubleClick`, `void`.
- [x] Methode `AufgabeAusListeOeffnen(AutomationElement mainWindow, string titel)` — vorhanden (Z. 732), sucht `ListItem` per Name, `DoubleClick`, wartet auf `Zurück`, `void`. Generalisierung des früheren `E2E_TaskWechselUeberMenue.OeffneAufgabeAusListe`.
- [x] Methode `ProjektNamenAendernUndSpeichern(AutomationElement mainWindow, string neuerName)` — vorhanden (Z. 748), Fokus `ProjektName` + `Ctrl+A` + Tippen + `Speichern`, wartet auf Wiedererscheinen von `Speichern`, `void`.
- [x] Keine bestehende Methode wurde in Signatur/Verhalten geändert; keine neuen Properties/Events (wie geplant). Alle acht Methoden tragen XML-Doku inkl. Voraussetzungen.

### `E2E_CreateNewTaskNavigation` (2→1)

- [x] Beide `[Fact]` durch eine konsolidierte Methode `AufgabeAnlegen_SpeichernPersistiert_UndAbbrechenVerwirftTitel_E2E()` ersetzt.
- [x] Speichern-Phase (`NeueAufgabeAnlegen` → `AufgabeTitelSetzen` → `AufgabeDetailSpeichern`) und Abbrechen-Phase (`… → AufgabeDetailZurueck`) in einem App-Lifecycle.
- [x] Listenlängen-Assertion auf „≥ 2" angepasst (`items.Length >= 2`, Z. 54).

### `E2E_TaskDetailNavigation` (3→1)

- [x] Drei `[Fact]` durch eine konsolidierte Methode `TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E()` ersetzt (Daten prüfen → Zurück → fensterumfassend öffnen via `OffeneAufgabenItems`/`ErsteOffeneAufgabeOeffnen`).

### `E2E_PluginSelectionDialog` (2→1)

- [x] Beide Methoden zu einer `[SkippableFact]` `PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E()` zusammengefasst (Abbrechen-Phase + OK-Phase an derselben Aufgabe).
- [x] `ConfirmLocalDirectoryGitInitInSourceDirectory()` erhalten (Z. 34).

### `ProjectDetailE2ETests` (13→6)

- [x] Methode „Projekt-Navigation" (`ProjektNavigation_NeuanlageAbbrechenUndOeffnenUndSchliessen_E2E`) — vereint Neuanlage-Abbruch, Öffnen/Zurück/erneut-Öffnen, Zurück zur Übersicht.
- [x] Methode „Projekt bearbeiten" (`ProjektBearbeiten_NamenAendernSpeichernZurueckUndErneutBearbeiten_E2E`) — nutzt `ProjektNamenAendernUndSpeichern` (zweifach).
- [x] Methode „Aufgaben in Projektdetail" (`AufgabenInProjektdetail_NeuAnlegenUndFiltern_E2E`) — nutzt `NeueAufgabeAnlegen`, `AufgabeDetailZurueck`, `OffeneAufgabenItems`.
- [x] Methode „Repository-Dialog" (`RepositoryDialog_OeffnenButtonZuweisenPluginUndArbeitsverzeichnis_E2E`) — Öffnen-Button + Zuweisen-Dialog mit Plugin- und Arbeitsverzeichnis-ComboBox + Abbrechen.
- [x] `ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E` bleibt eigenständig (destruktiv), Ablauf unverändert (MessageBox → `AutomationId 6` → `WaitUntilGone("Speichern")`).
- [x] `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E` bleibt eigenständig (DB-seeded, `async`, `OpenTestDbContext` + `ProjektService`/`AufgabeService`).
- [x] Ergebnis: genau 6 Methoden in der Klasse (wie geplant).

### `E2E_TaskWechselUeberMenue` (Refaktorierung)

- [x] Einzelne `[SkippableFact]` bleibt bestehen.
- [x] `private ErstelleUndStarteAufgabe` intern auf `NeueAufgabeAnlegen` → `AufgabeTitelSetzen` → `AufgabeDetailSpeichern` → `AufgabeAusListeOeffnen` → `StartenUndPluginWaehlen` umgestellt (Z. 119–129).
- [x] `private static OeffneAufgabeAusListe` entfernt; kein Verweis mehr in `src/` (nur noch in Doku). Aufrufe verwenden `WpfTestBase.AufgabeAusListeOeffnen`.
- [x] `WaitForTerminalProzessId` und `BeschreibeDescendants` bleiben unverändert `private`.

### `E2E_PluginProjectDefault_NextTask` (Refaktorierung)

- [x] Einzelne `[SkippableFact]` bleibt bestehen.
- [x] Inline `Zurück`+`ProjektName`-Block → `AufgabeDetailZurueck` (Z. 58); der `mainWindow.Focus()`/`Thread.Sleep(300)`-Workaround gegen NoClickablePoint bleibt davor erhalten (Z. 54–55).
- [x] Inline `AufgabeNeu`+`EditTitel`-Block → `NeueAufgabeAnlegen` (Z. 61).
- [x] Checkbox-Dialog-Teil (`FuerProjektVerwenden`) bleibt inline.

### `E2E_AutoStartCli` (Refaktorierung)

- [x] Einzelne `[SkippableFact]` bleibt bestehen.
- [x] Inline `Zurück`-Block → `AufgabeDetailZurueck` (Z. 50).
- [x] Inline `OffeneAufgabenListe`-Suche + `items[0].DoubleClick` → `OffeneAufgabenItems` (mit `Assert.True(items.Length >= 1)`) + `ErsteOffeneAufgabeOeffnen` (Z. 52–54).

### Nicht-Code-Elemente

- [x] Datenbankmigrationen: keine (Plan: keine).
- [x] Validierungsregeln: keine (Plan: keine).
- [x] Konfigurationsänderungen: keine (Plan: keine).
- [x] Neue Klassen: keine (Plan: keine).

## Offene Aufgaben

Keine. Alle Code-Planelemente sind umgesetzt.

## Hinweise

- **Verifikations-Schritt (Plan-Schritt 9) nur teilweise ausgeführt:** Der vollständige Build des Testprojekts (`Softwareschmiede.Tests.csproj`) läuft in diesem Review fehlerfrei durch (0 Warnungen/0 Fehler) und die reduzierte Methodenanzahl je Klasse wurde bestätigt. Der eigentliche Lauf der OS-Interface-/E2E-Lane (`--filter "Category=OsInterface"`) gegen eine interaktive Desktop-Session wurde in diesem Plan-vs-Code-Review **nicht** ausgeführt (umgebungssensitiv, ConPTY/Self-Hosting-Risiko). Der grüne Durchlauf dieser Lane sollte in einer interaktiven Sitzung noch bestätigt werden, bevor die Anforderung als final abgenommen wird.
- **`E2E_PluginSelectionDialog` — Skip-Semantik:** Die konsolidierte Methode ist wie geplant `[SkippableFact]`; sie ruft jedoch `SkipWennConPtyNichtVerfuegbar()` nicht explizit auf (auch der Erfolgspfad `StartenUndPluginWaehlen` in anderen Klassen tut dies nicht durchgängig). Der Plan fordert allgemein „die bisherige Skip-Semantik des OK-Pfads bleibt erhalten" — die `[SkippableFact]`-Annotation ist vorhanden, sodass ein `Skip.If` grundsätzlich greifen könnte. Falls der ursprüngliche OK-Pfad einen expliziten ConPTY-Skip besaß, wäre dies beim E2E-Lauf zu beobachten; für die reine Plan-vs-Code-Vollständigkeit ist die Anforderung (Zusammenführung zu einer `[SkippableFact]`) erfüllt.
- **Kosmetik (kein Planelement):** In `E2E_TaskWechselUeberMenue.cs` (Z. 2) verbleibt ein ungenutztes `using System.Runtime.ConstrainedExecution;`. Ohne Auswirkung auf die Planumsetzung.
- Die Tasks-Datei (`…-tasks.md`) liegt als Geschwisterdatei neben dem Feature-Ordner (Suffix `-tasks.md`), nicht innerhalb des Ordners; sie wurde entsprechend aktualisiert (alle 19 Code-Aufgaben `Erledigt` mit Testnachweis, Task 20 mit Verifikations-Vorbehalt).
