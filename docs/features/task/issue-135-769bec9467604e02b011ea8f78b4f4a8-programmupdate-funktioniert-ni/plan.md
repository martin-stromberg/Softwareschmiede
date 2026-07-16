# Umsetzungsplan: Programmupdate-Fehler beheben

## Übersicht

Der Update-Fortschrittsdialog (`UpdateProgressDialog`) kann beim Öffnen nicht angezeigt werden, weil die WPF-Databinding-Engine auf die schreibgeschützten (`private set`) Properties des `UpdateProgressViewModel` — allen voran `Percent` — nicht wie benötigt zugreifen kann und eine `InvalidOperationException` auslöst. Behoben wird dies, indem alle databindingrelevanten Properties des ViewModels von `private set` auf öffentliche Setter umgestellt werden. Betroffen ist ausschließlich die WPF-Präsentationsschicht (`Softwareschmiede.App`) plus die zugehörigen Tests; es gibt keine Änderungen an Domäne, Datenbank oder Konfiguration.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `UpdateProgressViewModel`-Properties | Alle sieben Properties (`PhaseText`, `Message`, `Percent`, `IsIndeterminate`, `HasError`, `CanClose`, `CanCancel`) erhalten öffentliche Setter (`set` statt `private set`); interne Steuerung über `SetProperty()` bleibt unverändert | Geklärter offener Punkt 2: Einheitlich öffentliche Setter stellen Konsistenz her und vermeiden zukünftige gleichartige Binding-Fehler. Die Klasse ist `sealed`; die vorhandenen Mutationsmethoden (`Apply`, `SetError`, `MarkUpdaterStarting`, `RequestCancel`) bleiben die vorgesehenen Änderungswege — externe Direktschreibzugriffe sind nicht vorgesehen, aber technisch nicht mehr blockiert. |
| Verifikationsstrategie | Empirischer Nachweis über einen automatisierten Test, der den Dialog mit gebundenem ViewModel real instanziiert/anzeigt und prüft, dass keine Binding-`InvalidOperationException` auftritt | Geklärter offener Punkt 1: Der Fix wird durch einen Reproduktionstest empirisch abgesichert statt durch manuelle Analyse des Binding-Modes. Der Test belegt zugleich das Vorher-Verhalten (Fehler) und das Nachher-Verhalten (Erfolg). |
| Kein `[EditorBrowsable]`/Signalattribut | Wird nicht ergänzt | Die ursprüngliche Sicherheitsfrage aus der Anforderung wird durch die Konsistenzentscheidung (offener Punkt 2) obsolet; ein zusätzliches Attribut würde die gewünschte Einheitlichkeit wieder aufbrechen und ist nicht Teil der Anforderung. |

## Programmabläufe

### Anzeige des Update-Fortschrittsdialogs (bestehender Ablauf, nach Fix funktionsfähig)

1. Der Update-Vorgang ruft `WpfUpdateProgressDialogService.Show(UpdateProgressViewModel)` auf.
2. `Show` läuft über `Dispatcher.Invoke`, erstellt eine neue `UpdateProgressDialog`, setzt `DataContext` auf das ViewModel und `Owner` auf das `MainWindow`.
3. `dialog.Show()` initialisiert die XAML-Bindings, u. a. `Value="{Binding Percent}"` der `ProgressBar`. Nach dem Fix findet die Binding-Engine einen öffentlichen Setter und der Dialog wird ohne `InvalidOperationException` angezeigt.
4. Fortschrittsmeldungen aktualisieren das ViewModel über `Apply(UpdatePreparationProgress)`; die Properties lösen `PropertyChanged` aus und die UI aktualisiert sich.
5. `SetError(string)` bzw. `MarkUpdaterStarting()` steuern Fehler- und Abschlusszustand; `WpfUpdateProgressDialogService.Close(...)` schließt den Dialog.

Beteiligte Klassen/Komponenten: `WpfUpdateProgressDialogService`, `UpdateProgressDialog`, `UpdateProgressViewModel`

## Neue Klassen

