# Aufgaben & KI-Entwicklungsprozess — Beschreibung

## Zweck

Eine Aufgabe kapselt eine Entwicklungsanforderung: Titel, Beschreibung und optional eine Issue-Referenz aus dem Git-Provider. Die Softwareschmiede führt die Aufgabe KI-gestützt durch, indem sie das Repository klont, einen Branch anlegt und den KI-Agenten mit dem Prompt startet.

## Funktionsweise

### Lebenszyklus

Eine Aufgabe durchläuft folgende Status:

| Status | Bedeutung |
|--------|-----------|
| `Neu` | Angelegt, noch nicht gestartet |
| `ArbeitsverzeichnisEingerichtet` | Lokaler Git-Klon wurde angelegt |
| `Gestartet` | Branch erstellt, bereit für CLI-Start |
| `InArbeit` | CLI-Prozess läuft aktiv |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Abgeschlossen (erfolgreich oder mit Fehler) |
| `Archiviert` | Dauerhaft archiviert |

### Ausführungsansicht (WPF)

Die WPF-Aufgabendetailansicht (`TaskDetailView`) zeigt:

- **Aufgabentitel und Status** — mit farbigem `StatusIndicatorControl`
- **CLI-Steuerung** — KI-Plugin auswählen, optionale Parameter eingeben, „CLI starten" / „CLI stoppen"
- **Eingebettetes CLI-Fenster** — das Terminalfenster des KI-Tools wird via Win32 `SetParent` direkt in die Ansicht eingebettet (`ProcessWindowHost`)
- **Protokoll** — alle gespeicherten `Protokolleintrag`-Einträge der Aufgabe
- **Status-Übergänge** — Buttons „Gestartet setzen" und „Aufgabe abschließen"

### CLI-Prozess-Management

Der `KiAusfuehrungsService` läuft als Singleton. Er startet und stoppt CLI-Prozesse und gibt Statusänderungen über das `CliProcessStatusChanged`-Event weiter. Für jede Aufgabe kann nur ein CLI-Prozess gleichzeitig laufen.

### Rate-Limit-Vorschlag

Erkennt die KI ein Rate-Limit (Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601]]` in der Ausgabe), speichert der `ProtokollService` automatisch einen Prompt-Vorschlag mit Ausführungszeitpunkt in der Aufgabe. Der Status wechselt auf `Wartend`.

### Recovery-Mechanismus

Der `AufgabeRecoveryService` findet beim Dashboard-Laden Aufgaben im Status `InArbeit` oder `Wartend`, deren Heartbeat älter als 5 Minuten ist und für die kein aktiver Prozess läuft. Diese werden im `RecoveryBannerControl` angezeigt. Ein Klick auf „Wiederherstellen" setzt den Status auf `Gestartet` und inkrementiert `RecoveryVersion` (optimistic concurrency).

## Beispiele

1. Aufgabe „Login-Bug beheben" im Projekt „Backend-API" anlegen.
2. In der Aufgabendetailansicht „Gestartet setzen" klicken.
3. KI-Plugin auswählen (z.B. Claude CLI), „CLI starten" klicken.
4. Das CLI-Fenster erscheint eingebettet in der Ansicht — Aufgabe wechselt auf `InArbeit`.
5. KI bearbeitet den Branch; nach Abschluss „Aufgabe abschließen" klicken.

## Einschränkungen

- Für eine Aufgabe kann immer nur ein CLI-Prozess gleichzeitig aktiv sein.
- Das CLI-Fenster-Einbetten via `SetParent` funktioniert nur auf Windows; bei Scheitern erscheint das Fenster separat.
- Die Aufgabenwiederherstellung (Recovery) steht nur zur Verfügung, wenn der letzte Heartbeat älter als 5 Minuten ist.
