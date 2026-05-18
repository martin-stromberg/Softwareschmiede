# Anforderungsanalyse – Live Project Browser mit Git-Status (Konsolidierte Version)

> **Dokument-Typ:** Requirements Analysis (Konsolidiert & Erweitert)  
> **Status:** ✅ Implementiert und dokumentiert  
> **Version:** 2.2.0  
> **Datum:** 2026-05-20 | Aktualisiert: 2026-05-18

---

## 1. Überblick und Projektkontext

Die Aufgabenseite soll den aktuellen Git-Status des verknüpften lokalen Repositories sichtbar machen. Zusätzlich wird eine eigene Explorer-Ansicht bereitgestellt, in der der Projektbaum, Git-Status, Dateivorschau und Diff-Vergleich direkt aus dem lokalen Arbeitsverzeichnis abgeleitet werden.

### 1.1 Zielbild

- Schnell sichtbare Kennzahlen auf der Aufgabenseite (Commits, lokale Änderungen)
- Umschaltbare Explorer-Ansicht für das Repository
- Visuelle Unterscheidung von staged und unstaged Änderungen
- Dateivorschau für Textdateien sowie Diff für geänderte Inhalte
- Git-Status transparent visualisiert (Farbcodes, Icons)

### 1.2 Geschäftlicher Kontext

**Problemstellung:**
- Benutzer können derzeit den Git-Status eines aufgabenspezifischen Branches nicht sofort erfassen
- Keine Übersicht über Commits seit Branch-Start
- Keine Sicht auf lokale Änderungen (staged/unstaged getrennt)
- Keine Möglichkeit, Dateien und ihre Diffs schnell zu inspizieren
- Explorer-Navigation muss über externe Tools erfolgen

**Geschäftswert:**
- Schnellere Feedback-Schleifen bei KI-gesteuerten Aufgaben
- Bessere Kontrolle über Branch-Zustand vor PR-Erstellung
- Verbesserte Fehleranalyse durch Diff-Vergleich
- Integrierte Entwicklungs-Umgebung (alle Tools an einem Ort)

### 1.3 Referenzen

