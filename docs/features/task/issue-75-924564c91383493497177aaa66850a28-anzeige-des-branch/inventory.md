# Bestandsaufnahme: Anzeige des Branch-Names in der Statusleiste

Diese Bestandsaufnahme dokumentiert den aktuellen Zustand des Projektcodes bezüglich der Anforderung zur Anzeige des Git-Branch-Names in der Statusleiste (Fußzeile) der Aufgabendetailansicht.

## Zusammenfassung

- **Datenmodell:** Die Property `BranchName : string?` existiert bereits in der `Aufgabe`-Entität und ist persistiert.
- **UI-Kontrolle:** Das `StatusIndicatorControl` zeigt derzeit nur Status-Text und Status-Farbe an — Dependency Property und XAML-Template für Branch-Name fehlen.
- **ViewModel:** Es gibt in `TaskDetailViewModel` Convenience-Properties wie `AufgabeTitel` und `AufgabeStatus`, aber KEINE Property `AufgabeBranchName`. Der Setter der `Aufgabe`-Property triggert mehrere `OnPropertyChanged`-Aufrufe, aber NICHT für Branch-Name.
- **View:** Das `TaskDetailView`-XAML bindet `StatusIndicatorControl.Status`, aber KEIN Binding für Branch-Name.

Folgende Schritte sind erforderlich:
1. Dependency Property `BranchName` zu `StatusIndicatorControl` hinzufügen
2. XAML-Template von `StatusIndicatorControl` erweitern
3. Convenience-Property `AufgabeBranchName` zu `TaskDetailViewModel` hinzufügen
4. `TaskDetailView.xaml` mit Binding für Branch-Name erweitern
5. `TaskDetailViewModel.Aufgabe` Property-Setter um `OnPropertyChanged(nameof(AufgabeBranchName))` ergänzen

## Details

- [Datenmodell](inventory/models.md)
- [Logik und UI-Kontrollkomponenten](inventory/logic.md)
