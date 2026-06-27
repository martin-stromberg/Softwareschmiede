# Umsetzungsplan: Anzeige des Branch-Names in der Statusleiste

## Übersicht

Die Statusleiste (Fußzeile) der Aufgabendetailansicht wird erweitert, um den Namen des aktuellen Git-Branches anzuzeigen. Diese Information wird zusammen mit dem Aufgabenstatus dargestellt, damit der Benutzer sehen kann, auf welchem Branch die KI-gestützte Aufgabenbearbeitung läuft. Die Änderungen betreffen die UI-Kontrolle `StatusIndicatorControl`, das ViewModel `TaskDetailViewModel` und die View `TaskDetailView.xaml`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Branch-Name in `StatusIndicatorControl` | Dependency Property mit XAML-Template-Erweiterung | Folgt dem bestehenden Muster für `Status`, `StatusText`, `StatusColor`; einfache Datenanbindung ohne komplexe Logik |
| Convenience-Property in ViewModel | `AufgabeBranchName` wie `AufgabeTitel` und `AufgabeStatus` | Konsistente Namenskonvention und Entkopplung der View von direktem `Aufgabe`-Zugriff |
| Property-Changed-Benachrichtigung | `OnPropertyChanged(nameof(AufgabeBranchName))` im `Aufgabe`-Setter | Reaktive Aktualisierung des Bindings wenn die Aufgabe wechselt, folgt bestehendem Muster |

## Programmabläufe

### Laden und Anzeige eines Task mit Branch-Information

1. Benutzer navigiert zur Aufgabendetailansicht
2. `TaskDetailViewModel.LadenAsync(CancellationToken)` wird aufgerufen
3. Methode lädt die `Aufgabe` aus der Datenbank (inklusive `BranchName`-Property)
4. `Aufgabe`-Property wird gesetzt, triggert den Property-Setter
5. Setter ruft `OnPropertyChanged(nameof(AufgabeBranchName))` auf
6. DataBinding in `TaskDetailView.xaml` aktualisiert `StatusIndicatorControl.BranchName`
7. XAML-Template von `StatusIndicatorControl` rendert Status und Branch-Name zusammen

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `Aufgabe`, `StatusIndicatorControl`, `TaskDetailView.xaml`

## Neue Klassen

Keine neuen Klassen erforderlich.

## Änderungen an bestehenden Klassen

### `StatusIndicatorControl` (XAML-Code-Behind, Typ: WPF UserControl)

- **Neue Dependency Properties:** 
  - `BranchNameProperty` — Registrierte Dependency Property für den Branch-Namen
  - `BranchName` (Property) — Public Get/Set für den Branch-Namen; Typ: `string?`

- **Neue Methoden:** Keine erforderlich (DataBinding kümmert sich um Updates)

- **XAML-Template-Änderungen:** 
  - Neuer TextBlock oder ähnliches UI-Element für Branch-Name
  - Binding: `{Binding BranchName, RelativeSource={RelativeSource AncestorType=local:StatusIndicatorControl}, Mode=OneWay}`
  - Conditional Visibility basierend auf `BranchName` (nur anzeigen wenn nicht null/leer)
  - Formatierung: Branch-Name wird zusammen mit Status angezeigt (z. B. "Gestartet • feature/login" oder ähnlich)

### `TaskDetailViewModel` (Typ: MVVM ViewModel)

- **Neue Eigenschaften:** 
  - `AufgabeBranchName` (Property) — Convenience-Property, die `Aufgabe?.BranchName ?? string.Empty` zurückgibt; Typ: `string`

- **Geänderte Methoden:** 
  - `Aufgabe` Property-Setter — Zusätzlich `OnPropertyChanged(nameof(AufgabeBranchName))` aufrufen, um DataBinding in der View zu triggern

- **Neue Events:** Keine

- **Neue Event-Handler:** Keine

### `TaskDetailView.xaml` (Typ: XAML UserControl)

- **Binding-Erweiterung:** 
  - Zum bestehenden `StatusIndicatorControl`-Element das Attribute `BranchName="{Binding AufgabeBranchName, Mode=OneWay}"` hinzufügen

## Datenbankmigrationen

Keine. Die Property `BranchName` existiert bereits in der `Aufgabe`-Entität und ist persistiert.

## Validierungsregeln

Keine neuen Validierungen erforderlich. Der Branch-Name wird vom System (nicht vom Benutzer) gesetzt.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

Keine bekannten Seiteneffekte. Die Änderungen sind auf UI und DataBinding beschränkt und beeinflussen keine bestehenden Geschäftslogik.

## Umsetzungsreihenfolge

1. **Dependency Property `BranchName` zu `StatusIndicatorControl` hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: In `StatusIndicatorControl.xaml.cs` eine neue Dependency Property `BranchNameProperty` registrieren und eine entsprechende Public Property `BranchName` implementieren (Get/Set). Pattern folgt dem bestehenden `StatusProperty`, `StatusTextProperty`, `StatusColorProperty`.

2. **XAML-Template von `StatusIndicatorControl` erweitern**
   - Voraussetzungen: Schritt 1 (Dependency Property `BranchName`)
   - Beschreibung: In `StatusIndicatorControl.xaml` ein neues TextBlock-Element hinzufügen, das den Branch-Namen anzeigt. Binding: `{Binding BranchName, RelativeSource={RelativeSource AncestorType=local:StatusIndicatorControl}, Mode=OneWay}`. Conditional Visibility (nur anzeigen, wenn `BranchName` nicht leer). Formatierung mit Trennzeichen zwischen Status und Branch (siehe offene Punkte).

