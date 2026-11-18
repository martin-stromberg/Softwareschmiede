# Usability-Review

## Ergebnis

**Status:** Keine Befunde

Geprüft gegen den Branch-Stand der Umsetzung. Die geforderte Konfiguration der
Ausführungsbegrenzung ist anhand der Oberfläche ohne Kenntnis interner Strukturen
erreichbar und verständlich beschriftet. Ergänzend zur statischen Prüfung wurde
der Benutzerfluss per FlaUI-E2E ausgeführt
(`Einstellungen_AusfuehrungsLimitsGlobalUndProPlugin_E2E`, siehe
[test-results.md](test-results.md)) — der Nachweis ersetzt die statische
Sichtprüfung nicht, deckt aber Setzen, Validieren und Persistieren ab.

## Geprüfte Interaktionen

- **Globales Limit finden und setzen** → unauffällig. Das Feld
  „Maximale parallele Ausführungen“ steht im „Automatisierung“-Tab im Expander
  „Aufgaben ausführen“, direkt unter den SCM-Ereignis-Einstellungen, für die es
  fachlich gehört. Der Erläuterungstext nennt alle Kernregeln in einem Satz:
  Ganzzahl ≥ 1, leer = unbegrenzt, Wartegrund bei vollem Limit, Priorisierung
  (Fortsetzungen vor fälligen Verzögerungen vor Eingangsreihenfolge) und den
  Verweis auf das zusätzliche Limit pro KI-Plugin unter „Plugins“.
- **Plugin-Limit finden und setzen** → unauffällig. Im „Plugins“-Tab erscheint
  nach Auswahl eines KI-Plugins die Gruppe „Ausführungsbegrenzung“ mit dem Feld
  „Maximale parallele Ausführungen“ (Platzhalter „unbegrenzt“); die
  Feld-Beschreibung verweist ausdrücklich auf das zusätzlich geltende globale
  Limit. SCM-Plugins zeigen die Gruppe nicht — fachlich korrekt, da das Limit
  nur CLI-Ausführungen betrifft.
- **Leer lassen = unbegrenzt** → unauffällig. Beide Felder akzeptieren Leerwerte;
  leer wird als `null` persistiert und beim Neuladen wieder leer angezeigt
  (E2E- und ViewModel-Tests).
- **Ungültige Eingabe** → unauffällig. „0“, negative Zahlen und Nicht-Zahlen
  führen beim Speichern zu einer deutschsprachigen Fehlermeldung im
  Fehlerbanner („…muss leer (unbegrenzt) oder eine Ganzzahl größer oder gleich
  1 sein.“); es wird nichts persistiert, „Verwerfen“ setzt den Zustand zurück.
- **Wartegrund für den Anwender** → unauffällig. Bei erreichtem Limit wartet das
  Ereignis mit benutzerlesbarem Grund („Das globale Limit … ist erreicht; der
  Start erfolgt, sobald ein Platz frei wird.“) plus Systemmeldung im Protokoll;
  es entsteht kein stilles Hängen.

## Befunde

Keine.

## Geprüfte Dateien

- `src/Softwareschmiede.App/Views/SettingsView.xaml` — globaler Bereich
  `MaxParalleleAusfuehrungenBereich` (Label, TextBox, Erläuterungstext).
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` —
  `HostParallelLimitField` (Label/Placeholder/Description), Validierungstexte.
- `src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs` —
  benutzerlesbare Wartegründe.
