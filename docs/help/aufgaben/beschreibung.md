# Aufgaben & KI-Entwicklungsprozess — Beschreibung

## Zweck

Eine Aufgabe kapselt eine Entwicklungsanforderung: Titel, Beschreibung und optional eine Issue-Referenz aus dem Git-Provider. Die Softwareschmiede führt die Aufgabe KI-gestützt durch, indem sie das Repository klont, einen Branch anlegt, eine lokale `issue.md`-Datei mit der Aufgabenbeschreibung erstellt und den KI-Agenten mit dem Prompt startet.

## Funktionsweise

### Aktive Aufgaben im Navigationsmenü

Die WPF-Desktopanwendung zeigt alle aktiven Aufgaben (Status `Gestartet` oder `Wartend`) in der linken Navigationsseitenleiste an. Diese Funktion ermöglicht schnellen Zugriff auf laufende Arbeiten:

- **Seitenleisten-Anzeige:** Unterhalb der bestehenden Navigationseinträge (Dashboard, Projekte) wird eine neue Sektion „Aktive Aufgaben" angezeigt. Diese Sektion enthält bis zu 20 aktive Aufgaben als gerahmte Kacheln.
- **Kachel-Inhalte:** Jede Kachel zeigt den Aufgabentitel, den Projektnamen, das SCM-/SCI-Plugin, das KI-Plugin und den aktuellen KI-Ausführungsstatus:
  - `▶ Läuft` — `AktiveRunId` ist gesetzt und `LastHeartbeatUtc` ist jünger als 5 Minuten
  - `⏸ Wartet` — Status ist `Wartend` (Rate-Limit erreicht)
  - `✓ Bereit` — Keine aktive Ausführung erkannt (Fallback)
- **Aktive Markierung:** Wenn im Inhaltsbereich eine Aufgabe geöffnet ist, wird genau diese Aufgabe in der Seitenleiste hervorgehoben.
- **Stabile Sortierung:** Die Aufgaben werden absteigend nach `LetzterCliStartUtc` sortiert. Dieser Zeitstempel wird nur beim echten CLI-Prozessstart aktualisiert, nicht beim Anzeigen einer bereits laufenden Hintergrundaufgabe. Für ältere Aufgaben ohne Wert wird auf `ErstellungsDatum`, danach Titel und ID zurückgefallen.
- **Navigation:** Ein Navigations-Button (→) auf jeder Kachel ermöglicht den direkten Zugriff auf die Aufgabendetailansicht.
- **Dashboard-Integration:** Die Menü-Sektion wird automatisch verborgen, wenn das Dashboard aktiv ist. Das Dashboard zeigt stattdessen die gleiche Aufgabenliste ohne Höhenlimit an — keine doppelte Anzeige.
- **Automatische Statusaktualisierung:** Der Aufgabenstatus wird ohne manuelles Neuladen aktualisiert:
  - **Sofortreaktion auf Prozess-Änderungen:** Wenn eine Aufgabe gestartet oder beendet wird, wird die Seitenleiste sofort aktualisiert.
  - **Periodische Überprüfung:** Alle 5 Sekunden wird der Status neu abgerufen, um Rate-Limit-Übergänge (▶ Läuft → ⏸ Wartet) und Heartbeat-Ablauf (▶ Läuft → ✓ Bereit nach 5 Minuten ohne Aktivität) zu erkennen.
  - **Visuelle Übergangsanimation:** Wenn der Status einer Aufgabe sich ändert, wird ein dezenter Opacity-Fade (250 ms) auf dem Status-Text angezeigt, um den Wechsel hervorzuheben.

### Navigation zwischen Projekt und Aufgabe

Die Aufgabendetailansicht ist eine vollständige, fensterumfassende Ansicht, nicht mehr inline in die Projektdetailansicht eingebettet:

