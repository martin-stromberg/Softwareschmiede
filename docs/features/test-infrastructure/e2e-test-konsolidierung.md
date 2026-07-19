# E2E-Test-Struktur: Konsolidierung logisch zusammenhängender Szenarien

## Übersicht

Diese technische Dokumentation beschreibt die Konsolidierung logisch zusammenhängender E2E-Szenarien in der Test-Suite, um Testlaufzeiten durch weniger App-Prozess-Starts zu reduzieren. Das Feature schafft eine Facade-Schicht für wiederholte UI-Interaktionen in `WpfTestBase`, sodass Tests lesbarer und wartbarer werden, und fasst mehrere eng verwandte Szenarien in einer Testmethode zusammen.

**Laufzeitgewinn:** Ungefähr 34 eingesparte App-Starts pro Testlauf (≈ 7–12 Minuten Zeiteinsparung), verteilt über drei Bearbeitungsrunden (11 + 9 + 14 App-Starts).

---

## Neue Hilfsmethoden in `WpfTestBase`

Alle neuen Methoden sind `protected`, folgen fachlicher Semantik (was der Anwender tut, nicht welche UI-Elemente angeclickt werden) und dokumentieren ihre Voraussetzungen via XML-Kommentare.

### Aufgaben-Operationen

#### `NeueAufgabeAnlegen(AutomationElement mainWindow)`

Klickt den `AufgabeNeu`-Button. Die App legt die Aufgabe sofort mit Status „Neu" an und navigiert in die separate `TaskDetailView`. Wartet auf das `EditTitel`-Feld und gibt es als `AutomationElement` zurück.

```csharp
// Beispiel
var titelField = NeueAufgabeAnlegen(mainWindow);
Assert.NotNull(titelField);
```

**Voraussetzung:** `ProjectDetailView` sichtbar.

---

#### `AufgabeTitelSetzen(AutomationElement mainWindow, string titel)`

Fokussiert das `EditTitel`-Feld, markiert seinen Inhalt (Strg+A) und tippt `titel`.

```csharp
// Beispiel
NeueAufgabeAnlegen(mainWindow);
AufgabeTitelSetzen(mainWindow, "Neue Aufgabe");
```

**Voraussetzung:** `TaskDetailView` im Edit-Modus sichtbar (z. B. nach `NeueAufgabeAnlegen()`).

---

#### `AufgabeDetailSpeichern(AutomationElement mainWindow)`

Klickt den `Speichern`-Button in der `TaskDetailView` und wartet auf das Wiedererscheinen von `ProjektName` (Rückkehr zur `ProjectDetailView`).

```csharp
// Beispiel
AufgabeDetailSpeichern(mainWindow);
// NavigatIon zurück zur Projektansicht abgeschlossen
```

---

#### `AufgabeDetailZurueck(AutomationElement mainWindow)`

Verwirft die Bearbeitung über den `Zurück`-Button in der `TaskDetailView` und wartet auf das Wiedererscheinen von `ProjektName` (Rückkehr zur `ProjectDetailView`).

```csharp
// Beispiel
AufgabeDetailZurueck(mainWindow);
// Rückkehr zur Projektansicht abgeschlossen, Änderungen verworfen
```

---

### Aufgaben-Auflistung und Öffnen

#### `OffeneAufgabenItems(AutomationElement mainWindow)`

Wartet auf die `OffeneAufgabenListe` und gibt ihre `ListItem`-Kinder als `AutomationElement[]` zurück.

```csharp
// Beispiel
var items = OffeneAufgabenItems(mainWindow);
Assert.True(items.Length > 0);
```

---

#### `ErsteOffeneAufgabeOeffnen(AutomationElement[] items)`

Öffnet das erste Item aus einer bereits ermittelten Aufgabenliste per Doppelklick, wodurch die `TaskDetailView` fensterumfassend erscheint.

```csharp
// Beispiel
var items = OffeneAufgabenItems(mainWindow);
ErsteOffeneAufgabeOeffnen(items);
// TaskDetailView ist nun fensterumfassend sichtbar
```

**Voraussetzung:** `items` enthält mindestens ein Element.

---

#### `AufgabeAusListeOeffnen(AutomationElement mainWindow, string titel)`

Sucht in der Aufgabenliste das `ListItem` mit dem angegebenen `titel`, öffnet es per Doppelklick und wartet auf den `Zurück`-Button (Bestätigung, dass die `TaskDetailView` geladen ist).