3. **Convenience-Property `AufgabeBranchName` zu `TaskDetailViewModel` hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: In `TaskDetailViewModel.cs` eine neue Public-Only-Property `AufgabeBranchName` hinzufügen, die `Aufgabe?.BranchName ?? string.Empty` zurückgibt. Folgt dem bestehenden Muster von `AufgabeTitel` und `AufgabeStatus`.

4. **`TaskDetailViewModel.Aufgabe` Setter erweitern**
   - Voraussetzungen: Schritt 3 (Property `AufgabeBranchName`)
   - Beschreibung: Im Setter der `Aufgabe`-Property (aktuell Zeilen 69–87) zusätzlich `OnPropertyChanged(nameof(AufgabeBranchName))` aufrufen. Wird zusammen mit den bestehenden `OnPropertyChanged`-Aufrufen eingefügt.

5. **`TaskDetailView.xaml` mit Binding für Branch-Name erweitern**
   - Voraussetzungen: Schritte 1, 3, 4 (Dependency Property, ViewModel-Property, Setter-Update)
   - Beschreibung: Im `StatusIndicatorControl`-Element (aktuell Zeile 317) das Attribute `BranchName="{Binding AufgabeBranchName, Mode=OneWay}"` hinzufügen.

6. **Unit-Tests für neue Properties schreiben**
   - Voraussetzungen: Schritte 1–5
   - Beschreibung: Tests für `AufgabeBranchName` schreiben — überprüft, dass die Property den `Aufgabe.BranchName`-Wert oder einen Standardwert zurückgibt. Tests für die Dependency Property `BranchName` in `StatusIndicatorControl` — überprüft Get/Set.

7. **E2E-Tests für Branch-Anzeige schreiben**
   - Voraussetzungen: Schritte 1–5, bestehende E2E-Test-Infrastruktur
   - Beschreibung: E2E-Test, der eine Aufgabe mit einem Branch-Namen lädt und überprüft, dass der Branch-Name in der Statusleiste angezeigt wird.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AufgabeBranchName_WhenAufgabeIsNull_ReturnsEmptyString` | `TaskDetailViewModelTests` | `AufgabeBranchName` gibt leerer String zurück wenn `Aufgabe` null ist |
| `AufgabeBranchName_WhenAufgabeHasBranchName_ReturnsBranchName` | `TaskDetailViewModelTests` | `AufgabeBranchName` gibt `Aufgabe.BranchName` zurück |
| `BranchName_DependencyProperty_GetSet` | `StatusIndicatorControlTests` | Dependency Property `BranchName` kann gesetzt und gelesen werden |
| `BranchName_DisplayedInUI_WhenNotEmpty` | E2E-Tests | Branch-Name wird in der Statusleiste angezeigt wenn nicht leer |
| `BranchName_Hidden_WhenEmpty` | E2E-Tests | Branch-Name wird in der Statusleiste verborgen wenn leer |

### Betroffene bestehende Tests

Keine. Die Änderungen sind additive und brechen keine bestehenden Signaturen oder Verhalten.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aufgabe mit Branch-Name laden und anzeigen | `TaskDetailViewTests` oder ähnlich | Branch-Name wird in der Statusleiste zusammen mit dem Status angezeigt |
| Aufgabe ohne Branch-Name laden | `TaskDetailViewTests` oder ähnlich | Statusleiste zeigt nur Status an, keine leere Branch-Anzeige |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine. Die neue Branch-Anzeige ist optional und wird nur angezeigt, wenn ein Branch-Name vorhanden ist.

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | **Trennzeichen:** Welches Trennzeichen zwischen Status und Branch-Name? | Empfohlenes Zeichen: `•` (Bullet, Unicode U+2022). Format: `"Gestartet • feature/login-fix"`. Alternative: `|`, `—`, oder mit Klammern `"Gestartet (feature/login-fix)"`. |
| 2 | **Styling:** Soll der Branch-Name in einer anderen Farbe oder Schriftgröße angezeigt werden? | Empfohlenes Styling: Gleiche Farbe und Größe wie Status-Text für visuelle Konsistenz. Alternativ: Branch-Name in grau oder kleinerer Schrift als Sekundärinformation. |
| 3 | **Kürzen von Branch-Namen:** Sollen lange Branch-Namen gekürzt werden? | Empfohlenes Verhalten: Vollständiger Branch-Namen anzeigen (keine Kürzung). Bei sehr langen Namen kann TextTrimming oder Ellipsis in Betracht gezogen werden, aber das ist optional und sollte nicht Teil dieser Anforderung sein. |
| 4 | **Sichtbarkeitskriterium genau:** Nur bei Status `Gestartet`, oder auch bei `Wartend`, `Beendet`, `Archiviert`? | Die Anforderung sagt "Status `Gestartet` oder höher wenn ein Branch vorhanden ist". Das bedeutet wahrscheinlich nur bei `Gestartet` anzuzeigen (und nicht bei `Neu`, `Wartend`, etc.). Dies sollte mit dem Product Owner geklärt werden. Empfohlene Implementierung: Branch-Name nur anzeigen wenn Status == `Gestartet` AND BranchName nicht leer. |