- **Aufgabe öffnen:** Doppelklick auf eine Aufgabe in der Aufgaben-Kachel der Projektdetailansicht öffnet die Aufgabendetailansicht. Nicht beendete Aufgaben sind dort direkt sichtbar; beendete Aufgaben liegen im initial zugeklappten Register „Beendete Aufgaben". Die Projektdetailansicht wird ausgeblendet.
- **Fenstertitel:** Beim Öffnen einer Aufgabe zeigt die Titelleiste zunächst `Softwareschmiede – Aufgabe` und nach dem Laden `Softwareschmiede – {Aufgabentitel}`. Beim Wechsel zurück zu Dashboard, Projekten oder Einstellungen wird der jeweilige Ansichtstitel gesetzt, damit kein alter Aufgabentitel stehen bleibt.
- **Zurück zur Projektansicht:** Der „Zurück"-Button im Ribbon navigiert zurück zur Projektdetailansicht. Alle Aufgabenänderungen bleiben erhalten.
- **Neue Aufgabe:** Klick auf „Neue Aufgabe" im Ribbon der Projektdetailansicht erstellt eine neue Aufgabe mit Status „Neu" und öffnet sofort die Aufgabendetailansicht in der Info-Ansicht mit bearbeitbaren Stammdaten.
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

**Platzierung — Repository-Root oder Arbeitsverzeichnis:**

Beide Dateien werden lokal im geklonten Repository gespeichert und sind nur für diese spezifische Aufgabe relevant. Sie werden nicht in den Git-Remote-Repositories committet.

- Wenn für das Repository **kein Arbeitsverzeichnis konfiguriert** ist: Die Dateien werden im **Repository-Root** erstellt (`issue.md`, `.gitignore` im Klonverzeichnis).
- Wenn ein **Arbeitsverzeichnis konfiguriert** ist (z.B. `backend`, `frontend`): Die Dateien werden im **konfigurierten Arbeitsverzeichnis** erstellt (z.B. `<Klon>/backend/issue.md`, `<Klon>/backend/.gitignore`), damit sich Datei und ihr `.gitignore`-Eintrag im gleichen Verzeichnis befinden.

Diese Platzierungslogik ermöglicht es dem KI-Agenten und dem Entwickler, die Aufgabenbeschreibung dort zu finden, wo sie tatsächlich arbeiten — insbesondere bei Monorepos mit mehreren Arbeitsverzeichnissen.

### Lebenszyklus

Eine Aufgabe durchläuft folgende Status:

| Status | Bedeutung |
|--------|-----------|
| `Neu` | Angelegt, noch nicht gestartet |
| `Gestartet` | Repository geklont, Branch erstellt, CLI läuft oder sollte laufen |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Abgeschlossen (erfolgreich oder mit Fehler) |
| `Archiviert` | Dauerhaft archiviert |

### Aufgabendetailansicht (WPF)

Die WPF-Aufgabendetailansicht (`TaskDetailView`) nutzt eine gemeinsame Ansichtsleiste oberhalb des Inhalts. Die verfügbaren Ansichten sind explizit benannt:

#### Info-Ansicht
Zeigt die Stammdaten der Aufgabe, insbesondere Titel, Status, Beschreibung, optionale Issue-Referenz und Protokollinformationen. Bei neuen Aufgaben enthält sie die bearbeitbaren Felder für Titel und Anforderungsbeschreibung mit „Speichern"-Button im Ribbon. Die Info-Ansicht ist unabhängig vom Aufgabenstatus erreichbar, also auch bei gestarteten, wartenden und beendeten Aufgaben.

#### CLI-Ansicht
Zeigt das Terminalfenster des KI-Tools. Das Fenster wird via Win32 `SetParent` direkt in die Ansicht eingebettet (`ProcessWindowHost`). Die CLI-Ansicht wird angeboten, wenn für die Aufgabe eine CLI-Ausführung sinnvoll ist, insbesondere bei gestarteten oder wartenden Aufgaben.

Während eine CLI läuft, zeigt die Fußzeile den Namen des aktiven KI-Plugins bzw. CLI-Plugins an. Wenn keine CLI läuft oder ein Start/Stop fehlschlägt, wird dieser Wert geleert, damit kein veralteter CLI-Name sichtbar bleibt. Der technische Laufstatus bleibt davon getrennt.

#### Diff-Ansicht
Zeigt die Änderungen im Git-Arbeitsverzeichnis nach Abschluss der Aufgabe. Bei beendeten Aufgaben wird die Diff-Ansicht bevorzugt ausgewählt, sofern sie verfügbar ist; die Info-Ansicht bleibt weiterhin auswählbar.