```csharp
// Beispiel
AufgabeAusListeOeffnen(mainWindow, "Existierende Aufgabe");
// TaskDetailView mit der benannten Aufgabe ist nun sichtbar
```

---

### Projekt-Operationen

#### `ProjektNamenAendernUndSpeichern(AutomationElement mainWindow, string neuerName)`

Fokussiert das `ProjektName`-Feld, markiert seinen Inhalt (Strg+A), tippt `neuerName` und klickt `Speichern` (UpdateAsync-Pfad, bleibt in der Detailansicht). Wartet anschließend auf das Wiedererscheinen von `Speichern` (Ladevorgang abgeschlossen).

```csharp
// Beispiel
ProjektNamenAendernUndSpeichern(mainWindow, "Projekt-Aktualisiert");
// Projekt umbenannt, Speichern-Button ist wieder interaktiv
```

**Voraussetzung:** `ProjectDetailView` im Edit-Modus sichtbar.

---

#### `DeleteCurrentProject(AutomationElement mainWindow)` / `DeleteCurrentTask(AutomationElement mainWindow)`

Löschen des aktuell geöffneten Projekts bzw. der aktuell geöffneten Aufgabe über den "Löschen"-Button inklusive Bestätigung des nativen Win32-Dialogs (sprachunabhängig über die Automation-ID `6` = `IDYES`, statt über den lokalisierten Button-Text). Für mehrphasige Tests, die nach einer Phase ihre Daten aufräumen müssen, damit DB-Abfragen der nächsten Phase (z. B. `Single()`/`SingleOrDefault()`) gültig bleiben, obwohl mehrere Projekte/Aufgaben nacheinander im selben App-Lifecycle angelegt wurden.

```csharp
// Beispiel: Phase abschließen und für die nächste Phase aufräumen
DeleteCurrentProject(mainWindow);
NavigateBackToDashboard(mainWindow);
```

**Voraussetzung:** `DeleteCurrentProject` erwartet `ProjectDetailView` (Element `AufgabeNeu` sichtbar); `DeleteCurrentTask` erwartet die Aufgabenansicht im nicht-laufenden Zustand (Element `Starten` sichtbar — bei laufendem CLI-Prozess zuerst stoppen).

---

#### `NavigateBackToDashboard(AutomationElement mainWindow)` / `NavigateBackFromProjectCardToProjectsList(AutomationElement mainWindow)`

Navigiert explizit zum Dashboard bzw. von einer geöffneten Projektkachel zurück zur Projektliste. Notwendig, weil ein erneuter Klick auf den " Projekte"-Navigationsbutton direkt aus einer bereits geöffneten Projektdetailansicht heraus nicht zuverlässig zur Übersicht navigiert, sondern in der zuletzt geöffneten Projektansicht hängen bleibt — vor jeder erneuten Navigation über `NavigateToProjects` sollte daher zunächst explizit zum Dashboard bzw. zur Liste zurückgekehrt werden.

---

#### `LaunchAppAndGetMainWindow()` / `SetupProjectMitNeuerAufgabeForStartedApp(AutomationElement mainWindow, ...)`

`LaunchAppAndGetMainWindow()` startet die Anwendung und liefert direkt das Hauptfenster. `SetupProjectMitNeuerAufgabeForStartedApp` ist das Gegenstück zu `SetupProjectMitNeuerAufgabe`, das kein neues `LaunchApp()` mehr durchführt, sondern ein bereits laufendes Hauptfenster entgegennimmt — für Testphasen, die als weiterer Schritt in einem gemeinsamen App-Lifecycle laufen, statt jeweils eine eigene Anwendung zu starten.

---

## Konsolidierte Testklassen

### `E2E_CreateNewTaskNavigation` (2 → 1 Methode)

**Ursprüngliche Tests:**
- `CreateNewTaskAndSave_TitleIsUpdatedInList_E2E()` — legt Aufgabe an, speichert sie, prüft Titel in Liste.
- `CreateNewTaskAndCancel_TitleIsNotSavedInList_E2E()` — legt Aufgabe an, bricht über Zurück ab, prüft fehlender Titel.

