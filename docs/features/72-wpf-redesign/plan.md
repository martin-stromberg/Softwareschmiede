# Umsetzungsplan: Projektdetailansicht

## Übersicht

Die Projektdetailansicht wird von einer einfachen Listenansicht zu einer vollständigen Bearbeitungsoberfläche mit Ribbon-Menü erweitert. Die Ansicht erhält gruppierte Aktionen (Navigation, Projekt, Aufgaben, Repository) und zeigt Projekteigenschaften und Aufgaben in Kacheln statt in einer einfachen Liste. Betroffen sind die UI-Komponenten `ProjectDetailView.xaml`, `ProjectDetailView.xaml.cs` und `ProjectDetailViewModel.cs` sowie ggf. die Navigation.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ribbon-Menü | Eigenes Ribbon-Control im XAML | Folgt dem WPF-Standard für Ribbon-Interfaces und ermöglicht konsistente Gruppierung von Aktionen |
| Projekt-Kachel | Border mit Grid-Layout für Symbol, Titel, Beschreibung | Einfach, erweiterbar und konsistent mit dem Dashboard-Design der Projektübersicht |
| Aufgaben-Kachel | Erweiterte ListBox mit Filter-Möglichkeit | Behält die bestehende Aufgabenliste bei, fügt aber Filter-Funktionalität hinzu |
| Projektsymbol | Festes Emoji-Icon (📁) | Einfachste Lösung, kann später erweitert werden |
| Bearbeitbarkeit | Nur Name und Beschreibung bearbeitbar, Status schreibgeschützt | Status wird durch Archivieren/Löschen gesteuert, nicht durch direkte Bearbeitung |
| Lösch-Bestätigung | MessageBox vor dem Löschen | Standard-WPF-Muster für kritische Aktionen |
| Repository-Zuweisung | Dialog zur Auswahl eines bestehenden Repository | Einfache Auswahl aus bestehenden Repositories |
| Aufgaben-Filter | Status-Filter (Alle, Aktiv, Archiviert) | Deckt die Hauptfilterbedürfnisse ab, kann erweitert werden |
| Projekt-Anlage | Detailansicht wird auch für Anlage verwendet | Konsistente UX für Anlage und Bearbeitung, Status "Neu" bei Anlage |

## Programmabläufe

### Zurück zur Projektübersicht

1. Benutzer klickt auf "Zurück"-Button im Ribbon-Menü (Gruppe "Navigation")
2. `ProjectDetailViewModel` löst NavigationEvent aus oder setzt NavigationState
3. `MainWindow` oder `NavigationViewModel` empfängt Event und wechselt zur Projektübersicht

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `NavigationViewModel`, `MainWindow`

### Projekt anlegen

1. Benutzer klickt auf "Neu"-Button in der Projektübersicht
2. `ProjectListViewModel` öffnet `ProjectDetailView` im Anlage-Modus (leeres Projekt)
3. Benutzer gibt Name und Beschreibung in der Projekt-Kachel ein
4. Benutzer klickt auf "Speichern"-Button im Ribbon-Menü (Gruppe "Projekt")
5. `ProjectDetailViewModel` ruft `ProjektService.CreateAsync` auf mit Status "Neu"
6. Bei Erfolg: Projekt wird erstellt, Ansicht wechselt in Bearbeitungsmodus
7. Bei Fehler: Fehlermeldung anzeigen

Beteiligte Klassen/Komponenten: `ProjectListViewModel`, `ProjectDetailViewModel`, `ProjektService`

### Projekt speichern

1. Benutzer ändert Name oder Beschreibung in der Projekt-Kachel
2. Benutzer klickt auf "Speichern"-Button im Ribbon-Menü (Gruppe "Projekt")
3. `ProjectDetailViewModel` ruft `ProjektService.UpdateAsync` auf
4. Bei Erfolg: Erfolgsmeldung anzeigen, Daten neu laden
5. Bei Fehler: Fehlermeldung anzeigen

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `ProjektService`

### Projekt löschen

