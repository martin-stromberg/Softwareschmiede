# Aufgaben & KI-Entwicklungsprozess — Beschreibung

## Zweck

Eine Aufgabe kapselt eine Entwicklungsanforderung: Titel, Beschreibung und optional eine Issue-Referenz aus dem Git-Provider. Die Softwareschmiede führt die Aufgabe KI-gestützt durch, indem sie das Repository klont, einen Branch anlegt, eine lokale `issue.md`-Datei mit der Aufgabenbeschreibung erstellt und den KI-Agenten mit dem Prompt startet.

## Funktionsweise

### Aktive Aufgaben im Navigationsmenü

Die WPF-Desktopanwendung zeigt alle aktiven Aufgaben (Status `Gestartet` oder `Wartend`) in der linken Navigationsseitenleiste an. Diese Funktion ermöglicht schnellen Zugriff auf laufende Arbeiten:

- **Seitenleisten-Anzeige:** Unterhalb der bestehenden Navigationseinträge (Dashboard, Projekte) wird eine neue Sektion „Aktive Aufgaben" angezeigt. Diese Sektion enthält bis zu 20 aktive Aufgaben als gerahmte Kacheln.
- **Kachel-Inhalte:** Jede Kachel zeigt den Aufgabentitel und den aktuellen KI-Ausführungsstatus:
  - `▶ Läuft` — `AktiveRunId` ist gesetzt und `LastHeartbeatUtc` ist jünger als 5 Minuten
  - `⏸ Wartet` — Status ist `Wartend` (Rate-Limit erreicht)
  - `✓ Bereit` — Keine aktive Ausführung erkannt (Fallback)
- **Navigation:** Ein Navigations-Button (→) auf jeder Kachel ermöglicht den direkten Zugriff auf die Aufgabendetailansicht.
- **Dashboard-Integration:** Die Menü-Sektion wird automatisch verborgen, wenn das Dashboard aktiv ist. Das Dashboard zeigt stattdessen die gleiche Aufgabenliste ohne Höhenlimit an — keine doppelte Anzeige.
- **Automatische Statusaktualisierung:** Der Aufgabenstatus wird ohne manuelles Neuladen aktualisiert:
  - **Sofortreaktion auf Prozess-Änderungen:** Wenn eine Aufgabe gestartet oder beendet wird, wird die Seitenleiste sofort aktualisiert.
  - **Periodische Überprüfung:** Alle 5 Sekunden wird der Status neu abgerufen, um Rate-Limit-Übergänge (▶ Läuft → ⏸ Wartet) und Heartbeat-Ablauf (▶ Läuft → ✓ Bereit nach 5 Minuten ohne Aktivität) zu erkennen.
  - **Visuelle Übergangsanimation:** Wenn der Status einer Aufgabe sich ändert, wird ein dezenter Opacity-Fade (250 ms) auf dem Status-Text angezeigt, um den Wechsel hervorzuheben.

### Navigation zwischen Projekt und Aufgabe

Die Aufgabendetailansicht ist eine vollständige, fensterumfassende Ansicht, nicht mehr inline in die Projektdetailansicht eingebettet:

- **Aufgabe öffnen:** Doppelklick auf eine Aufgabe in der Aufgaben-Kachel der Projektdetailansicht öffnet die Aufgabendetailansicht. Nicht beendete Aufgaben sind dort direkt sichtbar; beendete Aufgaben liegen im initial zugeklappten Register „Beendete Aufgaben". Die Projektdetailansicht wird ausgeblendet.
- **Zurück zur Projektansicht:** Der „Zurück"-Button im Ribbon navigiert zurück zur Projektdetailansicht. Alle Aufgabenänderungen bleiben erhalten.
- **Neue Aufgabe:** Klick auf „Neue Aufgabe" im Ribbon der Projektdetailansicht erstellt eine neue Aufgabe mit Status „Neu" und öffnet sofort die Aufgabendetailansicht mit dem Edit-Panel.
- **Aufgabenlisten-Update:** Nach dem Speichern einer Aufgabe wird die Aufgabenliste in der Projektdetailansicht aktualisiert (nur das geänderte Element wird neu geladen, nicht die gesamte Liste).

### Automatische Aufgabendokumentierung im Repository

Beim Start einer Aufgabe (Repository-Klon) werden automatisch zwei lokale Dateien erstellt:

- **`issue.md`** — Eine Markdown-Datei mit der Aufgabenbeschreibung:
  - Aufgabentitel
  - Aufgaben-ID (eindeutige Kennung)
  - Branch-Name
  - Erstellungsdatum
  - Vollständige Anforderungsbeschreibung
  
  Diese Datei dient dem KI-Agenten und dem Entwickler als Referenzmaterial während der Aufgabenbearbeitung.

- **`.gitignore`-Eintrag** — Die `.gitignore`-Datei wird automatisch um den Eintrag `issue.md` erweitert, um sicherzustellen, dass diese lokale, aufgabenspezifische Datei nicht in die Versionskontrolle gelangt. Falls die `.gitignore` nicht vorhanden ist, wird sie automatisch erstellt.