- **Hauptdokumentation:** docs/requirements.md (Abschnitt 3.3 – Aufgabenverwaltung, 3.8 – Dashboard)
- **Architektur-Blueprint:** docs/architecture/live-project-browser-git-status-architecture-blueprint.md
- **ERM:** docs/architecture/live-project-browser-git-status-entity-relationship-model.md
- **Architecture Review:** docs/improvements/live-project-browser-git-status-architecture-review.md
- **Git-Orchestrierung:** src/Softwareschmiede/Application/Services/GitOrchestrationService.cs
- **Git-Plugin-Vertrag:** src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs
- **Lokales Repository-Plugin:** plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs
- **Aufgabenseite:** src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Kennzahlenanzeige Commits:** Aufgabenseite zeigt die Anzahl der Commits im aktuellen Branch an (via `git rev-list --count HEAD`). → [Architektur-Blueprint](../architecture/live-project-browser-git-status-architecture-blueprint.md) | Repository-Status | MUST HAVE | ✅ Umgesetzt |
| **FR-2** | **Kennzahlenanzeige Änderungen:** Aufgabenseite zeigt die Anzahl lokaler Dateiänderungen an (staged + unstaged, dedupliziert via `WorkspaceSnapshot.ChangedFileCount`). → [Architektur-Blueprint](../architecture/live-project-browser-git-status-architecture-blueprint.md) | Repository-Status | MUST HAVE | ✅ Umgesetzt |
| **FR-3** | **Automatische Aktualisierung beim Laden:** Kennzahlen werden beim initialen Laden der Aufgabenseite über `OnInitializedAsync` → `LadeAsync()` → `LadeWorkspaceAsync()` automatisch ermittelt. | Repository-Status | MUST HAVE | ✅ Umgesetzt |
| **FR-4** | **Refresh nach KI-Ausführung:** Nach Abschluss des KI-Laufs wird `LadeAsyncWithScope()` → `LadeWorkspaceAsync()` über den `onCompleted`-Callback ausgelöst. Zusätzlich steht ein manueller 🔄-Button bereit. | Repository-Status | MUST HAVE | ✅ Umgesetzt |
| **FR-5** | **Explorer-Toggle:** Ein Aktionsbutton ("🗂️ Explorer") wechselt via `SwitchToExplorerViewAsync()` in die Explorer-Ansicht; die Aufgabenseite wird ausgeblendet. | UI-Navigation | MUST HAVE | ✅ Umgesetzt |
| **FR-6** | **Kontexterhaltung via Query-Parameter:** In der Explorer-Ansicht bleibt der Aufgaben-Kontext über `?view=task` bzw. `?view=tree` erhalten; der View-Toggle wird per `NavigationManager.NavigateTo(..., replace: true)` gesetzt und bei Reload über `[SupplyParameterFromQuery]` wiederhergestellt. → [Architecture Review AR-04](../improvements/live-project-browser-git-status-architecture-review.md) | UI-Navigation | MUST HAVE | ✅ Implementiert |
| **FR-7** | **Verzeichnisstruktur (geänderte Dateien):** Der Explorer zeigt alle Dateien mit Git-Änderungen (staged + unstaged) in einer zweispaltigen Ansicht. Die Implementierung baut hierfür einen rekursiven Baum aus `WorkspaceSnapshot.RootNodes` und eine flache Liste aus `WorkspaceSnapshot.FlatFiles` auf. → [Architektur-Blueprint §5.1](../architecture/live-project-browser-git-status-architecture-blueprint.md) | Explorer-Anzeige | MUST HAVE | ✅ Implementiert |
| **FR-8** | **Standard-Aufklappen & .gitignore-Filterung:** Ignorierte Dateien (Porcelain-Code `!!`) werden explizit gefiltert; im Baum werden Verzeichnisse aufklappbar dargestellt, in der Listenansicht flach gerendert. | Explorer-Anzeige | MUST HAVE | ✅ Implementiert |
| **FR-9** | **Staged/Unstaged-Trennung:** Git-Änderungen werden in zwei separaten, beschrifteten Sektionen ("Staged Changes" und "Unstaged Changes") dargestellt. | Git-Status-Anzeige | MUST HAVE | ✅ Umgesetzt |
| **FR-10** | **Status-Icons und Farbcodes:** Jede Datei zeigt einen Buchstaben-Badge (M, SM, SA, SD, SR, D, ?) mit CSS-Farbklasse gemäß `WorkspaceFileStatus` (7 Varianten). → [Architecture Review AR-01](../improvements/live-project-browser-git-status-architecture-review.md) | Git-Status-Anzeige | MUST HAVE | ✅ Umgesetzt |
| **FR-11** | **Sortierung gelöschter Dateien:** Gelöschte Dateien (`Deleted` / `StagedDeleted`) werden innerhalb jeder Sektion ans Ende sortiert (primär: delete-flag, sekundär: RelativePath lexikografisch). | Explorer-Anzeige | MUST HAVE | ✅ Umgesetzt |
| **FR-12** | **Dateiauswahl & Vorschau:** Klick auf eine Datei löst `DateiAuswaehlenAsync()` aus; dieser prüft `IsTooBig`, `IsBinary`, `IsDeleted` und zeigt Inhalt, Hint oder Diff. | Dateivorschau | MUST HAVE | ✅ Umgesetzt |
| **FR-13** | **Vergleichsansicht:** Die Aufgabenseite zeigt für geänderte Dateien eine Vergleichsansicht mit Side-by-Side- und Inline-Darstellung, Whitespace-Toggle und Trunkierung. | Dateivorschau | MUST HAVE | ✅ Umgesetzt |
| **FR-14** | **Größen- und Binärdatei-Handling:** Dateien > 1 MB (`MaxInlineBytes = 1_048_576`) setzen `FilePreview.IsTooBig = true`. Binärdateien werden per Null-Byte-Heuristik auf den ersten 8 KB erkannt (`IsBinary`-Methode); beide Fälle zeigen einen Hinweis-Panel statt Inline-Inhalt. → [Architecture Review AR-03](../improvements/live-project-browser-git-status-architecture-review.md) | Dateivorschau | MUST HAVE | ✅ Umgesetzt |
| **FR-15** | **Fehlerbehandlung:** `WorkspaceSnapshot.FromError()` kapselt Fehlerzustände; die UI zeigt Fehler-Alerts mit Hinweistext. Alle Exceptions werden geloggt (ohne sensible Inhalte). | Fehlerbehandlung | MUST HAVE | ✅ Umgesetzt |
| **FR-16** | **Gelöschte Dateien – Originalversion:** In `GetDiffContentsAsync` wird für gelöschte Dateien (`isDeleted = true`) `GetFilePreviewAsync(..., fromHead: true)` aufgerufen, was `git show HEAD:path` nutzt. → [Architecture Review AR-02](../improvements/live-project-browser-git-status-architecture-review.md) | Dateivorschau | MUST HAVE | ✅ Umgesetzt |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Keine neue Persistierung:** Alle angezeigten Informationen werden aus dem lokalen Repositoryzustand zur Laufzeit abgeleitet; keine neuen Tabellen oder Spalten erforderlich. | Persistenz | MUST HAVE | ✅ Umgesetzt |
| **NFR-2** | **Performance bei großen Repositories:** Status-Ermittlung via `git status --porcelain=v1 --untracked-files=all` bleibt für übliche Repositories schnell; die UI nutzt `Virtualize` für die Dateiliste. Sehr große Mengen untracked Dateien bleiben ein dokumentierter Trade-off. → [Architecture Review](../improvements/live-project-browser-git-status-architecture-review.md) | Performance | HIGH | ✅ Implementiert |
| **NFR-3** | **UI-Konsistenz & Lokalisierung:** Die Ansicht nutzt die bestehende deutsche Textstrategie; Fehlermeldungen und Labels werden konsistent aus der UI-Schicht ausgegeben. | Internationalisierung | MUST HAVE | ✅ Implementiert |
| **NFR-4** | **Robustheit:** `FilePreview.IsTooBig` und `FilePreview.IsBinary` verhindern blockierende Inline-Darstellung. `WorkspaceSnapshot.FromError()` kapselt alle Git-Fehler defensiv. | Robustheit | MUST HAVE | ✅ Umgesetzt |
| **NFR-5** | **Sicherheit in Logs:** `_logger.LogWarning` / `.LogError` loggen nur Pfad und Fehlermeldung, keine Dateiinhalte oder Tokens. | Sicherheit | MUST HAVE | ✅ Umgesetzt |
| **NFR-6** | **Responsive Design:** Die Explorer-Ansicht bleibt auf Desktop-, Tablet- und kleineren Bildschirmen nutzbar; die zweispaltige Darstellung fällt bei schmalen Viewports in die vorhandene Grid-/Card-Struktur zurück. | UX/Accessibility | HIGH | ✅ Implementiert |
| **NFR-7** | **Datenfresh-Garantie:** Der Status-Snapshot wird bei jedem Öffnen des Explorers und nach jedem KI-Lauf neu ermittelt (kein Cache). Ein manueller 🔄-Button steht zusätzlich bereit. | Data Freshness | MUST HAVE | ✅ Umgesetzt |

