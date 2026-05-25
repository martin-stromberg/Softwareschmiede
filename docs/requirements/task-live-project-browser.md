# Anforderung: Live Project Browser mit Git Status

## Zusammenfassung

Der Anwender soll während der Ausführung von KI-Anfragen **Sichtbarkeit über vorgenommene Änderungen** haben. Auf der Aufgabenseite sollen die Anzahl der Commits und nicht committeten Dateien angezeigt werden. Ein neuer Bereich zeigt die Verzeichnisstruktur des Projektverzeichnisses als interaktiven Explorer mit Farbcodierung und Statusbadges an. Geänderte, hinzugefügte und gelöschte Dateien werden farblich unterschieden. Der Benutzer kann einzelne Dateien auswählen, ihren Inhalt anzeigen und bei geänderten Dateien einen Diff-Vergleich sehen.

---

## Auslöser und Akteure

- **Auslöser:** Anwender öffnet eine Aufgabenseite; KI-Anfrage wird ausgeführt oder das KI-Protokoll wird aktualisiert
- **Akteure:** Anwender, KI-System (generiert Änderungen), Git-Repository
- **Bezug:** Auf der Aufgabenseite (aktueller Branch einer Aufgabe)

---

## Beschreibung

### 3.1 Kennzahlen auf Aufgabenseite

Auf der Aufgabenseite werden zwei Kennzahlen angezeigt:
- **Commit-Anzahl:** Anzahl der Commits im aktuellen Branch (seit Erstellung des Branches)
- **Nicht committete Dateien:** Anzahl der Dateien mit lokalen Änderungen (staged + unstaged)

**Aktualisierung:**
- Beim initialen Laden der Aufgabenseite
- Nach Abschluss einer KI-Anfrage (Plugin-Integration)
- Wenn das KI-Protokoll (Execution Log) aktualisiert wird
- Optional: Manueller Refresh-Button für den Benutzer

### 3.2 Projektverzeichnis-Explorer (neue Ansicht)

Ein neuer **Aktionsbutton** auf der Aufgabenseite ("Projektverzeichnis anzeigen" o.ä.) wechselt in eine Ansicht, die nur das Projektverzeichnis anzeigt. Dabei ist die Aufgabenseite nicht mehr sichtbar. Der Benutzer kann über einen "Zurück"-Button oder einen zweiten Toggle-Button zur Aufgabenseite zurückkehren.

**Implementierung:** Umschaltung auf derselben Route (z.B. `?view=tree` oder `?view=task`), um den Kontext Aufgabe + Branch zu erhalten.

### 3.3 Verzeichnisstruktur mit Git-Status

Der Explorer zeigt:
- **Verzeichnisbaum** mit Dateien und Ordnern
- **Standardzustand:** Alle Ordner sind aufgeklappt
- **Ausnahmen:** Ignorierte Dateien (.gitignore) werden nicht angezeigt
- **Farbcodierung + Icons/Badges:** Jede Datei erhält einen visuellen Status

#### Git-Status-Kategorien

Die Dateien werden in zwei Sektionen unterteilt:

**Sektion "Staged Changes" (Inszenierte Änderungen)**
- Dateien, die `git add` wurden (für Commit bereit)
- Status kann sein: `staged modified`, `staged added`, `staged deleted`

**Sektion "Unstaged Changes" (Nicht inszenierte Änderungen)**
- Modifizierte, hinzugefügte oder gelöschte Dateien, die noch nicht gestagt sind
- Status kann sein: `modified`, `added`, `untracked`, `deleted`

#### Status-Icons und Farbcodes

| Status | Farbe | Icon/Badge | Beschreibung |
|--------|-------|-----------|-------------|
| **modified** | Orange | M (oder Stift-Icon) | Datei wurde geändert |
| **added** | Grün | A (oder Plus-Icon) | Neue Datei hinzugefügt |
| **deleted** | Rot | D (oder X-Icon) | Datei gelöscht |
| **untracked** | Blau | ? (oder Fragezeichen) | Neue Datei, nicht im Repo |
| **staged modified** | Orange | S+M | Geänderte Datei, gestagt |
| **staged added** | Grün | S+A | Neue Datei, gestagt |
| **staged deleted** | Rot | S+D | Gelöschte Datei, gestagt |

**Hinweis Gelöschte Dateien:** Gelöschte Dateien werden innerhalb ihrer Verzeichnisebene als letzter Eintrag aufgelistet.

### 3.4 Dateiauswahl und Vorschau

Wenn der Benutzer auf eine Datei im Explorer klickt, wird je nach Dateiart unterschiedlich vorgegangen:

#### a) Textbasierte Dateien

**Erkennung:** Dateiendung-Liste + MIME-Typ

**Größe < 1 MB:**
- Dateiinhalt wird angezeigt