**Neue konsolidierte Methode:**
```csharp
[Fact]
public void AufgabeAnlegen_SpeichernPersistiert_UndAbbrechenVerwirftTitel_E2E()
{
    var mainWindow = StartAndNavigateToProjects("NeueAufgabe-Test");

    // Phase Speichern
    NeueAufgabeAnlegen(mainWindow);
    AufgabeTitelSetzen(mainWindow, "Persistierte Neue Aufgabe");
    AufgabeDetailSpeichern(mainWindow);
    
    var projektNameBox = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Medium);
    Assert.NotNull(projektNameBox);
    
    var aufgabenTitel = WaitForElement(mainWindow, cf => cf.ByName("Persistierte Neue Aufgabe"), Short);
    Assert.NotNull(aufgabenTitel);

    // Phase Abbrechen
    NeueAufgabeAnlegen(mainWindow);
    AufgabeTitelSetzen(mainWindow, "Nicht gespeicherter Titel");
    AufgabeDetailZurueck(mainWindow);
    
    var nichtGespeicherterTitel = mainWindow.FindFirstDescendant(cf => cf.ByName("Nicht gespeicherter Titel"));
    Assert.Null(nichtGespeicherterTitel);
    
    var items = OffeneAufgabenItems(mainWindow);
    Assert.True(items.Length >= 2, "Aufgabenliste sollte beide angelegten Aufgaben enthalten.");
}
```

**Gewinn:** Eine App-Start weniger, Speichern- und Abbrechen-Pfad in einem Durchlauf getestet, granulare Asserts erhalten.

---

### `E2E_TaskDetailNavigation` (3 → 1 Methode)

**Ursprüngliche Tests:**
- `TaskDetailContainsCorrectData_E2E()` — prüft Daten in `TaskDetailView`.
- `ClickBackButton_NavigatesToProjectView_E2E()` — klickt Zurück, prüft Rückkehr.
- `DoubleClickTaskItem_OpensFullscreenDetailView_E2E()` — öffnet Aufgabe per Doppelklick, prüft fensterumfassend.

**Neue konsolidierte Methode:**
```csharp
[Fact]
public void TaskDetail_ZeigtDaten_Zurueck_UndOeffnenFensterumfassend_E2E()
{
    var mainWindow = StartAndNavigateToProjects("TaskDetail-Test");

    NeueAufgabeAnlegen(mainWindow);
    var titelField = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
    Assert.NotNull(titelField);
    Assert.Equal("Neue Aufgabe", titelField.Text);

    AufgabeDetailZurueck(mainWindow);
    var projektName = WaitForElement(mainWindow, cf => cf.ByName("ProjektName"), Short);
    Assert.NotNull(projektName);

    var items = OffeneAufgabenItems(mainWindow);
    Assert.True(items.Length >= 1);
    ErsteOffeneAufgabeOeffnen(items);
    
    var saveButton = WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short);
    Assert.NotNull(saveButton);
    var projektNameGone = mainWindow.FindFirstDescendant(cf => cf.ByName("ProjektName"));
    Assert.Null(projektNameGone); // fensterumfassend = kein Hintergrund
}
```

**Gewinn:** Zwei App-Starts weniger, alle Navigations-Szenarien in einem Testlauf.

---

### `E2E_PluginSelectionDialog` (2 → 1 Methode)

**Ursprüngliche Tests:**
- `StartAndCancelPluginDialog_TaskRemainsNew_E2E()` — startet Dialog, bricht ab, prüft Status.
- `StartAndSelectPlugin_CliStopsButtonAppears_E2E()` — startet, wählt Plugin, prüft CLI-Stop-Button.

**Neue konsolidierte `[SkippableFact]`:**
```csharp
[SkippableFact]
public void PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E()
{
    SkipWennConPtyNichtVerfuegbar();
    ConfirmLocalDirectoryGitInitInSourceDirectory();

    var mainWindow = SetupProjectMitNeuerAufgabe("test-repo", "Plugin-Test");

    // Phase Abbrechen
    var startenButton = WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);
    startenButton.AsButton().Click();
    var dialog = WaitForWindow("KI-Plugin auswählen", Medium);
    var abbrechenButton = WaitForElement(dialog, cf => cf.ByName("Abbrechen"), Short);
    abbrechenButton.AsButton().Click();
    
    var editTitelWeiterhin = WaitForElement(mainWindow, cf => cf.ByName("EditTitel"), Short);
    Assert.NotNull(editTitelWeiterhin);
    var cliStoppenNicht = mainWindow.FindFirstDescendant(cf => cf.ByName("CliStoppen"));
    Assert.Null(cliStoppenNicht);

    // Phase OK (an derselben Aufgabe)
    StartenUndPluginWaehlen(mainWindow, "Softwareschmiede.KiSimulator");
    var cliStoppen = WaitForElement(mainWindow, cf => cf.ByName("CliStoppen"), Short);
    Assert.NotNull(cliStoppen);
}
```

