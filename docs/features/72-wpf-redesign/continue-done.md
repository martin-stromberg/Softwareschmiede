# Offene Punkte: Projektdetailansicht (Branch `72-wpf-redesign`)

Stand: 2026-06-13

---

## 1. Kritische Bugs (Code-Review)

### 1.1 Projekt-Anlage funktioniert nicht
**Datei:** `ProjectListViewModel.cs:112`, `ProjectDetailViewModel.cs:249`

`ZeigeErstellungsFormularCommand` öffnet die Detailansicht mit `ProjektId = Guid.Empty`. `ProjektSpeichernAsync` kehrt bei leerer ID sofort zurück (`if (_projektId == Guid.Empty) return`). Es existiert kein `CreateAsync`-Aufruf in `ProjectDetailViewModel`.

**Fix:** In `ProjektSpeichernAsync` prüfen, ob `_projektId == Guid.Empty`. Falls ja, `_projektService.CreateAsync(ProjektName, ProjektBeschreibung, ct)` aufrufen, danach `ProjektId` auf die neue ID setzen.

---

### 1.2 Keine Navigation nach Löschen
**Datei:** `ProjectDetailViewModel.cs:268`

`ProjektLoeschenAsync` ruft nach erfolgreichem `DeleteAsync` weder `_zurueckAction?.Invoke()` auf, noch setzt es den Zustand zurück. Die Detailansicht bleibt auf dem gelöschten Projekt stehen.

**Fix:** Nach `DeleteAsync` `_zurueckAction?.Invoke()` aufrufen.

---

### 1.3 `IsCreateFormVisible`-Overlay ist toter Code
**Datei:** `ProjectListViewModel.cs:113`

`IsCreateFormVisible` wird nirgends auf `true` gesetzt — das XAML-Overlay ist dauerhaft unsichtbar.

**Fix:** Wenn das Overlay nicht mehr gebraucht wird (Anlage läuft jetzt über die Detailansicht): Overlay-Code aus `ProjectListView.xaml` entfernen. Andernfalls an einer geeigneten Stelle `IsCreateFormVisible = true` setzen.

---

### 1.4 `DetailViewModel` wird nicht disposed
**Datei:** `ProjectListViewModel.cs:44`

Der `DetailViewModel`-Setter ruft kein `Dispose()` auf dem alten `IDisposable`-Wert auf. Bei jedem Projektwechsel läuft die vorherige `CancellationTokenSource` weiter.

**Fix:** Setter analog zu `SelectedTaskViewModel` in `ProjectDetailViewModel.cs:76` implementieren:
```csharp
private set
{
    if (_detailViewModel is IDisposable d) d.Dispose();
    SetProperty(ref _detailViewModel, value);
}
```

---

## 2. Teilweise umgesetzte Plan-Elemente (Plan-Review)

### 2.1 `RepositoryZuweisenCommand` — leeres Lambda
**Datei:** `ProjectDetailViewModel.cs:175`

```csharp
RepositoryZuweisenCommand = new RelayCommand(() => { });
```

**Fix:** `RepositoryAssignDialog` instanziieren, als `Window` oder `Popup` öffnen, nach Bestätigung `ProjektService.AddRepositoryAsync` aufrufen.

---

### 2.2 `RepositoryAssignViewModel` — Stubs
**Datei:** `RepositoryAssignViewModel.cs:33`

`BestaetigenCommand` und `AbbrechenCommand` sind leere Lambdas. Der Dialog kann weder bestätigt noch abgebrochen werden.

**Fix:** Commands mit Dialog-Schließlogik füllen. `RepositoryAssignDialog` muss als echtes Fenster (z. B. `Window`) geöffnet werden, damit es sich schließen kann (`DialogResult`-Muster).

---

### 2.3 `LadenAsync` setzt `SelectedRepository` nicht
**Datei:** `ProjectDetailViewModel.cs:195`

`GetDetailAsync` liefert Repositories als Include. `LadenAsync` setzt `ProjektName` und `ProjektBeschreibung`, aber nicht `SelectedRepository`.

**Fix:**
```csharp
if (Projekt != null)
{
    ProjektName = Projekt.Name;
    ProjektBeschreibung = Projekt.Beschreibung;
    SelectedRepository = Projekt.Repositories.FirstOrDefault();
}
```