1. Benutzer klickt auf "Löschen"-Button im Ribbon-Menü (Gruppe "Projekt")
2. MessageBox zeigt Bestätigungsabfrage
3. Bei Bestätigung: `ProjectDetailViewModel` ruft `ProjektService.DeleteAsync` auf
4. Bei Erfolg: Navigation zur Projektübersicht
5. Bei Fehler: Fehlermeldung anzeigen

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `ProjektService`, `NavigationViewModel`

### Aufgabe neu anlegen

1. Benutzer klickt auf "Neu"-Button im Ribbon-Menü (Gruppe "Aufgaben")
2. `ProjectDetailViewModel` ruft `AufgabeService.CreateAsync` auf
3. Bei Erfolg: Aufgabe zur Liste hinzufügen, Aufgabendetailansicht öffnen
4. Bei Fehler: Fehlermeldung anzeigen

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `AufgabeService`, `TaskDetailViewModel`

### Aufgaben-Filter öffnen

1. Benutzer klickt auf "Filter"-Button im Ribbon-Menü (Gruppe "Aufgaben")
2. Overlay-Panel wird angezeigt mit Filteroptionen
3. Benutzer wählt Filter (z.B. Status)
4. Aufgabenliste wird gefiltert angezeigt

Beteiligte Klassen/Komponenten: `ProjectDetailView`, `ProjectDetailViewModel`

### Repository zuweisen

1. Benutzer klickt auf "Zuweisen"-Button im Ribbon-Menü (Gruppe "Repository")
2. Dialog wird angezeigt mit Liste der verfügbaren Repositories
3. Benutzer wählt ein Repository aus der Liste
4. `ProjectDetailViewModel` ruft `ProjektService.AddRepositoryAsync` auf mit den Repository-Daten
5. Bei Erfolg: Repository zur Liste hinzufügen, Dialog schließen
6. Bei Fehler: Fehlermeldung anzeigen

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`, `ProjektService`, Repository-Dialog

### Repository öffnen

1. Benutzer wählt ein Repository aus der Liste
2. Benutzer klickt auf "Öffnen"-Button im Ribbon-Menü (Gruppe "Repository")
3. `ProjectDetailViewModel` öffnet die Repository-URL im Standardbrowser

Beteiligte Klassen/Komponenten: `ProjectDetailViewModel`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `RepositoryAssignDialog` | UserControl | Dialog für die Zuweisung eines Repository zu einem Projekt |
| `RepositoryAssignViewModel` | ViewModelBase | ViewModel für den Repository-Zuweisungs-Dialog |

## Änderungen an bestehenden Klassen

### `ProjectDetailView.xaml` (UserControl)

- **Neue Controls:** Ribbon-Menü mit gruppierten Buttons, Projekt-Kachel, Aufgaben-Kachel, Filter-Overlay-Panel
- **Geänderte Controls:** Aufgabenliste wird in Kachel-Layout integriert, einfache Liste entfernt

### `ProjectDetailView.xaml.cs` (Code-behind)

- **Keine Änderungen:** Alle Logik wird im ViewModel gehalten

### `ProjectDetailViewModel` (ViewModelBase)

- **Neue Eigenschaften:** `ProjektName` (string) — bearbeitbarer Projektname, `ProjektBeschreibung` (string?) — bearbeitbare Beschreibung, `SelectedRepository` (GitRepository?) — ausgewähltes Repository, `AufgabenFilter` (AufgabenFilterTyp) — aktueller Filter
- **Neue Methoden:** `ProjektSpeichernAsync` — Speichert Projektänderungen, `ProjektLoeschenAsync` — Löscht Projekt mit Bestätigung, `RepositoryOeffnenAsync` — Öffnet Repository im Browser, `FilterAnzeigenCommand` — Zeigt Filter-Overlay
- **Neue Commands:** `ZurueckCommand` — Navigiert zur Projektübersicht, `SpeichernCommand` — Speichert Projekt, `LoeschenCommand` — Löscht Projekt, `FilterCommand` — Öffnet Filter-Overlay, `RepositoryZuweisenCommand` — Öffnet Repository-Zuweisungs-Dialog, `RepositoryOeffnenCommand` — Öffnet Repository im Browser
- **Geänderte Methoden:** `LadenAsync` — Lädt auch Repositories, setzt bearbeitbare Eigenschaften

### `NavigationViewModel` (ViewModelBase)

- **Keine Änderungen:** Navigation wird über bestehendes Event/Command-System abgewickelt

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProjektName` | Nicht leer, max. 100 Zeichen | Name darf nicht leer sein |
| `ProjektBeschreibung` | Max. 500 Zeichen | Beschreibung zu lang |
| `RepositoryUrl` | Gültige URL | Ungültige URL-Format |
| `RepositoryName` | Nicht leer, max. 100 Zeichen | Name darf nicht leer sein |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Navigation:** Änderungen an der Projektdetailansicht könnten die Navigation beeinflussen, wenn das Event-System nicht konsistent bleibt
- **Aufgabenliste:** Filter-Funktionalität könnte die bestehende Aufgabenlogik beeinflussen, wenn nicht korrekt integriert
- **Repository-Zuweisung:** Neue Dialog-Komponente erhöht die Komplexität der UI