**Gewinn:** Eine App-Start weniger, Skip-Semantik erhalten, beide Pfade an derselben Aufgabe.

---

### `ProjectDetailE2ETests` (13 → 6 Methoden)

**Konsolidierungsgruppen:**

1. **Projekt-Navigation** (4 Tests → 1): Neuanlage abbrechen, öffnen/zurück, erneut öffnen, zurück zur Übersicht.
2. **Projekt bearbeiten** (2 Tests → 1): Umbenennen, Kachel aktualisiert, erneut öffnen, Update persistent.
3. **Aufgaben in Projektdetail** (2 Tests → 1): Aufgabe anlegen in Liste, Filter-Overlay öffnet/schließt.
4. **Repository-Dialog** (4 Tests → 1): Öffnen-Button prüfen, Dialog mit Plugin- und Arbeitsverzeichnis-ComboBox, Abbrechen.
5. **Projekt löschen** (1 Test): Eigenständig (destruktiv).
6. **Offene/beendete Aufgaben trennen** (1 Test): Eigenständig (DB-seeded, async).

**Gewinn:** Sieben App-Starts weniger durch Konsolidierung, zwei Szenarien bleiben eigenständig aus fachlichen Gründen.

---

### `WpfE2ETests` (Datei `WpfE2EPlaceholderTests.cs`, 8 → 2 Methoden)

**Ursprüngliche Tests:** `ProjektErstellen_ZeigtAufgabenListe_E2E`, `ProjektErstellen_UndNeueAufgabeAnlegen_E2E`, `AufgabeAnlegen_ZeigtStartenButton_E2E` (Projekt-/Aufgaben-Flow); `Dashboard_KeineRecoveryBanner_BeiSauberemStart_E2E`, `EinstellungenOeffnen_ZeigtEinstellungsSeite_E2E`, `DarkModeAktivierenUndPersistieren_E2E`, `EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E`, `EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E` (Einstellungen-Flow).

**Neue konsolidierte Methoden:**
- `Projekt_ErstellenUndAufgabeAnlegen_ZeigtListeUndStartenButton_E2E()` — Projekt anlegen/öffnen, Aufgabe anlegen, Starten-Button und Fenster-Handle prüfen (3 alte Szenarien).
- `Einstellungen_OeffnenAendernUndNavigationBleibtStabil_E2E()` — sauberer Start ohne Recovery-Banner, Einstellungen öffnen, Dark Mode umschalten und Persistenz prüfen, Arbeitsverzeichnis ändern und speichern, mehrfache Navigation bleibt stabil (5 alte Szenarien).

**Gewinn:** Sechs App-Starts weniger, zwei kohärente Phasen-Flows statt acht isolierter Einzeltests.

---

### `E2E_SettingsCommandLineParameters` (3 → 1 Methode)

**Ursprüngliche Tests:** `Einstellungen_ZeigtCommandLineParametersTextBox_BeiCodexCliPlugin_E2E()`, `Einstellungen_SpeichertUndLaeadtCommandLineParameters_E2E()`, `Einstellungen_HilfeButton_OeffnetDialogDerMitSchliessen_GeschlossenWerdenKann_E2E()` — alle drei teilen dieselbe Vorbedingung (`OpenKiSettingsWithCodexCli`).

**Neue konsolidierte Methode:** `CommandLineParameters_TextBoxSpeichertWertUndHilfeDialogFunktioniert_E2E()` — Feld-Sichtbarkeit, Speichern/Neuladen-Persistenz und Hilfe-Dialog in einem Durchlauf.

**Gewinn:** Zwei App-Starts weniger.

---

### `E2E_FileExplorer` (2 → 1 Methode)

**Ursprüngliche Tests:** `DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E()`, `DateiViewButton_DannInfoRegister_BlendetDateiexplorerAusUndZeigtInfoWiederAn_E2E()` — beide teilen dasselbe Setup (Repository klonen, Aufgabe starten, Dateiexplorer öffnen).

