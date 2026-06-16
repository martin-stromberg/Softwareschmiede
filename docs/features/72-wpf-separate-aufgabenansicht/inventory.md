# Bestandsaufnahme: Separate Aufgabendetailansicht

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezogen auf die Anforderung "Separate Aufgabendetailansicht" (Feature 72). Sie dokumentiert, welche Klassen, ViewModels, Services und UI-Komponenten bereits vorhanden sind und wie sie derzeit miteinander interagieren.

## Zusammenfassung

**Vorhanden:**
- Datenmodellklassen `Aufgabe` und `Projekt` mit vollständiger Persistierung und Status-Verwaltung
- `AufgabeService` mit umfangreichen CRUD- und Lifecycle-Methoden (Neuanlage, Speichern, Löschen, Archivieren, Status-Übergänge)
- `ProjectDetailViewModel` mit Commands für Aufgabenlisten-Verwaltung
- `TaskDetailViewModel` mit vollständiger Bearbeitungs- und Statusverwaltung
- Ribbon-Menü-basierte UI in `ProjectDetailView` und `TaskDetailView`
- Event-Handler für Aufgaben-Doppelklick in `ProjectDetailView`
- Status-abhängiges Content-Switching in `TaskDetailView` (Edit-Panel, CLI-Panel, Diff-Panel)
- Umfangreiche Unit- und E2E-Tests für ViewModels

**Fehlt oder muss angepasst werden:**
- `INavigationService` oder zentrales Navigationsmodell zur Verwaltung der View-Umschaltung zwischen Projekt- und Aufgabendetail
- Inline-Binding der `TaskDetailView` in `ProjectDetailView` muss entfernt werden (derzeit in Zeile 230-231)
- Container-View oder Anpassung der App-Struktur für fensterumfassendes View-Switching
- Navigation-State-Property im ViewModel zur Steuerung der Sichtbarkeit
- Explizite Behandlung von Neuanlage-Szenarios mit "Abbrechen"-Button (nicht "Zurück")

## Details

- [Datenmodell](inventory/models.md)
- [Logik (Services)](inventory/logic.md)
- [ViewModels](inventory/viewmodels.md)
- [Enums](inventory/enums.md)
- [UI-Views](inventory/views.md)
- [Tests](inventory/tests.md)