**Größe ≥ 1 MB:**
- Hinweistext: "Datei zu groß für direkte Vorschau"
- Download-Button für die aktuelle Version
- Download-Button für die Ursprungsversion (falls geändert)

#### b) Binärdateien

- Hinweistext: "Binärdatei – Vorschau nicht verfügbar"
- Download-Button für die aktuelle Version
- Download-Button für die Ursprungsversion (falls geändert)

#### c) Gelöschte Dateien

- Inhalt aus dem letzten Git-Stand (HEAD) wird angezeigt
- Gilt als "Vorschau der Ursprungsversion"
- Download-Button für die Ursprungsversion

### 3.5 Diff-Anzeige (neue Razor-Komponente)

Für geänderte, hinzugefügte und gelöschte Dateien wird ein **Side-by-Side und Inline-Diff** angezeigt. Dafür wird eine neue, wiederverwendbare **Razor-Komponente** erstellt: `FileComparisonComponent`.

#### Eingaben der Komponente

```csharp
// Beide Versionen als Strings (bei > 1MB: nicht gesetzt, stattdessen Hinweis + Download)
string? OriginalContent { get; set; }    // Inhalt aus HEAD oder Ursprung
string? ModifiedContent { get; set; }    // Aktueller Arbeitsbaum
string FileName { get; set; }             // Für Sprach-Erkennung und Anzeige
string? Language { get; set; }            // Optional für Syntax-Highlighting
string Status { get; set; }               // modified, added, deleted
```

#### Diff-Modus

- **Side-by-Side:** Links Original (HEAD), Rechts Modified (Working Tree)
- **Inline-Diff:** Änderungen im Kontext mit Markierungen
- **Whitespace:** Whitespace-only-Änderungen sind standardmäßig ausgeblendet, können aber per Checkbox angezeigt werden

#### Besonderheiten

- **Hinzugefügte Dateien:** Linke Seite leer
- **Gelöschte Dateien:** Rechte Seite leer (oder "gelöscht" Hinweis)
- **Untracked Dateien:** Linke Seite leer, rechts Inhalt
- **Unveränderte Dateien:** Können auch ausgewählt werden, zeigen dann Diff mit sich selbst (keine Unterschiede)

---

## Eingaben und Ausgaben

### Eingaben
- Repository-Pfad (wird aus der Aufgabe bezogen)
- Aktueller Branch (wird aus der Aufgabe bezogen)
- Git-Status-Informationen (via `git status`, `git diff`, `git log`)

### Ausgaben / Sichtbare Ergebnisse
1. **Auf Aufgabenseite:**
   - Kennzahl: Anzahl Commits
   - Kennzahl: Anzahl nicht committeter Dateien
   - Aktionsbutton: "Projektverzeichnis anzeigen"

2. **In Explorer-Ansicht:**
   - Verzeichnisbaum mit Farbcodes und Icons
   - Getrennte Sektionen für Staged und Unstaged Changes
   - Dateiauswahl mit Inhaltsanzeige / Diff-Vergleich

---

## Fehlerbehandlung

| Szenario | Handling |
|----------|----------|
| Projektverzeichnis existiert nicht | Fehlermeldung auf Aufgabenseite + Explorer deaktiviert |
| Repository nicht lesbar | Fehlermeldung + Kennzahlen auf Fehler zurückgesetzt |
| Git-Befehle schlagen fehl | Fehlerbenachrichtigung; Retry möglich |
| Dateiinhalt unlesbar | Hinweistext "Datei konnte nicht gelesen werden" |
| Whitespace/Encoding-Probleme im Diff | Diff wird trotzdem angezeigt, Warnung bei Encoding-Unklarheiten |
| Netzwerkfehler beim Download | Fehlermeldung + Retry-Button |

---

## Abgrenzung

- **Nicht enthalten:** Direkte Git-Operationen (Commit, Push, Pull) aus dem Explorer – diese bleiben auf der Aufgabenseite oder im CLI
- **Nicht enthalten:** Merge-Konflikt-Auflösung – nur Anzeige
- **Nicht enthalten:** Historische Commits vor dem Branch – nur aktuelle Änderungen im Working Tree + Staging
- **Nicht enthalten:** Multirepository-Ansicht – nur das Repository der aktuellen Aufgabe
- **Nicht enthalten:** Zusammenführung von Staged und Unstaged in einer gemeinsamen Liste – Trennung ist gesamt

---

## Akzeptanzkriterien

### Sprint 1 (MVP)

#### Kennzahlen auf Aufgabenseite
- [ ] Auf der Aufgabenseite werden zwei Kennzahlen angezeigt:
  - [ ] Anzahl der Commits im aktuellen Branch (z.B. "5 Commits")
  - [ ] Anzahl der nicht committeten Dateien (z.B. "3 Dateien mit Änderungen")
