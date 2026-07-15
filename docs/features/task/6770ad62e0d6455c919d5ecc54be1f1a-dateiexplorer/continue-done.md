# Offene Aufgaben

Erstellt am: 2026-07-15
Abbruchgrund: Neu gemeldeter Fehler nach Abschluss des Feature-Zyklus (siehe `continue-done.md`-Historie in der Git-Historie dieses Verzeichnisses für den vorherigen, abgeschlossenen Nacharbeitszyklus).

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Keine.

## Gemeldete Fehler

- [x] **Regression: Info- und CLI-Register nicht mehr anzeigbar, während der Dateiexplorer angezeigt wird.** Ursache gefunden: In `TaskDetailView.xaml` band `views:FileExplorerView.Visibility` auf `IsFileExplorerViewSelected` mit `RelativeSource={RelativeSource AncestorType=UserControl}`. Das fand als Quelle die `TaskDetailView`-Instanz selbst (statt deren `DataContext`) und suchte dort per Reflection nach einer CLR-Eigenschaft `IsFileExplorerViewSelected` — die es auf `TaskDetailView` nicht gibt, nur auf `TaskDetailViewModel`. Die Bindung schlug daher fehl und `Visibility` blieb dauerhaft beim Default `Visible`, wodurch `FileExplorerView` (letztes Element im selben Grid-Zellen-Stapel, mit undurchsichtigem `Background`) die Info-/CLI-/Diff-Panels darunter permanent überdeckte. Fix: `TaskDetailView` erhielt `x:Name="Root"`, die Bindung wurde auf `{Binding DataContext.IsFileExplorerViewSelected, ElementName=Root, ...}` umgestellt. Regressionstest `E2E_FileExplorer.DateiViewButton_DannInfoRegister_BlendetDateiexplorerAusUndZeigtInfoWiederAn_E2E` reproduziert den Fehler (schlägt gegen den alten Bindungsausdruck fehl, verifiziert) und besteht mit dem Fix.

## Änderungswünsche

- [x] **Buttons für den Verzeichnisbaum-Modus sollen ein Symbol statt Text erhalten.** `FileExplorerView.xaml`: die drei Buttons "Standard"/"Vergleich"/"Aktualisieren" zeigen jetzt Icons (📁/🔀/🔄) statt Text, mit `ToolTip` und `AutomationProperties.HelpText` für die Beschriftung; `AutomationProperties.Name` (für E2E-Tests) unverändert.
- [x] **Symbole für Verzeichnisse und Dateien im Baum.** Neuer Converter `WorkspaceFileNodeIconConverter` (generisches Ordner-/Dateisymbol, mit spezifischen Symbolen für Markdown, Bilder, Konfigurationsdateien und gängige Quellcode-Dateitypen), eingebunden in beide Baum-Templates (Standard- und Vergleichsmodus) in `FileExplorerView.xaml`, registriert in `App.xaml`. Unit-Tests in `WorkspaceFileNodeIconConverterTests.cs`.
- [x] **Rechtsbündiges Status-Symbol im Baum (neu/gelöscht/geändert).** Neuer Converter `WorkspaceFileNodeStatusIconConverter` (🆕 neu/untracked/added, 🗑 gelöscht, ✏ geändert, sonst leer), rechtsbündig in beiden Baum-Templates ergänzt (dritte Grid-Spalte, `TreeView.ItemContainerStyle` mit `HorizontalContentAlignment="Stretch"` für die Ausrichtung), registriert in `App.xaml`. Unit-Tests in `WorkspaceFileNodeStatusIconConverterTests.cs`.
- [x] **Navigation zur nächsten/vorherigen Änderung über der Dateianzeige.** Neue Buttons "◀"/"▶" (`FileExplorerVorherigeAenderungButton`/`FileExplorerNaechsteAenderungButton`) oberhalb der Dateianzeige in `FileExplorerView.xaml`, nur sichtbar wenn `ZeigtDiffAnsicht` aktiv ist. `FileExplorerViewModel` erhielt `NaechsteAenderungCommand`/`VorherigeAenderungCommand` (springen zyklisch durch zusammenhängende Added/Removed/Modified-Blöcke in `DiffZeilen`, Wrap-Around) sowie das Event `DiffZeileFokussiert`. `DiffViewer` erhielt eine neue Methode `ScrollToIndex(int)`, die `FileExplorerView.xaml.cs` beim Event aufruft, um die Zielzeile per `BringIntoView()` sichtbar zu scrollen. Unit-Tests in `FileExplorerViewModelTests_DiffNavigation.cs`.
- [x] **Button zum Öffnen einer Datei mit der Standardanwendung.** Vom Anwender gemeldet am 2026-07-15. `FileExplorerViewModel` erhielt `DateiMitStandardanwendungOeffnenCommand` (`RelayCommand`, `CanExecute` aktiv wenn der ausgewählte Knoten kein Verzeichnis und nicht als gelöscht markiert ist), das `Process.Start(new ProcessStartInfo { FileName = ..., UseShellExecute = true })` aufruft — dieselbe Konvention wie `RepositoryOeffnenCommand` (`ProjectDetailViewModel`) und `IssueBrowserOeffnenCommand` (`TaskDetailViewModel`). Neuer Button "📂" (`FileExplorerDateiOeffnenButton`) in `FileExplorerView.xaml` oberhalb der Dateianzeige, unabhängig vom Diff-Modus sichtbar (nur die ◀/▶-Navigationsbuttons bleiben auf `ZeigtDiffAnsicht` beschränkt, dazu wurden sie in eine verschachtelte StackPanel verschoben). Unit-Tests in `FileExplorerViewModelTests_DateiOeffnen.cs` (CanExecute für Datei/Verzeichnis/gelöschte Datei/keine Auswahl); der eigentliche Prozessstart wird wie bei den bestehenden Process.Start-Commands nicht getestet, um keine echte Fremdanwendung zu öffnen. `E2E_FileExplorer.DateiViewButton_ZeigtExplorerMitBaumUndModeButtons_E2E` um eine Sichtbarkeitsprüfung des neuen Buttons erweitert.
