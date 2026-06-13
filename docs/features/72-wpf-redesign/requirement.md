# Übersetzte Anforderung: Projektdetailansicht

## Fachliche Zusammenfassung

Die Projektdetailansicht wird erweitert, um eine vollständige Bearbeitungsoberfläche für Projekte bereitzustellen. Die Ansicht wird aus der Projektübersicht aufgerufen und zeigt ein Ribbon-Menü mit gruppierten Aktionen (Navigation, Projekt, Aufgaben, Repository). Der Inhalt besteht aus zwei Kacheln: eine für die Haupteigenschaften des Projekts (Symbol, Titel, Beschreibung) und eine für die Aufgabenliste des Projekts.

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- `Projekt` (bestehend, keine Änderungen erforderlich)
- `GitRepository` (bestehend, keine Änderungen erforderlich)
- `Aufgabe` (bestehend, keine Änderungen erforderlich)

### UI-Komponenten
- `ProjectDetailView.xaml` (bestehend, zu erweitern)
- `ProjectDetailView.xaml.cs` (bestehend, zu erweitern)
- `ProjectDetailViewModel.cs` (bestehend, zu erweitern)
- Ribbon-Menü-Komponenten (neu zu erstellen)

### Logikklassen / Services
- `ProjektService` (bestehend, ggf. zu erweitern für Löschfunktion)
- `AufgabeService` (bestehend, ggf. zu erweitern)

### Enums
- `ProjektStatus` (bestehend, keine Änderungen erforderlich)

## Implementierungsansatz

### Ribbon-Menü
- Erstellung eines Ribbon-Menü-Controls mit gruppierten Aktionen
- Gruppe "Navigation": Button "Zurück" zur Navigation zur Projektübersicht
- Gruppe "Projekt": Buttons "Speichern" und "Löschen" für Projektbearbeitung
- Gruppe "Aufgaben": Buttons "Neu" (Aufgabendetailansicht öffnen) und "Filter" (Overlay-Panel für Ansichtfilter)
- Gruppe "Repository": Buttons "Zuweisen" (Dialog für Repository-Zuweisung) und "Öffnen" (URL im Standardbrowser öffnen)

### Projekt-Kachel
- Anzeige des Projektsymbols (Icon)
- Anzeige des Projekttitels
- Anzeige der Projektbeschreibung
- Bearbeitbarkeit der Eigenschaften (für Speichern-Funktion)

### Aufgaben-Kachel
- Liste der Aufgaben des Projekts
- Integration mit bestehender Aufgabenlogik
- Filter-Funktionalität (Overlay-Panel)

### Navigation
- Integration mit bestehendem Navigationssystem
- Zurück-Button zur Projektübersicht

## Konfiguration

Keine Konfiguration erforderlich. Das Verhalten ist fest definiert durch die UI-Struktur.

## Offene Fragen

1. Soll die Projektdetailansicht sowohl für die Anlage als auch für die Bearbeitung verwendet werden?
2. Welche Felder des Projekts sollen bearbeitbar sein (nur Name und Beschreibung, auch Status)?
3. Soll beim Löschen eines Projekts eine Bestätigungsabfrage erscheinen?
4. Welche Filteroptionen sollen für die Aufgabenliste verfügbar sein?
5. Soll das Repository-Zuweisungs-Dialog ein neues Repository erstellen oder nur bestehende auswählen?
6. Wie soll das Projektsymbol bestimmt werden (festes Icon, konfigurierbar, aus Repository-Typ abgeleitet)?