**Neue konsolidierte Methode:** `DateiExplorer_ZeigtBaumUndModeButtons_UndWechseltZuInfoUndZurueck_E2E()` — Baum- und Mode-Buttons, anschließend Wechsel zu Info- und CLI-Register in einem Durchlauf.

**Gewinn:** Ein App-Start weniger.

---

### `E2E_WorkingDirectory` (6 → 2 Methoden)

Die ursprünglich als "bewusst nicht konsolidierbar" eingestufte Klasse wurde in einer weiteren Runde doch konsolidiert, nachdem die tatsächlichen Ursachen der drei früher fehlgeschlagenen Versuche identifiziert wurden (statt der Konsolidierung selbst zugeschrieben zu werden):

- **Navigation nach Projekt-Detailansicht:** Ein erneuter Klick auf " Projekte" direkt aus einer bereits geöffneten Projektdetailansicht heraus navigiert nicht zuverlässig zur Übersicht, sondern bleibt in der zuletzt geöffneten Projektansicht hängen. Fix: Vor jeder erneuten Navigation zu " Projekte" zuerst explizit über `NavigateBackToDashboard`/`NavigateBackFromProjectCardToProjectsList` zum Dashboard bzw. zur Liste zurückkehren.
- **`UnauthorizedAccessException` beim Löschen eines lokalen Git-Testrepositories:** Trat nur auf, wenn im Quellverzeichnis tatsächlich ein Git-Repository initialisiert wurde (der Zuweisungsdialog hält währenddessen einen Datei-Zugriff auf das Git-Verzeichnis offen). Der betroffene Fallback-Test benötigt aber gar kein echtes Git-Repository, um den fehlgeschlagenen Strukturabruf zu simulieren — Fix: `CreateLocalSourceDirectory(..., initializeGitRepository: false)`.
- **DB-fragile Abfragen bei mehreren Projekten/Repositories im selben Lifecycle:** Einzelne Abfragen (`Single()`/`SingleOrDefault()`) setzten implizit voraus, dass zu jedem Zeitpunkt nur ein Projekt/Repository in der Test-DB existiert. Fix: Jede Phase räumt ihr Projekt über die neuen Basishelfer `DeleteCurrentProject`/`DeleteCurrentTask` auf, bevor die nächste Phase beginnt; betroffene Abfragen wurden zusätzlich auf Repository-Namen statt auf implizite Eindeutigkeit umgestellt.

**Neue konsolidierte Methode:** `RepositoryZuweisung()` führt fünf der sechs Szenarien (beide Repository-Zuweisungs-Pfade, Arbeitsverzeichnis-Bearbeitung, beide Start-Fehlerfälle) als Phasen in einem App-Lifecycle aus. `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E` bleibt eigenständig, da sie als einziges Szenario einen erfolgreich laufenden CLI-Prozess hinterlässt.

**Gewinn:** Vier App-Starts weniger.

---

### `ProjectDetailE2ETests` (6 → 1 Methode)

Die beiden zuvor als "bewusst eigenständig" eingestuften Methoden (`ProjektLoeschen_...`, destruktiv; `Projektdetailansicht_TrenntOffeneUndBeendeteAufgaben_E2E`, DB-seeded) wurden nach demselben Muster in die übrigen vier Phasen integriert: `DeleteCurrentProject` räumt nach jeder Phase auf, `NavigateBackToDashboard` stellt sicher, dass die nächste Phase sauber ab dem Dashboard startet. Da keine der sechs Phasen eine implizite DB-Eindeutigkeit voraussetzt (alle Lookups erfolgen namensbasiert), war für diese Klasse — anders als bei `E2E_WorkingDirectory` — keine Anpassung an DB-Abfragen nötig.

**Neue konsolidierte Methode:** `ProjektDetailSzenarien()` führt alle sechs Szenarien (Navigation, Bearbeiten, Aufgaben/Filtern, Repository-Dialog, Offene/beendete-Aufgaben-Trennung, Löschen) nacheinander aus; Löschen steht bewusst als letzte Phase.

**Gewinn:** Fünf App-Starts weniger.

---

### ConPTY-Lifecycle: `E2E_ConPtyTerminalStart`, `E2E_ConPtyResize`, `E2E_ConPtyProcessEnd`, `E2E_ConPtyKeyboardInput` (4 → 1 Methode)