#### Ribbon-Menü
Aktionsgruppen:
- **Navigation:** „Zurück"-Button zur Rückkehr zur Projektdetailansicht
- **Aufgabe:** Buttons für Speichern, Löschen, Starten (Status=Neu→Gestartet mit kombiniertem Klone+CLI-Start), Beenden (Status=Gestartet/Wartend→Beendet), Plugin ändern (nur bei laufender CLI)
- **CLI:** „CLI stoppen" Button (nur sichtbar wenn aktiv)
- **Issue:** „Issue anlegen" wird angeboten, wenn das Repository die Anlage unterstützt und der Aufgabe noch kein Issue zugeordnet ist. „Issue zuweisen" bleibt für die Auswahl eines vorhandenen Issues verfügbar; nach erfolgreicher Anlage zeigt „Issue öffnen" die gespeicherte Referenz.
- **Pull Request:** „PR erstellen" Button, sobald Branch, verknüpftes Git-Repository und Pull-Request-Unterstützung des Git-Plugins vorhanden sind

### Issue aus einer Aufgabe anlegen

Über „Issue anlegen" wird ein neues Issue im Provider des zugeordneten Repositorys vorbereitet. Der Aufgabentitel ist als Titel vorausgefüllt, die Anforderungsbeschreibung als editierbare Beschreibung. Falls der Provider Templates liefert, können diese ausgewählt werden. Der Dialog ergänzt dann den Template-Inhalt, eine Trennlinie und den Abschnitt `Originalanforderung:`; der gesamte Inhalt bleibt bearbeitbar.

Optional kann ein ausgewählter Template-Text mit einem im Dialog gewählten KI-Provider ausgefüllt werden. Die KI-Ausfüllhilfe ist unabhängig von der normalen Issue-Anlage und kann bei fehlendem oder nicht geeignetem KI-Provider nicht verwendet werden.

Beim Absenden wird das Issue zuerst extern erstellt. Erst danach wird die Issue-Referenz an der Aufgabe gespeichert und die Detailansicht aktualisiert. Abbrechen, Providerfehler oder eine fehlgeschlagene lokale Zuordnung erzeugen keine erfolgreiche Zuordnung. Ist bereits ein Issue zugeordnet, wird die Anlageaktion für diese Aufgabe nicht mehr angeboten.

### Zeitgesteuerter Prompt-Versand

Die Promptvorlagen-Auswahlbox im Ribbon wird durch zwei Eingabefelder (Stunde und Minute) und einen Button „Zeitgesteuert senden" ergänzt. Diese erlauben es, einen aufgelösten Prompt bis zu einer angegebenen Uhrzeit zu planen statt ihn sofort zu versenden:

- **Eingabefelder:** Zwei `TextBox`-Felder für Stunde (0–23) und Minute (0–59). Sind beide leer, wird der Prompt über die ComboBox-Auswahl sofort versendet (bisheriges Verhalten). Sind die Felder befüllt, erfolgt kein Sofortversand.
- **Zeitgesteuert senden Button:** Nur aktiv, wenn eine Vorlage ausgewählt, die Zeitfelder valide gefüllt und die CLI läuft. Klick plant den Prompt zur angegebenen Uhrzeit.
- **Zielzeitberechnung:** Die eingegebene Uhrzeit wird als lokale Zeit des Anwenders (via `DateTime.Now`) interpretiert. Liegt die Zielzeit in der Vergangenheit/Gegenwart, wird der Prompt sofort versendet.
- **Statusanzeige:** Während der Prompt geplant ist, zeigt ein `TextBlock` „Prompt in Wartestellung" mit der Zielzeit im Format `HH:mm`. Nach erfolgreichem Versand oder beim CLI-Stop wird dieser Status automatisch gelöscht.
- **Statusanzeige in der Seitenleiste:** Solange ein Prompt für eine Aufgabe geplant ist, zeigt auch die Kachel der Aufgabe in der Seitenleisten-Sektion „Aktive Aufgaben" (siehe oben) „⏳ Prompt in Wartestellung" anstelle des üblichen Laufzeitstatus.
- **Automatische Stornierung:** Beim Wechsel der Aufgabendetailansicht, beim Dispose des ViewModels oder beim Aufgabenabschluss wird der geplante Prompt storniert; der Timer wird abgebrochen.
- **Keine Persistierung:** Die Planung ist rein sitzungsgebunden. Ein App-Neustart löscht alle geplanten Prompts. Ein Prompt wird **nicht** persistiert und bei Wiederstart automatisch versendet.

