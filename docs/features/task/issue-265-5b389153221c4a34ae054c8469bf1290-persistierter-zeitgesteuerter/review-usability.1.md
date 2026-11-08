# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailView.xaml / PromptZeitVersandService (Aufgabendetailansicht, Ribbon-Gruppe „CLI")

- **Unerklärte Blockade** — Die Statusanzeige zeigt für einen wartenden Plan ausschließlich „Prompt in Wartestellung" plus die Zielzeit im Format HH:mm (`ScheduledPromptStatus`/`ScheduledPromptTimeDisplay`, befüllt aus `GetScheduledPromptStatus(...).TargetTime`). Neu ist durch die Persistenz der Zustand „Zielzeit verstrichen, aber keine CLI-Session vorhanden" (`WartetAufSession`): Der Plan wird dann erst beim nächsten CLI-Start zugestellt. In genau diesem Zustand zeigt die Anzeige eine **vergangene Uhrzeit** unter der Zeile „Prompt in Wartestellung" (z. B. „14:30", obwohl es 18:00 ist und nichts passiert ist) — etwa nach App-Neustart mit überfälligem Plan oder wenn die CLI vor Fälligkeit gestoppt wurde. Ein Laie kann daraus nicht erkennen, dass der Plan weiterhin gültig ist und auf den nächsten CLI-Start wartet; er wird annehmen, der Prompt sei bereits versendet worden, verfallen oder defekt — und ggf. unnötig neu planen (was den Plan ersetzt) oder stornieren.

  Empfehlung: Den Zustand „wartet auf CLI-Start" in der Statusanzeige verständlich unterscheiden — z. B. Statustext „Prompt wartet auf CLI-Start" (statt oder ergänzend zur vergangenen Uhrzeit). Das Flag `WartetAufSession` existiert bereits im Service-Eintrag und müsste dafür über `GetScheduledPromptStatus` (oder eine eigene Abfrage) an die Oberfläche gereicht werden.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Promptvorlage auswählen und zeitgesteuert senden („Zeitgesteuert senden", ComboBox mit Klartext-Namen, HH:mm-Eingabe, Validierungsfehler „Ungültige Stunde (0–23)"/„Ungültige Minute (0–59)") → unauffällig
- Geplanten Prompt im Status sehen („Prompt in Wartestellung" + Uhrzeit), inkl. Wiederherstellung nach Verlassen der Ansicht und App-Neustart (`AktualisiereWartestellungsAnzeige` in `LadenAsync`) → unauffällig für zukünftige Zielzeit; Befund bei verstrichener Zielzeit ohne Session
- Wartestellung manuell aufheben (neuer Button „Wartestellung aufheben", `AutomationProperties.Name="WartestellungAufheben"` → `CancelScheduledPromptCommand` → `CancelScheduledPromptAsync`: Timer + persistierter Datensatz werden entfernt, Anzeige verschwindet via `ScheduledPromptChanged`) → unauffällig; auch bei gestoppter CLI klickbar (kein CanExecute-Gate — nötig, da der Plan den CLI-Stopp nun überlebt)
- Versand-Feedback nach Zustellung (bei geöffneter Detailansicht Wechsel in die CLI-Ansicht via `PromptSent`; bei Hintergrundversand Protokolleintrag „Zeitgesteuerter Prompt versendet." / „Zeitgesteuerter Prompt nach CLI-Start zugestellt.") → unauffällig
- Persistenz-Indikator in der aktiven Aufgabenliste (`⏳`/`HasScheduledPrompt` über `GetScheduledPromptStatus` — spiegelt auch wiederhergestellte Pläne) → unauffällig
- Zielzeit-Granularität nur HH:mm des heutigen Tages (vergangene Uhrzeit ⇒ Sofortversand) → unauffällig; dokumentiertes, von der Anforderung bewusst unverändert belassenes Verhalten (kein neuer Befund)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/App.xaml.cs` (nur App-Start-Re-Armierung, keine Bedienelemente)
- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` (soweit es die Bedienelemente/Statusanzeige versorgt)
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs` (E2E-PageObject, nicht anwenderseitig)
