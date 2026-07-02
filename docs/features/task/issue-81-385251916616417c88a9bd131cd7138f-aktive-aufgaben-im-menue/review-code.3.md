# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ActiveTasksListControl.xaml (ActiveTasksListControl)

- **Doppelter Code** — Die beiden `DataTemplate`-Ressourcen `AufgabenKachelMitNavigationButtonTemplate` (Zeilen 5–46) und `AufgabenKachelVollflaechigKlickbarTemplate` (Zeilen 48–78) enthalten denselben Inhalt (StackPanel mit `Titel`-TextBlock, `Projekt.Name`-TextBlock und dem `KiAusfuehrungsStatusConverter`-TextBlock, jeweils mit identischen `FontSize`/`Foreground`/`TextTrimming`-Werten für die ersten beiden TextBlocks). Der einzige fachliche Unterschied zwischen beiden Templates ist die Art der Navigation (separater Pfeil-Button vs. `MouseBinding` auf der gesamten Kachel) sowie Padding/Margin/CornerRadius der äußeren `Border`.

  Empfehlung: Den gemeinsamen Inhalt (Titel/Projekt/Status-StackPanel) in ein eigenes `DataTemplate` oder eine `ContentControl`/`Style`-Ressource auslagern und in beiden Varianten per `{StaticResource}`/`ContentPresenter` referenzieren, sodass nur noch die Navigations-Mechanik (Button vs. `MouseBinding`) und das äußere `Border`-Styling variieren.

### AufgabeService.cs (AufgabeService)

- **Doppelter Code** — Das Filterprädikat `a.Status == AufgabeStatus.Gestartet || a.Status == AufgabeStatus.Wartend` ist identisch sowohl in `GetAktiveUndWartendeCountAsync` (Zeile 51) als auch in der neuen Methode `GetAktiveAufgabenAsync` (Zeile 466) enthalten. Die fachliche Definition "was als aktive Aufgabe zählt" ist damit an zwei Stellen dupliziert und kann bei künftigen Änderungen (z. B. Erweiterung um einen weiteren Status) auseinanderlaufen.

  Empfehlung: Das Prädikat als wiederverwendbaren `static readonly Expression<Func<Aufgabe, bool>>` (oder als private Hilfsmethode, die auf `IQueryable<Aufgabe>` angewendet wird) in `AufgabeService` zentralisieren und in beiden Methoden referenzieren.

### KiAusfuehrungsStatusConverterTests.cs (KiAusfuehrungsStatusConverterTests)

- **Testqualität / Namenskonvention** — Die Testmethode `Convert_ShouldReturnBereitOrStatusFallback_WhenNoActiveRunOrOldHeartbeat` (Zeile 50) suggeriert im Namen einen "StatusFallback"-Zweig, den `KiAusfuehrungsStatusConverter.Convert` gar nicht besitzt: Der entsprechende Codepfad gibt in jedem Fall (unabhängig vom `Status`, solange dieser nicht `Wartend` ist) ausschließlich `"✓ Bereit"` zurück. Der Testname beschreibt damit ein nicht existierendes Verhalten und erschwert das Verständnis, was tatsächlich geprüft wird.

  Empfehlung: Testmethode in `Convert_ShouldReturnBereitString_WhenNoActiveRunOrHeartbeatExpired` (oder vergleichbar) umbenennen, damit der Name exakt das geprüfte Verhalten wiedergibt.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/Converters/AppConverters.cs`
- `src/Softwareschmiede.App/Extensions/ObservableCollectionExtensions.cs`
- `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`
- `src/Softwareschmiede.App/Views/DashboardView.xaml`
- `src/Softwareschmiede.App/Views/MainWindow.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`
- `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/DashboardViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