Beide Dateien werden lokal im geklonten Repository gespeichert und sind nur für diese spezifische Aufgabe relevant. Sie werden nicht in den Git-Remote-Repositories committet.

### Lebenszyklus

Eine Aufgabe durchläuft folgende Status:

| Status | Bedeutung |
|--------|-----------|
| `Neu` | Angelegt, noch nicht gestartet |
| `Gestartet` | Repository geklont, Branch erstellt, CLI läuft oder sollte laufen |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Abgeschlossen (erfolgreich oder mit Fehler) |
| `Archiviert` | Dauerhaft archiviert |

### Ausführungsansicht (WPF)

Die WPF-Aufgabendetailansicht (`TaskDetailView`) zeigt unterschiedliche Inhalte je nach Aufgabenstatus:

#### Edit-Panel (Status: Neu)
Bearbeitbare Felder für Titel und Anforderungsbeschreibung mit „Speichern"-Button im Ribbon. Wird verwendet, um neue Aufgaben zu erstellen oder bereits erstellte Aufgaben nachträglich anzupassen, bevor sie gestartet werden.

#### CLI-Panel (Status: Gestartet, Wartend)
Das Hauptpanel für die aktive Aufgabenbearbeitung. Zwei Anzeigemodi:
- **CLI-Fenster (Standard):** Das Terminalfenster des KI-Tools wird via Win32 `SetParent` direkt in die Ansicht eingebettet (`ProcessWindowHost`).
- **Info-Ansicht:** Zeigt Aufgabeeigenschaften (Titel, Status, Beschreibung) und das Protokoll mit allen bisherigen Einträgen. Umschaltung via Toggle-Button „Info"/"CLI".

Das Ribbon enthält KI-Plugin-Auswahl, optionale Parameter und „CLI stoppen" Button. Der „Starten"-Button initiiert einen kombinierten Ablauf (Klone + CLI-Start).

#### Diff-Panel (Status: Beendet)
Zeigt die Änderungen im Git-Arbeitsverzeichnis nach Abschluss der Aufgabe. Aktuell ein Platzhalter; zukünftig wird hier eine visuelle Diff-Darstellung implementiert.

#### Ribbon-Menü
Aktionsgruppen:
- **Navigation:** „Zurück"-Button zur Rückkehr zur Projektdetailansicht
- **Aufgabe:** Buttons für Speichern, Löschen, Starten (Status=Neu→Gestartet mit kombiniertem Klone+CLI-Start), Beenden (Status=Gestartet/Wartend→Beendet), Plugin ändern (nur bei laufender CLI)
- **CLI:** „CLI stoppen" Button (nur sichtbar wenn aktiv)

### CLI-Prozess-Management

Der `KiAusfuehrungsService` läuft als Singleton. Er startet und stoppt CLI-Prozesse und gibt Statusänderungen über das `CliProcessStatusChanged`-Event weiter. Für jede Aufgabe kann nur ein CLI-Prozess gleichzeitig laufen.

### Rate-Limit-Vorschlag

Erkennt die KI ein Rate-Limit (Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601]]` in der Ausgabe), speichert der `ProtokollService` automatisch einen Prompt-Vorschlag mit Ausführungszeitpunkt in der Aufgabe. Der Status wechselt auf `Wartend`.

### Recovery-Mechanismus

Der `AufgabeRecoveryService` findet beim Dashboard-Laden Aufgaben im Status `InArbeit` oder `Wartend`, deren Heartbeat älter als 5 Minuten ist und für die kein aktiver Prozess läuft. Diese werden im `RecoveryBannerControl` angezeigt. Ein Klick auf „Wiederherstellen" setzt den Status auf `Gestartet` und inkrementiert `RecoveryVersion` (optimistic concurrency).

## Beispiele

1. Aufgabe „Login-Bug beheben" im Projekt „Backend-API" anlegen.
2. In der Aufgabendetailansicht „Starten" klicken.
3. Falls kein KI-Plugin für das Projekt gespeichert: Dialog zur Plugin-Auswahl wird angezeigt (z.B. Claude CLI).
4. Optional: Checkbox „Für dieses Projekt verwenden" aktivieren, um das Plugin als Projekt-Standard zu speichern.
5. Das CLI-Fenster klont automatisch das Repository und erscheint eingebettet in der Ansicht — Aufgabe hat Status `Gestartet`.
6. KI bearbeitet den Branch; während der Laufzeit kann das Plugin via „Plugin ändern" gewechselt werden.
7. Nach Abschluss „Aufgabe abschließen" klicken.

## Einschränkungen

- Für eine Aufgabe kann immer nur ein CLI-Prozess gleichzeitig aktiv sein.
- Das CLI-Fenster-Einbetten via `SetParent` funktioniert nur auf Windows; bei Scheitern erscheint das Fenster separat.
- Die Aufgabenwiederherstellung (Recovery) steht nur zur Verfügung, wenn der letzte Heartbeat älter als 5 Minuten ist.
- Der Status `Gestartet` bedeutet: Repository geklont und CLI läuft (oder sollte laufen). Wenn die Ansicht eines Status-`Gestartet`-Tasks ohne laufende CLI geöffnet wird, wird die CLI automatisch neu gestartet.
