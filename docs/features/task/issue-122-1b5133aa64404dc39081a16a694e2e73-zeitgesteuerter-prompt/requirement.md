# Kundenanforderung – Zeitgesteuerter Prompt

## Fachliche Zusammenfassung

Die Promptvorlagen-Auswahlbox im Ribbon-Menü der Aufgabendetailansicht soll um die Möglichkeit erweitert werden, einen Prompt zeitverzögert zu versenden. Benutzende erhalten zwei Eingabefelder für Stunde und Minute, um eine Zielausführungszeit anzugeben. Sind diese Felder leer, wird der Prompt sofort versendet; ist eine Uhrzeit angegeben, wird der Prompt bis zur genannten Uhrzeit in einer Warteschlange gepuffert und dann automatisch an die CLI versendet. Der Ausführungsstatus wird während der Wartezeit als „Prompt in Wartestellung" angezeigt.

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- **`PromptVorlage`** (bestehend, Erweiterung ggf. nicht nötig): Persistierte Vorlage bleibt unverändert; die zeitliche Verzögerung ist zur Laufzeit (keine Persistierung erforderlich)

### Logikklassen / Services
- **`PromptZeitVersandService`** (neu): Verwaltet zeitgesteuerte Prompt-Versände
  - `SchedulePromptAsync(aufgabeId, promptText, targetTime)`: Plant einen Prompt zur angegebenen Uhrzeit
  - `CancelScheduledPromptAsync(aufgabeId)`: Bricht einen geplanten Versand ab (z.B. bei Aufgabenabschluss oder Ansichtswechsel)
  - `GetScheduledPromptStatusAsync(aufgabeId)`: Gibt Status und Zielzeit des geplanten Prompts zurück
  - Interner Timer/Background-Task, der zum Versand-Zeitpunkt den Prompt an die `PseudoConsoleSession` schreibt