Alle vier Tests teilten exakt dasselbe Setup (`SetupProjectMitNeuerAufgabe` + `StartenUndPluginWaehlen`) und prüften nacheinander Facetten **derselben laufenden ConPTY-Session** (Start, Resize, Tastatureingabe, Prozessende) — anders als beim Lösch-Cleanup-Muster ist hier **kein** Aufräumen zwischen den Phasen nötig, da alle bis auf die letzte Phase denselben laufenden Prozess voraussetzen.

**Neue Klasse `E2E_ConPtyLifecycle`, Methode:** `ConPtyLifecycle_StartResizeTastatureingabeUndProzessende_E2E()`. Prozessende steht bewusst als letzte Phase, da sie den Prozess beendet.

**Gewinn:** Drei App-Starts weniger.

---

### Plugin-Auswahl und -Wechsel: `E2E_PluginSelectionDialog` + `E2E_PluginWechsel` (2 → 1 Methode)

Die "OK"-Phase des Auswahl-Dialogs endet bereits mit laufender CLI (Softwareschmiede.KiSimulator) — genau die Vorbedingung, die der Plugin-Wechsel-Test benötigt.

**Neue Klasse `E2E_PluginAuswahlUndWechsel`, Methode:** `PluginAuswahlAbbrechenOkUndWechsel_E2E()` führt Abbrechen-Phase, OK-Phase (CLI startet) und Plugin-Wechsel-Phase (CLI läuft bereits) nacheinander an derselben Aufgabe aus.

**Gewinn:** Ein App-Start weniger.

---

### `E2E_PluginProjectDefault` + `E2E_PluginProjectDefault_NextTask` (2 → 1 Methode)

Beide Tests waren bereits im XML-Doc-Kommentar der ersten Methode explizit als zusammengehörig cross-referenziert (die zweite Aufgabe muss zwingend im selben Projekt liegen wie die erste, die den Projekt-Standard speichert).

**Neue konsolidierte Methode:** `PluginProjectDefault_SpeichernUndAutomatischeUebernahmeInFolgeaufgabe_E2E()` führt beide Phasen im selben Projekt aus.

**Gewinn:** Ein App-Start weniger.

---

### Refaktorierte Einzelmethoden-Klassen

Diese Klassen haben nur eine Testmethode (keine Konsolidierung möglich), werden aber auf neue Hilfsmethoden refaktoriert, um DRY-Prinzip zu folgen:

- **`E2E_TaskWechselUeberMenue`:** Interner `private`-Helfer `ErstelleUndStarteAufgabe()` nutzt neu `NeueAufgabeAnlegen`, `AufgabeTitelSetzen`, `AufgabeDetailSpeichern`, `AufgabeAusListeOeffnen`, `StartenUndPluginWaehlen`. Bisheriger `private static OeffneAufgabeAusListe` entfernt (ersetzt durch `WpfTestBase.AufgabeAusListeOeffnen`).
- **`E2E_PluginProjectDefault_NextTask`:** Inline-FlaUI-Blöcke durch `NeueAufgabeAnlegen`, `AufgabeDetailZurueck` ersetzt; Checkbox-Dialog-Handling bleibt inline.
- **`E2E_AutoStartCli`:** Inline `Zurück`-Block durch `AufgabeDetailZurueck`, Listen-Öffnen durch `OffeneAufgabenItems` + `ErsteOffeneAufgabeOeffnen` ersetzt.

---

## Fehlerbehandlung und Diagnostik

### Fail-Fast bei fehlenden Elementen

Alle Hilfsmethoden nutzen bestehende `WaitForElement()`-Methode, die bei Timeout `TimeoutException` wirft:

```csharp
throw new TimeoutException($"Element wurde nicht innerhalb von {timeout.TotalSeconds}s gefunden.");
```

### Fehlerbanner-Diagnose

`WaitForElement()` prüft parallel nach `FehlerMeldung`-Banner. Wenn ein erwartetes Element nicht erscheint, aber ein Fehlerbanner sichtbar ist, wirft die Methode mit aussagekräftiger Diagnose:

```csharp
throw new InvalidOperationException(
    $"In der Anwendung wird eine Fehlermeldung angezeigt: {GetFehlerText(fehlerMeldung)}");
```

Dies ermöglicht schnelle Diagnose von Testfehlern durch die UI selbst gemeldete Fehler.

