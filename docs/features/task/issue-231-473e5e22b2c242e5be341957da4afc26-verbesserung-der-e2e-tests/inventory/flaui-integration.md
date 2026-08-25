# FlaUI-Integration

Diese Datei dokumentiert die bereits vorhandene FlaUI-Integration und deren Nutzung in der Test-Infrastruktur.

## FlaUI-Abhängigkeiten

**NuGet-Pakete:**
- `FlaUI.Core`
- `FlaUI.UIA3` (für Windows-UI-Automation-API)

**Namespaces in WpfTestBase:**
- `FlaUI.Core.AutomationElements` — Automatisierungselemente (Window, AutomationElement)
- `FlaUI.Core.Conditions` — Such- und Bedingungsfabrik (ConditionFactory)
- `FlaUI.Core.Input` — Tastatur-Eingabe (Keyboard)
- `FlaUI.UIA3` — UIA3-Automatisierungs-Implementierung

## Zentrale FlaUI-Klassen in der Test-Nutzung

### `UIA3Automation`

**Zweck:** Root-Automatisierungskontext für alle UI-Automation-Abfragen

**Nutzung in WpfTestBase:**
```
_automation = new UIA3Automation();
```

**Verfügbar als:** `protected UIA3Automation Automation { get; }`

### `FlaUI.Core.Application`

**Zweck:** Repräsentation der gestarteten Anwendung

**Nutzung in WpfTestBase:**
```
_application = FlaUI.Core.Application.Launch(appPath);
_application.WaitWhileMainHandleIsMissing(Long);
_application.GetMainWindow(Automation, Long);
```

**Zentrale Methoden:**
- `Launch(string path)` — Startet die App
- `GetMainWindow(UIA3Automation automation, TimeSpan timeout)` — Ruft das Hauptfenster ab
- `Close()` — Schließt die App
- `WaitWhileMainHandleIsMissing(TimeSpan timeout)` — Wartet bis Fenster erscheint
- `HasExited` — Property: ob Prozess beendet ist
- `MainWindowHandle` — Property: Fenster-Handle
- `ProcessId` — Property: Prozess-ID

**Verfügbar als:** `protected FlaUI.Core.Application FlaUiApp { get; }`

### `Window` (AutomationElements.Window)

**Zweck:** Repräsentation eines WPF-Fensters

**Nutzung in WpfTestBase:**
```
var mainWindow = app.GetMainWindow(Automation, Long)!;
// oder
var dialog = WaitForWindow("Repository zuweisen", Short);
```

**Zentrale Methoden:**
- `FindFirstDescendant(Func<ConditionFactory, ConditionBase> conditionFunc)` — Sucht erstes übereinstimmendes Element
- `FindAllChildren(Func<ConditionFactory, ConditionBase> conditionFunc)` — Sucht alle direkten Kind-Elemente
- `Click()` — Klickt auf Fenster
- `Focus()` — Setzt Fokus
- `AsButton()`, `AsTextBox()`, `AsCheckBox()`, `AsComboBox()` — Typ-spezifische Wrapper

### `AutomationElement`

**Zweck:** Allgemeine Repräsentation von UI-Elementen (Buttons, TextBoxen, etc.)

**Zentrale Methoden:**
- `FindFirstDescendant(Func<ConditionFactory, ConditionBase> conditionFunc)` — Sucht erstes übereinstimmendes Element
- `FindAllChildren(Func<ConditionFactory, ConditionBase> conditionFunc)` — Sucht alle direkten Kind-Elemente
- `Click()` — Klickt auf Element
- `DoubleClick()` — Doppelklick
- `Focus()` — Setzt Fokus
- `AsButton()`, `AsTextBox()`, `AsCheckBox()`, `AsComboBox()` — Typ-spezifische Wrapper
- `Name` — Property: Automation Name des Elements
- `HelpText` — Property: Hilfetext des Elements (fallback wenn Name leer)

### Typ-Spezifische Wrapper

**Button:**
```
var button = element.AsButton();
button.Click();
```

**TextBox:**
```
var textBox = element.AsTextBox();
textBox.Text = "neuer Text";
var text = textBox.Text;
```

**CheckBox:**
```
var checkbox = element.AsCheckBox();
checkbox.IsChecked = true;
var isChecked = checkbox.IsChecked;
```

**ComboBox:**
```
var comboBox = element.AsComboBox();
var selectedItem = comboBox.SelectedItem;
comboBox.Click();
```