Der neue `PromptZeitVersandService` (Singleton) verwaltet intern pro Aufgabe einen geplanten Prompt in einer Laufzeit-Warteschlange und einen Timer. Bei Erreichen der Zielzeit ruft der Timer-Callback automatisch `PseudoConsoleSession.WritePromptAsync()` auf und versendet den Prompt. Ist die CLI zwischenzeitlich beendet worden, wird der Prompt still verworfen (nur Log-Warnung, kein UI-Feedback).

### CLI-Prozess-Management

Der `KiAusfuehrungsService` läuft als Singleton. Er startet und stoppt CLI-Prozesse und gibt Statusänderungen über das `CliProcessStatusChanged`-Event weiter. Für jede Aufgabe kann nur ein CLI-Prozess gleichzeitig laufen.

### Rate-Limit-Vorschlag

Erkennt die KI ein Rate-Limit (Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:ISO8601]]` in der Ausgabe), speichert der `ProtokollService` automatisch einen Prompt-Vorschlag mit Ausführungszeitpunkt in der Aufgabe. Der Status wechselt auf `Wartend`.

### Recovery-Mechanismus

Der `AufgabeRecoveryService` findet beim Dashboard-Laden Aufgaben im Status `Gestartet` oder `Wartend`, deren Heartbeat älter als 5 Minuten ist und für die kein aktiver Prozess läuft. Diese werden im `RecoveryBannerControl` angezeigt. Ein Klick auf „Wiederherstellen" setzt den Status auf `Gestartet` und inkrementiert `RecoveryVersion` (optimistic concurrency).

## Beispiele

### Klassischer Workflow

1. Aufgabe „Login-Bug beheben" im Projekt „Backend-API" anlegen.
2. In der Aufgabendetailansicht „Starten" klicken.
3. Falls kein KI-Plugin für das Projekt gespeichert: Dialog zur Plugin-Auswahl wird angezeigt (z.B. Claude CLI).
4. Optional: Checkbox „Für dieses Projekt verwenden" aktivieren, um das Plugin als Projekt-Standard zu speichern.
5. Das CLI-Fenster klont automatisch das Repository und erscheint eingebettet in der Ansicht — Aufgabe hat Status `Gestartet`.
6. KI bearbeitet den Branch; während der Laufzeit kann das Plugin via „Plugin ändern" gewechselt werden.
7. Nach Abschluss „Aufgabe abschließen" klicken.

### Zeitgesteuerter Prompt-Versand

1. Aufgabe ist gestartet, CLI läuft.
2. Im Ribbon-Feld „Zielzeit" 16 als Stunde und 30 als Minute eingeben (16:30 Uhr).
3. Eine Promptvorlage aus der ComboBox auswählen (z.B. „Fehleranalyse").
4. Button „Zeitgesteuert senden" klicken.
5. Status „Prompt in Wartestellung" erscheint mit „16:30" unter dem Button.
6. Die Zeitfelder werden geleert, die Vorlage wird zurückgesetzt.
7. Bei Erreichen von 16:30 Uhr wird der Prompt automatisch an die CLI versendet; die Status-Anzeige verschwindet.
8. Falls die CLI zwischenzeitlich beendet wurde, wird der Prompt still verworfen (ohne Fehlermeldung).

## Einschränkungen

- Für eine Aufgabe kann immer nur ein CLI-Prozess gleichzeitig aktiv sein.
- Das CLI-Fenster-Einbetten via `SetParent` funktioniert nur auf Windows; bei Scheitern erscheint das Fenster separat.
- Die Aufgabenwiederherstellung (Recovery) steht nur zur Verfügung, wenn der letzte Heartbeat älter als 5 Minuten ist.
- Der Status `Gestartet` bedeutet: Repository geklont und CLI läuft (oder sollte laufen). Wenn die Ansicht eines Status-`Gestartet`-Tasks ohne laufende CLI geöffnet wird, wird die CLI automatisch neu gestartet.
- Zeitgesteuerter Prompt-Versand: Pro Aufgabe kann maximal ein Prompt gleichzeitig geplant sein; erneutes Planen ersetzt den vorhandenen Eintrag. Die Planung ist rein sitzungsgebunden und wird nicht persistiert — ein App-Neustart löscht alle geplanten Prompts. Ist die CLI zur Zielzeit nicht mehr aktiv, wird der Prompt still verworfen.
