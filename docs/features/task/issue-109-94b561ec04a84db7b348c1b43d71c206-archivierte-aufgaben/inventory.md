# Bestandsaufnahme: Archivierte Aufgaben

## Kontext

Die Anforderung betrifft ausschliesslich die Darstellung von Aufgaben in der Projektdetailansicht. Nicht beendete Aufgaben sollen direkt sichtbar bleiben; beendete Aufgaben sollen in einem separaten, initial zugeklappten Register angezeigt werden. Datenmodell und Statusdefinition sollen unveraendert bleiben.

Da in dieser Codex-Umgebung keine separaten Unteragenten verfuegbar sind, wurde Schritt 4 direkt ausgefuehrt.

## Detaildokumente

- [Projektdetailansicht und Aufgabenliste](inventory/project-detail-ui.md)
- [Statusmodell und Service-Schnittstellen](inventory/status-und-services.md)
- [UI-Komponenten und Styles](inventory/ui-komponenten.md)
- [Tests und Abdeckung](inventory/tests.md)

## Relevante Befunde

- Die Projektdetailansicht verwendet aktuell eine einzige Aufgabenliste: `ProjectDetailView.xaml` bindet die `ListBox` an `ProjectDetailViewModel.GefilterteAufgaben`.
- `ProjectDetailViewModel` haelt zwei Collections: `Aufgaben` als geladene Quelle und `GefilterteAufgaben` als UI-Quelle. Die Filterung trennt aktuell nur `Alle`, `Aktiv` und `Archiviert`.
- `AufgabeService.GetByProjektAsync()` liefert nur Aufgaben mit `Status != AufgabeStatus.Archiviert`. Beendete Aufgaben mit `Status == Beendet` werden dadurch bereits geladen und in der aktiven Liste angezeigt.
- Der Abschlussstatus ist im bestehenden Modell `AufgabeStatus.Beendet`. `AufgabeStatus.Archiviert` ist ein eigener Status und darf fuer diese Anforderung nicht mit "beendet" gleichgesetzt werden.
- Es gibt bereits UI-Bausteine fuer Ribbon-Buttons, Statusanzeige und aktive Aufgabenlisten, aber kein bestehendes Expander-/Register-Control speziell fuer beendete Aufgaben in der Projektdetailansicht.
- Die Testbasis deckt Projektdetail-ViewModel, AufgabeService, Status-Enum und E2E-Flows der Projektdetailansicht ab. Es fehlen gezielte Tests fuer die Trennung von beendeten und nicht beendeten Aufgaben in der Projektdetailansicht.

## Potenziell betroffene Dateien

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`
- `src/Softwareschmiede/Domain/Enums/AufgabenFilterTyp.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`

## Umsetzungshinweise fuer die Planung

- Die Trennung sollte im ViewModel ueber neue abgeleitete Collections oder klar benannte Properties erfolgen, damit die XAML keine Statuslogik dupliziert.
- Als "nicht beendet" gelten nach aktueller Statusdefinition voraussichtlich `Neu`, `Gestartet` und `Wartend`. `Beendet` gehoert in den zugeklappten Bereich. `Archiviert` bleibt nach bestehender Service-Logik aus der Projektdetail-Aufgabenliste ausgeschlossen, sofern die Planung nichts anderes festlegt.
- Die bestehende Filterfunktion muss bewusst behandelt werden: Die neue Trennung darf nicht unklar mit `AufgabenFilterTyp.Aktiv/Archiviert` kollidieren.
- Fuer Akzeptanzkriterien sollten ViewModel-Tests die Statuszuordnung abdecken; XAML/E2E-Tests sollten mindestens die initial zugeklappte Darstellung und das Aufklappen der beendeten Aufgaben pruefen.
