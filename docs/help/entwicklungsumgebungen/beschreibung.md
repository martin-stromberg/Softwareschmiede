← [Zurück zur Übersicht](index.md)

# Entwicklungsumgebungen — Beschreibung

## Zweck

Das IDE-Plugin-System ersetzt das bisherige statische IDE-Aufrufsystem durch ein flexibles, erweiterbares Architektur-Muster. Anstatt immer eine fest konfigurierte IDE zu öffnen, wählt das System automatisch die beste IDE für das aktuelle Repository basierend auf dessen Struktur.

**Probleme der bisherigen Lösung:**
- Statische IDE-Auswahl (z. B. immer Visual Studio), unabhängig vom Repository-Typ
- Keine Flexibilität bei mehreren Repositorys mit unterschiedlichen Anforderungen
- Benutzer konnten nicht steuern, welche IDEs verfügbar sein sollen

**Lösung durch IDE-Plugins:**
- Dynamische, kompatibilitätsbasierte Auswahl der IDE pro Repository
- Benutzer bestimmt, welche IDEs aktiviert sind und in welcher Prioritätsreihenfolge
- System ist erweiterbar: neue IDEs können als Plugins hinzugefügt werden (z. B. JetBrains Rider)

## Funktionsweise

Die IDE-Auswahl folgt einem dreistufigen Prozess:

### 1. Kompatibilitätsprüfung

Jedes aktivierte IDE-Plugin prüft, ob es mit dem Repository kompatibel ist und meldet eines dieser Ergebnisse:

- **`Explicit` (Explizite Kompatibilität):** Das Plugin erkannt Repository-spezifische Merkmale. Beispiel: Visual Studio findet `.sln` oder `.slnx`-Dateien.
- **`Fallback` (Rückfalllösung):** Das Plugin funktioniert mit jedem Repository. Beispiel: Visual Studio Code, das beliebige Verzeichnisse öffnen kann.
- **`Incompatible` (Nicht kompatibel):** Das Plugin ist für dieses Repository nicht geeignet und wird nicht berücksichtigt.

### 2. Auswahl nach Priorität

Die aktivierten Plugins werden in der benutzer-konfigurierten Reihenfolge geprüft:

1. **Explizite Kompatibilität gewinnt:** Das erste Plugin mit `Explicit` wird sofort ausgewählt und verwendet.
2. **Fallback als Sicherheitsnetz:** Wenn kein Plugin explizit kompatibel ist, wird das erste Plugin mit `Fallback` verwendet.
3. **Default als letztes Mittel:** Falls kein Plugin aktiv oder kompatibel ist, wird das System-Standardplugin verwendet.

### 3. IDE öffnen

Das ausgewählte Plugin öffnet das Repository in der entsprechenden IDE:

- **Visual Studio:** Die erste gefundene `.sln` oder `.slnx`-Datei wird mit dem Betriebssystem-Standardhandler geöffnet (meist direkt in Visual Studio).
- **Visual Studio Code:** Das Repository-Verzeichnis wird mit dem `code`-Befehlszeilenwerkzeug geöffnet.

## Beispiele

### Szenario 1: C#-Projekt mit Visual-Studio-Lösung

1. Benutzer klickt „IDE öffnen" für ein Repository mit `.sln`-Datei.
2. System prüft Visual Studio: findet `.sln` → meldet `Explicit`.
3. Visual Studio wird sofort ausgewählt und geöffnet.
4. Visual Studio Code wird nicht geprüft (weil bereits ein explizites Match).

### Szenario 2: Python-Projekt ohne Projektdatei

1. Benutzer klickt „IDE öffnen" für ein reines Python-Repository ohne `.sln`.
2. System prüft Visual Studio: findet `.sln` → meldet `Incompatible`.
3. System prüft Visual Studio Code: meldet `Fallback` (akzeptiert alle Verzeichnisse).
4. Visual Studio Code wird ausgewählt und geöffnet.

### Szenario 3: Benutzerkonfigurierte Reihenfolge

1. Benutzer bevorzugt VS Code und stellt es oben in die Prioritätsliste.
2. Für ein Repository ohne `.sln`-Datei:
   - VS Code meldet `Fallback` (erste Prüfung) → wird sofort ausgewählt.
   - Visual Studio wird nicht geprüft.
3. Ergebnis: VS Code öffnet sich, obwohl Visual Studio installiert ist.

## Einschränkungen

- **Visual Studio:** Prüft nur den Repository-Root auf `.sln`/`.slnx`-Dateien, nicht untergeordnete Verzeichnisse. Falls die Solution-Datei nicht im Root liegt, wird Visual Studio als inkompatibel erkannt.
- **Visual Studio Code:** Erfordert, dass die `code`-CLI installiert und im PATH verfügbar ist. Ist dies nicht der Fall, schlägt das Öffnen fehl.
- **Mindestens ein aktives Plugin erforderlich:** Der Benutzer muss immer mindestens ein IDE-Plugin aktiviert lassen. Alle Plugins zu deaktivieren ist nicht möglich.
- **Nur Betriebssystem-Handler:** Die `.sln`-Datei wird mit dem registrierten Standard-Handler des Betriebssystems geöffnet. Dies ist normalerweise Visual Studio, kann aber bei mehreren Installationen oder benutzerdefinierten Assoziationen unterschiedlich sein.
- **Keine Mehrfachauswahl:** Der Benutzer sieht keine IDE-Auswahl-Dialog, wenn mehrere Plugins aktiv sind. Die beste IDE wird automatisch gewählt.
