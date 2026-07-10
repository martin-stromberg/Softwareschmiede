← [Zurück zur Übersicht](index.md)

# Projekte — Business Rules

## Projekt-Anlage und -Speicherung

**Beschreibung:** Unterschiedliches Verhalten je nachdem, ob ein Projekt neu angelegt oder bereits vorhanden ist.

**Bedingungen:**
- Wenn `ProjektId == Guid.Empty`: Neuanlage-Modus
- Wenn `ProjektId != Guid.Empty`: Bearbeitungsmodus

**Verhalten:**
- **Neuanlage-Modus (leere ID):**
  - Der "Speichern"-Button ruft `ProjektService.CreateAsync()` auf
  - Ein neues Projekt wird mit dem angegebenen Namen und der optionalen Beschreibung angelegt
  - Projektname ist Pflichtfeld (Button deaktiviert wenn leer)
  - Nach erfolgreichem Speichern: ProjectListViewModel wird benachrichtigt (`ProjektListeAktualisierenCallback`), Navigation zurück zur Projektübersicht
  - Aufgaben-Kachel ist im Anlage-Modus nicht sichtbar (nur Projekt-Kachel sichtbar)

- **Bearbeitungsmodus (ID vorhanden):**
  - Der "Speichern"-Button ruft `ProjektService.UpdateAsync()` auf
  - Nach erfolgreichem Speichern: Projekt wird neu geladen mit `LadenAsync()`, um sicherzustellen, dass die UI mit DB-Daten synchron ist
  - ProjectListViewModel wird benachrichtigt
  - Aufgaben-Kachel ist sichtbar

**Umsetzung:** `ProjectDetailViewModel.ProjektSpeichernAsync()` prüft `_projektId == Guid.Empty` und ruft entsprechend `CreateAsync` oder `UpdateAsync` auf

## Projekt-Löschung mit Bestätigung

**Beschreibung:** Löschvorgänge erfordern explizite Benutzerbestätigung.

**Bedingungen:**
- Benutzer klickt "Löschen"-Button
- `LoeschenBestaetigenFunc` wird aufgerufen (überschreibbar in Tests)

**Verhalten:**
- Eine `MessageBox` mit Text "Soll das Projekt wirklich gelöscht werden?" wird angezeigt
- Buttons: "Ja" und "Nein"
- Bei "Ja": `ProjektService.DeleteAsync()` wird aufgerufen, Projekt wird gelöscht (inklusive aller zugeordneten Aufgaben durch Cascade-Delete)
- Bei "Nein": Löschung wird abgebrochen, Projekt bleibt erhalten
- Nach erfolgreicher Löschung: ProjectListViewModel wird benachrichtigt, Navigation zurück zur Projektübersicht

**Umsetzung:** `ProjectDetailViewModel.LoeschenBestaetigenFunc` ist ein `Func<bool>`, das bei Tests durch Mock ersetzt werden kann. Standard: `MessageBox.Show()`

## Repository-Zuweisung aus Vorrat

**Beschreibung:** Repositories können nur aus der Liste bereits bekannter Repositories zugewiesen werden, nicht neu erstellt.

**Bedingungen:**
- Der Repository-Zuweisung-Dialog wird geöffnet
- `ProjektService.GetAllRepositoriesAsync()` lädt alle vorhandenen Repositories
- Benutzer wählt eines aus und klickt "Zuweisen"

**Verhalten:**
- Der Dialog zeigt alle verfügbaren Repositories mit Name und URL
- Der Benutzer muss ein Repository auswählen (BestaetigenCommand ist sonst deaktiviert)
- Nach "Zuweisen": `ProjektService.AddRepositoryAsync()` wird mit den Daten des ausgewählten Repository aufgerufen
- Das Repository wird dem Projekt zugeordnet
- Das Projekt wird neu geladen, um die neuen Repositories anzuzeigen
- Ein Projekt kann mehrere Repositories haben

**Umsetzung:** `ProjectDetailViewModel.RepositoryZuweisenAsync()` öffnet `RepositoryAssignDialog` modal und wartet auf Bestätigung; `RepositoryAssignDialog` verwaltet die Auswahl über `RepositoryAssignViewModel`

## Filter-Status ist flüchtig

**Beschreibung:** Der Aufgabenfilter wird nicht persistiert und wird bei jedem Öffnen der Ansicht zurückgesetzt.

**Bedingungen:**
- Benutzer setzt einen Filter (Alle / Aktiv / Archiviert)
- Benutzer navigiert weg von der Projektdetailansicht oder schließt die Anwendung
- Benutzer öffnet das Projekt erneut

**Verhalten:**
- Der gespeicherte Filter-Status wird auf den Standard "Alle" zurückgesetzt
- Alle Aufgaben werden angezeigt (kein Filter aktiv)
- Der Filter-Status ist nur für die aktuelle Sitzung relevant

**Umsetzung:** `ProjectDetailViewModel.AufgabenFilter` wird als Property gespeichert, aber nicht in der Datenbank persistiert; beim Laden des Projekts wird der Filter immer auf `AufgabenFilterTyp.Alle` zurückgesetzt

## Aufgabenanzeige nach Abschlussstatus

**Beschreibung:** Die Projektdetailansicht trennt Aufgaben nach Abschlussstatus, damit laufende Arbeit direkt sichtbar bleibt und abgeschlossene Arbeit die Ansicht nicht dominiert.

**Bedingungen:**
- Aufgabe hat Status `Neu`, `Gestartet` oder `Wartend`: nicht beendete Aufgabe
- Aufgabe hat Status `Beendet`: beendete Aufgabe
- Aufgabe hat Status `Archiviert`: nicht Teil der offenen oder beendeten Projektdetail-Aufgabenlisten

**Verhalten:**
- Nicht beendete Aufgaben werden in der Aufgaben-Kachel direkt sichtbar angezeigt.
- Beendete Aufgaben werden im Register „Beendete Aufgaben" angezeigt.
- Das Register „Beendete Aufgaben" ist beim Öffnen der Projektdetailansicht initial zugeklappt.
- Das Register bleibt auch bei leerer Liste vorhanden und kann aufgeklappt werden.
- Die Trennung basiert auf dem Aufgabenstatus und ersetzt nicht den flüchtigen Aufgabenfilter.

**Umsetzung:** `ProjectDetailViewModel` baut aus der geladenen Aufgabenquelle getrennte Collections für nicht beendete und beendete Aufgaben auf; `ProjectDetailView.xaml` bindet diese an die direkt sichtbare Aufgabenliste und den Expander für beendete Aufgaben.

## Fokusmanagement in Projekt-Kachel

**Beschreibung:** Der Fokus wird automatisch auf das Projek**t-Name-Textfeld gesetzt.

**Bedingungen:**
- Projektdetailansicht wird geladen
- View ist vollständig initialisiert (`Loaded` Event)

**Verhalten:**
- Das `ProjektNameTextBox` Textfeld erhält automatisch den Fokus
- Benutzer kann sofort mit der Eingabe beginnen, ohne zuerst ein Textfeld anklicken zu müssen

**Umsetzung:** `ProjectDetailView.xaml.cs` im `Loaded` Event-Handler wird `ProjektNameTextBox.Focus()` aufgerufen
