# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Promptvorlage auswählen (ComboBox mit Klartext-Namen, `PromptVorlagenAuswahl`) → unauffällig
- Zielzeit per HH:mm-Eingabefeldern eingeben und per Ribbon-Button „Zeitgesteuert senden" planen → unauffällig (Command verdrahtet, `SchedulePromptCommand` → `SchedulePromptAsync`; Validierung mit verständlichen Fehlermeldungen „Ungültige Stunde (0–23)" / „Ungültige Minute (0–59)"; keinerlei interne Kennungen erforderlich)
- Statusanzeige nach dem Planen: „Prompt in Wartestellung" + Zielzeit im Format HH:mm → unauffällig (wird immer aus dem tatsächlichen Service-Zustand befüllt — auch bei Sofortversand oder vom Service aufs Pausenende verschobener Zielzeit wird kein falscher „wartend"-Status angezeigt)
- Plan „wartet auf Session" (Zielzeit verstrichen, keine aktive CLI-Session): Anzeige „Prompt wartet auf CLI-Start" statt vergangener Uhrzeit → unauffällig. Die Formulierung beschreibt wahrheitsgemäß den Zustand (Zustellung erfolgt automatisch beim nächsten CLI-Start), verwendet die etablierte App-Terminologie („CLI starten", „CLI-Status") und vermeidet den falschen Eindruck eines verfallenen/defekten Plans. Die Handlungsaffordanz ist gegeben: Der Ribbon-Button „CLI starten" (`CliNeustarten`) ist im selben Zustand sichtbar, ein Klick löst die Zustellung aus; auch ohne Nutzeraktion bleibt der Plan gültig und wird beim nächsten CLI-Start zugestellt.
- Explizites Storno: Button „Wartestellung aufheben" (`WartestellungAufheben`) → unauffällig (in beiden Wartestellungs-Zuständen sichtbar, da die Sichtbarkeit an `ScheduledPromptStatus` hängt; verdrahtet mit `CancelScheduledPromptCommand` → `CancelScheduledPromptAsync`, löscht Eintrag + persistierten Datensatz; die Anzeige verschwindet über `ScheduledPromptChanged` → `AktualisiereWartestellungsAnzeige`)
- Wiederanzeige beim (Wieder-)Öffnen der Detailansicht, auch für Pläne aus früheren Sitzungen/App-Läufen → unauffällig (`AktualisiereWartestellungsAnzeige()` wird in `LadenAsync` aufgerufen; `RestoreScheduledPromptsAsync` beim App-Start befüllt die Warteschlange inkl. „wartet auf Session"-Zustände)
- Verlassen der Detailansicht bzw. CLI-Stopp: Der persistierte Plan wird nicht mehr verworfen → unauffällig (kein `CancelScheduledPrompt` mehr in `Dispose`/`OnCliProcessStatusChanged`; die Statusanzeige wird ausschließlich über `ScheduledPromptChanged`/`PromptSent` synchronisiert)
- ⏳-Indikator in der aktiven Aufgabenliste (`HasScheduledPrompt` → `GetScheduledPromptStatus`) spiegelt den persistierten Zustand inkl. „wartet auf Session" → unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` (nur `HasScheduledPrompt`-Anbindung, unverändert)
- `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs` (anzeigerelevantes `WartetAufSession`-Flag)
- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` (Statussemantik hinter der Anzeige)