---

## 4. Akzeptanzkriterien (SMART)

### 4.1 Kennzahlen-Anzeige (AC-Gruppe 1)

- **AC-1.1:** Auf der Aufgabenseite gibt es zwei Anzeigen: "Commits" und "Lokale Änderungen".
- **AC-1.2:** "Commits" zeigt die exakte Zahl der Commits im aktuellen Branch (ermittelt via \git rev-list --count HEAD\).
- **AC-1.3:** "Lokale Änderungen" zeigt die Summe aller staged + unstaged Dateiänderungen (ermittelt aus \git status --porcelain\).
- **AC-1.4:** Beim Laden der Seite werden beide Kennzahlen automatisch abgefragt und angezeigt (Timeout: max. 2 Sekunden).
- **AC-1.5:** Nach KI-Ausführung oder Protokoll-Update werden Kennzahlen erneut geladen, ohne Seite neu zu laden.

### 4.2 Explorer-Umschaltung & Navigation (AC-Gruppe 2)

- **AC-2.1:** ✅ Die Aufgabenseite hat einen Button "🗂️ Explorer" (wenn `LokalerKlonPfad` gesetzt ist).
- **AC-2.2:** ✅ Klick auf diesen Button zeigt die Explorer-Ansicht und blendet die Aufgabenseite aus.
- **AC-2.3:** ❌ **Offen:** Die URL ändert sich **nicht** zu `?view=tree` – der Toggle ist als In-Memory-Boolean implementiert (`_showExplorer`). `NavigationManager` ist in der Komponente vorhanden aber wird für diesen Zweck nicht genutzt.
- **AC-2.4:** ❌ **Offen:** Ein expliziter URL-basierter Back-Button (`?view=task`) ist nicht implementiert. Der Zurück-Button blendet den Explorer über `_showExplorer = false` aus.
- **AC-2.5:** ❌ **Offen:** F5-Refresh setzt `_showExplorer` auf `false` zurück; die zuletzt gewählte Ansicht wird nicht wiederhergestellt.

### 4.3 Explorer-Verzeichnisbaum (AC-Gruppe 3)

