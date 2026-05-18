# Anforderungsanalyse – Live Project Browser mit Git-Status

> **Dokument-Typ:** Requirements Analysis  
> **Status:** ✅ Implementiert und final abgeglichen  
> **Version:** 1.2.0  
> **Datum:** 2026-05-18 | Aktualisiert: 2026-05-18

---

## 1. Überblick und Projektkontext

Die Aufgabenseite soll den aktuellen Git-Status des verknüpften lokalen Repositories sichtbar machen. Zusätzlich wird eine eigene Explorer-Ansicht bereitgestellt, in der der Projektbaum, Git-Status, Dateivorschau und Diff-Vergleich direkt aus dem lokalen Arbeitsverzeichnis abgeleitet werden.

### 1.1 Zielbild

- Schnell sichtbare Kennzahlen auf der Aufgabenseite
- Umschaltbare Explorer-Ansicht für das Repository
- Visuelle Unterscheidung von staged und unstaged Änderungen
- Dateivorschau für Textdateien sowie Diff für geänderte Inhalte

### 1.2 Referenzen

- Ausgangsanforderung: [../../27c6f791-7ae3-4799-b591-ff2dd9d65a6c.copilot-task.md](../../27c6f791-7ae3-4799-b591-ff2dd9d65a6c.copilot-task.md)
- Aufgabenseite: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- Git-Orchestrierung: `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- Git-Plugin-Vertrag: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- Lokales Repository-Plugin: `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Priorität | Status |
|---|---|---|---|
| **FR-1** | Die Aufgabenseite zeigt die Anzahl der Commits im aktuellen Branch an. | MUST HAVE | ✅ Umgesetzt |
| **FR-2** | Die Aufgabenseite zeigt die Anzahl lokaler Dateiänderungen an (staged + unstaged). | MUST HAVE | ✅ Umgesetzt |
| **FR-3** | Beim Laden der Aufgabenseite werden beide Kennzahlen automatisch aktualisiert. | MUST HAVE | ✅ Umgesetzt |
| **FR-4** | Nach KI-Ausführung bzw. Protokoll-Update werden beide Kennzahlen erneut geladen. | MUST HAVE | ✅ Umgesetzt |
| **FR-5** | Ein Aktionsbutton wechselt in eine Explorer-Ansicht für das Repository. | MUST HAVE | ✅ Umgesetzt |
| **FR-6** | In der Explorer-Ansicht bleibt die Aufgabenseite über `?view=task` bzw. `?view=tree` kontextstabil; der Toggle ist per Query-Parameter und Reload wiederherstellbar. | MUST HAVE | ✅ Umgesetzt |
| **FR-7** | Der Explorer zeigt git-geänderte Dateien (staged + unstaged) als zweispaltige Ansicht mit rekursivem Baum und Listenansicht. | MUST HAVE | ✅ Umgesetzt |
| **FR-8** | .gitignore-Filterung über native Git-Porcelain-Codes; Ordner-Aufklappen im Baum und flache Listenansicht sind implementiert. | MUST HAVE | ✅ Umgesetzt |
| **FR-9** | Git-Änderungen werden in staged und unstaged Änderungen getrennt dargestellt. | MUST HAVE | ✅ Umgesetzt |
| **FR-10** | Jede Datei zeigt Status-Badge und Farbcode gemäß Git-Status (7 Varianten). | MUST HAVE | ✅ Umgesetzt |
| **FR-11** | Gelöschte Dateien werden innerhalb einer Ebene als letzte Einträge dargestellt. | MUST HAVE | ✅ Umgesetzt |
| **FR-12** | Klick auf eine Datei zeigt Inhalt oder Diff-Vorschau abhängig von Dateityp, Status und Größe. | MUST HAVE | ✅ Umgesetzt |
| **FR-13** | Für geänderte, hinzugefügte und gelöschte Dateien steht eine Vergleichsansicht mit Side-by-Side + Inline bereit. | MUST HAVE | ✅ Umgesetzt |
| **FR-14** | Dateien > 1 MB oder Binärdateien (Null-Byte-Heuristik) werden nicht inline gerendert, sondern mit Hinweis behandelt. | MUST HAVE | ✅ Umgesetzt |
| **FR-15** | Fehlerzustände (Repository fehlt, Git-Fehler, Datei unlesbar) werden nachvollziehbar angezeigt. | MUST HAVE | ✅ Umgesetzt |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Priorität | Status |
|---|---|---|---|
| **NFR-1** | Die Anzeige ist vollständig aus dem lokalen Repositoryzustand ableitbar; keine zusätzliche Persistenz ist erforderlich. | MUST HAVE | ✅ Umgesetzt |
| **NFR-2** | Statusermittlung und Dateivorschau bleiben auch bei größeren Repositories performant; die Listenansicht wird virtualisiert. | HIGH | ✅ Implementiert |
| **NFR-3** | UI- und Statusmeldungen bleiben in der bestehenden Lokalisierungsstrategie konsistent. | MUST HAVE | ✅ Implementiert |
| **NFR-4** | Binärdaten und große Dateien werden defensiv behandelt, ohne die Seite zu blockieren. | MUST HAVE | ✅ Umgesetzt |
| **NFR-5** | Logs enthalten keine sensiblen Dateiinhalte oder vollständigen Tokens. | MUST HAVE | ✅ Umgesetzt |

