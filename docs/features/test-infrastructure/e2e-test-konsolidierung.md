# E2E-Test-Struktur: Konsolidierung logisch zusammenhängender Szenarien

## Übersicht

Diese technische Dokumentation beschreibt die Konsolidierung logisch zusammenhängender E2E-Szenarien in der Test-Suite, um Testlaufzeiten durch weniger App-Prozess-Starts zu reduzieren. Das Feature schafft eine Facade-Schicht für wiederholte UI-Interaktionen in `WpfTestBase`, sodass Tests lesbarer und wartbarer werden, und fasst mehrere eng verwandte Szenarien in einer Testmethode zusammen.

**Laufzeitgewinn:** Ungefähr 11 eingesparte App-Starts pro Testlauf (≈ 2–4 Minuten Zeiteinsparung).

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