---

## Timeouts

Hilfsmethoden verwenden bestehende `TimeSpan`-Konstanten:

| Timeout | Wert | Verwendung |
|---------|------|-----------|
| `Short` | 20s | Schnell erscheinende UI-Elemente, Speicher-/Navigations-Abschlüsse |
| `Medium` | 15s | Asynchrone Operationen, Listenladen |
| `Long` | 30s | Initiales Fenster-Erscheinen |

Timeouts sind großzügig, um CI-Jitter (JIT-Warmup) zu kompensieren, ohne echte Performance-Regressionen zu maskieren.

---

## Test-Isolation

Alle E2E-Tests verwenden `[Collection("E2E")]`-Isolation:

```csharp
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_CreateNewTaskNavigation : WpfTestBase
```

Dies verhindert parallele Ausführung und sichert Datenbankinstanz-Isolation pro Test.

---

## Architektur und Abhängigkeiten

**Keine Architekturveränderung:**

- `WpfTestBase` wird um Facade-Methoden erweitert, bleibt aber Basisklasse für alle E2E-Tests.
- Bestehende `WaitForElement()`, `Keyboard.Type()`, `DoubleClick()`, FlaUI-Aufrufe bleiben funktional identisch.
- Keine neuen NuGet-Abhängigkeiten, keine Änderungen an Produktivcode.
- Credential-Store-Isolation (`CredentialStoreSnapshot`) bereits vorhanden, wird konsistent verwendet.

---

## Verifikation und Häufige Fehler

### Test läuft zu lange oder hängt

**Ursache:** Timeout ist zu kurz, oder Element ist nicht unter erwarteter ID zu finden.

**Lösung:** 
1. Prüfe das App-Log unter `src/Softwareschmiede.App/bin/<Config>/logs/softwareschmiede-*.log` auf Startup-Exception.
2. Erhöhe `Medium`/`Short` Timeout lokal (z. B. `WaitForElement(..., TimeSpan.FromSeconds(30))`).
3. Prüfe Element-Hierachie mit UI Automation Inspector (`Inspect.exe` in Windows 10+ enthalten).

### Test schlägt mit „Element wurde nicht gefunden" fehl

**Ursache:** Hilfsmethode wartet auf falsches Element oder Seitenelement ist nicht mit erwarteter `Name`/`AutomationId` vorhanden.

**Lösung:**
1. Prüfe, dass Methode mit korrekten Voraussetzungen aufgerufen wird (z. B. `ProjectDetailView` sichtbar bei `NeueAufgabeAnlegen()`).
2. Prüfe App-Log auf Fehler, die der Test nicht erkannt hat (Fehlerbanner-Diagnose sollte das melden).
3. Nutze `WaitForWindow(title)` um Top-Level-Fenster (Dialoge) zu prüfen.

### Element verschwindet, bevor Hilfsmethode es nutzen kann

**Ursache:** Timing-Problem zwischen Test und asynchroner App-Logik.

**Lösung:** Das ist ein echtes Race-Condition-Bug in der App oder im Test. Test-Helfer können das nicht verbergen. 
- Prüfe, dass asynchrone Operationen (Create/Update) `await`-Punkte haben.
- Nutze `WaitUntilGone()` vor neuen Operationen, um vorherige abzuschließen.

---

## Zusammenfassung für Maintainer

Diese Refaktorierung ist eine **reine Infrastruktur-Optimierung** ohne Verhaltenswechsel:

✅ Alle Akzeptanzkriterien aus Anforderung und Plan sind als granulare Asserts erhalten.  
✅ Fail-Fast-Semantik und Fehlerbanner-Diagnose bleiben aktiv.  
✅ Keine neuen Abhängigkeiten oder Architektur-Brüche.  
✅ Verwandte Tests sind kohärent; Fehlerquelle ist bei fehlschlagender Phase sichtbar.  
✅ Laufzeitgewinn: ≈ 11 weniger App-Starts, ≈ 2–4 Minuten Zeiteinsparung pro Testlauf.  

**Wartung:** Neue Hilfsmethoden folgen bestehendem Muster (`protected`, XML-Doku, `WaitForElement()` + Timeout). Bei neuen UI-Aufgaben-Mustern, die mehrfach auftreten, ergänze neue Methoden in `WpfTestBase` statt Inline-Code in Tests.
