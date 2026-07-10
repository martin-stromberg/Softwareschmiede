# Umsetzungsplan: Archivierte Aufgaben

## Ziel

Die Projektdetailansicht zeigt Aufgaben kuenftig in zwei klar getrennten Bereichen:

- Nicht beendete Aufgaben sind direkt sichtbar.
- Beendete Aufgaben stehen in einem separaten, beim Oeffnen zugeklappten Bereich.

Die Trennung basiert ausschliesslich auf `AufgabeStatus.Beendet`. Archivierte Aufgaben bleiben durch die bestehende Service-Logik von `AufgabeService.GetByProjektAsync()` ausgeblendet und werden nicht Teil dieser Darstellung.

## Leitentscheidungen

- Die Statuszuordnung erfolgt im `ProjectDetailViewModel`, nicht in XAML.
- Es werden zwei eigene UI-Collections eingefuehrt, damit die View keine fachliche Statuslogik enthaelt.
- `AufgabeStatus.Beendet` gilt als beendet.
- `AufgabeStatus.Neu`, `AufgabeStatus.Gestartet` und `AufgabeStatus.Wartend` gelten als nicht beendet.
- `AufgabeStatus.Archiviert` wird nicht neu geladen oder angezeigt. Falls ein archivierter Eintrag dennoch in der lokalen Collection landet, darf er in keiner der beiden neuen sichtbaren Collections erscheinen.
- Der bestehende Filter bleibt ausserhalb dieser Anforderung unveraendert, wird aber fuer die neue Projektdetail-Aufgabenanzeige nicht als fachliche Trennung verwendet.

