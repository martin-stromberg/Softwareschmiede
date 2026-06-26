# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Softwareschmiede.Tests.App.ViewModels.TaskDetailViewModelTests

- **LoeschenCommand_CanExecuteFalse_WennStatusBeendet** — Expected sut.LoeschenCommand.CanExecute(null) to be False, but found True.
- **KannLoeschen_IsTrue_WhenStatusGestartet** — Expected sut.KannLoeschen to be True, but found False.
- **KannLoeschen_IsFalse_WhenStatusArchiviert** — Expected sut.KannLoeschen to be False, but found True.
- **KannLoeschen_IsFalse_WhenStatusBeendet** — Expected sut.KannLoeschen to be False, but found True.

### Softwareschmiede.Tests.E2E.WpfE2ETests

- **ProjektErstellen_UndNeueAufgabeAnlegen_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- **DarkModeAktivierenUndPersistieren_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 5s gefunden.
- **AufgabeAnlegen_ZeigtStartenButton_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- **EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- **ProjektErstellen_ZeigtAufgabenListe_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- **EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E** — System.Exception: Could not find process with id: 55560.

### Softwareschmiede.Tests.E2E.ProjectDetailE2ETests

- **ProjektOeffnenUndZurueck_ErneutOeffnen_E2E** — System.Exception: Could not find process with id: 53516.
- **RepositoryZuweisenDialog_ScmPluginListe_EnthaeltErwartetePlugins_E2E** — System.Exception: Could not find process with id: 52440.
- **ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E** — System.Exception: Could not find process with id: 53420.
- **AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- **ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E** — System.Exception: Could not find process with id: 54580.
- **AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E** — System.Exception: Could not find process with id: 34412.
- **ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- **NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E** — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.

## Zusammenfassung

- Gesamt: 18
- Bestanden: 0
- Fehlgeschlagen: 18
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** Nicht messbar

Grund: Testhostprozess ist abgestürzt. Runtime-Fehler bei .NET 10 (hostpolicy.dll nicht in 'C:\Program Files\dotnet' gefunden). Keine Coverage-Daten verfügbar.

## Fehlende Tests

### Ursachenanalyse der Fehler

#### Unit-Tests (4 Fehler)
Alle fehlgeschlagenen Tests sind in `TaskDetailViewModelTests` und betreffen die Delete-Logik:
- `KannLoeschen` Property gibt falsche Werte zurück für Status 'Gestartet', 'Archiviert', 'Beendet'
- `LoeschenCommand.CanExecute()` gibt True zurück, wenn es False sein sollte für Status 'Beendet'
- Diese Tests existieren und sind getestet, aber die Implementierung erfüllt nicht die Anforderungen

#### E2E-Tests (14 Fehler)
Die E2E-Tests schlagen in zwei Kategorien fehl:
1. **UI-Element Timeouts (8 Tests):** Warten auf Buttons, Dialoge oder Navigation-Elemente schlägt fehl
   - Betroffen: ProjektErstellen_ZeigtAufgabenListe, AufgabeAnlegen_ZeigtStartenButton, etc.
   - Grund: Wahrscheinlich Rendering- oder Initialisierungsverzögerungen in WPF-Tests

2. **Prozessabstürze (6 Tests):** FlaUI kann die WPF-Anwendungsprozesse nicht mehr finden
   - Betroffen: E2E-Tests nach etwa 1-2 Minuten Laufzeit
   - Grund: Application-Host bricht ab (siehe Runtime-Fehler unten)

#### Runtime-Fehler
Der Testhostprozess ist während der E2E-Tests abgestürzt mit:
```
A fatal error was encountered. The library 'hostpolicy.dll' required to execute 
the application was not found in 'C:\Program Files\dotnet'.
Failed to run as a self-contained app.
```

Dies verhindert eine vollständige Ausführung aller Tests und die Erfassung von Coverage-Daten.

### Test-Dateien Status

Alle vorhanden Test-Dateien werden ausgeführt:
- `Softwareschmiede.Tests.dll` — 18 Tests ausgeführt (alle fehlgeschlagen)
- `Softwareschmiede.IntegrationTests.dll` — Assembly nicht gefunden (kompiliert nicht oder nicht im Debug-Verzeichnis)

Keine fehlenden Test-Dateien identifizierbar basierend auf der Eingabe-Konvention (die vorhandenen Tests werden ausgeführt).

## Anmerkungen

### Kritische Probleme
1. **.NET 10 Runtime-Fehler** — hostpolicy.dll nicht in .NET-Verzeichnis
   - Blockiert alle E2E-Tests nach kurzer Laufzeit
   - Verhindert Coverage-Messung

2. **Unit-Test Failures** — Logik-Fehler in TaskDetailViewModel
   - Die Delete-Permission Logic ist fehlerhaft
   - 4 Tests dokumentieren das erwartete Verhalten, das nicht implementiert ist

3. **E2E-Test Timeouts** — UI-Automatisierungs-Probleme
   - Wahrscheinlich fehlende oder verzögert rendernde Elemente
   - Oder zu kurze Wartezeiten für Anwendungsinitialisierung

### Empfohlene Maßnahmen
1. `.NET 10 Runtime` reparieren oder neustarten
2. `TaskDetailViewModel.KannLoeschen` und `LoeschenCommand` implementierungs-Logic überprüfen
3. E2E-Test `WaitForElement` Timeouts erhöhen und Wartelogik verbessern
