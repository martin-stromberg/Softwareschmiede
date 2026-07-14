← [Zurück zur Übersicht](index.md)

# Dateiexplorer — Ablauf für Anwender

## Voraussetzungen

- Ein Task mit konfiguriertem, geklontem Repository (mit lokalem Arbeitsverzeichnis unter `LokalerKlonPfad`)
- Die Aufgabendetailansicht ist offen
- Das neue „Dateien"-Register ist sichtbar (Button neben „Info", „CLI", „Diff")

## Schritt-für-Schritt-Anleitung

### 1. Dateiexplorer öffnen

Klicke auf den Button **„Dateien"** in der Registergruppe der Aufgabendetailansicht (neben „Info", „CLI", „Diff").

Der Explorer wird angezeigt und lädt sofort den vollständigen Dateiebaum des geklonten Repositories im **Standardmodus**.

> **Hinweis:** Das Laden kann je nach Repositorygröße einige Sekunden dauern. Während des Ladens erscheint ein Hinweis.

### 2. Datei im Standardmodus suchen

Klicke die Verzeichnisse links in der Baumstruktur an, um sie auf- oder zuzuklappen:
- `src` → `Softwareschmiede.App` → `Views` → `TaskDetailView.xaml` zum Beispiel
- Nutze das Verzeichnis-Icon (📁) und Datei-Icon (📄) zur visuellen Orientierung

Wenn du eine Datei auswählst (Einfach-Klick), wird ihr Inhalt sofort rechts angezeigt (Read-Only Ansicht).

> **Hinweis:** Bei Binärdateien oder Dateien über 1 MB erscheint ein Hinweis statt des Inhalts (z. B. „[Binärdatei kann nicht angezeigt werden]").

### 3. In den Vergleichsmodus wechseln

Klicke oberhalb des Baums auf den Button **„≍ Vergleich"**, um nur geänderte Dateien anzuzeigen.

Der Explorer lädt nun die Commits aus dem aktuellen Branch (seit der Basis-Referenz wie `origin/main`):
- Jeder Commit wird als aufklappbarer Knoten mit Commit-Subjekt und Kurzform-SHA angezeigt (z. B. `feat: Dateiexplorer hinzufügen (a1b2c3d)`)
- Kinder-Dateien sind zunächst nicht sichtbar

### 4. Geänderte Dateien eines Commits einsehen

Klicke auf das Pfeil-Symbol (▶) neben einem Commit-Knoten, um ihn auszuklappen.

Die Dateien dieses Commits werden angezeigt — mit Status-Icon:
- ✚ Neue Datei (Added)
- ⊕ Geänderte Datei (Modified)
- ✕ Gelöschte Datei (Deleted)

### 5. Diff einer geänderten Datei inspizieren

Wähle im aufgeklappten Commit eine Datei aus.

Rechts erscheint das **Diff** mit farblicher Hervorhebung:
- **Grüner Hintergrund** = neue Zeile hinzugefügt
- **Roter Hintergrund** = Zeile gelöscht
- **Orange Hintergrund** = Zeile geändert; innerhalb der Zeile sind die exakt geänderten Wortteile farblich zusätzlich hervorgehoben

Zeilennummern links helfen bei der Orientierung.

> **Hinweis:** Sehr große Diffs können die Anzeige verlangsamen. Warte ggf. ein oder zwei Sekunden, bis alle Zeilen gerendert sind.

### 6. Refresh durchführen

Klicke oberhalb des Baums auf den Button **„↻ Aktualisieren"**, um den aktuellen Modus (Standard oder Vergleich) neu zu laden.

Dies kann sinnvoll sein, wenn sich das lokale Repository durch externe Änderungen (z. B. `git pull`) verändert hat und du die aktuelle Struktur sehen möchtest.

### 7. Zurück zum Standardmodus

Klicke oberhalb des Baums auf den Button **„☐ Standard"**, um zur vollständigen Baumansicht zurückzukehren.

Der Baum wird neu geladen und zeigt wieder alle Dateien des Repositories (nicht nur geänderte).

## Ergebnis

Nach diesen Schritten hast du:
- Eine vollständige oder gefilterte Sicht auf die Repository-Struktur
- Direkten Zugriff auf Dateiinhalte ohne externes Tool
- Ein visuelles Diff-Vergleich für Änderungen im Branch

## Barrierefreiheit

Der Dateiexplorer nutzt folgende Zugänglichkeitsfeatures:
- **Tastaturnavigation:** TreeView unterstützt Pfeil-Tasten (auf/ab zum Navigieren, rechts/links zum Auf-/Zuklappen)
- **Screenreader:** Baum-Knoten haben AutomationNames für Screenreader-Unterstützung
- **Tastatür-Shortcuts:** Die Buttons haben `AutomationName`-Properties für schnelle Tastaturbindung (z. B. Alt+D für „Dateien")

Die rechte Inhaltsanzeige ist Read-Only und kann mittels Strg+A / Strg+C kopiert werden.