- **AC-3.1:** ❌ **Offen:** Der Explorer zeigt **keine vollständige rekursive Baumstruktur** – er zeigt ausschließlich Dateien mit Git-Änderungen als Flachliste. Der Titel lautet "🗂️ Geänderte Dateien". `WorkspaceFileNode.IsDirectory` und `.Children` sind im Modell vorhanden, werden aber von `GetStatusEntriesAsync` nicht befüllt.
- **AC-3.2:** ❌ **Offen:** Da kein Verzeichnisbaum aufgebaut wird, ist dieses Kriterium derzeit nicht anwendbar.
- **AC-3.3:** ✅ `.gitignore`-Filterung greift nativ über Git: Porcelain-Einträge mit Code `!!` werden in `GetStatusEntriesAsync` explizit übersprungen.
- **AC-3.4:** ✅ Untracked-Dateien (`??`) werden mit Status `WorkspaceFileStatus.Untracked` und Badge `?` angezeigt.
- **AC-3.5:** ❌ **Offen:** Kein virtualisiertes Scrolling implementiert.

### 4.4 Git-Status & Farbcodes (AC-Gruppe 4)

- **AC-4.1:** ✅ Staged Changes und Unstaged Changes sind in separaten, beschrifteten Sektionen angeordnet.
- **AC-4.2:** ✅ Jede Datei hat einen Status-Badge mit Buchstaben-Kürzel und CSS-Klasse:
  - `M` (Modified, unstaged)
  - `D` (Deleted, unstaged)
  - `?` (Untracked)
  - `SM` (StagedModified)
  - `SA` (StagedAdded)
  - `SD` (StagedDeleted)
  - `SR` (StagedRenamed)
- **AC-4.3:** ✅ Gelöschte Dateien sind in ihrer Sektion am Ende sortiert (`OrderBy(deleted).ThenBy(relativePath)`).
- **AC-4.4:** ✅ Status deterministisch aus `git status --porcelain=v1` abgeleitet. Die Implementierung behandelt auch `C` (Copied), `T` (Type-change) und `U` (Unmerged) über `WorkspaceFileStatus`.

### 4.5 Dateivorschau & Diff (AC-Gruppe 5)

- **AC-5.1:** ✅ Klick auf eine Textdatei < 1 MB zeigt ihren aktuellen Inhalt (via `ReadFromWorkingTree`, Byte-Grenze: 1.048.576 Bytes).
- **AC-5.2:** ✅ Klick auf eine geänderte Datei zeigt einen Diff (Original aus HEAD via `git show HEAD:path` vs. Arbeitsversion via Dateisystem). Beide Seiten werden parallel geladen.
- **AC-5.3:** ✅ Klick auf eine gelöschte Datei lädt Originalversion via `git show HEAD:path` (`fromHead: true` in `GetDiffContentsAsync`).
- **AC-5.4:** ✅ Klick auf eine Binärdatei zeigt Hint "🔒 Binärdatei – Vorschau nicht verfügbar" (`FilePreview.IsBinary`). **Hinweis:** Erkennung erfolgt per Null-Byte-Heuristik auf den ersten 8.192 Bytes, nicht per MIME-Typ.
- **AC-5.5:** ✅ Klick auf Datei > 1 MB zeigt Hint "📦 Datei zu groß für direkte Vorschau (XMB)" (`FilePreview.IsTooBig`). **Hinweis:** Für Dateien aus HEAD (via `git show`) wird die Grenze auf Zeichenanzahl (nicht Bytes) geprüft – bei reinem UTF-8 kein relevanter Unterschied.
- **AC-5.6:** ✅ Die Vergleichsansicht bietet Toggle zwischen Side-by-Side und Inline-Ansicht.
- **AC-5.7:** ✅ Whitespace-Differenzen können eingeblendet werden (Checkbox "Leerzeichen anzeigen", Default: ausgeblendet). Der Diff wird bei Änderung via `RecomputeDiff` neu berechnet.

### 4.6 Fehlerbehandlung (AC-Gruppe 6)

- **AC-6.1:** Ist der Repositorypfad ungültig, wird Meldung "Repository nicht gefunden" angezeigt.
- **AC-6.2:** Git-Fehler (z.B. Permission Denied) zeigen aussagekräftige Fehlermeldung.
- **AC-6.3:** Unlesbare Dateien zeigen "Datei unlesbar: [Grund]".
- **AC-6.4:** Alle Fehler werden geloggt, ohne sensible Daten zu enthalten.

---

## 5. Scope und Out-of-Scope

### ✅ In-Scope

- Kennzahlenanzeige (Commits, Änderungen) auf Aufgabenseite
- Repository-Explorer mit Verzeichnisbaum und Git-Status
- Fileigur-Statusanzeige (modified, added, deleted, untracked)
- Staged/Unstaged Trennung in separaten Sektionen
- Dateivorschau (Textdateien, Binärdateien, große Dateien)
- Vergleichsansicht (Side-by-Side und Inline)
- Fehlerbehandlung und aussagekräftige Fehlermeldungen
- Query-Parameter-basierte View-Umschaltung (?view=task vs. ?view=tree)