---

## 4. Akzeptanzkriterien

- **AC-1:** Die Aufgabenseite zeigt Commit-Anzahl und lokale Änderungsanzahl an.
- **AC-2:** Die Kennzahlen aktualisieren sich beim Laden und nach relevanten KI-/Protokoll-Updates.
- **AC-3:** Ein Toggle wechselt in eine Explorer-Ansicht für das Repository.
- **AC-4:** Der Explorer zeigt Ordner und Dateien inklusive Git-Status farblich differenziert.
- **AC-5:** Staged und unstaged Änderungen sind getrennt dargestellt.
- **AC-6:** Klick auf eine Textdatei < 1 MB zeigt Inhalt oder Diff.
- **AC-7:** Klick auf große oder binäre Dateien zeigt Hinweis plus Download-Option.
- **AC-8:** Klick auf gelöschte Dateien zeigt die Ursprungsversion aus dem Git-Stand.
- **AC-9:** Die Vergleichsansicht unterstützt Side-by-Side und Inline-Ansicht.

---

## 5. Scope und Out-of-Scope

### In-Scope

- Kennzahlen auf der Aufgabenseite
- Repository-Explorer in derselben Route
- Dateiinhalt, Datei-Hinweise und Diff-Anzeige
- Statusdarstellung für staged/unstaged Änderungen

### Out-of-Scope

- Commit-, Push- oder Pull-Aktionen im Explorer
- Merge-Konflikt-Lösung
- Historische Branch-Vergleiche
- Persistente Speicherung von Browserzuständen

---

## 6. Domänenmodell und Glossar

- **RepositoryContext:** Aufgabe mit lokalem Klonpfad und Branch-Kontext.
- **WorkspaceSnapshot:** Zur Laufzeit ermittelter Zustand von Dateien, Ordnern und Git-Status.
- **FileStatusEntry:** Eine Datei mit Status wie modified, added, deleted oder untracked.
- **FilePreview:** Inhalt oder Hinweistext für die aktuell ausgewählte Datei.
- **DiffView:** Darstellung der Unterschiede zwischen Original- und Arbeitsversion.

---

## 7. Nutzungsfälle

### UC-1: Aufgabenseite öffnen
1. Benutzer öffnet eine Aufgabe.
2. System lädt Commit-Anzahl und lokale Änderungsanzahl.
3. Kennzahlen werden oberhalb der Aktionsfläche angezeigt.

### UC-2: Repository-Explorer öffnen
1. Benutzer klickt auf „Projektverzeichnis anzeigen“.
2. System wechselt in die Explorer-Ansicht.
3. Verzeichnisbaum und Statussektionen werden geladen.

### UC-3: Datei selektieren
1. Benutzer klickt auf einen Datei-Eintrag.
2. System prüft Typ, Status und Größe.
3. System zeigt Inhalt, Hinweis oder Diff.

---

## 8. Annahmen und Abhängigkeiten

| Typ | Eintrag | Bewertung |
|---|---|---|
| Abhängigkeit | Die Aufgabe ist mit einem lokalen Repositorypfad verknüpft. | Voraussetzung |
| Abhängigkeit | Git ist lokal verfügbar und für das Repository nutzbar. | Voraussetzung |
| Annahme | Die Vorschaugröße 1 MB ist für Inline-Anzeige ausreichend konservativ. | ✅ Validiert – `MaxInlineBytes = 1_048_576` implementiert |
| Annahme | Binärdateierkennung per Null-Byte-Scan (erste 8 KB) ist ausreichend. | ✅ Implementiert – keine MIME-Erkennung |
| Annahme | Die Explorer-Ansicht bleibt innerhalb derselben Route steuerbar. | ✅ In-Memory-Toggle umgesetzt |
| Annahme | URL-Query-Parameter (`?view=tree`) werden für den View-Toggle genutzt. | ✅ Implementiert – FR-6 abgeschlossen |

---

## 9. Ergebnis

- FR-6, FR-7 und FR-8 sind in der aktuellen UI abgebildet.
- NFR-2 und NFR-3 sind durch die implementierte Service-/UI-Logik abgedeckt.
- Verbleibende Trade-offs betreffen ausschließlich sehr große Repositories mit vielen untracked Dateien.

---

## 10. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-18 | planning-orchestrator | Initiale Anforderungsanalyse für Live Project Browser mit Git-Status |
| 1.1.0 | 2026-05-27 | planning-requirements-analysis | Implementierungsabgleich: FR/NFR-Statusfelder aktualisiert, Annahmen validiert, Nächste Schritte auf offene Gaps fokussiert |
| 1.2.0 | 2026-05-18 | documentation-orchestrator | Finaler Abgleich: View-Toggle, rekursiver Baum, Virtualisierung und Lokalisierung als abgeschlossen dokumentiert |
