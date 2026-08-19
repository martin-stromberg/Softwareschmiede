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

> **Hinweis:** Diese priorisierte Einzelplugin-Auswahl bestimmt das Verhalten des **Haupt-Buttons** (öffnet direkt den ersten Einstiegspunkt genau dieses einen Plugins). Der **Dropdown-Button** des Split-Buttons weicht davon ab: Er zeigt die aggregierten Einstiegspunkte **aller** aktivierten, kompatiblen Plugins zur Auswahl an, nicht nur des hier priorisierten (siehe Abschnitt 3).

### 3. IDE öffnen (Split-Button-Muster)

Das ausgewählte Plugin wird mit einem Split-Button-Muster geöffnet, das Flexibilität bei mehreren Einstiegspunkten (z. B. mehrere `.sln`-Dateien) bietet:

**Haupt-Button (öffnet direkt):**
- Öffnet automatisch den **ersten Einstiegspunkt** des Plugins, ohne Dialog
- Verhalten wie der Klassische Einzelklick, schnell und unkompliziert
- Beispiele:
  - Visual Studio: die erste gefundene `.sln` oder `.slnx`-Datei
  - Visual Studio Code: das Repository-Verzeichnis

**Dropdown-Button (nur sichtbar bei mehreren Einstiegspunkten insgesamt):**
- Wird **nur angezeigt**, wenn insgesamt mehr als ein Einstiegspunkt gefunden wurde
- Anders als der Haupt-Button beschränkt sich der Dropdown-Button dabei nicht auf das eine für den Haupt-Button aufgelöste (priorisierte) Plugin: Er aggregiert die Einstiegspunkte **aller aktivierten, kompatiblen IDE-Plugins** (sowohl `Explicit`- als auch `Fallback`-kompatible), nicht nur des einen priorisierten
- Zeigt einen **Auswahl-Dialog** mit allen aggregierten Einstiegspunkten, **plugin-qualifiziert** beschriftet im Format „{PluginName}: {Einstiegspunkt-Bezeichnung}" (z. B. „Visual Studio: MyProject.sln"), außer die Bezeichnung ist bereits identisch mit dem Plugin-Namen — dann erscheint nur der Plugin-Name (z. B. „Visual Studio Code")
- Die Liste ist sortiert: zuerst alle Einstiegspunkte der `Explicit`-kompatiblen Plugins, danach alle der `Fallback`-kompatiblen Plugins, jeweils in der konfigurierten Reihenfolge (`plugins.ide.order`)
- Benutzer kann eine spezifische Solution, ein Workspace oder ein Verzeichnis wählen — auch aus einem anderen Plugin als dem für den Haupt-Button priorisierten
- Nach der Auswahl öffnet das zum gewählten Eintrag gehörende Plugin den gewählten Einstiegspunkt (nicht zwingend das für den Haupt-Button priorisierte Plugin)

**Beispiel: Repository mit mehreren Visual-Studio-Solutions**
- Haupt-Button: öffnet immer die erste Solution des priorisierten Plugins (z. B. `backend.sln`)
- Dropdown-Button: zeigt Dialog mit „Visual Studio: backend.sln", „Visual Studio: frontend.sln", „Visual Studio: shared.sln" zur Auswahl

**Beispiel: Repository mit Solution und zusätzlich aktiviertem Fallback-Plugin**
- Ist z. B. Visual Studio (`Explicit`, da `.sln` gefunden) und Visual Studio Code (`Fallback`) beide aktiviert, zeigt der Dropdown-Dialog alle Solutions von Visual Studio **und** den Eintrag von Visual Studio Code gemeinsam an (z. B. „Visual Studio: backend.sln", „Visual Studio: frontend.sln", „Visual Studio Code") — auch wenn der Haupt-Button weiterhin nur die erste Visual-Studio-Solution öffnet

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

- **Visual Studio:** Prüft nur den Repository-Root auf `.sln`/`.slnx`-Dateien, nicht untergeordnete Verzeichnisse. Falls die Solution-Datei nicht im Root liegt, wird Visual Studio als inkompatibel erkannt. Der Auswahl-Dialog zeigt alle gefundenen Solutions im Root.
- **Visual Studio Code:** Erfordert, dass die `code`-CLI installiert und im PATH verfügbar ist. Ist dies nicht der Fall, schlägt das Öffnen fehl. VS Code öffnet immer das gesamte Repository-Verzeichnis (ein Einstiegspunkt).
- **Mindestens ein aktives Plugin erforderlich:** Der Benutzer muss immer mindestens ein IDE-Plugin aktiviert lassen. Alle Plugins zu deaktivieren ist nicht möglich.
- **Nur Betriebssystem-Handler:** Die `.sln`-Datei wird mit dem registrierten Standard-Handler des Betriebssystems geöffnet. Dies ist normalerweise Visual Studio, kann aber bei mehreren Installationen oder benutzerdefinierten Assoziationen unterschiedlich sein.
- **Auswahl-Dialog nur für mehrere Einstiegspunkte:** Der Auswahl-Dialog (Dropdown-Button) wird nur angezeigt, wenn die aggregierte Gesamtanzahl der Einstiegspunkte über alle aktivierten, kompatiblen Plugins hinweg mehr als eins beträgt (nicht nur bezogen auf das eine für den Haupt-Button priorisierte Plugin). Ist insgesamt nur ein Einstiegspunkt vorhanden, erfolgt direkt die Öffnung ohne Dialog.
