# Enums und Statuswerte

## `ScmPromptReadyStatus` — verifizierter Eingabestatus (Freigabesignal der Anforderung)

Datei: `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs` (Z. 9–21)

| Wert | Bedeutung |
|------|-----------|
| `Unknown` | Kein verifizierter Eingabeprompt. |
| `Ready` | Bestätigter KI-Eingabeprompt für den aktuellen Lauf — **das neue Freigabekriterium**. |
| `Busy` | Ein Turn oder eine Benutzereingabe läuft. |
| `TrustPrompt` | Die CLI wartet auf eine Vertrauensentscheidung für das Arbeitsverzeichnis. |
| `Exited` | Der gebundene Lauf ist beendet oder fehlgeschlagen (auch Fallback bei `!IsAlive`). |

## `CliRuntimeStatus` — I/O-Ruhe-Heuristik der Terminalsession

Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Z. 650–658)

| Wert | Bedeutung |
|------|-----------|
| `Inactive` | Kein laufender CLI-Prozess aktiv. |
| `Running` | CLI läuft mit kürzlicher Ein-/Ausgabe. |
| `WaitingForInput` | Läuft, aber ≥ ~4 s ohne Terminal-I/O (`CliRuntimeStatusEvaluator.Determine`); möglicher **Auslöser**, laut Anforderung kein Zählkriterium. |

## `WorkItemRunStatus` — persistierter Lauf-Substatus (Anzeige)

Datei: `src/Softwareschmiede/Domain/Enums/WorkItemRunStatus.cs` (Z. 21–28)

| Wert | Bedeutung |
|------|-----------|
| `Running` | CLI läuft mit kürzlicher Ein-/Ausgabe. |
| `WaitingForInput` | CLI läuft, vermutlich wartend (I/O-Ruhe); rein informativ (Kachel „⏸ Wartet"), kein Freigabekriterium. |

## `ScmIncomingEvent.Status` — String-Literale (kein Enum!)

`Status` ist ein `string` (Default `"Aufgenommen"`, `ScmIncomingEvent.cs` Z. 22). Im Code
beobachtete Werte:

| Literal | Bedeutung / gesetzt in |
|---------|------------------------|
| `"Aufgenommen"` | Neu eingegangen (`ScmIncomingEvent` Default); auch Reset bei `ResendAsync` (Z. 407). |
| `"Wartend"` | Wartet mit Reason (`ScmEventOrchestrator.WaitAsync`, Z. 1004). |
| `"Verzögert"` | Direktiven-Verzögerung bis `NextAttemptUtc` (`ScmEventStore` Z. 101). |
| `"ZustellungVorbereitet"` | Antwortkanal vorbereitet, Prompt noch nicht gesendet (Z. 306). |
| `"Zugestellt"` | Prompt geschrieben und bestätigt (Z. 321, 775). |
| `"ZustellungUngewiss"` | Schreibversuch begonnen, Bestätigung unklar (Z. 326, 584, 780, 955). |
| `"InBearbeitung"` | CLI hat den Eingang quittiert (`accepted`-Nachricht, Z. 505, 848). |
| `"VerarbeitungUngeklärt"` | Abgleich fehlgeschlagen/unklar (Z. 167, 173, 499, 516, 526, 594, 627, 633, 841, 877, 888, 900, 940, 947, 1030). |
| `"Verarbeitet"` | **Endstatus** — fachlich abgeschlossen (Z. 537, 621, 666, 915, 932). |
| `"Abgebrochen"` | **Endstatus** — Benutzerabbruch (`CancelAsync`, Z. 429). |

Pending-Definition (Endstatus-Ausschluss), konsistent im ganzen Code:
`Status != "Verarbeitet" && Status != "Abgebrochen"`.
