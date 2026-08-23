← [Zurück zur Übersicht](index.md)

# Initialisierungsskript-Konfiguration — Beschreibung

## Zweck

Die Initialisierungsskript-Konfiguration ermöglicht es, pro Git-Repository ein optionales Skript zu hinterlegen, das unmittelbar nach dem Klonen des Repositorys für eine Aufgabe automatisch ausgeführt wird. Dies dient der projektspezifischen Vorbereitung des geklonten Repositorys — z. B. zur Installation von Git-Hooks, Einrichtung von Build-Tools, Setzen von Git-Konfigurationswerten oder anderen Initialisierungsschritten, die vor der eigentlichen Aufgabenarbeit erforderlich sind.

Im Gegensatz zum optionalen Startskript (das nach der Aufgaben-Initialisierung, beim Aufruf der CLI läuft) wird das Initialisierungsskript früher im Aufgaben-Lifecycle ausgeführt — direkt nach dem Repository-Klon. Fehler beim Initialisierungsskript blockieren die Aufgabe nicht, sondern werden nur geloggt.

## Funktionsweise

### Konfiguration im Projektdetail

Wenn Sie ein Git-Repository zu einem Projekt zuweisen oder die Projektdetailansicht öffnen, können Sie für das Repository ein Initialisierungsskript konfigurieren:

1. **Projekt öffnen** → Projektdetailansicht
2. **Repository auswählen** aus der Liste
3. **Button „Initialisierungsskript laden"** klicken neben dem Repository
4. Die Anwendung lädt eine Liste ausführbarer Dateien aus dem Remote-Repository (z. B. `.ps1`, `.bat`, `.cmd`, `.sh`, `.exe`):
   - Wenn ein **Basis-Branch** für das Repository konfiguriert ist, werden die Dateien aus diesem Branch geladen
   - Andernfalls wird der Remote-Standard-Branch verwendet (z. B. `main` oder `master`)
5. **Skript auswählen oder eingeben** — die Vorschlagsliste wird live gefiltert beim Tippen:
   - **Suchfeld verwenden:** Tippen Sie einen Teil des Dateinamens ein — die Liste wird live gefiltert und zeigt nur noch Dateien, deren Pfad den eingegebenen Text enthält (Case-insensitive)
   - **Aus gefilterte Liste auswählen:** Der Eintrag wird übernommen
   - **Manuell eingeben:** Sie können jeden relativen Pfad eingeben, auch wenn er nicht in der Vorschlagsliste sichtbar ist (z. B. um Dateien in Branches zu referenzieren, die nicht gepullt wurden)
6. **Button „Speichern"** klicken, um die Konfiguration zu persistieren.

Die Konfiguration wird in der Datenbank gespeichert und ist persistent — auch nach einem Neustart der Anwendung.

### Automatische Ausführung beim Aufgabenstart

Wenn Sie eine Aufgabe für das Repository starten (über `EntwicklungsprozessService.ProzessStartenAsync()`), läuft der folgende Ablauf ab:

1. **Repository wird geklont** an den lokalen Pfad
2. **Feature-Branch wird erstellt** (vom konfigurierten Basis-Branch, oder vom Standard-Branch, wenn nicht konfiguriert)
3. **Initialisierungsskript wird ausgeführt** (falls konfiguriert) — das Skript wird aus dem lokalen Klon (der auf dem Basis-Branch basiert, falls konfiguriert) ausgeführt:
   - Konfiguration wird auf Aktiv-Status geprüft
   - Skriptpfad wird aufgelöst (relativ zum Repository-Root)
   - Pfad wird validiert (muss innerhalb des Repository-Roots liegen — kein Directory-Escape)
   - Skript wird per PowerShell ausgeführt mit Parametern: `-NoProfile`, `-NonInteractive`, `-ExecutionPolicy Bypass`, `-File <skriptpfad>`
   - **Fehlerbehandlung:** Fehler werden als Warnung geloggt; die Aufgabe läuft normal weiter (nicht blockiert)
4. **Startskript wird ausgeführt** (falls konfiguriert; nach Initialisierungsskript)
5. **KI-Agent-Ausführung startet** normal

### Reihenfolge bei mehreren Skripten

Wenn sowohl Initialisierungs- als auch Startskript konfiguriert sind:

1. **Initialisierungsskript** wird zuerst ausgeführt
2. **Startskript** wird danach ausgeführt

Dies ermöglicht, dass das Initialisierungsskript die Umgebung vorbereitet (z. B. Abhängigkeiten installiert, Konfiguration setzt), auf die das Startskript dann aufbauen kann.

### Deaktivierung der Konfiguration

Um ein konfiguriertes Initialisierungsskript zu deaktivieren, ohne die Konfiguration zu löschen:

1. Bearbeitungsmodus öffnen (Button „Initialisierungsskript laden")
2. **Alle Inhalte löschen** im Eingabefeld
3. Button „Speichern" klicken

Die Konfiguration wird gelöscht, das Initialisierungsskript wird bei zukünftigen Aufgabenstarts nicht mehr ausgeführt.

## Beispiele

### Git-Hooks automatisch installieren

Sie haben ein Repository mit einem Hooks-Verzeichnis `scripts/git-hooks/` und möchten diese automatisch für jeden lokalen Klon installieren:

1. Initialisierungsskript auf `scripts/install-hooks.ps1` setzen
2. Das Skript wird automatisch nach dem Klonen ausgeführt
3. Git-Hooks sind sofort verfügbar für die Aufgabenarbeit

### Build-Dependencies vorbereiten

Für ein C#-Projekt mit NuGet-Abhängigkeiten:

1. Initialisierungsskript auf `scripts/restore-packages.cmd` setzen
2. Das Skript lädt alle NuGet-Pakete herunter und bereitet die Umgebung vor
3. Der darauf folgende KI-Agent oder Entwickler kann sofort mit aktuellen Dependencies arbeiten

### Umgebungsvariablen setzen

Ein Ruby-Projekt benötigt bestimmte `.env`-Werte:

1. Initialisierungsskript auf `scripts/setup-env.sh` setzen
2. Das Skript kopiert `example.env` zu `.env` und setzt projektspezifische Werte
3. Nachfolgende Schritte können die Umgebungsvariablen verwenden

## Einschränkungen

- **Relative Pfade erforderlich:** Der Skriptpfad muss relativ zum Repository-Root sein (z. B. `scripts/init.ps1`). Absolute Pfade werden aus Sicherheitsgründen abgelehnt.
- **Lazy-Validierung:** Das Skript wird erst beim Aufgabenstart validiert. Ein nicht-existentes Skript wird beim Speichern der Konfiguration nicht geprüft — dies ermöglicht Szenarien, in denen das Skript später hinzugefügt wird.
- **Keine Parallelisierung:** Das Skript wird sequenziell nach dem Klon ausgeführt, blockiert aber nicht die Aufgabe bei Fehlschlag.
- **Logs im Protokoll:** Die Ausführung und Fehler werden im Aufgaben-Protokoll dokumentiert, können aber auch übersehen werden, wenn der Anwender die Logs nicht prüft.
- **Keine Standard-Umgebungsvariablen:** Das Initialisierungsskript läuft in derselben PowerShell-Instanz wie das Startskript, teilt aber nicht automatisch alle Umgebungsvariablen — diese müssen ggf. im Skript selbst gesetzt werden.
- **Path-Traversal-Schutz:** Pfade wie `../../../sensitive.ps1` werden abgelehnt. Das Skript muss innerhalb des Repository-Roots liegen.