- [ ] Kennzahlen werden beim Laden der Aufgabenseite aktualisiert
- [ ] Kennzahlen werden nach Abschluss einer KI-Anfrage aktualisiert
- [ ] Kennzahlen werden beim Aktualisieren des KI-Protokolls aktualisiert
- [ ] Fehlerzustand (z.B. kein Git-Repository) wird deutlich angezeigt

#### Explorer-Ansicht (Umschaltung)
- [ ] Ein Aktionsbutton auf der Aufgabenseite ermöglicht Umschaltung zur Explorer-Ansicht
- [ ] In der Explorer-Ansicht ist die Aufgabenseite nicht mehr sichtbar
- [ ] Ein "Zurück"- oder Toggle-Button ermöglicht Rückkehr zur Aufgabenseite
- [ ] Der Kontext (Aufgabe + Branch) bleibt erhalten

#### Verzeichnisbaum und Git-Status
- [ ] Der Explorer zeigt die vollständige Verzeichnisstruktur (Root des Repositories)
- [ ] Alle Ordner sind standardmäßig aufgeklappt
- [ ] Ignorierte Dateien (.gitignore) werden nicht angezeigt
- [ ] Dateien sind in zwei Sektionen unterteilt:
  - [ ] "Staged Changes" (mit Subkategorien: modified, added, deleted)
  - [ ] "Unstaged Changes" (mit Subkategorien: modified, added, deleted, untracked)
- [ ] Jede Datei zeigt ein visuelles Status-Icon/Badge (z.B. M, A, D, ?)
- [ ] Gelöschte Dateien werden als letzter Eintrag der Verzeichnisebene aufgelistet

#### Status-Farbcodes
- [ ] Modified (Unstaged): Orange
- [ ] Added (Unstaged): Grün
- [ ] Deleted (Unstaged): Rot
- [ ] Untracked: Blau
- [ ] Staged Modified: Orange mit S-Badge
- [ ] Staged Added: Grün mit S-Badge
- [ ] Staged Deleted: Rot mit S-Badge

#### Dateiauswahl und Vorschau
- [ ] Klick auf eine textbasierte Datei < 1 MB zeigt ihren Inhalt
- [ ] Klick auf eine Datei > 1 MB zeigt Hinweistext + Download-Buttons (beide Versionen)
- [ ] Klick auf eine Binärdatei zeigt Hinweistext + Download-Buttons
- [ ] Klick auf gelöschte Dateien zeigt Inhalt aus Git-Stand (HEAD) + Download-Button

#### Razorkomponente FileComparison
- [ ] Eine neue wiederverwendbare Razor-Komponente `FileComparisonComponent` ist implementiert
- [ ] Sie empfängt Parameter: `OriginalContent`, `ModifiedContent`, `FileName`, `Language`, `Status`
- [ ] Sie unterstützt sowohl Side-by-Side als auch Inline-Diff-Ansicht
- [ ] Sie zeigt Änderungen deutlich an (Farben, Markierungen, Zeilennummern)
- [ ] Whitespace-only-Änderungen sind ausgeblendet, können aber per Checkbox angezeigt werden

#### Diff-Anzeige für verschiedene Status
- [ ] Modified-Dateien: Side-by-Side und Inline-Diff (HEAD vs. Working Tree)
- [ ] Added-Dateien: Linke Seite leer, rechts neuer Inhalt
- [ ] Deleted-Dateien: Linke Seite Ursprungsinhalt (HEAD), rechts leer
- [ ] Untracked-Dateien: Linke Seite leer, rechts Vorschau (falls < 1 MB)

#### Fehlerfälle
- [ ] Nicht vorhandenes Repository: Fehlermeldung, Explorer deaktiviert
- [ ] Git-Befehle schlagen fehl: Fehlermeldung mit Retry-Möglichkeit
- [ ] Datei unlesbar: Hinweistext statt Inhalt

---

## Offene Punkte / Für spätere Versionen

- Live-Aktualisierung (Polling / WebSocket) – aktuell manueller Refresh oder Event-basiert
- Massenoperationen im Explorer (z.B. Alle Dateien staggen)
- Direkte Git-Operationen aus dem Explorer (z.B. Reset)
- Syntax-Highlighting im Diff (abhängig von Texteditor-Library)
- Favoriten / Bookmarks für häufig angesehene Dateien
- Vergleich gegen andere Branches oder beliebige Commits

---

## Abhängigkeiten & Annahmen

- **Git ist installiert** und über CLI erreichbar
- **Aufgabe ist mit Branch verknüpft** und Repository ist lokal verfügbar
- **Benutzer hat Lesezugriff** auf das Repository-Verzeichnis
- **Textdateien sind UTF-8 oder kompatibel kodiert** (bei Encoding-Problemen Warnung)
