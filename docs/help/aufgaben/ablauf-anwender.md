# Aufgaben & KI-Entwicklungsprozess — Ablauf für Anwender

## Voraussetzungen

- Ein Projekt mit mindestens einem aktiven Git-Repository ist vorhanden.
- Ein KI-Plugin (z.B. Claude CLI) und ein SCM-Plugin (z.B. GitHub) sind konfiguriert.
- Ein Agentenpaket ist im System hinterlegt.

## Schritt-für-Schritt-Anleitung

### 1. Neue Aufgabe anlegen

Navigiere zum Projekt → „Neue Aufgabe". Vergib einen Titel und optional eine Anforderungsbeschreibung. Ist ein Issue-Tracking aktiv, kann eine Issue-Referenz verknüpft werden. Bestätige mit „Anlegen".

### 2. Entwicklung starten

Öffne die Aufgabe → Button **🚀 Entwicklung starten**. Im Dialog:

1. **KI-Plugin** wählen (z.B. „Claude CLI").
2. **Agentenpaket** wählen.
3. **Agent** aus dem Paket wählen.
4. Optional: **Basis-Branch** wählen, um einen vorhandenen Remote-Branch zu nutzen (leer lassen für einen neuen `task/`-Branch).
5. Auf **🚀 Starten** klicken.

Die Anwendung klont das Repository, legt den Branch an und deployt das Agentenpaket. Status wechselt auf **In Bearbeitung**.

### 3. KI-Anfrage senden

Wechsle in das Register **Ausführung**.

1. KI-Plugin und Agentenpaket bestätigen oder ändern.
2. Prompt im Textfeld eingeben (Was soll die KI tun?).
3. Optional: Ausführungszeitpunkt eingeben (Format `HH:mm`), um den Start zeitgesteuert zu planen.
4. **▶️ Senden** klicken.

Die KI startet im Hintergrund. Live-Ausgabe erscheint im Bereich **KI arbeitet...**. Du kannst weiternavigieren – der Lauf läuft weiter.

> **Hinweis:** Bei Folgeanweisungen erscheint zusätzlich das Feld **Kontextmodus**. Wähle „Kontext mitgeben" (Standard), um den bisherigen Gesprächsverlauf beizubehalten.

### 4. Ergebnis prüfen

Nach Abschluss des KI-Laufs wechselt der Status zurück auf **In Bearbeitung**. Das vollständige Protokoll ist im unteren Bereich des Ausführungsregisters einsehbar.

Wechsle in das Register **Projektverzeichnis**, um die geänderten Dateien zu sehen. Per Klick auf eine Datei öffnet sich die Vorschau.

### 5. Commit, Push und Pull Request erstellen

Im Register **Projektverzeichnis**:

- **📝 Commit** — Commit-Nachricht eingeben und bestätigen.
- **⬆️ Push** — Branch auf den Remote pushen.
- **🔀 Pull Request** — Titel und Beschreibung eingeben, Pull Request erstellen.

### 6. Aufgabe abschließen

Button **✅ Abschließen** (in jedem Register verfügbar). Das lokale Klonverzeichnis wird gelöscht, Status wechselt auf **Abgeschlossen**.

## Ergebnis

Die Aufgabe ist mit Status „Abgeschlossen" abgelegt. Alle Protokolleinträge bleiben erhalten. Der zugehörige Pull Request liegt im Remote-Repository zur Review bereit.

## Sonderfälle

- **Rate-Limit:** Erscheint ein Hinweis mit Uhrzeit, wurde ein Vorschlag gespeichert. Beim nächsten Öffnen der Ausführungsansicht ist der Prompt vorausgefüllt und kann direkt gesendet werden.
- **Aufgabe wiederherstellen (🩹):** Wenn eine Aufgabe im Status `InBearbeitung` oder `KiAktiv` hängt und kein aktiver Lauf mehr läuft, setzt dieser Button den Status manuell zurück.
- **Status zurücksetzen (↩️):** Wenn der Status `KiAktiv` ist, aber kein Lauf mehr aktiv ist, ermöglicht dieser Button das Senden einer neuen Anfrage.