---

### 2.4 `ProjektLoeschenAsync` — fehlende MessageBox-Bestätigung
**Datei:** `ProjectDetailViewModel.cs:268`

Plan fordert eine Bestätigungsabfrage vor dem Löschen. Die ist nicht implementiert.

**Fix:**
```csharp
var result = MessageBox.Show(
    "Soll das Projekt wirklich gelöscht werden?",
    "Löschen bestätigen",
    MessageBoxButton.YesNo,
    MessageBoxImage.Warning);
if (result != MessageBoxResult.Yes) return;
```

---

## 3. Sonstiger Code-Review-Befund

### 3.1 Overlay-Backdrop visuell falsch
**Datei:** `ProjectListView.xaml:109`

Äußerer Border hat `Background=SurfaceBrush`, innerer Border `Background=Black Opacity=0.5`. Das erzeugt keinen Dimm-Effekt über dem Seiteninhalt, sondern einen opaken SurfaceBrush-Hintergrund mit schwachem Schwarz-Overlay.

**Fix:** `Background="Black" Opacity="0.5"` direkt auf den äußeren Border — oder das Overlay ganz entfernen (siehe 1.3).

### 3.2 Unbenutztes `_logger`-Feld
**Datei:** `RepositoryAssignViewModel.cs:10`

`ILogger<RepositoryAssignViewModel>` wird injiziert, aber nie verwendet.

**Fix:** Feld und Konstruktorparameter entfernen, bis tatsächliche Fehlerbehandlung implementiert wird.

---

## 4. Fehlende Tests (Plan-Review + Test-Ergebnisse)

### 4.1 Unit-Tests (`ProjectDetailViewModelTests`)

| Testname | Was wird geprüft |
|----------|-----------------|
| `ProjektSpeichernAsync_Success` | Speichern ruft `UpdateAsync` auf, lädt Daten neu |
| `ProjektSpeichernAsync_ValidationError` | Leerer Name → `SpeichernCommand.CanExecute` false |
| `ProjektLoeschenAsync_Success` | Löschen ruft `DeleteAsync` und `ZurueckAction` auf |
| `ProjektLoeschenAsync_Aborted` | MessageBox-Abbruch → kein `DeleteAsync`-Aufruf |
| `RepositoryZuweisenAsync_Success` | Dialog bestätigt → `AddRepositoryAsync` aufgerufen |
| `RepositoryOeffnenAsync_Success` | `Process.Start` mit Repository-URL aufgerufen |

### 4.2 E2E-Tests (`ProjectDetailE2ETests`)

| Szenario | Akzeptanzkriterium |
|----------|--------------------|
| Projekt bearbeiten und speichern | Felder bearbeitbar, Speichern persistiert Änderungen |
| Projekt löschen | Bestätigungsdialog erscheint, Löschen entfernt Projekt |
| Aufgabe neu anlegen | Neu-Button erstellt Aufgabe, öffnet Detailansicht |
| Aufgaben filtern | Filter-Overlay erscheint, Aufgabenliste wird gefiltert |
| Repository zuweisen | Zuweisen-Button öffnet Dialog, Dialog speichert Repository |
| Repository öffnen | Öffnen-Button öffnet Repository-URL im Browser |
| Zurück zur Übersicht | Zurück-Button navigiert zur Projektliste |

---

## 5. Empfohlene Umsetzungsreihenfolge

1. **Bug 1.4** (Dispose) — betrifft alle Projektwechsel, sofort beheben
2. **Bug 1.1** (Anlage) — Core-Feature, ohne das ist "Neu" nutzlos
3. **Bug 1.2** (Löschen/Navigation) + **2.4** (MessageBox) — zusammen umsetzen
4. **2.1 + 2.2** (Repository-Zuweisung) — Dialog-Logik implementieren
5. **2.3** (SelectedRepository in LadenAsync) — ein-Zeilen-Fix
6. **1.3 + 3.1** (Overlay-Bereinigung) — Aufräumen
7. **3.2** (Logger entfernen)
8. **4.1** (Unit-Tests) — nach Bug-Fixes
9. **4.2** (E2E-Tests) — abschließend