### ❌ Out-of-Scope

- **Commit-Aktionen im Explorer** (alle Commit-Funktionen bleiben in Aufgabenseite)
- **Push/Pull-Operationen** im Explorer (bestehende Funktionen)
- **Pull-Request-Erstellung** (bestehende Funktionen)
- **Merge-Konflikt-Lösung** (separate Anforderung)
- **Historische Branch-Vergleiche** (separate Anforderung)
- **Persistente Speicherung von Explorer-Zuständen** (kein UI-State-Persistierung)
- **Dateibearbeitung im Explorer** (read-only)
- **Branch-Wechsel im Explorer** (Single-Branch-Kontext)

---

## 6. Domänenmodell und Glossar

### 6.1 Kernentitäten (Laufzeit)

**AUFGABE** (persistent, bestehend)
- aufgabeId: string (PK)
- branchName: string (z.B. "task/ai-42-feature")
- lokalerKlonPfad: string (absolut, z.B. "C:\\repos\\work\\task-ai-42")
- status: enum { Offen | InBearbeitung | KI_Aktiv | Abgeschlossen | Fehlgeschlagen }

**REPOSITORY_CONTEXT** (logisch, aus AUFGABE abgeleitet)
- contextId: string (eindeutig für diese Session)
- aufgabeId: string (FK)
- lokalerPfad: string
- repositoryTyp: enum { LocalDirectory | GitHub | ... }

**WORKSPACE_SNAPSHOT** (logisch, Laufzeit-Erfassung)
- snapshotId: string (eindeutig)
- erfasstAm: DateTime
- commitAnzahl: int
- geaenderteDateienAnzahl: int
- statusEintraege: FILE_STATUS_ENTRY[]

**FILE_STATUS_ENTRY** (logisch)
- entryId: string
- relativerPfad: string
- statusCode: string (M, A, D, ??, AD, etc.)
- istStaged: bool
- istVerzeichnis: bool
- groesseBytes: long
- mimetype: string (optional)

**FILE_PREVIEW** (logisch, bei Klick erzeugt)
- entryId: string (FK)
- istText: bool
- istBinary: bool
- previewModus: enum { InlineContent | DiffSideBySide | DiffInline | DownloadHint }
- inhaltsVorschau: string (max. 1 MB)

**DIFF_VIEW_STATE** (logisch, bei Klick auf geänderte Datei)
- entryId: string (FK)
- originalRevision: string ("HEAD")
- modifizierteRevision: string ("working-tree")
- zeigeWhitespace: bool (default false)

### 6.2 Glossar

| Begriff | Definition |
|---------|-----------|
| **Branch-Kontext** | Der aufgabenspezifische Entwicklungszweig, in dem KI arbeitet (z.B. task/ai-42-feature) |
| **Lokaler Klon** | Eine Kopie des Repositories im aufgabenspezifischen Arbeitsverzeichnis |
| **Workspace Snapshot** | Der zur Laufzeit erfasste Zustand von Dateien, Ordnern und Git-Status |
| **Staged Changes** | Änderungen, die mit \git add\ für den nächsten Commit vorbereitet sind |
| **Unstaged Changes** | Änderungen im Arbeitsverzeichnis, die noch nicht staged sind |
| **Porcelain-Status** | Das strukturierte Format von \git status --porcelain\ (z.B. "M  src/main.cs") |
| **Diff-View** | Die Side-by-Side oder Inline-Darstellung der Unterschiede zwischen zwei Versionen |
| **Größenlimit** | Schwellenwert für Inline-Anzeige: 1 MB (Dateien darüber zeigen Download-Hinweis) |
| **Repository-Explorer** | Die Tree- bzw. Listenansicht mit Dateien, Git-Status und Vorschau (Query-Parameter \?view=tree\ / \?view=task\) |

---

## 7. Nutzungsfälle

### UC-1: Kennzahlen beim Aufgaben-Öffnen anzeigen

**Akteur:** Benutzer  
**Vorbedingung:** Aufgabe existiert im System und hat einen lokalen Klon

**Ablauf:**
1. Benutzer öffnet eine Aufgabe über Dashboard oder Projektseite
2. System lädt AufgabeDetail.razor
3. System ruft GitWorkspaceBrowserService.LoadTaskMetricsAsync() auf
4. Service führt aus:
   - \git rev-list --count HEAD\ → CommitAnzahl
   - \git status --porcelain\ → Änderungsliste
5. System zeigt Kennzahlen oben auf der Seite an (z.B. "12 Commits | 5 Änderungen")

**Nachbedingung:** Kennzahlen sind sichtbar und aktuell

---

### UC-2: KI-Ausführung mit Auto-Refresh

