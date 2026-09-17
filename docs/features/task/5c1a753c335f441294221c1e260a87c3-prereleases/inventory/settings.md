# Detailinventar: Einstellungsmodell und UI

## Persistenz

`src/Softwareschmiede/Application/Services/AppEinstellungService.cs` implementiert Anwendungseinstellungen als Schlüssel-Wert-Paare in `AppEinstellungen`. Es gibt generische String-, Integer- und Boolean-Lese-/Schreiboperationen. Neue Updateeinstellungen können dem bestehenden Muster folgen:

- Konstante für den Update-Prüfmodus
- Konstante für `Prerelease-Versionen laden`
- typisierte Boolean-/String-Zugriffe oder ein klar definiertes Enum-Format

`SetSettingAsync` erzeugt fehlende Einträge und aktualisiert sie anschließend. Eine zusätzliche EF-Migration ist für diese Form der Einstellung nicht erforderlich.

## ViewModel

`src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` lädt Einstellungen in `LadenAsync` und speichert sie in `SpeichernAsync`. Der allgemeine Tab enthält bereits Designmodus, Arbeitsverzeichnis und das Feature-Flag `IsAutonomAufgabenEnabled`. Die neuen Properties, Defaults und Persistenzaufrufe gehören in diese Schicht.

Die Auswahlwerte sollten intern stabil repräsentiert werden, damit Anzeige-Labels nicht als fachliche Steuerwerte missbraucht werden. Das vorhandene Projekt verwendet bei vergleichbaren Einstellungen sowohl Strings als auch Enums; der Plan muss die kompatible Variante festlegen.

## XAML und UI-Automation

`src/Softwareschmiede.App/Views/SettingsView.xaml` bindet allgemeine Einstellungen direkt an `SettingsViewModel`. Neue Controls benötigen eindeutige `AutomationProperties.Name`-Werte, damit sie über die bestehende FlaUI-View getestet werden können. Der allgemeine Tab liegt im Bereich der ersten Registerkarte.

`src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs` kapselt vorhandene Settings-Interaktionen wie Auswahlfelder, Checkboxen, Speichern und erneutes Öffnen. Für die neuen Controls sollten dort entsprechende Getter/Setter ergänzt werden.