Keine neuen Produktivklassen. (Eine neue Testklasse siehe Abschnitt „Tests".)

## Änderungen an bestehenden Klassen

### `UpdateProgressViewModel` (WPF-ViewModel, `sealed class`)

Datei: `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs`

- **Geänderte Properties:** Bei allen folgenden Properties wird der Setter von `private set` auf öffentliches `set` umgestellt; die Setter-Implementierung (`SetProperty(ref ...)`) bleibt jeweils unverändert:
  - `PhaseText` (`string`)
  - `Message` (`string`)
  - `Percent` (`double`) — Ursache des Fehlers
  - `IsIndeterminate` (`bool`)
  - `HasError` (`bool`)
  - `CanClose` (`bool`)
  - `CanCancel` (`bool`) — behält den zusätzlichen `RelayCommand.Refresh`-Callback im `SetProperty`-Aufruf bei
- **Unverändert:** Konstruktor, `CancelCommand`, `Apply(...)`, `SetError(...)`, `MarkUpdaterStarting()`, `RequestCancel()`, alle Felder.

Keine Änderungen an `WpfUpdateProgressDialogService`, `IUpdateProgressDialogService` oder `UpdateProgressDialog.xaml` erforderlich.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Kapselung von `UpdateProgressViewModel`:** Die Properties sind nach der Änderung von außerhalb der Klasse schreibbar. Da die Klasse `sealed` ist und die Zustandsführung über die vorhandenen Methoden erfolgt, ist das Risiko unsachgemäßer externer Schreibzugriffe gering und wird bewusst zugunsten der Konsistenz in Kauf genommen.
- **Bestehende Unit-Tests:** Die vier Tests in `UpdateProgressViewModelTests` lesen die Properties nur; sie brechen durch die erweiterte Setter-Sichtbarkeit nicht.
- **Keine Domänen-/Datenbank-Auswirkungen:** Die Änderung ist auf die Präsentationsschicht begrenzt.

## Umsetzungsreihenfolge

1. **Setter der sieben Properties in `UpdateProgressViewModel` öffentlich machen**
   - Voraussetzungen: Keine (Datei und `ViewModelBase.SetProperty`/`RelayCommand.Refresh` bereits vorhanden).
   - Beschreibung: In `src/Softwareschmiede.App/ViewModels/UpdateProgressViewModel.cs` bei `PhaseText`, `Message`, `Percent`, `IsIndeterminate`, `HasError`, `CanClose`, `CanCancel` jeweils `private set` durch `set` ersetzen. Setter-Rümpfe unverändert lassen.

2. **Regressionstest für die Dialog-Anzeige (empirischer Nachweis) hinzufügen**
   - Voraussetzungen: Schritt 1 abgeschlossen; STA-fähige Testausführung (WPF). Vorhandene E2E-/WPF-Testinfrastruktur (`WpfTestBase`, `src/Softwareschmiede.Tests/E2E`) als Referenz.
   - Beschreibung: Test anlegen, der die `UpdateProgressDialog` mit einem gebundenen `UpdateProgressViewModel` auf einem STA-Thread instanziiert und anzeigt und verifiziert, dass keine Binding-`InvalidOperationException` auftritt (siehe Abschnitt „Tests").

3. **Bestehende ViewModel-Tests gegenprüfen**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: `UpdateProgressViewModelTests` unverändert ausführen und bestätigen, dass alle vier Tests weiterhin grün sind.

4. **Voller Build und Testlauf**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: Vollständigen Build ausführen (kein `--no-build`), danach die betroffenen Tests laufen lassen (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` im Sandbox-Kontext).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Show_ShouldNotThrowBindingException` (o. ä.) | Neue Testklasse `UpdateProgressDialogTests` (`src/Softwareschmiede.Tests/E2E/` oder passendes WPF-Testverzeichnis) | Instanziiert `UpdateProgressDialog` mit gebundenem `UpdateProgressViewModel` auf STA-Thread und weist empirisch nach, dass die Anzeige mit den öffentlichen Settern ohne `InvalidOperationException` gelingt (Reproduktion/Absicherung des Fixes). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UpdateProgressViewModelTests` | Keine Anpassung nötig — nur Verifikation, dass die Tests nach Erweiterung der Setter-Sichtbarkeit weiterhin bestehen. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Update-Fortschrittsdialog wird ohne Binding-Fehler angezeigt | `UpdateProgressDialogTests` (STA-WPF-Test, siehe „Neue Tests") | Der Dialog öffnet sich beim Update-Vorgang, ohne mit `InvalidOperationException` zu scheitern (Kernanforderung von Issue 135). |

Anmerkung: Ein vollständiger FlaUI-basierter E2E-Test über den realen Update-Trigger ist nicht praktikabel, da der Dialog nur im Verlauf eines echten Update-Vorgangs erscheint (externer Updater, App-Neustart). Der STA-Instanziierungstest deckt den fehlerauslösenden Pfad — das Aufbauen der XAML-Bindings des Dialogs mit dem realen ViewModel — direkt und deterministisch ab.

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

Keine.
