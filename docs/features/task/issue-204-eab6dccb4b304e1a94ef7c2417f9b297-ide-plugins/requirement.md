# Anforderung

## Fachliche Zusammenfassung

Die UI-Schicht (Ribbon im TaskDetailView) soll ein Split-Button-Muster für die IDE-Öffnen-Funktion implementieren. Der Haupt-Button öffnet direkt die erste (priorisierte) IDE/den ersten Einstiegspunkt – nach dem bestehenden Fallback-Verhalten. Ein daneben platzierter Dropdown-Button mit Pfeil nach unten wird nur bei mehreren verfügbaren Einstiegspunkten sichtbar und zeigt eine Auswahlliste, über die der Anwender gezielt zwischen kompatiblen IDE-Plugins und/oder mehreren Einstiegspunkten (z. B. mehrere `.sln`-Dateien) einer IDE wählen kann. Diese Änderung betrifft die WPF-UI-Schicht und erweitert das bestehende IDE-Plugin-System, ohne die Domain- oder Application-Logik zu beeinflussen.

## Betroffene Klassen und Komponenten

### UI-Komponenten / Views & ViewModels
- **`Softwareschmiede.App/Views/TaskDetailView.xaml`** — Ribbon-Bereich mit IDE-Button
  - Ersetzung des einfachen `RibbonLargeButton` durch ein Split-Button-Konstrukt (Haupt-Button + Dropdown-Button)
  - Platzierung in der bestehenden `RibbonGroup` "Arbeitsverzeichnis"

- **`Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`** — ViewModel für TaskDetailView
  - Existierende Kommandos: `OeffneIdeCommand` (führt `OeffneIdeAsync` aus)
  - Ggf. neues Kommando: `OeffneIdeAuswahlCommand` (zeigt Auswahldialog bei Mehrfachwahl)
  - Ggf. neue Property: `KannIdeAuswaehlen` (Boolean, nur `true` wenn mehrere Einstiegspunkte gefunden werden)

- **`Softwareschmiede.App/Controls/RibbonLargeButton.xaml` und ggf. neue Komponente** — Ribbon-Button-Steuerung
  - Ggf. neue `RibbonSplitButton.xaml`-Komponente für Split-Button-Darstellung, oder Erweiterung bestehender Komponenten

### Dialog-Services
- **`Softwareschmiede.App/Services/IDialogService`** — Existierendes Interface
  - Methode `ShowSolutionSelectionDialogAsync` wird bereits verwendet; kann wiederverwendet oder um eine gezielte IDE-Auswahl erweitert werden
  - Ggf. neue Methode: `ShowIdeSelectionDialogAsync` (zeigt nicht nur Dateipfade, sondern auch IDE-Plugin-Informationen)

### Business-Logik (unverändert)
- **`Softwareschmiede.Application.Services.IdeOeffnenService`** — bestehende Methode `OpenRepositoryInIdeAsync`
  - Keine Änderungen erforderlich; wird weiterhin über Callbacks Einstiegspunkte ermitteln
  - `waehleEntryPointAsync`-Callback wird durch neue UI-Logik gespeist

- **`Softwareschmiede.Domain.Interfaces.IIdePlugin`** — bestehende Plugin-Schnittstelle
  - Keine Änderungen erforderlich

- **`Softwareschmiede.Application.Services.PluginSelectionService`** — Plugin-Auflösung
  - Methode `ResolveIdePluginAsync` bleibt unverändert
  - Ggf. neue Hilfsmethode: Auflösen aller kompatiblen IDE-Plugins statt nur des priorisierten

### Tests
- `Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_*` — Bestehende ViewModel-Tests
  - Tests für neue Commands / Properties (`OeffneIdeAuswahlCommand`, `KannIdeAuswaehlen`)
  - Tests für Fallback-Verhalten (Aufruf von `OeffneIdeCommand` bei keinem/einem Einstiegspunkt)
  - Tests für Auswahldialog-Anruf bei mehreren Einstiegspunkten

- `Softwareschmiede.Tests/App/Views/E2E_*` oder `ViewModelTests` — ggf. E2E-Tests
  - Visuelle Prüfung des Split-Button-Layouts bei 0/1/n Einstiegspunkten
  - Interaktion: Haupt-Button-Klick öffnet direkt, Dropdown-Button-Klick zeigt Dialog

## Implementierungsansatz

### Schritt 1: UI-Komponente definieren
Entweder:
- *Option A:* Erweitere `RibbonLargeButton` um optionale `DropdownCommand`-Bindung und Sichtbarkeitskontrolle des Dropdown-Buttons
- *Option B:* Erstelle neue Komponente `RibbonSplitButton.xaml` mit zwei untergeordneten Button-Bereichen (Haupt + Dropdown)