**Akteur:** KI-Agent (via GitHubCopilotPlugin)  
**Vorbedingung:** Aufgabe ist offen, Explorer ist nicht aktiv

**Ablauf:**
1. KI führt Änderungen im Branch durch (neue/geänderte Dateien, Commits)
2. KI meldet Fertigstellung via Protokoll-Update
3. System erkennt Update und löst `LadeAsyncWithScope()` aus
4. Kennzahlen werden erneut ermittelt und in der UI aktualisiert

**Nachbedingung:** Benutzer sieht aktuelle Commit- und Änderungsanzahl

---

### UC-3: Repository-Explorer öffnen

**Akteur:** Benutzer  
**Vorbedingung:** Aufgabenseite ist offen

**Ablauf:**
1. Benutzer klickt auf "Projektverzeichnis anzeigen"
2. System ändert URL zu \?view=tree\
3. GitWorkspaceBrowserService.LoadSnapshotAsync() wird aufgerufen
4. Baum wird rekursiv aufgebaut (`WorkspaceSnapshot.RootNodes` + `WorkspaceSnapshot.FlatFiles`)
5. Git-Status wird pro Datei ermittelt (Staged/Unstaged)
6. System zeigt Explorer mit Verzeichnisbaum, Sektionen und Statusfarben

**Nachbedingung:** Explorer ist sichtbar, Baum ist vollständig

---

### UC-4: Dateivorschau mit Diff anzeigen

**Akteur:** Benutzer  
**Vorbedingung:** Explorer ist offen, Datei ist geänderte Textdatei < 1 MB

**Ablauf:**
1. Benutzer klickt auf eine Datei (z.B. "src/Components/Home.razor")
2. System prüft Dateityp, Status und Größe
3. Für geänderte Datei:
   - System liest Arbeitsbaum-Version
   - System liest HEAD-Version via \git show HEAD:path\
   - Die Vergleichsansicht wird mit beiden Versionen befüllt
4. System zeigt Diff (Side-by-Side oder Inline, standardmäßig Inline)

**Nachbedingung:** Diff ist sichtbar, Unterschiede sind farblich differenziert

---

### UC-5: Gelöschte Datei anzeigen

**Akteur:** Benutzer  
**Vorbedingung:** Explorer offen, Datei hat Status "Deleted"

**Ablauf:**
1. Benutzer klickt auf gelöschte Datei (🔴 Status)
2. System liest Originalversion aus \git show HEAD:path\
3. System zeigt Originalversion mit Label "Gelöschte Datei – Originalversion"

**Nachbedingung:** Benutzer sieht Originalinhalt

---

### UC-6: Binärdatei oder große Datei anklicken

**Akteur:** Benutzer  
**Vorbedingung:** Explorer offen, Datei ist > 1 MB oder Binärdatei

**Ablauf:**
1. Benutzer klickt auf große/binäre Datei
2. System prüft Dateityp und Größe
3. System zeigt Hinweis mit Download-Link

**Nachbedingung:** Benutzer kann Datei herunterladen

---

### UC-7: Fehler bei fehlender Datei

**Akteur:** System  
**Vorbedingung:** Repositorypfad ist ungültig

**Ablauf:**
1. Benutzer öffnet Aufgabenseite oder Explorer
2. System versucht Git-Befehle auszuführen
3. Git gibt Fehler zurück
4. System zeigt Fehlermeldung: "Repository nicht gefunden. Bitte überprüfen Sie den Pfad oder starten Sie die Aufgabe neu."

**Nachbedingung:** Benutzer wird informiert und kann Maßnahmen ergreifen

---

## 8. Annahmen und Abhängigkeiten

