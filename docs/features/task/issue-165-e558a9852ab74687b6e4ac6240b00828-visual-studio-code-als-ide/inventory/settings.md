# Settings und Persistenz

## AppEinstellungService

`AppEinstellungService` ist der zentrale Service fuer globale, nicht geheime App-Einstellungen. Er speichert Key/Value-Paare in `AppEinstellungen`.

Vorhandene Keys:

- `window.position.x`
- `window.position.y`
- `window.size.width`
- `window.size.height`
- `ui.designmode.name`
- `ki.plugin.default`
- `scm.plugin.default`
- `logging.level`

Der Service bietet bereits boolesche Helfer:

- `GetBoolSettingAsync(string schluessel, CancellationToken ct = default)`
- `SetBoolSettingAsync(string schluessel, bool wert, CancellationToken ct = default)`

Ein fehlender oder unparsbarer Boolean kommt als `null` zurueck. Fuer die neue Einstellung sollte das ViewModel bzw. der Fachservice daraus `false` machen.

Fundstellen:

- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs:11`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs:25`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs:49`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs:68`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs:104`

## Datenmodell

`AppEinstellung` hat `Id`, `Schluessel`, `Wert` und `AktualisiertAm`. Im DbContext ist `Schluessel` required, auf 200 Zeichen begrenzt und unique indiziert; `Wert` ist auf 2000 Zeichen begrenzt.

Eine neue boolesche Einstellung braucht voraussichtlich keine Migration, weil `AppEinstellungen` ein generischer Key/Value-Store ist. Ein neuer Konstanten-Key in `AppEinstellungService` genuegt.

Fundstellen:

- `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs:4`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:193`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:197`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:200`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:201`

## SettingsViewModel

`SettingsViewModel` laedt und speichert aktuell:

- Arbeitsverzeichnis
- Design-Modus
- Standard-KI-Plugin
- Standard-SCM-Plugin
- Plugin-spezifische Einstellungen
- Promptvorlagen

`LadenAsync()` liest globale App-Einstellungen ueber `AppEinstellungService.GetSettingAsync()`. `SpeichernAsync()` schreibt Standard-Plugins und delegiert Arbeitsverzeichnis sowie Design-Modus an eigene Services.

Fundstellen:

- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:18`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:180`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:187`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:190`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:193`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:233`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:248`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs:249`

## SettingsView.xaml

Die allgemeine Einstellungsseite liegt im ersten Tab `Allgemein`. Dort stehen derzeit:

- Design-ComboBox
- Arbeitsverzeichnis-TextBox

Eine Checkbox fuer "Visual Studio Code oeffnen, wenn keine Visual-Studio-Solution gefunden wurde" passt fachlich in diesen Tab, vermutlich unterhalb des Arbeitsverzeichnisses oder in eine eigene kleine Gruppe fuer Entwicklungsumgebung.

Fundstellen:

- `src/Softwareschmiede.App/Views/SettingsView.xaml:171`
- `src/Softwareschmiede.App/Views/SettingsView.xaml:178`
- `src/Softwareschmiede.App/Views/SettingsView.xaml:180`
- `src/Softwareschmiede.App/Views/SettingsView.xaml:197`

## Umsetzungskonsequenzen

- Neuer Key, z. B. `IdeOpenVisualStudioCodeWhenNoSolutionFoundKey = "ide.vscode.openWhenNoSolutionFound"`.
- Neue `bool`-Property im `SettingsViewModel`, z. B. `OpenVisualStudioCodeWhenNoSolutionFound`.
- `LadenAsync()` liest `await _einstellungService.GetBoolSettingAsync(key, ct) ?? false`.
- `SpeichernAsync()` schreibt `await _einstellungService.SetBoolSettingAsync(key, OpenVisualStudioCodeWhenNoSolutionFound, ct)`.
- XAML bindet eine `CheckBox` TwoWay an die neue Property.
- Fuer `TaskDetailViewModel` muss die Einstellung ebenfalls verfuegbar sein, entweder direkt via `AppEinstellungService` oder ueber einen kleinen Settings-/Options-Service, damit keine UI-Settings-Logik dupliziert wird.