Keine bekannten kritischen Seiteneffekte.

## Umsetzungsreihenfolge

1. **ViewModel-Erweiterungen**
   - Voraussetzungen: Keine
   - Beschreibung: `ProjectDetailViewModel` um neue Eigenschaften, Commands und Methoden erweitern (ProjektName, ProjektBeschreibung, SpeichernCommand, LoeschenCommand, etc.)

2. **Repository-Dialog erstellen**
   - Voraussetzungen: Keine
   - Beschreibung: `RepositoryAssignDialog.xaml` und `RepositoryAssignViewModel.cs` erstellen für die Repository-Zuweisung

3. **XAML-Layout umstrukturieren**
   - Voraussetzungen: ViewModel-Erweiterungen
   - Beschreibung: `ProjectDetailView.xaml` um Ribbon-Menü, Projekt-Kachel und Aufgaben-Kachel erweitern, einfache Liste entfernen

4. **Filter-Overlay implementieren**
   - Voraussetzungen: XAML-Layout umstrukturieren
   - Beschreibung: Filter-Overlay-Panel in XAML erstellen und mit ViewModel verbinden

5. **Navigation integrieren**
   - Voraussetzungen: ViewModel-Erweiterungen
   - Beschreibung: Zurück-Command mit Navigationssystem verbinden

6. **Repository-Öffnen implementieren**
   - Voraussetzungen: ViewModel-Erweiterungen
   - Beschreibung: RepositoryOeffnenAsync mit Process.Start für Browser-URL implementieren

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ProjektSpeichernAsync_Success` | ProjectDetailViewModelTests | Speichern von Projektänderungen bei gültigen Daten |
| `ProjektSpeichernAsync_ValidationError` | ProjectDetailViewModelTests | Fehler bei leeren Projektname |
| `ProjektLoeschenAsync_Success` | ProjectDetailViewModelTests | Löschen eines Projekts mit Bestätigung |
| `ProjektLoeschenAsync_Aborted` | ProjectDetailViewModelTests | Abbruch des Löschens bei Benutzerentscheidung |
| `RepositoryZuweisenAsync_Success` | ProjectDetailViewModelTests | Zuweisen eines Repository mit gültigen Daten |
| `RepositoryOeffnenAsync_Success` | ProjectDetailViewModelTests | Öffnen einer Repository-URL im Browser |

### Betroffene bestehende Tests

Keine.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Projekt bearbeiten und speichern | ProjectDetailE2ETests | Projekt-Kachel zeigt bearbeitbare Felder, Speichern speichert Änderungen |
| Projekt löschen | ProjectDetailE2ETests | Lösch-Button zeigt Bestätigung, Löschen entfernt Projekt |
| Aufgabe neu anlegen | ProjectDetailE2ETests | Neu-Button erstellt Aufgabe und öffnet Detailansicht |
| Aufgaben filtern | ProjectDetailE2ETests | Filter-Filter zeigt Overlay, Filter reduziert Aufgabenliste |
| Repository zuweisen | ProjectDetailE2ETests | Zuweisen-Button öffnet Dialog, Dialog speichert Repository |
| Repository öffnen | ProjectDetailE2ETests | Öffnen-Button öffnet Repository-URL im Browser |
| Zurück zur Übersicht | ProjectDetailE2ETests | Zurück-Button navigiert zur Projektübersicht |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

Keine.
