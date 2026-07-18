# Umsetzungsplan: E2E-Test-Struktur zur Reduktion von Testlaufzeiten optimieren

## Übersicht

Logisch zusammenhängende E2E-Szenarien werden in jeweils eine Testmethode zusammengefasst, sodass pro Testlauf weniger App-Prozesse gestartet werden müssen. Dafür werden die wiederholten, inline ausgeschriebenen FlaUI-Interaktionen der Aufgaben- und Projektdetailansicht (Anlegen, Titel setzen, Speichern, Zurück, Öffnen, Projekt umbenennen) als fachlich benannte `protected`-Hilfsmethoden in `WpfTestBase` extrahiert. In diesem Durchlauf werden **alle passenden E2E-Klassen** konsolidiert: die drei ursprünglich identifizierten Klassen (`E2E_CreateNewTaskNavigation`, `E2E_TaskDetailNavigation`, `E2E_PluginSelectionDialog`) sowie zusätzlich `ProjectDetailE2ETests`, `E2E_TaskWechselUeberMenue`, `E2E_PluginProjectDefault_NextTask` und `E2E_AutoStartCli`. Betroffen sind ausschließlich Testklassen unter `src/Softwareschmiede.Tests/E2E/`; Produktivcode, Datenbank, Validierung und Konfiguration bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Organisation der neuen Hilfsmethoden | Erweiterung von `WpfTestBase` um neue `protected`-Methoden (kein separates `TaskPageHelper`) | Alle bestehenden Hilfen (`CreateProject`, `OpenProject`, `SetupProjectMitNeuerAufgabe`, `StartenUndPluginWaehlen`) liegen bereits in `WpfTestBase`. Eine eigene Page-Object-Klasse würde die etablierte Konvention brechen und Zugriff auf `WaitForElement`/`Short`/`Medium` umständlich machen. Die neuen Methoden bilden eine schlanke Facade-Schicht über FlaUI (analog Service Layer). |
| Granularität der Hilfsmethoden | Workflow-semantische Methoden, die eine vollständige Anwenderaktion kapseln und das relevante `AutomationElement` zurückgeben (z. B. `NeueAufgabeAnlegen`), keine feinkörnigen `ClickElement(name)`-Wrapper | Entspricht dem vorhandenen Muster (`CreateProject` kapselt Klick + Tippen + Warten). Feinkörnige Wrapper würden die Testmethoden nicht kürzer, sondern nur indirekter machen. |
| Fehlerbehandlung in Hilfsmethoden | Fail-Fast: bei fehlendem Element `TimeoutException` über das bestehende `WaitForElement` werfen (kein Fehler-Rückgabeobjekt) | Konsistent mit dem gesamten bestehenden `WpfTestBase`-Verhalten; die Fail-Fast-Diagnose (Fehlerbanner-Check) in `WaitForElement` bleibt so erhalten. |
| Umgang mit den bisherigen Einzeltests | Vollständige Konsolidierung (Einzelmethoden entfernen, Szenarien in einer Methode mit mehreren granularen Asserts sequenziell durchlaufen) | Erhalten gebliebene Duplikate würden das Laufzeitziel (weniger App-Starts) unterlaufen. Granularität bleibt durch mehrere Assert-Punkte mit aussagekräftigen Meldungen erhalten. |
| Konsolidierungsumfang (geklärt) | **Alle passenden Klassen** dieses Durchlaufs: `E2E_CreateNewTaskNavigation` (2→1), `E2E_TaskDetailNavigation` (3→1), `E2E_PluginSelectionDialog` (2→1), `ProjectDetailE2ETests` (13→6) sowie DRY-Refaktorierung der Einzelmethoden-Klassen `E2E_TaskWechselUeberMenue`, `E2E_PluginProjectDefault_NextTask` und `E2E_AutoStartCli` auf die neuen Hilfsmethoden | Der Anwender hat den zuvor offenen Punkt entschieden: Es sollen jetzt alle Klassen mit demselben Inline-Muster konsolidiert werden. Klassen mit mehreren gleichartigen Szenarien werden methodisch zusammengefasst; Einzelmethoden-Klassen können nicht in der Methodenanzahl reduziert werden, ihr Inline-FlaUI-Code wird aber auf die gemeinsamen Helfer umgestellt (Kern der Anforderung: „UI-Interaktionen in wiederverwendbare Hilfsmethoden auslagern"). |
| Behandlung der Einzelmethoden-Klassen | Keine Methodenzusammenfassung (nur eine Testmethode vorhanden), stattdessen Refaktorierung des Inline-Codes und ggf. der klasseninternen `private`-Helfer auf die neuen `WpfTestBase`-Methoden | Diese Klassen haben nur eine `[SkippableFact]`; ein Zusammenlegen ist nicht möglich/sinnvoll. Der Laufzeitgewinn entsteht bei ihnen nicht durch weniger App-Starts, sondern die Anforderung nach zentralisierten Hilfsmethoden gilt trotzdem. |
| Klasseninterne Helfer in `E2E_TaskWechselUeberMenue` | `OeffneAufgabeAusListe` wird als generalisierter `protected`-Basishelfer `AufgabeAusListeOeffnen(mainWindow, titel)` nach `WpfTestBase` hochgezogen; `WaitForTerminalProzessId` und `BeschreibeDescendants` bleiben `private` in der Klasse | `AufgabeAusListeOeffnen` (Aufgabe per Name aus Liste öffnen) ist ein wiederverwendbares Muster (auch für künftige Adoption); die PID-/Diagnose-Helfer sind spezifisch für das Terminal-Wechsel-Szenario dieser einen Klasse und würden `WpfTestBase` unnötig aufblähen. |
| Repository-Dialog-Prüfungen in `ProjectDetailE2ETests` | Die vier Tests, die denselben „Zuweisen"-Dialog öffnen (`RepositoryZuweisen_...`, `RepositoryZuweisenDialog_ScmPluginListe_...`, `RepositoryZuweisenDialog_ArbeitsverzeichnisAuswahl_...`, `RepositoryOeffnen_...`), werden zu einer Methode zusammengefasst, die den Dialog einmal öffnet und alle Teilprüfungen sequenziell mit granularen Asserts durchführt | Alle vier teilen exakt dasselbe Setup (Projekt anlegen + öffnen) und dieselbe Dialog-Öffnung; die Zusammenfassung spart drei App-Starts ohne Verlust an Prüftiefe. |

## Programmabläufe

### Neue Aufgabe anlegen und Titel setzen (Hilfsmethoden-Ablauf)

1. `NeueAufgabeAnlegen(mainWindow)` klickt den `AufgabeNeu`-Button; die App legt die Aufgabe sofort mit Status „Neu" an und navigiert in die separate `TaskDetailView`.
2. Die Methode wartet auf das `EditTitel`-Feld und gibt es als `AutomationElement` (TextBox) zurück.
3. `AufgabeTitelSetzen(mainWindow, titel)` klickt in das `EditTitel`-Feld, markiert bestehenden Text (`Ctrl+A`) und tippt den neuen Titel.

Beteiligte Klassen/Komponenten: `WpfTestBase`, FlaUI `Keyboard`, `WaitForElement`

### Aufgabe speichern bzw. abbrechen (Rücknavigation)

1. `AufgabeDetailSpeichern(mainWindow)` klickt den `Speichern`-Button in der `TaskDetailView` und wartet auf das Wiedererscheinen von `ProjektName` (Rückkehr zur `ProjectDetailView`).
2. Alternativ verwirft `AufgabeDetailZurueck(mainWindow)` über den `Zurück`-Button und wartet ebenfalls auf `ProjektName`.

Beteiligte Klassen/Komponenten: `WpfTestBase`, `WaitForElement`

### Aufgabe in der Liste öffnen

1. `OffeneAufgabenItems(mainWindow)` wartet auf die `OffeneAufgabenListe` und gibt deren `ListItem`-Kinder zurück.
2. `ErsteOffeneAufgabeOeffnen(mainWindow)` ermittelt über `OffeneAufgabenItems` das erste Item und öffnet es per `DoubleClick`, wodurch die `TaskDetailView` fensterumfassend erscheint.
3. `AufgabeAusListeOeffnen(mainWindow, titel)` sucht das `ListItem` mit dem angegebenen `titel`, öffnet es per `DoubleClick` und wartet auf den `Zurück`-Button (Bestätigung, dass die `TaskDetailView` geladen ist).

Beteiligte Klassen/Komponenten: `WpfTestBase`, FlaUI `ControlType.ListItem`

### Projektnamen ändern und speichern (Hilfsmethoden-Ablauf)

1. `ProjektNamenAendernUndSpeichern(mainWindow, neuerName)` klickt in das `ProjektName`-Feld, markiert bestehenden Text (`Ctrl+A`) und tippt `neuerName`.
2. Klickt `Speichern` (UpdateAsync-Pfad, bleibt in der Detailansicht) und wartet auf das Wiedererscheinen des `Speichern`-Buttons (Ladevorgang abgeschlossen).

Beteiligte Klassen/Komponenten: `WpfTestBase`, FlaUI `Keyboard`, `WaitForElement`

### Konsolidierter Ablauf: Aufgabe anlegen — Speichern vs. Abbrechen (`E2E_CreateNewTaskNavigation`)

1. App + Projekt einmalig starten (`StartAndNavigateToProjects`).
2. Phase Speichern: `NeueAufgabeAnlegen` → `AufgabeTitelSetzen("Persistierte Neue Aufgabe")` → `AufgabeDetailSpeichern`; assert `ProjektName` sichtbar und Titel erscheint in der Liste.
3. Phase Abbrechen: `NeueAufgabeAnlegen` → `AufgabeTitelSetzen("Nicht gespeicherter Titel")` → `AufgabeDetailZurueck`; assert, dass der nicht gespeicherte Titel nicht auftaucht, und dass die Liste beide angelegten Aufgaben (Status „Neu") enthält.

Beteiligte Klassen/Komponenten: `E2E_CreateNewTaskNavigation`, `WpfTestBase`

### Konsolidierter Ablauf: TaskDetail öffnen/Daten/Zurück (`E2E_TaskDetailNavigation`)

1. App + Projekt einmalig starten.
2. `NeueAufgabeAnlegen`; assert `EditTitel`-Text == „Neue Aufgabe" (korrekte Daten).
3. `AufgabeDetailZurueck`; assert `ProjektName` sichtbar (Rücknavigation).
4. `OffeneAufgabenItems` (assert ≥ 1) → `ErsteOffeneAufgabeOeffnen`; assert `Speichern` sichtbar und `ProjektName` nicht mehr vorhanden (fensterumfassende `TaskDetailView`).

Beteiligte Klassen/Komponenten: `E2E_TaskDetailNavigation`, `WpfTestBase`

### Konsolidierter Ablauf: Plugin-Auswahl-Dialog Abbrechen vs. OK (`E2E_PluginSelectionDialog`)

1. `ConfirmLocalDirectoryGitInitInSourceDirectory` setzen (für die spätere OK-Phase mit Klon).
2. `SetupProjectMitNeuerAufgabe(...)` einmalig — es entsteht genau eine Aufgabe im Status „Neu".
3. Phase Abbrechen: `Starten`-Button → `WaitForWindow("KI-Plugin auswählen")` → `Abbrechen`; assert `EditTitel` weiterhin sichtbar und kein `CliStoppen`-Button (Aufgabe bleibt „Neu").
4. Phase OK: erneut `Starten` an derselben (weiterhin „Neu") Aufgabe → Dialog → `SelectComboBoxItemByClick("Softwareschmiede.KiSimulator")` → `OK`; assert `CliStoppen` erscheint (kombinierter Start-Ablauf läuft).

Beteiligte Klassen/Komponenten: `E2E_PluginSelectionDialog`, `WpfTestBase`

### Konsolidierte Abläufe: Projektdetailansicht (`ProjectDetailE2ETests`)

Die bisher 13 Einzeltests werden zu 6 Methoden zusammengefasst. Gruppierung nach gemeinsamem Setup und fachlicher Verwandtschaft:

1. **Projekt-Navigation** (`NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E` + `ProjektOeffnenUndZurueck_ErneutOeffnen_E2E` + `ZurueckZurUebersicht_SchliesstOverlayUndZeigtListe_E2E`): App + Projekt „Bestehendes-Projekt" einmal anlegen; Neuanlage über `Neu` → `Zurück` abbrechen (assert `Speichern` verschwindet); Projekt öffnen → `Zurück` → erneut öffnen; zuletzt `Zurück` zur Übersicht (assert Kachel sichtbar). Nutzt bestehende `CreateProject`/`OpenProject`.
2. **Projekt bearbeiten/umbenennen** (`ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E` + `ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E`): App + Projekt einmal anlegen und öffnen; `ProjektNamenAendernUndSpeichern("...-Aktualisiert")`; `Zurück` (assert Kachel mit neuem Namen); Kachel erneut öffnen; erneut `ProjektNamenAendernUndSpeichern` und assert `ProjektName`-Text bleibt aktualisiert.
3. **Aufgaben in Projektdetail** (`AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E` + `AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E`): App + Projekt einmal anlegen und öffnen; `NeueAufgabeAnlegen` (assert `EditTitel`) → `AufgabeDetailZurueck` → `OffeneAufgabenItems` (assert ≥ 1); danach Filter-Overlay öffnen, RadioButton „Aktiv" wählen, Overlay schließen (assert „Aufgaben filtern" verschwindet).
4. **Repository-Dialog** (`RepositoryZuweisen_...` + `RepositoryZuweisenDialog_ScmPluginListe_...` + `RepositoryZuweisenDialog_ArbeitsverzeichnisAuswahl_...` + `RepositoryOeffnen_ButtonExistiertInDetailansicht_E2E`): App + Projekt einmal anlegen und öffnen; assert `Öffnen`-Button existiert; `Zuweisen` klicken → `WaitForWindow("Repository zuweisen")`; assert Plugin-ComboBox (≥ 1), assert Label „Arbeitsverzeichnis im Repository" + zweite ComboBox (≥ 2); Dialog über `Abbrechen` schließen (assert `Speichern` im Hauptfenster weiterhin sichtbar).
5. **Projekt löschen** (`ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E`): bleibt eigenständig (destruktiv, eigene Projektinstanz); Ablauf unverändert (Löschen → MessageBox „Löschen bestätigen" → Button `AutomationId 6` → `WaitUntilGone("Speichern")`).
6. **Offene/beendete Aufgaben getrennt** (`Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E`): bleibt eigenständig (setzt Vorbedingungen direkt in der DB via `OpenTestDbContext` und `ProjektService`/`AufgabeService`, eigener `async`-Ablauf, eigener Startpfad `LaunchApp`).

Beteiligte Klassen/Komponenten: `ProjectDetailE2ETests`, `WpfTestBase`, `ProjektService`, `AufgabeService`

### Refaktorierter Ablauf: Aufgabenwechsel über Seitenleiste (`E2E_TaskWechselUeberMenue`)

1. Setup unverändert (`ConfirmLocalDirectoryGitInitInSourceDirectory`, `CreateLocalSourceDirectory`, `LaunchApp`, `ConfigureLocalDirectoryPlugin`, `CreateAndOpenProject`, `AssignLocalDirectoryRepository`).
2. Der klasseninterne `private`-Helfer `ErstelleUndStarteAufgabe` wird intern auf die neuen Basis-Methoden umgestellt: `NeueAufgabeAnlegen` → `AufgabeTitelSetzen(titel)` → `AufgabeDetailSpeichern` → `AufgabeAusListeOeffnen(titel)` → `StartenUndPluginWaehlen("Softwareschmiede.KiSimulator")` → warten auf `CliStoppen`.
3. Der bisherige `private static OeffneAufgabeAusListe` entfällt; alle Aufrufe verwenden den neuen `WpfTestBase.AufgabeAusListeOeffnen`.
4. `WaitForTerminalProzessId` und `BeschreibeDescendants` bleiben unverändert `private` in der Klasse; Testlogik (PID-Vergleiche, Seitenleisten-Navigation, Info-Panel) unverändert.

Beteiligte Klassen/Komponenten: `E2E_TaskWechselUeberMenue`, `WpfTestBase`

### Refaktorierter Ablauf: Projekt-Standard-Plugin für Folgeaufgabe (`E2E_PluginProjectDefault_NextTask`)

1. Setup unverändert (`SetupProjectMitNeuerAufgabe(..., useInSourceDirectoryMode: false)`).
2. Erste Aufgabe: `Starten` → Dialog `KI-Plugin auswählen` → `SelectComboBoxItemByClick(...)` → Checkbox `FuerProjektVerwenden` setzen → `OK` (bleibt inline, da einmalig genutztes Checkbox-Muster).
3. Nach `CliStoppen`: `AufgabeDetailZurueck` ersetzt den bisherigen inline `Zurück`-Klick samt Warten auf `ProjektName` (der zwischengeschaltete `mainWindow.Focus()`/`Thread.Sleep(300)` bleibt vor dem `AufgabeDetailZurueck`-Aufruf erhalten, da er das NoClickablePoint-Problem adressiert).
4. Zweite Aufgabe: `NeueAufgabeAnlegen` ersetzt den inline `AufgabeNeu`-Klick + Warten auf `EditTitel`; anschließend `Starten` → assert `CliStoppen` erscheint direkt und kein Dialog `KI-Plugin auswählen` (unverändert).

Beteiligte Klassen/Komponenten: `E2E_PluginProjectDefault_NextTask`, `WpfTestBase`

### Refaktorierter Ablauf: Automatischer CLI-Neustart (`E2E_AutoStartCli`)

1. Setup unverändert (`ConfirmLocalDirectoryGitInitInSourceDirectory`, `SetupProjectMitNeuerAufgabe`, `StartenUndPluginWaehlen`).
2. CLI manuell stoppen (`CliStoppen`), Status bleibt „Gestartet" (unverändert).
3. `AufgabeDetailZurueck` ersetzt den inline `Zurück`-Klick; danach `OffeneAufgabenItems` (assert ≥ 1) → `ErsteOffeneAufgabeOeffnen` ersetzen die inline `OffeneAufgabenListe`-Suche + `items[0].DoubleClick`.
4. Assert: `CliStoppen` erscheint automatisch (unverändert).

Beteiligte Klassen/Komponenten: `E2E_AutoStartCli`, `WpfTestBase`

## Neue Klassen

Keine. Es werden ausschließlich Methoden zu `WpfTestBase` hinzugefügt.

| Klasse | Typ | Zweck |
|--------|-----|-------|
| — | — | Keine neuen Klassen |

## Änderungen an bestehenden Klassen

### `WpfTestBase` (Testinfrastruktur-Basisklasse, `src/Softwareschmiede.Tests/E2E/WpfTestBase.cs`)

- **Neue Methoden:**
  - `NeueAufgabeAnlegen(AutomationElement mainWindow)` — klickt `AufgabeNeu`, wartet auf `EditTitel`, gibt die `EditTitel`-TextBox zurück. Rückgabe: `AutomationElement`.
  - `AufgabeTitelSetzen(AutomationElement mainWindow, string titel)` — fokussiert `EditTitel`, `Ctrl+A`, tippt `titel`. Rückgabe: `void`. Voraussetzung: `TaskDetailView` im Edit-Modus sichtbar.
  - `AufgabeDetailSpeichern(AutomationElement mainWindow)` — klickt `Speichern`, wartet auf `ProjektName` (Rückkehr zur Projektansicht). Rückgabe: `void`.
  - `AufgabeDetailZurueck(AutomationElement mainWindow)` — klickt `Zurück`, wartet auf `ProjektName`. Rückgabe: `void`.
  - `OffeneAufgabenItems(AutomationElement mainWindow)` — wartet auf `OffeneAufgabenListe`, gibt deren `ListItem`-Kinder zurück. Rückgabe: `AutomationElement[]`.
  - `ErsteOffeneAufgabeOeffnen(AutomationElement mainWindow)` — öffnet das erste Item aus `OffeneAufgabenItems` per `DoubleClick`. Rückgabe: `void`.
  - `AufgabeAusListeOeffnen(AutomationElement mainWindow, string titel)` — sucht das `ListItem` mit dem angegebenen Titel, öffnet es per `DoubleClick` und wartet auf `Zurück`. Rückgabe: `void`. (Generalisierung des bisherigen `E2E_TaskWechselUeberMenue.OeffneAufgabeAusListe`.)
  - `ProjektNamenAendernUndSpeichern(AutomationElement mainWindow, string neuerName)` — fokussiert `ProjektName`, `Ctrl+A`, tippt `neuerName`, klickt `Speichern`, wartet auf Wiedererscheinen von `Speichern` (bleibt in Detailansicht). Rückgabe: `void`. Voraussetzung: `ProjectDetailView` im Edit-Modus sichtbar.
- **Neue Eigenschaften:** Keine.
- **Geänderte Methoden:** Keine bestehende Methode wird in Signatur oder Verhalten geändert.
- **Neue Events / Event-Handler:** Keine.

### `E2E_CreateNewTaskNavigation` (E2E-Testklasse)

- Die beiden `[Fact]`-Methoden werden durch eine konsolidierte Methode ersetzt, die Speichern- und Abbrechen-Phase in einem App-Lifecycle durchläuft und die neuen Hilfsmethoden nutzt. Listenlängen-Assertion auf „≥ 2" anpassen.

### `E2E_TaskDetailNavigation` (E2E-Testklasse)

- Die drei `[Fact]`-Methoden werden durch eine konsolidierte Methode ersetzt (Daten prüfen → Zurück → Öffnen), die die neuen Hilfsmethoden nutzt.

### `E2E_PluginSelectionDialog` (E2E-Testklasse)

- Die beiden Methoden (`[SkippableFact]` OK-Pfad, `[Fact]` Abbrechen-Pfad) werden zu einer `[SkippableFact]`-Methode zusammengefasst, die Abbrechen- und OK-Phase an derselben Aufgabe durchläuft. `ConfirmLocalDirectoryGitInitInSourceDirectory` und die bisherige Skip-Semantik des OK-Pfads bleiben erhalten.

### `ProjectDetailE2ETests` (E2E-Testklasse)

- Die 13 `[Fact]`/`[Fact] async`-Methoden werden zu 6 Methoden konsolidiert (siehe Programmablauf „Konsolidierte Abläufe: Projektdetailansicht"):
  - vier Navigations-/Zurück-Tests → eine Methode „Projekt-Navigation".
  - zwei Umbenennungs-/Bearbeiten-Tests → eine Methode „Projekt bearbeiten"; nutzt neuen Helfer `ProjektNamenAendernUndSpeichern`.
  - `AufgabeNeuAnlegen_...` + `AufgabenFiltern_...` → eine Methode „Aufgaben in Projektdetail"; nutzt `NeueAufgabeAnlegen`, `AufgabeDetailZurueck`, `OffeneAufgabenItems`.
  - vier Repository-Dialog-Tests → eine Methode „Repository-Dialog".
  - `ProjektLoeschen_...` bleibt eigenständig (destruktiv).
  - `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E` bleibt eigenständig (DB-seeded, async).

### `E2E_TaskWechselUeberMenue` (E2E-Testklasse)

- Einzelne `[SkippableFact]` bleibt bestehen (keine Methodenzusammenfassung möglich). Der `private`-Helfer `ErstelleUndStarteAufgabe` wird intern auf `NeueAufgabeAnlegen`, `AufgabeTitelSetzen`, `AufgabeDetailSpeichern` und `AufgabeAusListeOeffnen` umgestellt. Der bisherige `private static OeffneAufgabeAusListe` wird entfernt (ersetzt durch `WpfTestBase.AufgabeAusListeOeffnen`). `WaitForTerminalProzessId`/`BeschreibeDescendants` bleiben unverändert.

### `E2E_PluginProjectDefault_NextTask` (E2E-Testklasse)

- Einzelne `[SkippableFact]` bleibt bestehen. Inline `AufgabeNeu`+`EditTitel`-Block → `NeueAufgabeAnlegen`; inline `Zurück`+`ProjektName`-Block → `AufgabeDetailZurueck`. Checkbox-Dialog-Teil bleibt inline.

### `E2E_AutoStartCli` (E2E-Testklasse)

- Einzelne `[SkippableFact]` bleibt bestehen. Inline `Zurück`-Block → `AufgabeDetailZurueck`; inline `OffeneAufgabenListe`-Suche + `items[0].DoubleClick` → `OffeneAufgabenItems` (Assert) + `ErsteOffeneAufgabeOeffnen`.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Geringere Fehlerisolation je Testmethode:** Fällt in einem konsolidierten Test eine frühe Phase aus, laufen die Folgephasen nicht mehr. Mitigation: granulare Asserts mit aussagekräftigen Meldungen nach jeder Phase, sodass die Fehlerursache eindeutig bleibt.
- **Zustandsübertragung im gemeinsamen App-Lifecycle:** In `E2E_CreateNewTaskNavigation` legt die Abbrechen-Phase eine zweite Aufgabe an (Anlegen persistiert sofort). Die Assertion auf die Listenlänge muss von „≥ 1" auf „≥ 2" angepasst werden; die Prüfung auf Abwesenheit des nicht gespeicherten Titels bleibt gültig. In den konsolidierten `ProjectDetailE2ETests`-Methoden laufen mehrere Aktionen gegen dasselbe Projekt — die Reihenfolge (z. B. erst umbenennen, dann erneut öffnen) muss die jeweils vorherige Phase als Vorbedingung berücksichtigen.
- **Reduzierte App-Starts:** Original-Klassen −4 Starts (2→1, 3→1, 2→1), `ProjectDetailE2ETests` −7 Starts (13→6). Insgesamt ≈ 11 entfallende App-Starts pro Testlauf (≈ 2–4 min Ersparnis). Kein Einfluss auf abgedeckte Akzeptanzkriterien, da jedes bisherige Szenario als Phase/Assert erhalten bleibt.
- **Einzelmethoden-Klassen ohne Start-Ersparnis:** `E2E_TaskWechselUeberMenue`, `E2E_PluginProjectDefault_NextTask` und `E2E_AutoStartCli` reduzieren keine App-Starts (nur eine Methode); ihr Nutzen ist ausschließlich die Zentralisierung des Inline-FlaUI-Codes. Risiko: Verhaltensänderung durch abweichende Timeouts der Basishelfer — Mitigation: die Basishelfer verwenden dieselben oder großzügigere Timeouts (`Medium` statt `Short`), sodass keine Test enger getaktet wird als zuvor.
- **Promotion von `AufgabeAusListeOeffnen`:** Der Signaturwechsel betrifft nur `E2E_TaskWechselUeberMenue` (einzige Nutzerin des bisherigen `private`-Helfers). Kein anderer Aufrufer bricht.
- **Referenzen auf entfernte Testnamen:** Es bestehen keine CI-Filter auf einzelne Testmethodennamen (Filter laufen über `Category`/`OsInterface`), daher kein Bruch durch Umbenennung/Entfernung.

## Umsetzungsreihenfolge

1. **Hilfsmethoden in `WpfTestBase` ergänzen**
   - Voraussetzungen: Keine (FlaUI, `WaitForElement`, `WaitUntilGone`, `Short`/`Medium`, `Keyboard`, `ControlType.ListItem` bereits vorhanden).
   - Beschreibung: Die acht neuen `protected`-Methoden (`NeueAufgabeAnlegen`, `AufgabeTitelSetzen`, `AufgabeDetailSpeichern`, `AufgabeDetailZurueck`, `OffeneAufgabenItems`, `ErsteOffeneAufgabeOeffnen`, `AufgabeAusListeOeffnen`, `ProjektNamenAendernUndSpeichern`) inkl. XML-Doku mit Voraussetzungen implementieren.

2. **`E2E_CreateNewTaskNavigation` konsolidieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Beide Tests durch eine konsolidierte Methode ersetzen; Speichern- und Abbrechen-Phase über die neuen Hilfen; Listenlängen-Assert auf „≥ 2" anpassen.

3. **`E2E_TaskDetailNavigation` konsolidieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Drei Tests durch eine konsolidierte Methode ersetzen (Daten → Zurück → Öffnen per `OffeneAufgabenItems`/`ErsteOffeneAufgabeOeffnen`) über die neuen Hilfen.

4. **`E2E_PluginSelectionDialog` konsolidieren**
   - Voraussetzungen: Schritt 1; bestehende Hilfen `SetupProjectMitNeuerAufgabe`, `ConfirmLocalDirectoryGitInitInSourceDirectory`, `WaitForWindow`, `SelectComboBoxItemByClick` (alle vorhanden).
   - Beschreibung: Abbrechen- und OK-Pfad an derselben Aufgabe zu einer `[SkippableFact]` zusammenfassen; Git-Init-Confirm und Skip-Semantik des OK-Pfads erhalten.

5. **`ProjectDetailE2ETests` konsolidieren**
   - Voraussetzungen: Schritt 1; bestehende Hilfen `CreateProject`, `OpenProject`, `CreateAndOpenProject`, `WaitUntilGone`, `WaitForWindow`, `OpenTestDbContext` (alle vorhanden).
   - Beschreibung: 13 Tests zu 6 Methoden zusammenfassen (Navigation, Bearbeiten, Aufgaben, Repository-Dialog; Löschen und DB-seeded Trennung bleiben eigenständig). `ProjektNamenAendernUndSpeichern`, `NeueAufgabeAnlegen`, `AufgabeDetailZurueck`, `OffeneAufgabenItems` verwenden.

6. **`E2E_TaskWechselUeberMenue` refaktorieren**
   - Voraussetzungen: Schritt 1 (insbesondere `AufgabeAusListeOeffnen`).
   - Beschreibung: `ErstelleUndStarteAufgabe` auf die neuen Basishelfer umstellen; `private static OeffneAufgabeAusListe` entfernen; Aufrufe auf `AufgabeAusListeOeffnen` umstellen. Testlogik unverändert.

7. **`E2E_PluginProjectDefault_NextTask` refaktorieren**
   - Voraussetzungen: Schritt 1 (`NeueAufgabeAnlegen`, `AufgabeDetailZurueck`).
   - Beschreibung: Inline-Blöcke durch Helfer ersetzen; Checkbox-Dialog-Teil und Focus/Sleep-Workaround erhalten.

8. **`E2E_AutoStartCli` refaktorieren**
   - Voraussetzungen: Schritt 1 (`AufgabeDetailZurueck`, `OffeneAufgabenItems`, `ErsteOffeneAufgabeOeffnen`).
   - Beschreibung: Inline `Zurück`-Block und Listen-Öffnen durch Helfer ersetzen. Testlogik unverändert.

9. **Verifikation**
   - Voraussetzungen: Schritte 1–8.
   - Beschreibung: Vollständigen Build ausführen, dann die OS-Interface-/E2E-Lane laufen lassen (`--filter "Category=OsInterface"`, mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` im Sandbox) und bestätigen, dass die konsolidierten/refaktorierten Tests grün sind und die zusammengefassten Klassen die erwartete reduzierte Methodenanzahl aufweisen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `NeueAufgabeAnlegen` | `WpfTestBase` | Legt Aufgabe an, wartet auf `EditTitel`, liefert TextBox |
| `AufgabeTitelSetzen` | `WpfTestBase` | Setzt Aufgabentitel in `EditTitel` (Ctrl+A + Tippen) |
| `AufgabeDetailSpeichern` | `WpfTestBase` | Speichert und wartet auf Rückkehr zur `ProjectDetailView` |
| `AufgabeDetailZurueck` | `WpfTestBase` | Verwirft über `Zurück` und wartet auf `ProjectDetailView` |
| `OffeneAufgabenItems` | `WpfTestBase` | Liefert die `ListItem`-Kinder der `OffeneAufgabenListe` |
| `ErsteOffeneAufgabeOeffnen` | `WpfTestBase` | Öffnet die erste Aufgabe per Doppelklick |
| `AufgabeAusListeOeffnen` | `WpfTestBase` | Öffnet die benannte Aufgabe per Doppelklick, wartet auf `Zurück` |
| `ProjektNamenAendernUndSpeichern` | `WpfTestBase` | Ändert `ProjektName` (Ctrl+A + Tippen) und speichert (bleibt in Detailansicht) |
| `AufgabeAnlegen_SpeichernPersistiert_UndAbbrechenVerwirftTitel_E2E` (konsolidiert) | `E2E_CreateNewTaskNavigation` | Anlage+Speichern persistiert und navigiert zurück; Anlage+Abbrechen verwirft Titeländerung |
| `TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E` (konsolidiert) | `E2E_TaskDetailNavigation` | Korrekte Daten, Rücknavigation, fensterumfassende Detailansicht per Doppelklick |
| `PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E` (konsolidiert) | `E2E_PluginSelectionDialog` | Abbrechen setzt Start nicht fort; Auswahl+OK startet CLI |
| Projekt-Navigation (konsolidiert) | `ProjectDetailE2ETests` | Neuanlage abbrechen, öffnen/zurück/erneut öffnen, zurück zur Übersicht |
| Projekt bearbeiten (konsolidiert) | `ProjectDetailE2ETests` | Umbenennen, Kachel aktualisiert, erneut öffnen, Update bleibt persistent |
| Aufgaben in Projektdetail (konsolidiert) | `ProjectDetailE2ETests` | Aufgabe anlegen erscheint in Liste; Filter-Overlay öffnet/schließt |
| Repository-Dialog (konsolidiert) | `ProjectDetailE2ETests` | Öffnen-Button; Zuweisen-Dialog mit Plugin- und Arbeitsverzeichnis-ComboBox; Abbrechen |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_CreateNewTaskNavigation` (beide Methoden) | Werden zu einer konsolidierten Methode zusammengeführt; Listenlängen-Assertion angepasst |
| `E2E_TaskDetailNavigation` (drei Methoden) | Werden zu einer konsolidierten Methode zusammengeführt |
| `E2E_PluginSelectionDialog` (zwei Methoden) | Werden zu einer konsolidierten `[SkippableFact]` zusammengeführt |
| `ProjectDetailE2ETests` (13 Methoden) | 11 Methoden zu 4 konsolidiert; 2 (`ProjektLoeschen_...`, `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E`) bleiben eigenständig, nutzen aber neue Helfer, wo passend |
| `E2E_TaskWechselUeberMenue` (eine Methode) | Interne `private`-Helfer auf Basishelfer umgestellt; `OeffneAufgabeAusListe` entfernt |
| `E2E_PluginProjectDefault_NextTask` (eine Methode) | Inline-FlaUI-Blöcke auf `NeueAufgabeAnlegen`/`AufgabeDetailZurueck` umgestellt |
| `E2E_AutoStartCli` (eine Methode) | Inline-FlaUI-Blöcke auf `AufgabeDetailZurueck`/`OffeneAufgabenItems`/`ErsteOffeneAufgabeOeffnen` umgestellt |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aufgabe anlegen + speichern + abbrechen | `E2E_CreateNewTaskNavigation` | Zusammengefasste, lauffähige CRUD-Anlage-/Abbruch-Szenarien in einem Durchgang |
| TaskDetail Daten/Zurück/Öffnen | `E2E_TaskDetailNavigation` | Zusammengefasste Detailansicht-Navigation in einem Durchgang |
| Plugin-Auswahl Abbrechen/OK | `E2E_PluginSelectionDialog` | Zusammengefasster Plugin-Dialog-Flow in einem Durchgang |
| Projektdetail Navigation/Bearbeiten/Aufgaben/Repository | `ProjectDetailE2ETests` | Zusammengefasste Projektdetail-Szenarien in je einem Durchgang |
| Aufgabenwechsel über Seitenleiste | `E2E_TaskWechselUeberMenue` | Regressionsszenario unverändert abgedeckt, auf Basishelfer refaktoriert |
| Projekt-Standard-Plugin für Folgeaufgabe | `E2E_PluginProjectDefault_NextTask` | Szenario unverändert abgedeckt, auf Basishelfer refaktoriert |
| Automatischer CLI-Neustart | `E2E_AutoStartCli` | Szenario unverändert abgedeckt, auf Basishelfer refaktoriert |

Welche bestehenden E2E-Tests müssen angepasst werden? Siehe Tabelle „Betroffene bestehende Tests" (dieselben sieben Klassen). Keine weiteren E2E-Klassen sind betroffen.

## Offene Punkte

Keine.