| Typ | Eintrag | Bewertung | Bewältigung |
|---|---|---|---|
| Abhängigkeit | Aufgabe hat gültigen lokalen Repositorypfad | Voraussetzung | ✅ Fehlerbehandlung für ungültige Pfade implementiert |
| Abhängigkeit | Git lokal verfügbar und im PATH | Voraussetzung | Installation ist Aufgabe des Benutzers |
| Abhängigkeit | Lokales Repository ist gültiges Git-Repository | Voraussetzung | ✅ Validierung beim ersten Load (Directory.Exists + git-Rückgabecode) |
| Abhängigkeit | IGitPlugin-Interface ist implementiert | Voraussetzung | ✅ Bereits vorhanden |
| Annahme | Größenlimit 1 MB ist ausreichend konservativ | ✅ Validiert | `MaxInlineBytes = 1_048_576` als Konstante; für HEAD-Reads wird String-Länge (nicht Bytes) geprüft |
| Annahme | Binärdatei-Erkennung via Null-Byte-Heuristik ist ausreichend | ⚠️ Teilvalidiert | Null-Bytes in ersten 8.192 Bytes → `IsBinary = true`. Keine MIME-Erkennung. Falsch-Negativ möglich bei binären Dateien ohne Null-Bytes im Anfangsbereich (selten). |
| Annahme | Query-Parameter sind sicher für View-Toggle | ✅ Implementiert | View-Toggle nutzt `?view=task` / `?view=tree` und wird bei Navigation/Refresh wiederhergestellt |
| Annahme | `--untracked-files=all` ist akzeptabel performant | ⚠️ Risiko | Bei Repos mit Build-Outputs ohne .gitignore-Eintrag kann dies langsam werden |
| Risiko | Repositoryzustand ändert sich während der Betrachtung | Gering | Manuelles Refresh-Button vorhanden |
| Risiko | Sehr viele geänderte Dateien verlangsamen Rendering | Mittel | Kein virtualisiertes Rendering implementiert |

---

## 9. Nächste Schritte

Alle zuvor offenen Punkte sind umgesetzt oder als bewusste Designentscheidung dokumentiert.

- `?view=task` / `?view=tree` steuern die Ansicht inzwischen per Query-Parameter.
- Der Verzeichnisbaum wird rekursiv aufgebaut; die Listenansicht bleibt als kompakte Alternative erhalten.
- Die Explorer-Liste wird virtualisiert; sehr große untracked-Mengen bleiben als Performance-Hinweis dokumentiert.
- Binär-/Großdatei-Hinweise, Lokalisierung und responsive Darstellung sind in der aktuellen Implementierung abgedeckt.

---

## 10. Implementierungsabgleich – finale Fassung

> Dieser Abschnitt wurde auf Basis der tatsächlichen Implementierung konsolidiert.  
> Referenzierte Quellen: `GitWorkspaceBrowserService.cs`, `AufgabeDetail.razor.cs`, `AufgabeDetail.razor`, `WorkspaceSnapshot.cs`, `FilePreview.cs`, `WorkspaceFileStatus.cs`, `WorkspaceFileNode.cs`, `WorkspaceNodeRow.cs`.

### OQ-1 – 1 MB Inline-Schwellenwert

**Status:** ✅ Implementiert und präzisiert

| Aspekt | Detail |
|--------|--------|
| Konstante | `private const long MaxInlineBytes = 1_048_576;` in `GitWorkspaceBrowserService` |
| Working-Tree-Lesen | `FileInfo.Length > MaxInlineBytes` → `FilePreview(IsTooBig: true, FileSizeBytes: info.Length)` |
| HEAD-Lesen (`git show`) | `content.Length > MaxInlineBytes` → Prüfung auf **Zeichenanzahl** (String), nicht Byte-Anzahl |
| Konsequenz | Für reine ASCII/UTF-8-Texte kein Unterschied. Bei UTF-16-ähnlichem Output marginal. Für die Praxis akzeptabel. |
| Testbarkeit | AC-5.1 und AC-5.5 sind mit einer 1.048.577-Byte-Testdatei verifizierbar. |

---

### OQ-2 – Binärdateierkennung

**Status:** ✅ Implementiert; Dokumentation korrigiert

Die bisherigen Docs nannten "MIME-/Content-Heuristik" — das ist **unzutreffend**. Die tatsächliche Implementierung:

```csharp
private static bool IsBinary(byte[] bytes)
{
    var sample = Math.Min(bytes.Length, 8192); // erste 8 KB
    for (int i = 0; i < sample; i++)
        if (bytes[i] == 0) return true;        // Null-Byte → binär
    return false;
}
```

| Aspekt | Detail |
|--------|--------|
| Methode | Null-Byte-Scan der ersten 8.192 Bytes |
| Falsch-Positiv | Sehr selten: binärlose Textdateien mit eingebettetem Null-Byte |
| Falsch-Negativ | Selten: Binärdateien ohne Null-Byte in den ersten 8 KB (z.B. manche Bildformate) |
| Nur Working-Tree | HEAD-Reads (`ReadFromHeadAsync`) prüfen **nicht** auf Binär – `git show` liefert String; bei echten Binärfiles werden Steuerzeichen geliefert |
| Einschränkung | Bei Binärdateien kein Download-Link implementiert, nur "🔒 Binärdatei – Vorschau nicht verfügbar" |

---

### OQ-3 – Refresh nach KI-Ausführung

**Status:** ✅ Implementiert

```
StartKiLauf(onCompleted: fehler => InvokeAsync(async () => {
    ...
    await LadeAsyncWithScope();   // → LadeWorkspaceAsync() → LoadSnapshotAsync()
    StateHasChanged();
}))
```

