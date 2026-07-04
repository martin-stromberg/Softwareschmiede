# Enumerationen

## `CliRuntimeStatus`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

Laufzeitstatus einer aktiven CLI-Sitzung. Wird von `PseudoConsoleSession.RuntimeStatus` verwendet und mit dem `RuntimeStatusChanged`-Event propagiert.

| Wert | Bedeutung |
|------|-----------|
| `Inaktiv` | Kein laufender CLI-Prozess ist aktiv |
| `Laeuft` | Die CLI läuft und hat kürzlich Ausgabe oder Eingabe verarbeitet |
| `WartetAufEingabe` | Die CLI läuft, erzeugt aber seit längerer Zeit keine Ausgabe und wartet vermutlich auf Benutzereingabe |

**Bestimmung:** Wird vom statischen `CliRuntimeStatusEvaluator.Determine()` basierend auf:
- Ob der Prozess noch läuft (`isRunning`)
- Zeitstempel der letzten Ausgabe (`lastOutputUtc`)
- Zeitstempel der letzten Eingabe (`lastInputUtc`)
- Aktueller Zeitstempel (`nowUtc`)
- Wartezeit-Threshold (Standard: 4 Sekunden)

**Verwendung:**
- Von `PseudoConsoleSession` alle 1 Sekunde neu berechnet (via `RefreshRuntimeStatus()`)
- Von `TaskDetailViewModel` im CLI-Status-Text verwendet (Zeile 839–847): "CLI-Status: Ausführung läuft" / "CLI-Status: Wartet auf Eingabe" / "CLI inaktiv"

---

## `CliProcessStatus`
Datei: (vermutet in `src/Softwareschmiede/Domain/Enums/` oder verwandtem Namespace)

Status eines gestarteten CLI-Prozesses, wird von `KiAusfuehrungsService.CliProcessStatusChanged`-Event propagiert.

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Der CLI-Prozess wurde erfolgreich gestartet |
| (weitere) | Weitere Status wie `Gestoppt`, `Fehler`, etc. (gemäß Implementierung) |

**Verwendung:**
- Wird von `KiAusfuehrungsService` beim Start (Zeile 140), Stop und Fehler-Fall gefeuert
- Wird von `TaskDetailViewModel.OnCliProcessStatusChanged()` empfangen; setzt `IsCliRunning` basierend auf Status
