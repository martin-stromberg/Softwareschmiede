# Umsetzungsplan: Anzeige der Programmversion in der Seitenleiste

## Übersicht

Die aktuell installierte Programmversion soll dauerhaft in der Fußzeile der Navigations-Seitenleiste (`MainWindow.xaml`, `Grid.Row="2"`) angezeigt werden. Betroffen sind ausschließlich die UI-Schicht (`MainWindow.xaml`) und das `MainWindowViewModel`. Das gesamte Service-Ökosystem zur Versionsermittlung (`IApplicationVersionProvider`, `ApplicationVersionProvider`, `InstalledVersionInfo`) ist bereits vorhanden, produktionsreif, getestet und im DI-Container registriert und wird nur wiederverwendet. Es sind keine neuen Klassen, keine Migrationen und keine Konfigurationsänderungen erforderlich.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Injektion von `IApplicationVersionProvider` in `MainWindowViewModel` | Als **optionaler Konstruktor-Parameter am Ende der Parameterliste** (`IApplicationVersionProvider? versionProvider = null`), nach `dialogService` | Die bestehenden optionalen Update-Parameter werden in `MainWindowViewModelTests.CreateSut` positionsbasiert übergeben. Ein Anhängen am Ende hält alle vorhandenen positionsbasierten Aufrufe kompilierfähig; ein Einfügen in der Mitte würde sie brechen. DI (Singleton-Registrierung) füllt den Parameter automatisch. |
| Laden der Version im Konstruktor | **Fire-and-Forget** über den bestehenden `SafeFireAndForget`-Helfer, analog zu `UpdatePruefenImHintergrund()` | `GetInstalledVersionAsync` ist asynchron; ein blockierendes `.Result`/`.Wait()` im Konstruktor würde die Fensteranzeige verzögern/deadlocken. Das Muster ist im ViewModel bereits etabliert. |
| Übernahme des Ergebnisses ins UI | Zuweisung der Property über den bestehenden Dispatcher-Delegaten `_dispatcherInvoke`, da die Zuweisung aus einem Hintergrund-Task erfolgt | `SetProperty`/`OnPropertyChanged` müssen auf dem UI-Thread laufen; der Delegat ist bereits im ViewModel vorhanden und wird auch für `AktiveAufgabenImHintergrundAktualisieren` genutzt. |
| Sichtbarkeit des Versions-`TextBlock` bei eingeklappter Navigation | Nur sichtbar bei aufgeklappter Navigation (`Visibility`-Binding an `IsNavigationExpanded` über `BoolToVisibilityConverter`), analog zu den Button-Beschriftungen | Die eingeklappte Leiste ist nur 48 px breit; der Versionstext würde dort abgeschnitten/überlaufen. Folgt dem etablierten Muster der Navigationsbeschriftungen. (Geklärt.) |
| Platzierung des Versions-`TextBlock` in der Fußzeile | **Oberhalb** der Update-Buttons, **unterhalb** der bestehenden Separator-`Border` | Trennt die statische Versionsinfo klar von den Aktions-Buttons. (Geklärt.) |
| Fallback-Text bei fehlender/ungültiger `version.json` | `"Version unbekannt"` | Konsistent mit der deutschsprachigen UI, vermeidet eine leere Zeile. (Geklärt.) |
| Formatierung des Versionstexts | `"Version {0}"` (z. B. „Version 1.2.3") | Beschreibend und selbsterklärend. (Geklärt.) |
| Interaktivität des Versions-`TextBlock` | Reine Anzeige, kein Klick-Verhalten | Die Anforderung nennt keine Interaktion; hält den Umfang minimal. (Geklärt.) |

## Programmabläufe

### Version beim Start laden und anzeigen

1. Das `MainWindowViewModel` wird per DI erzeugt; der Konstruktor erhält u. a. `IApplicationVersionProvider` injiziert und speichert ihn in einem Feld.
2. Am Ende des Konstruktors wird eine neue private Methode `VersionLadenImHintergrund()` aufgerufen (analog zur bestehenden `UpdatePruefenImHintergrund()`-Zeile).
3. `VersionLadenImHintergrund()` startet einen Fire-and-Forget-Task (`SafeFireAndForget`), der `IApplicationVersionProvider.GetInstalledVersionAsync()` aufruft.
4. Liefert der Aufruf ein `InstalledVersionInfo`, wird dessen `Version` im Format `Version {0}` (z. B. „Version 1.2.3") übernommen; liefert er `null` oder wirft er, wird der Fallback-Text `"Version unbekannt"` gesetzt.
5. Die Zuweisung an die neue Property `CurrentVersion` erfolgt über `_dispatcherInvoke`, damit `PropertyChanged` auf dem UI-Thread ausgelöst wird.
6. Der an `CurrentVersion` gebundene `TextBlock` in `MainWindow.xaml` (`Grid.Row="2"`) zeigt den Wert an; bei eingeklappter Navigation ist er ausgeblendet.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `IApplicationVersionProvider`, `InstalledVersionInfo`, `MainWindow.xaml`, `SafeFireAndForget`-Erweiterung.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `MainWindowViewModel` (ViewModel, `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`)

- **Neue Felder:** `_versionProvider` (`IApplicationVersionProvider?`) — hält den injizierten Provider; `_currentVersion` (`string?`) — Backing-Field der Property.
- **Neue Eigenschaften:** `CurrentVersion` (`string?`, `get` public, `set` private via `SetProperty`) — der im UI anzuzeigende Versionstext.
- **Geänderter Konstruktor:** Neuer optionaler Parameter `IApplicationVersionProvider? versionProvider = null` am Ende der Parameterliste; Zuweisung an `_versionProvider`; Aufruf von `VersionLadenImHintergrund()` am Ende des Konstruktors (neben `UpdatePruefenImHintergrund()`).
- **Neue Methoden:** `VersionLadenImHintergrund()` (private, void) — startet den Fire-and-Forget-Ladevorgang; `VersionLadenAsync(CancellationToken)` (private, `Task`) — ruft `GetInstalledVersionAsync` auf, ermittelt Anzeigetext bzw. Fallback und setzt `CurrentVersion` über `_dispatcherInvoke`. (Konstante für den Fallback-Text z. B. als `private const string`.)

### `MainWindow.xaml` (View, `src/Softwareschmiede.App/Views/MainWindow.xaml`)

- **Neues UI-Element:** `TextBlock` im `StackPanel` von `Grid.Row="2"` (Fußzeile) mit `Text="{Binding CurrentVersion}"`.
- Platzierung: oberhalb der Update-Buttons, unterhalb der bestehenden Separator-`Border`.
- Styling analog zur Sektion „Aktive Aufgaben": `FontSize="11"`, `Foreground="{DynamicResource SecondaryTextBrush}"`, dezenter `Margin`.
- `Visibility="{Binding IsNavigationExpanded, Converter={StaticResource BoolToVisibilityConverter}}"`.
- `AutomationProperties.AutomationId` (z. B. `"AppVersionText"`) setzen, damit der E2E-Test das Element eindeutig findet.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine. Die Version wird ausschließlich angezeigt (reine Lese-/Darstellungsoperation), es gibt keine Benutzereingabe.

## Konfigurationsänderungen

Keine. `IApplicationVersionProvider` ist bereits in `App.xaml.cs` (Zeile 198) registriert; `MainWindowViewModel` ist bereits als Transient registriert (Zeile 245). Die Versionsquelle `version.json` wird beim Build erzeugt.

## Seiteneffekte und Risiken

- **`MainWindowViewModel`-Konstruktorsignatur:** Der neue optionale Parameter am Ende ist quellcode-kompatibel zu allen bestehenden positionsbasierten Aufrufen (nur `CreateSut` in den Tests). DI-Auflösung bleibt unverändert. Kein bekannter Bruch.
- **Startverhalten:** Der zusätzliche Fire-and-Forget-Ladevorgang darf die Fensteranzeige nicht blockieren. Durch das etablierte `SafeFireAndForget`-Muster und Fallback bei Fehlern ist sichergestellt, dass eine fehlende/ungültige `version.json` weder eine Exception noch eine Blockade verursacht (der Provider selbst gibt in allen Fehlerfällen `null` zurück und loggt Warnungen).
- **UI-Layout:** Der zusätzliche `TextBlock` vergrößert die Fußzeile geringfügig. Da `Grid.Row="2"` `Height="Auto"` ist und der mittlere Bereich (`Height="*"`) den Platz stellt, entsteht kein Layoutkonflikt.

## Umsetzungsreihenfolge

1. **`IApplicationVersionProvider` in `MainWindowViewModel` injizieren und `CurrentVersion` einführen**
   - Voraussetzungen: `IApplicationVersionProvider` und `InstalledVersionInfo` vorhanden (bereits im Repo); `SafeFireAndForget`-Erweiterung vorhanden (bereits im Repo, in `UpdatePruefenImHintergrund` genutzt); `_dispatcherInvoke` vorhanden (bereits im Repo).
   - Beschreibung: Feld `_versionProvider`, Property `CurrentVersion` inkl. Backing-Field, optionalen Konstruktor-Parameter am Ende ergänzen und zuweisen.

2. **Ladelogik `VersionLadenImHintergrund()` / `VersionLadenAsync()` ergänzen**
   - Voraussetzungen: Schritt 1 abgeschlossen (Property und Feld vorhanden).
   - Beschreibung: Fire-and-Forget-Aufruf am Konstruktorende hinzufügen; `GetInstalledVersionAsync` aufrufen, Anzeigetext bzw. Fallback ermitteln und `CurrentVersion` über `_dispatcherInvoke` setzen.

3. **`TextBlock` in `MainWindow.xaml` einfügen**
   - Voraussetzungen: `CurrentVersion` im ViewModel vorhanden (Schritt 1); `BoolToVisibilityConverter` und `SecondaryTextBrush` vorhanden (bereits im Repo).
   - Beschreibung: `TextBlock` mit Binding an `CurrentVersion`, Styling, Visibility-Binding und `AutomationProperties.AutomationId` in der Fußzeile ergänzen.

4. **Unit-Tests für `CurrentVersion` ergänzen** (siehe Tests).
   - Voraussetzungen: Schritte 1–2 abgeschlossen.
   - Beschreibung: `CreateSut` um optionalen `IApplicationVersionProvider`-Parameter erweitern; Tests für gesetzten Wert und Fallback.

5. **E2E-Test für die Anzeige ergänzen** (siehe E2E-Tests).
   - Voraussetzungen: Schritte 1–3 abgeschlossen; WPF-E2E-Infrastruktur (`WpfTestBase`) vorhanden (bereits im Repo).
   - Beschreibung: Test, der den Versions-`TextBlock` in der aufgeklappten Seitenleiste findet und prüft, dass ein nicht-leerer Versionstext angezeigt wird.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| Erweiterung von `CreateSut` um Parameter `IApplicationVersionProvider? versionProvider = null` | `MainWindowViewModelTests` | Stellt Mock-Provider für die neuen Tests bereit, ohne bestehende Aufrufe zu brechen |
| `CurrentVersion_ShouldExposeInstalledVersion_WhenProviderReturnsValue` | `MainWindowViewModelTests` | Bei erfolgreichem `GetInstalledVersionAsync` wird `CurrentVersion` mit der formatierten Version gefüllt |
| `CurrentVersion_ShouldUseFallback_WhenProviderReturnsNull` | `MainWindowViewModelTests` | Bei `null`-Rückgabe wird der Fallback-Text (`"Version unbekannt"`) gesetzt, keine Exception |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MainWindowViewModelTests.CreateSut` | Muss den neuen optionalen Konstruktor-Parameter unterstützen (Standardwert `null`); bestehende Aufrufe bleiben unverändert gültig |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| App starten, aufgeklappte Seitenleiste, Versions-`TextBlock` (`AutomationId="AppVersionText"`) enthält nicht-leeren Versionstext | Neue Datei `src/Softwareschmiede.Tests/E2E/E2E_VersionAnzeige.cs` | Die installierte Version wird dauerhaft in der Fußzeile der Seitenleiste angezeigt |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

Keine.