Auslösepunkte für `LadeWorkspaceAsync()`:
1. `OnInitializedAsync` (Seitenlade)
2. `LadeAsyncWithScope` (nach KI-Abschluss, nach Commit, nach anderen Git-Aktionen)
3. Manueller 🔄-Button direkt in der UI

---

### OQ-4 – Sehr große Repositories

**Status:** ⚠️ Bewusster Trade-off – dokumentiert

| Aspekt | Detail |
|--------|--------|
| Git-Befehl | `git status --porcelain=v1 --untracked-files=all` — listet **alle** untracked Dateien rekursiv |
| Risiko | Repositories mit fehlenden .gitignore-Einträgen für Build-Artefakte können viele Zeilen produzieren |
| Server-seitig | Keine Zeilengrenze oder Paginierung implementiert |
| UI-seitig | `Virtualize` reduziert das DOM-Risiko; die Datenmenge wird dennoch vollständig aus Git ermittelt |
| Empfehlung | Als zukünftige Optimierung weiterhin im Blick behalten, falls Repositories mit sehr vielen untracked Dateien erwartet werden |

---

### OQ-5 – .gitignore-Striktheitsgrad

**Status:** ✅ Implementiert (native Git-Filterung)

Git-native .gitignore-Filterung greift automatisch: ignorierte Dateien erscheinen in `git status --porcelain=v1` mit dem Code `!!`. Der Parser überspringt diese explizit:

```csharp
if (x == '!' && y == '!')
    continue; // Ignorierte Datei – nicht anzeigen
```

Konsequenz: Nur Dateien, die Git als "interested" betrachtet (tracked, geändert, untracked-not-ignored), erscheinen im Explorer.

---

### OQ-6 – Komplexe Git-Status-Details

**Status:** ✅ Hauptfälle implementiert; Edge-Cases dokumentiert

**Implementierte Statuscodes:**

| Porcelain X | Staged-Status | Porcelain Y | Unstaged-Status |
|-------------|--------------|-------------|-----------------|
| `M` | `StagedModified` | `M` | `Modified` |
| `A` | `StagedAdded` | `D` | `Deleted` |
| `D` | `StagedDeleted` | `?` → `??` | `Untracked` |
| `R` | `StagedRenamed` (neuer Pfad) | | |
| `?` → `??` | — (unstaged-only) | | |

**Explizit behandelte Codes:**
- `C` – Copied (staged)
- `T` – Type-change (Datei ↔ Symlink)
- `U` – Unmerged (Merge-Konflikt)

**Rename-Pfad:** Bei `R  old -> new` wird der neue Pfad (`->` Suffix) extrahiert.

**Kombinationen (X != ' ' && Y != ' '):** Beide Zustände werden als separate Einträge in `staged` und `unstaged` eingetragen – z.B. `MM` erzeugt sowohl `StagedModified` als auch `Modified`.

---

### OQ-7 – Query-Parameter-Toggle

**Status:** ✅ Implementiert

Der View-Toggle nutzt `?view=task` bzw. `?view=tree` über `NavigationManager.NavigateTo(..., replace: true)` und `[SupplyParameterFromQuery(Name = "view")]`.
`ApplyViewFromQuery()` setzt `_showExplorer`, `OnParametersSetAsync()` übernimmt Aktualisierungen beim Navigieren.

**Auswirkung:** F5 und direkte Links landen wieder in der zuletzt adressierten Ansicht.

---

## 11. Versionierung

| Version | Datum | Autor | Änderung |
|---------|-------|-------|----------|
| 1.0.0 | 2026-05-18 | planning-orchestrator | Initiale Anforderungsanalyse |
| 2.0.0 | 2026-05-20 | planning-requirements-developer | Konsolidierte & erweiterte Analyse mit allen Akzeptanzkriterien, Use-Cases und Domänenmodell |
| 2.1.0 | 2026-05-27 | planning-requirements-analysis | Implementierungsabgleich: FR/NFR-Status aktualisiert; §10 Implementierungsabgleich mit Klärung aller offenen Fragen hinzugefügt (1-MB-Schwellenwert, Binärerkennung, KI-Refresh, große Repos, .gitignore, Git-Status-Codes, Query-Parameter-Lücke); Akzeptanzkriterien mit ✅/❌-Markierungen präzisiert |
| 2.2.0 | 2026-05-18 | documentation-orchestrator | Finaler Abgleich: Query-Parameter-Toggle, rekursiver Baum, Virtualisierung und responsive Explorer-Darstellung als implementiert dokumentiert; offene Punkte auf bewusste Trade-offs reduziert |