## Bedingungsfabrik (ConditionFactory)

**Zweck:** Definierung von Such-Kriterien für Element-Abfragen

**Zentrale Such-Methoden:**
- `cf.ByName(string name)` — Sucht nach Automation Name
- `cf.ByAutomationId(string id)` — Sucht nach Automation ID (z. B. "6" für IDYES in MessageBox)
- `cf.ByControlType(ControlType type)` — Sucht nach Element-Typ (List, ListItem, Button, etc.)
- `cf.And(ConditionBase condition)` — Kombiniert mehrere Bedingungen (AND-Verknüpfung)

**Beispiele aus WpfTestBase:**
```
// Nach Name suchen
WaitForElement(mainWindow, cf => cf.ByName("Speichern"), Short)

// Nach Automation ID suchen
WaitForElement(msgBox, cf => cf.ByAutomationId("6"), Short)

// Nach Kontroltyp suchen
listBox.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem))

// Kombinierte Suche
mainWindow.FindFirstDescendant(cf => 
    cf.ByName(titel).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)))
```

## Tastatur-Eingabe

**Klasse:** `FlaUI.Core.Input.Keyboard`

**Zentrale Methoden:**
- `Keyboard.Type(string text)` — Tippt Text
- `Keyboard.TypeSimultaneously(VirtualKeyShort key1, VirtualKeyShort key2, ...)` — Drückt mehrere Tasten gleichzeitig (z. B. Strg+A)

**Beispiel aus WpfTestBase:**
```
Keyboard.Type(name);
Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, 
                            FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
```

## Bestehende Suchmuster in Tests

### Patterns für häufige Szenarien

1. **Element mit Name suchen:**
   ```csharp
   var element = WaitForElement(mainWindow, cf => cf.ByName("ElementName"), Short);
   ```

2. **Fenster mit Titel finden:**
   ```csharp
   var dialog = WaitForWindow("DialogTitle", Short);
   ```

3. **Element verschwinden warten:**
   ```csharp
   WaitUntilGone(mainWindow, cf => cf.ByName("ElementName"), Short);
   ```

4. **ComboBox-Eintrag wählen:**
   ```csharp
   SelectComboBoxItemByClick(comboBoxElement, "ItemName", Short);
   WaitForSelectedComboBoxItem(comboBoxElement, "ItemName", Short);
   ```

5. **Listen-Items durchlaufen:**
   ```csharp
   var listBox = element.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
   var items = listBox.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
   ```

6. **Element per Zustand prüfen:**
   ```csharp
   var element = mainWindow.FindFirstDescendant(cf => cf.ByName("ElementName"));
   if (element is not null) { /* Element sichtbar */ }
   else { /* Element nicht sichtbar */ }
   ```

## Fehlerbehandlung in FlaUI

### Häufige Exceptions

- **`FlaUI.Core.Exceptions.PropertyNotSupportedException`** — Property nicht von Automatisierung unterstützt (z. B. `HelpText` bei manchen Elementen)
- **`TimeoutException`** — Element nicht innerhalb des Timeouts gefunden
- **`InvalidOperationException`** — Falsche Element-Typ-Konvertierung (z. B. `.AsButton()` auf non-Button)

### Fehlerdiagnose in WpfTestBase

- **Fail-Fast bei Fehlerbanner:** `WaitForElement()` prüft bei jedem Polling-Intervall, ob "FehlerMeldung" sichtbar ist
- **App-Startup-Log-Inspektion:** Wenn `LaunchApp()` das Fenster nicht findet, wird das App-Log auf Startup-Exceptions geprüft (z. B. XamlParseException)

## Prozess- und Handle-Management

**Process-Integration:**
- `System.Diagnostics.Process` wird für Git-Befehle und allgemeine Prozesssteuerung genutzt
- `Process.GetProcessById(processId)` zum Prüfen, ob Prozess noch läuft
- `process.WaitForExit()` zum Warten auf Prozessende

## Threading und Synchronisation

**Bestehende Patterns:**
- `Thread.Sleep(milliseconds)` für kurze Pausen zwischen Poll-Iterationen
- Deadline-basierte Loops (z. B. `while (DateTime.UtcNow < deadline)`) für zeitgesteuerte Abfragen
- Async-Methoden für langwierige Operationen (z. B. `WaitForProzessStartEintragAsync()`)
