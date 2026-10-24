# Enums und Statuswerte

## `ScmPromptReadyStatus` — `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs:9-21`

| Wert | Kommentar | Wer setzt ihn |
|---|---|---|
| `Unknown` | „Kein verifizierter Eingabeprompt." | Initialwert; `RefreshScreen`-Fallback (317); bleibt bei `!_recognizedCli` dauerhaft stehen — das beanstandete Endlosschweigen |
| `Ready` | „Bestätigter KI-Eingabeprompt für den aktuellen Lauf." | `RefreshScreen` (Prompt-Glyphe/Simulator), `ConfirmReady` |
| `Busy` | „Ein Turn oder eine Benutzereingabe läuft." | `RefreshScreen` (Busy-Marker), `MarkInputStarted` |
| `TrustPrompt` | „Die CLI wartet auf eine Vertrauensentscheidung für das Arbeitsverzeichnis." | `RefreshScreen` (TrustDialogMarkers) |
| `Exited` | „Der gebundene Lauf ist beendet oder fehlgeschlagen." | `ReadyStatus`-Getter (nicht lebend), `OnExited`/`OnFailed` |

Die Anforderung nennt als mögliche Alternative die Erweiterung um einen Zustand wie `UnsupportedCli` — heute gibt es dafür keinen Wert.

## `ScmExchangeAccessStatus` — `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs:10-18`

| Wert | Kommentar | Typische Gründe heute |
|---|---|---|
| `Supported` | „Der ausgewertete Startvertrag erlaubt den Arbeitsbereichskanal." | Whitelist-Startvertrag bzw. erfolgreiche Dateitool-Probe (Reason „Dateitool hat Lesen, Schreiben und Umbenennen im rungebundenen Antwortkanal nachgewiesen.") |
| `Denied` | „Eine bekannte Regel verweigert Dateizugriff." | Schreibschutz, Deny-/Ask-Regeln, Kanal außerhalb Schreibwurzel, unsicherer Pfad |
| `Unknown` | „Die effektiven Rechte sind mit diesem Adapter nicht sicher bestimmbar." | u. a. „CLI-Version oder Initialisierung ist nicht durch ein unterstütztes Promptformat bestätigt." (`ScmPromptSessionAdapter.cs:163`), fehlende Dateifreigabe, ausstehende Probe, geänderte Konfiguration/Identitäten, Shell statt KI-Kanal |

Die Anforderung verlangt zu prüfen, ob ein neuer Status nötig ist oder `Unknown` + konkreter `Reason` genügt — heute trägt `Unknown` sämtliche nicht nachweisbaren Fälle inklusive unbekannter CLI-Version.

## `ScmIncomingEvent.Status` — String-Werte (kein CLR-Enum), `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs:22`

Beobachtete Werte im Dispatch-Pfad (`ScmEventOrchestrator.cs`):

| Wert | Bedeutung |
|---|---|
| `Aufgenommen` | Initial (Default) |
| `Wartend` | Jeder blockierende Grund (`WaitAsync`), inkl. „Eingabebereitschaft unbekannt…" |
| `ZustellungVorbereitet` | Attempt persistiert, Prompt-Schreiben steht aus |
| `Zugestellt` | Prompt geschrieben |
| `InBearbeitung` | `accepted.json` gelesen |
| `Verarbeitet` | Endstatus (vollständiger Abschluss) |
| `ZustellungUngewiss` | Schreibversuch nicht sicher bestätigt / unterbrochen |
| `VerarbeitungUngeklärt` | Fehler im Abgleich |
| `Abgebrochen` | Endstatus (Benutzerabbruch) |

## Weitere fachlich relevante Enum-/Stringwerte (Kontext)

- `ScmResponseKind` (`Comment`, `Reaction`, `PullRequest`) — Outbox-Aktionen, u. a. `waiting`-Kommentar.
- `ScmProviderFailureKind` — `Authentication`/`Permission` setzen `NextAttemptUtc = long.MaxValue` (Fehlersperre), andere → `ScmEventStore.RetryAt` (`ScmEventOrchestrator.cs:225-228`). Referenz für „Wiederholungsverhalten"-Frage 4.
- `AufgabeStatus` (`Neu`, `Gestartet`, `Beendet`, `Archiviert`) — `BlockReason`/`CanConfirmReady`-Blockierung.