Empfehlung: **Option B** ist sauberer und wiederverwendbar für andere Split-Button-Anforderungen.

### Schritt 2: ViewModel erweitern
In `TaskDetailViewModel`:
- Neues `ICommand` `OeffneIdeAuswahlCommand` → ruft neue Methode `OeffneIdeAuswahlAsync` auf
- Neue Property `bool KannIdeAuswaehlen` → berechnet aus der Anzahl ermittelter Einstiegspunkte (gebunden an Dropdown-Sichtbarkeit)
- Ggf. Property `IReadOnlyList<IdeEntryPoint> VerfuegbareEinstiegspunkte` → gepufferte Liste aus letztem Check
- `OeffneIdeAuswahlAsync` → gleiche Logik wie aktuell `OeffneIdeAsync`, aber **erzwingt** den Dialog statt Fallback auf ersten Einstiegspunkt

### Schritt 3: TaskDetailView anpassen
In `TaskDetailView.xaml`:
- Ersetze den aktuellen `RibbonLargeButton` (Zeile ~180–183) durch neue `RibbonSplitButton`-Komponente
- Haupt-Button: `OeffneIdeCommand` (bisheriges Verhalten)
- Dropdown-Button: `OeffneIdeAuswahlCommand`, sichtbar nur wenn `KannIdeAuswaehlen == true`

### Schritt 4: Dialog-Service prüfen / erweitern
Bestehende `ShowSolutionSelectionDialogAsync` wird derzeit nur mit Dateipfaden gefüttert. Ggf.:
- Anpassung: Auch IDE-Plugin-Namen in der Anzeige aufführen (z. B. "Visual Studio 2022 – MyProject.sln")
- Alternative: Neue Methode `ShowIdeSelectionDialogAsync(IReadOnlyList<IdeEntryPoint>)` mit reicherer Anzeige

### Schritt 5: Fehlerbehandlung
- Fall "Keine Einstiegspunkte gefunden" (bereits von `IdeOeffnenService` abgedeckt)
- Fall "Anwender bricht Auswahldialog ab" (Callback liefert `null`, wird bereits behandelt)
- Fall "Mehrfach-Aufrufe während laufender Ermittlung" → ggf. Kommando-CanExecute bei Async-Lauf auf `false` setzen

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Das Verhalten ist vollständig an die Anzahl ermittelter Einstiegspunkte gebunden:
- **0 Einstiegspunkte**: Fehlerdialog (bestehend)
- **1 Einstiegspunkt**: Nur Haupt-Button sichtbar; Klick öffnet direkt (bestehend)
- **≥2 Einstiegspunkte**: Haupt-Button + Dropdown-Button; Haupt-Button öffnet den ersten, Dropdown zeigt Auswahl (neu)

IDE-Plugin-Priorisierung und Aktivierung bleiben über die bestehenden Einstellungen (`SettingsViewModel.IdePlugins`) konfigurierbar.

## Offene Fragen

1. **Dialog-Inhalt bei mehreren IDEs**: Wenn mehrere IDE-Plugins kompatibel sind (z. B. VS + VS Code für ein `.csharp`-Projekt), soll der Auswahldialog:
   - Nur die Einstiegspunkte des priorisierten Plugins zeigen (aktuell)?
   - Oder alle Einstiegspunkte aller kompatiblen Plugins inklusive Plugin-Name gruppiert anzeigen (neu, umfassender)?

2. **Haupt-Button Fallback-Logik**: Wenn der priorisierte Plugin A gibt keinen Einstiegspunkt zurück, Plugin B aber schon – soll Haupt-Klick dann:
   - Fallback auf Plugin B? (bestehend in `PluginSelectionService.ResolveIdePluginAsync`)
   - Oder Fehler werfen / Dialog erzwingen?

3. **Datei-Dialog vs. Struktur-Dialog**: Sollen die Einstiegspunkte als flache Pfad-Liste (bisherig) oder hierarchisch/gruppiert nach IDE angezeigt werden? Dies beeinflusst die Komponenten-Komplexität.

4. **Tastatur-Navigation**: Gibt es ein bestehendes Muster für Split-Buttons im Ribbon (z. B. `Alt+I` für Haupt-Button, `Alt+I, D` für Dropdown)? Oder Tab-Navigation zum Dropdown?

5. **Async-Ermittlung der Einstiegspunkte**: Sollen die verfügbaren Einstiegspunkte bei View-Initialisierung / Aufgaben-Wechsel ermittelt werden, oder erst on-demand beim Dropdown-Klick (schneller bei wenigen Aufgaben, aber ggf. Latenz beim Dialog)?