## Betroffene Dateien

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`

Optional, nur falls die Implementierung eine zentrale Statushilfe einfuehrt:

- `src/Softwareschmiede/Domain/Enums/AufgabeStatusExtensions.cs`
- `src/Softwareschmiede.Tests/Domain/Enums/AufgabeStatusEnumTests.cs`

## Umsetzungsschritte

### 1. ViewModel-Collections einfuehren

In `ProjectDetailViewModel` zwei neue Collections ergaenzen:

- `ObservableCollection<Aufgabe> NichtBeendeteAufgaben`
- `ObservableCollection<Aufgabe> BeendeteAufgaben`

Die bestehende Collection `Aufgaben` bleibt die geladene Quelle. `GefilterteAufgaben` kann fuer bestehende Filter-Tests und nicht betroffene UI-Teile erhalten bleiben, soll aber nicht mehr die einzige Quelle der Aufgaben-Kachel sein.

### 2. Zentrale Aktualisierung erweitern

`AktualisiereGefilterteAufgaben()` so erweitern oder in eine klarer benannte Methode wie `AktualisiereAufgabenAnsichten()` ueberfuehren, dass nach jeder Aenderung an `Aufgaben` alle abgeleiteten Collections konsistent neu aufgebaut werden:

- `GefilterteAufgaben` bleibt nach bestehender Logik kompatibel.
- `NichtBeendeteAufgaben` enthaelt nur Aufgaben mit Status `Neu`, `Gestartet` oder `Wartend`.
- `BeendeteAufgaben` enthaelt nur Aufgaben mit Status `Beendet`.
- `Archiviert` wird in den beiden neuen Collections nicht angezeigt.

Alle bestehenden Aufrufstellen muessen die neue Aktualisierung weiter ausfuehren:

- nach `LadenAsync`
- nach `AufgabeErstellenAsync`
- nach `ReloadAufgabenListAsync`
- nach `AufgabeAusIssueErstellenAsync`
- beim Setzen von `AufgabenFilter`, sofern `GefilterteAufgaben` weiter existiert

### 3. Projektdetail-XAML auf zwei Bereiche umbauen

In `ProjectDetailView.xaml` die Aufgaben-Kachel ersetzen bzw. erweitern:

- Titel der Kachel bleibt `Aufgaben`.
- Direkt sichtbare Liste fuer nicht beendete Aufgaben:
  - `ItemsSource="{Binding NichtBeendeteAufgaben}"`
  - `AutomationProperties.Name="OffeneAufgabenListe"`
  - vorhandenes Item-Template mit Titel und Status wiederverwenden oder lokal duplizieren.
  - Doppelklick auf `ListBoxItem` bleibt ueber `AufgabeDoubleClick` erhalten.
- Separater Bereich fuer beendete Aufgaben:
  - Standard-WPF-`Expander`
  - `AutomationProperties.Name="BeendeteAufgabenExpander"`
  - `IsExpanded="False"` oder kein Binding mit Default false.
  - Header z. B. `Beendete Aufgaben`.
  - enthaltene `ListBox` mit `ItemsSource="{Binding BeendeteAufgaben}"`.
  - `AutomationProperties.Name="BeendeteAufgabenListe"`.
  - gleiches Item-Template und gleicher Doppelklick-Handler wie bei offenen Aufgaben.

Die Listen sollen eigene `MaxHeight`-Werte behalten, damit viele Aufgaben die Projektdetailansicht nicht unkontrolliert vergroessern. Die vorhandenen DynamicResource-Farben und Abstaende sollen weiterverwendet werden.

### 4. Leere und einseitige Datenlagen bewusst behandeln

Keine Sonderlogik soll die Darstellung kaputtmachen, wenn:

- keine Aufgaben vorhanden sind,
- nur nicht beendete Aufgaben vorhanden sind,
- nur beendete Aufgaben vorhanden sind.

Die Collections duerfen dann einfach leer sein. Der Expander fuer beendete Aufgaben bleibt auch bei leerer Collection vorhanden und initial zugeklappt.

### 5. Keine Service- oder Datenmodell-Aenderung

`AufgabeService.GetByProjektAsync()` bleibt unveraendert, weil beendete Aufgaben bereits geliefert und archivierte Aufgaben bereits ausgeschlossen werden. `AufgabeStatus` bleibt unveraendert.

Eine neue Status-Extension ist nur sinnvoll, wenn die Implementierung dadurch lesbarer wird. In diesem Fall sollte sie klein bleiben, z. B. `IstBeendet()` oder `IstInProjektdetailOffen()`, und mit einem Domain-Test abgesichert werden.

## Tests

### ViewModel-Tests

In `ProjectDetailViewModelTests` gezielte Tests ergaenzen:

1. `LadenAsync_TrenntNichtBeendeteUndBeendeteAufgaben`
   - Projekt anlegen.
   - Aufgaben mit Status `Neu`, `Gestartet`, `Wartend` und `Beendet` erzeugen bzw. aktualisieren.
   - ViewModel laden.
   - Erwartung:
     - `NichtBeendeteAufgaben` enthaelt genau `Neu`, `Gestartet`, `Wartend`.
     - `BeendeteAufgaben` enthaelt genau `Beendet`.

2. `LadenAsync_LeeresProjekt_HatLeereGetrennteAufgabenlisten`
   - Projekt ohne Aufgaben laden.
   - Erwartung:
     - `NichtBeendeteAufgaben` ist leer.
     - `BeendeteAufgaben` ist leer.
     - kein Fehlerzustand.

3. `LadenAsync_NurBeendeteAufgaben_FuelltNurBeendeteListe`
   - Projekt mit ausschliesslich beendeten Aufgaben laden.
   - Erwartung:
     - `NichtBeendeteAufgaben` ist leer.
     - `BeendeteAufgaben` enthaelt die beendeten Aufgaben.

4. `ReloadAufgabenList_AktualisiertGetrennteListenBeiStatuswechsel`
   - Aufgabe laden und oeffnen.
   - Aufgabe ueber Service auf `Beendet` setzen.
   - `AufgabeListeAktualisierenCallback` ausfuehren.
   - Erwartung:
     - Aufgabe ist nicht mehr in `NichtBeendeteAufgaben`.
     - Aufgabe ist in `BeendeteAufgaben`.
     - keine Duplikate in `Aufgaben`.

Wenn eine Status-Extension eingefuehrt wird, zusaetzlich Domain-Test:

- `AufgabeStatusExtensions_ErkenntBeendetUndNichtBeendetKorrekt`

### E2E-Tests

In `ProjectDetailE2ETests` einen UI-Test ergaenzen oder bestehenden Projektdetail-Test erweitern:

- Testdaten mit mindestens einer nicht beendeten und einer beendeten Aufgabe vorbereiten.
- Projektdetailansicht oeffnen.
- `OffeneAufgabenListe` ist sichtbar und enthaelt die nicht beendete Aufgabe.
- `BeendeteAufgabenExpander` ist vorhanden und initial nicht expandiert.
- Die beendete Aufgabe ist vor dem Aufklappen nicht sichtbar bzw. nicht in der sichtbaren Liste erreichbar.
- Expander aufklappen.
- `BeendeteAufgabenListe` ist sichtbar und enthaelt die beendete Aufgabe.

Stabile Automation-Namen sind Pflicht:

- `OffeneAufgabenListe`
- `BeendeteAufgabenExpander`
- `BeendeteAufgabenListe`

### Regressionstests

Nach der Implementierung ausfuehren:

```powershell
dotnet test
```

Falls die Gesamtsuite zu lange dauert oder infrastrukturell scheitert, mindestens gezielt:

```powershell
dotnet test --filter ProjectDetailViewModelTests
dotnet test --filter ProjectDetailE2ETests
```

## Akzeptanzkriterien-Zuordnung

- Getrennte Darstellung: zwei gebundene Listen in `ProjectDetailView.xaml`.
- Nicht beendete Aufgaben direkt sichtbar: `OffeneAufgabenListe` ohne Expander.
- Beendete Aufgaben separat: `BeendeteAufgabenExpander` mit eigener Liste.
- Initial zugeklappt: Expander-Default `IsExpanded=False`.
- Aufklappbar: Standard-Expander-Interaktion plus E2E-Test.
- Korrekte Statuszuordnung: ViewModel-Tests fuer `Neu`, `Gestartet`, `Wartend`, `Beendet`.
- Edge Cases: ViewModel-Tests fuer leer, nur beendete Aufgaben und implizit nur nicht beendete Aufgaben.

## Risiken

- Der bestehende Filter `Aktiv` bedeutet derzeit "nicht archiviert", nicht "nicht beendet". Die neue Trennung darf diesen Begriff nicht stillschweigend umdeuten.
- `GefilterteAufgaben` kann von bestehenden Tests oder UI-Automation erwartet werden. Deshalb sollte sie nur dann entfernt werden, wenn alle Verwendungen sauber angepasst werden.
- Zwei Listen mit Doppelklick-Handlern muessen beide denselben Oeffnen-Flow verwenden, damit beendete Aufgaben weiterhin geoeffnet werden koennen.
- E2E-Automation kann empfindlich auf WPF-Visual-Tree-Aenderungen reagieren. Die neuen Automation-Namen sollen deshalb eindeutig und stabil sein.

## Offene Punkte

Keine.