### ViewModels
- **`TaskDetailViewModel`** (Erweiterung):
  - `ScheduledPromptTargetHours`: Bindbare Property für Stunde-Eingabefeld (int?, null bedeutet leer)
  - `ScheduledPromptTargetMinutes`: Bindbare Property für Minuten-Eingabefeld (int?, null bedeutet leer)
  - `ScheduledPromptStatus`: Property für Anzeigetext (z.B. „Prompt in Wartestellung" oder null)
  - `ScheduledPromptTimeDisplay`: Berechnete Property für die Anzeige der Zielzeit im Format `HH:mm`
  - `CanSchedulePrompt`: Boolean, ob zeitgesteuerte Versendung aktuell erlaubt ist (CLI läuft, Vorlage gewählt, Eingaben gültig)
  - `SchedulePromptCommand`: AsyncRelayCommand zum Versand-Button
  - Modifizierte `PromptVorlageAuswaehlenAsync`: Unterscheidung zwischen sofort und zeitgesteuert; bei Zeitangabe `PromptZeitVersandService.SchedulePromptAsync` aufrufen
  - Cleanup in `Dispose`: Alle geplanten Prompts für diese Aufgabe stornieren

### UI-Komponenten (XAML)
- **`TaskDetailView.xaml`** (Erweiterung im CLI-Ribbon):
  - Zwei `TextBox`-Eingabefelder für Stunde (Range 0–23) und Minute (Range 0–59) neben der Promptvorlage-Auswahlbox
  - `Button` mit Symbol (z.B. „⏰" oder „▶") zum Absenden mit Zeitangabe
  - Die Felder werden nach erfolgreichem Versand geleert
  - Die Felder sind deaktiviert, wenn `KannPromptVorlageSenden` false ist oder keine Vorlage ausgewählt

### Enums / Validation
- Neue Validierungslogik für Stunde (0–23) und Minute (0–59)
- Fehlermeldung bei ungültigen Eingaben (z.B. Stunde > 23)

### Tests
- **`PromptZeitVersandServiceTests`** (neu):
  - Test: Prompt wird sofort versendet, wenn Zielzeit in der Vergangenheit liegt
  - Test: Prompt wird gepuffert, wenn Zielzeit in der Zukunft liegt
  - Test: Nach Erreichen der Zielzeit wird Prompt automatisch versendet
  - Test: Abbruch eines geplanten Prompts
  - Test: Mehrere geplante Prompts pro Aufgabe werden korrekt verwaltet
  
- **`TaskDetailViewModelTests`** (Erweiterung):
  - Test: `ScheduledPromptTargetHours` und `ScheduledPromptTargetMinutes` Binding funktioniert
  - Test: Leere Eingabefelder führen zu sofortigem Versand
  - Test: Gültige Zeitangabe führt zu zeitgesteuert Versand
  - Test: Ungültige Eingaben (z.B. Stunde 25) werden abgelehnt
  - Test: ViewModel-Cleanup bei Dispose storniert geplante Prompts

## Implementierungsansatz

### Architektur
1. **Neuer Service `PromptZeitVersandService`**:
   - Singleton-Service (registriert in DI)
   - Verwaltet eine Warteschlange von geplanten Prompts pro Aufgabe (z.B. `Dictionary<Guid, ScheduledPromptInfo>`)
   - Nutzt einen `Timer` oder `BackgroundService`, der regelmäßig (z.B. alle 100ms) prüft, ob ein Versand-Zeitpunkt erreicht ist
   - Bei Erreichen der Zielzeit: Ruft `PseudoConsoleSession.InputStream.WriteAsync` auf (analog zu `PromptVorlageAuswaehlenAsync`)
   - Thread-safe durch `lock` oder `ReaderWriterLockSlim` (da Timer auf anderem Thread läuft)

2. **Modifikation von `TaskDetailViewModel`**:
   - Neue Properties `ScheduledPromptTargetHours`, `ScheduledPromptTargetMinutes`, `ScheduledPromptStatus`
   - `PromptVorlageAuswaehlenAsync` prüft, ob Zeitangabe vorhanden:
     - Leer → sofort versenden (bisheriges Verhalten)
     - Gültige Zeit → `_promptZeitVersandService.SchedulePromptAsync(…)` aufrufen
   - `Dispose` storniert geplante Prompts über `_promptZeitVersandService.CancelScheduledPromptAsync`

3. **UI-Integration (XAML)**:
   - Zwei neue `TextBox`-Felder mit `Int32` Konverter und `ValidationRule` für Range-Prüfung
   - Neuer Button neben der Promptvorlage-Auswahlbox; Binding auf `SchedulePromptCommand`
   - `ScheduledPromptStatus` als bindbare TextBlock-Property für Statusanzeige

### Events und Hooks
- **`PromptZeitVersandService.PromptGescheduled`**: Event, das auslöst, wenn ein Prompt geplant wurde (optional, für Logging/UI-Update)
- **`PromptZeitVersandService.PromptVersendet`**: Event nach erfolgreichem Versand (zum Neuladen des Aufgabenstatus)

### Abhängigkeiten
- `PromptZeitVersandService` ist abhängig von `KiAusfuehrungsService` (zum Abrufen der aktuellen `PseudoConsoleSession`)
- UI-Validierung nutzt WPF-Standard-Konverter (`Int32Converter`) und evtl. Custom `ValidationRule`

## Konfiguration

Kein benutzerdefinierbares Setting erforderlich. Die Wartezeit-Logik läuft automatisch im Hintergrund.

**Ggf. Konstanten (zur Laufzeit ggf. in Appsettings.json auslagern):**
- Timer-Intervall (z.B. 100ms für Prüfung der Versand-Zeitpunkte)
- Maximale Anzahl geplanter Prompts pro Aufgabe (z.B. 1, da nur einer zur Zeit geplant sein darf)

## Offene Fragen

1. **Parallelität:** Kann pro Aufgabe nur ein Prompt gleichzeitig geplant sein, oder mehrere? (Empfehlung: maximal einer, um Verwirrung zu vermeiden)

2. **Persistierung:** Sollen geplante Prompts über einen App-Neustart hinweg persistiert werden, oder nur während der aktuellen Session?

3. **Ungültige Zeiten:** Was ist das Verhalten, wenn die Zielzeit in der Vergangenheit liegt (z.B. 14:00 Uhr, aber es ist bereits 15:00 Uhr)?
   - Option A: Sofort versenden
   - Option B: Fehler anzeigen und abbrechen
   - Empfehlung: Option A

4. **Zeitzonen:** Sollen die Eingabefelder (Stunde/Minute) in der lokalen Zeitzone des Benutzers oder UTC interpretiert werden? (Empfehlung: Lokal, analog zur `System.DateTime.Now`)

5. **UI-Feedback:** Soll nach dem Absenden eine Bestätigung angezeigt werden (z.B. Toast-Nachricht oder Statusbar), dass der Prompt geplant wurde?

6. **Aufgabenabschluss:** Soll ein geplanter Prompt automatisch storniert werden, wenn die Aufgabe abgeschlossen wird (`Aufgabe.Status = Abgeschlossen`)?

7. **Fehlerbehandlung:** Wenn der Versand zur Zielzeit fehlschlägt (z.B. CLI wurde in der Zwischenzeit beendet), soll das dem Benutzer angezeigt werden, oder still ablaufen?

8. **Rücksetzverhalten:** Sollen die Eingabefelder nach erfolgreichem Versand geleert werden, und soll auch die Promptvorlage-Auswahl zurückgesetzt werden (wie beim sofortigen Versand)?
