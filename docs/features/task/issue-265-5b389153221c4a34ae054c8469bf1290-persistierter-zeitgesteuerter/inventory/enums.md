# Enums — Bestandsaufnahme

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Zeilen 826–835)

Wird vom Event `KiAusfuehrungsService.CliProcessStatusChanged` (`Action<Guid, CliProcessStatus>`, Zeile 62) publiziert; Abonnenten u. a. `TaskDetailViewModel.OnCliProcessStatusChanged` (beim Status ungleich `Gestartet` und nicht laufender CLI: Storno des geplanten Prompts, Zeilen 1529–1535) und `CliProcessManager` (Heartbeat-Verwaltung, Zeilen 131–151).

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft. |
| `Gestoppt` | Prozess wurde gestoppt. |
| `Fehler` | Prozess ist mit einem Fehler beendet. |

## `KiAusfuehrungsZustand` (internal)
Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs` (Zeilen 116–132)

Fachliche Zustandsklassifikation der KI-Ausführung; Ergebnis von `KiAusfuehrungsStatusKlassifikation.Klassifiziere` und Basis der Status-Converter (`KiAusfuehrungsStatusConverter`, `KiAusfuehrungsStatusSymbolConverter`) für Seitenleisten-/Dashboard-Kacheln.

| Wert | Bedeutung |
|------|-----------|
| `Pausiert` | Aufgabe ist pausiert (`PausiertBisUtc` in der Zukunft) — hat Vorrang vor allen übrigen Anzeigen (Zeilen 162–168). |
| `PromptInWartestellung` | Ein zeitgesteuerter Prompt steht in der Warteschlange (`HasScheduledPrompt`, Zeilen 170–173) — Anzeige „⏳ Prompt in Wartestellung" (Zeile 220) bzw. Glyph „⏳" (Zeile 252). |
| `Laeuft` | CLI-Ausführung läuft (aktiver Run ohne `WartetAufEingabe`-Substatus). |
| `Wartet` | Ausführung wartet (Substatus `WartetAufEingabe` oder Aufgabenstatus `Wartend`). |
| `Bereit` | Keine aktive Ausführung erkennbar. |

Hinweis: `HasScheduledPrompt` wird nur für `AktiveAufgabePanelItem`-Eingaben gesetzt; bei einer direkt übergebenen `Aufgabe`-Entität ist es fest `false` (`AppConverters.cs` Zeile 152).
