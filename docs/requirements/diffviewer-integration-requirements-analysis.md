# Anforderungsanalyse – DiffViewer-Integration in AufgabeDetail

> **Dokument-Typ:** Anforderungsanalyse  
> **Projekt:** Softwareschmiede  
> **Speicherort:** `docs/requirements/diffviewer-integration-requirements-analysis.md`  
> **Status:** 📋 Entwurf  
> **Version:** 1.0.0

---

## Inhaltsverzeichnis

1. [Überblick und Projektkontext](#1-überblick-und-projektkontext)
2. [Funktionale Anforderungen](#2-funktionale-anforderungen)
3. [Nicht-funktionale Anforderungen](#3-nicht-funktionale-anforderungen)
4. [Akzeptanzkriterien](#4-akzeptanzkriterien)
5. [Annahmen und Abhängigkeiten](#5-annahmen-und-abhängigkeiten)
6. [Scope und Out-of-Scope](#6-scope-und-out-of-scope)
7. [Domänenmodell und Glossar](#7-domänenmodell-und-glossar)
8. [Nutzungsfälle (Use Cases)](#8-nutzungsfälle-use-cases)
9. [Nächste Schritte](#9-nächste-schritte)
10. [Approval & Versionierung](#10-approval--versionierung)

---

## 1. Überblick und Projektkontext

### 1.1 Projektbeschreibung

Die AufgabeDetail-Seite zeigt aktuell im Repository-Explorer eine einfache Dateivorschau mit zwei `<pre>`-Blöcken (Original/Aktuell).  
Der vorhandene DiffViewer ist bereits technisch implementiert, aber als eigenständige Blazor-Page mit Route eingebunden. Für die Aufgabe-Ansicht soll der Viewer als wiederverwendbare Komponente bereitgestellt und anstelle der bisherigen Textvorschau eingebettet werden.

### 1.2 Geschäftsziele

| # | Ziel | Messbare Erfolgsgröße |
|---|------|-----------------------|
| Z-1 | Bessere Lesbarkeit von Dateiänderungen in Aufgaben | Nutzer sehen statt Rohtext eine strukturierte Diff-Darstellung |
| Z-2 | Reduzierung von Kontextwechseln | Dateiänderungen können direkt in AufgabeDetail geprüft werden |
| Z-3 | Wiederverwendbarkeit der Diff-Darstellung | Dieselbe Komponente kann eingebettet und ggf. weiterhin per Route genutzt werden |
| Z-4 | Konsistente Review-Erfahrung | Datei-Auswahl im Explorer und Diff-Ansicht verwenden dieselbe fachliche Datenbasis |

### 1.3 Stakeholder

| Rolle | Beschreibung | Interesse |
|-------|-------------|-----------|
| **Anwender** | Einzelner Entwickler im lokalen Workflow | Schnelle Prüfung von Änderungen direkt in der Aufgabe |
| **Entwickler** | Wartet und erweitert Blazor-Komponenten | Saubere Komponentenstruktur, geringe Duplikation |
| **QA / Review** | Prüft Diff-Darstellung indirekt über Funktionsflüsse | Vollständige und nachvollziehbare Änderungsanzeige |

### 1.4 Abgrenzung

- Die fachliche Diff-Erzeugung bleibt Bestandteil des bestehenden Diff-Backends.
- Die Anforderung betrifft primär die **UI-Integration** in `AufgabeDetail`.
- Die bestehende Diff-Darstellung soll nicht neu erfunden, sondern **als Komponente extrahiert** werden.
- Ein vollständiges Redesign des Repository-Explorers ist nicht Ziel dieser Analyse.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **DiffViewer als Komponente:** Die bisherige DiffViewer-Page wird zu einer wiederverwendbaren Komponente refaktoriert, die Diff-Daten per Parameter entgegennehmen und ohne eigene Navigation innerhalb anderer Seiten gerendert werden kann. → [Diff-Vergleichskomponente](diff-comparison-component-requirements.md) | UI / Komponenten | MUST HAVE | 📋 Geplant |
| **FR-2** | **Einbettung in AufgabeDetail:** Bei Auswahl einer Datei im Projektverzeichnis wird in der Dateivorschau statt der bisherigen `<pre>`-Original/Aktuell-Anzeige die DiffViewer-Komponente eingeblendet. | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-3** | **Datei-Auswahl bleibt erhalten:** Die Auswahl eines WorkspaceFileNode bleibt sichtbar und synchron zur Diff-Anzeige, sodass Nutzer erkennen, welche Datei aktuell verglichen wird. | UX / Navigation | MUST HAVE | 📋 Geplant |
| **FR-4** | **Unterstützung aller Preview-Zustände:** Nicht-komparierbare Dateien (z. B. binär, zu groß, Verzeichnis, Fehler, gelöscht) werden weiterhin verständlich dargestellt; der DiffViewer zeigt in diesen Fällen eine passende Hint-/Status-Ausgabe statt eines leeren Bereichs. | Datenanzeige | MUST HAVE | 📋 Geplant |
| **FR-5** | **Route-Kompatibilität:** Die bestehende Diff-Route bleibt als optionaler Einstiegspunkt erhalten oder wird als Wrapper auf die Komponente umgesetzt, damit bestehende Navigationspfade nicht brechen. | Rückwärtskompatibilität | HIGH | 📋 Geplant |
| **FR-6** | **Laden des letzten Diffs:** AufgabeDetail verwendet weiterhin `_latestDiffResultId`, um den aktuellsten Diff-Kontext der Aufgabe an die DiffViewer-Komponente bzw. den Wrapper weiterzugeben. | Kern-Feature | HIGH | 📋 Geplant |
| **FR-7** | **Fehler- und Ladezustände:** Beim Laden des DiffViewers werden Ladeindikator und verständliche Fehlermeldungen angezeigt, ohne die gesamte AufgabeDetail-Seite unbrauchbar zu machen. | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **FR-8** | **Keine doppelte Rendering-Logik:** Die bisherige manuelle Gegenüberstellung von Original- und CurrentContent wird aus AufgabeDetail entfernt oder nur noch als Fallback verwendet, nicht mehr als primäre Anzeige. | Wartbarkeit | HIGH | 📋 Geplant |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Wiederverwendbarkeit:** Die DiffViewer-Logik ist als eigenständige Komponente kapselbar und unabhängig von der AufgabeDetail-Seite testbar. | Wartbarkeit | MUST HAVE | 📋 Geplant |
| **NFR-2** | **UI-Reaktionsfähigkeit:** Das Umschalten zwischen Dateien darf die Seite nicht blockieren; Ladeoperationen erfolgen asynchron und ohne sichtbare Hänger. | Performance | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Konsistentes Layout:** Die eingebettete Diff-Ansicht fügt sich in das bestehende Blazor-Designsystem ein und verursacht keine Layoutbrüche im Repository-Explorer. | UX / Accessibility | HIGH | 📋 Geplant |
| **NFR-4** | **Zugänglichkeit:** Status-, Fehler- und Ladehinweise sind verständlich und semantisch nutzbar (z. B. für Screenreader). | UX / Accessibility | HIGH | 📋 Geplant |
| **NFR-5** | **Rückwärtskompatibilität:** Bestehende Deep-Links oder Aufrufe der Diff-Route funktionieren weiterhin, sofern sie im Produkt genutzt werden. | Zuverlässigkeit | HIGH | 📋 Geplant |
| **NFR-6** | **Testbarkeit:** Die Komponente muss so strukturiert sein, dass Unit-/Component-Tests für Auswahl, Fehlerzustände und Diff-Rendering möglich sind. | Wartbarkeit | MUST HAVE | 📋 Geplant |
| **NFR-7** | **Datenintegrität:** Die Darstellung verwendet ausschließlich vorhandene DiffResult-, DiffBlock- und DiffLine-Daten; keine clientseitige Neuberechnung der Diff-Logik. | Zuverlässigkeit | MUST HAVE | 📋 Geplant |

---

## 4. Akzeptanzkriterien

### US-1: Diff direkt in AufgabeDetail anzeigen

**Als** Anwender  
**möchte ich** beim Auswählen einer Datei im Projektverzeichnis direkt den Diff sehen,  
**damit** ich Änderungen ohne Seitenwechsel prüfen kann.

| # | Akzeptanzkriterium | Messung |
|---|--------------------|---------|
| AC-1.1 | Beim Klick auf eine Datei erscheint die DiffViewer-Komponente in der Vorschaufläche. | Sichtprüfung in AufgabeDetail |
| AC-1.2 | Die bisherige Zwei-`<pre>`-Darstellung ist nicht mehr die primäre Ansicht. | UI-Inspektion |
| AC-1.3 | Der DiffViewer zeigt die zur Datei gehörigen Änderungsinformationen oder einen verständlichen Hinweis, falls kein Diff verfügbar ist. | Funktionsprüfung |

### US-2: Diff-Komponente wiederverwenden

**Als** Entwickler  
**möchte ich** den DiffViewer als Komponente nutzen,  
**damit** dieselbe Logik eingebettet und optional als Seite verwendet werden kann.

| # | Akzeptanzkriterium | Messung |
|---|--------------------|---------|
| AC-2.1 | Die DiffViewer-UI kann ohne eigene Navigation in AufgabeDetail gerendert werden. | Technische Prüfung |
| AC-2.2 | Ein bestehender Route-Einstieg bleibt funktionsfähig oder wird durch einen Wrapper ersetzt. | Navigations-Test |
| AC-2.3 | Keine doppelte Rendering-Logik für denselben Diff existiert in AufgabeDetail und DiffViewer. | Code-/Komponentensicht |

### US-3: Fehlerfrei mit Sonderfällen umgehen

**Als** Anwender  
**möchte ich** bei nicht darstellbaren Dateien eine klare Rückmeldung erhalten,  
**damit** ich weiß, warum kein Diff angezeigt wird.

| # | Akzeptanzkriterium | Messung |
|---|--------------------|---------|
| AC-3.1 | Binärdateien zeigen einen verständlichen Hinweis. | UI-Text |
| AC-3.2 | Zu große Dateien zeigen einen verständlichen Hinweis. | UI-Text |
| AC-3.3 | Fehler beim Laden der Diff-Daten führen zu einer sichtbaren Fehlermeldung, nicht zu einem Seitenabbruch. | Fehlerfall-Test |

---

## 5. Annahmen und Abhängigkeiten

| Annahme/Abhängigkeit | Beschreibung | Impact |
|----------------------|--------------|--------|
| **DiffResult-Daten vorhanden** | Für die ausgewählte Aufgabe existiert ein gültiger `_latestDiffResultId` oder ein anderer Kontext zur Diff-Auflösung. | HIGH |
| **DiffService stabil** | `DiffService.GetDiffAsync(...)` liefert die benötigten Diff-Daten inklusive DiffBlocks und DiffLines. | HIGH |
| **AufgabeDetail steuert Auswahl** | Die Datei-Auswahl im Explorer bleibt in AufgabeDetail die zentrale Interaktion für den Preview-Bereich. | MEDIUM |
| **Komponentenstruktur anpassbar** | Die bestehende DiffViewer-Page und Unterkomponenten können ohne fachliche Änderungen in eine Komponente umgebaut werden. | HIGH |
| **Kein Schemawechsel nötig** | Für die UI-Integration sind keine Datenbankschemaänderungen erforderlich. | LOW |

---

## 6. Scope und Out-of-Scope

### In-Scope ✅
- ✅ DiffViewer von Page zu Komponente refaktorieren
- ✅ Einbettung der Diff-Ansicht in AufgabeDetail
- ✅ Ersetzen der bisherigen Original/Aktuell-`<pre>`-Anzeige
- ✅ Beibehaltung verständlicher Lade-/Fehler-/Hinweiszustände
- ✅ Route-Kompatibilität als Wrapper oder gleichwertiger Einstieg
- ✅ Abdeckung der Sonderfälle (binär, zu groß, gelöscht, kein Diff)

### Out-of-Scope ❌
- ❌ Neuer Diff-Algorithmus
- ❌ Neue Persistenz- oder Migrationslogik
- ❌ Exportfunktionen oder zusätzliche Diff-Formate
- ❌ Vollständiges Redesign des Repository-Explorers
- ❌ Syntax-Highlighting, virtuelle Listen oder weitere Diff-Features

---

## 7. Domänenmodell und Glossar

### Domänenobjekte

| Objekt | Beschreibung |
|--------|--------------|
| **Aufgabe** | Fachlicher Kontext der Seite `AufgabeDetail`; liefert den zugehörigen Workflow und die letzte Diff-Referenz |
| **WorkspaceFileNode** | Knoten im Repository-Explorer; repräsentiert Datei oder Verzeichnis inkl. Status |
| **FilePreview** | Vorschauobjekt mit OriginalContent, CurrentContent, Hint und Zustandsflags |
| **DiffResult** | Persistiertes Diff-Ergebnis als Container für Blocks und Lines |
| **DiffViewer** | Wiederverwendbare UI-Komponente zur Darstellung eines DiffResult |
| **DiffHeader / DiffToolbar / DiffContent / DiffLine / DiffFooter** | Unterkomponenten der Diff-Darstellung |

### Glossar

| Begriff | Definition |
|---------|-----------|
| **DiffViewer** | UI-Komponente zur Anzeige eines Diffs zwischen zwei Dateiversionen |
| **Einbettung** | Rendern einer Komponente innerhalb einer anderen Seite statt Navigation auf eine eigene Route |
| **Dateivorschau** | Bereich in AufgabeDetail, der die gewählte Datei anzeigt |
| **Fallback-Anzeige** | Ersatzdarstellung bei fehlenden oder nicht vergleichbaren Daten |

---

## 8. Nutzungsfälle (Use Cases)

### UC-1: Datei im Projektverzeichnis auswählen
**Akteur:** Anwender  
**Vorbedingung:** AufgabeDetail ist geöffnet, Repository-Explorer ist geladen

**Ablauf:**
1. Anwender öffnet eine Aufgabe.
2. Anwender wechselt in das Projektverzeichnis.
3. Anwender klickt auf eine Datei.
4. Das System lädt die passende Diff-/Preview-Darstellung.
5. Die DiffViewer-Komponente erscheint in der Vorschaufläche.

**Nachbedingung:** Dateiänderung ist direkt in AufgabeDetail sichtbar.

### UC-2: Nicht vergleichbare Datei öffnen
**Akteur:** Anwender  
**Vorbedingung:** AufgabeDetail ist geöffnet

**Ablauf:**
1. Anwender wählt eine binäre, zu große oder gelöschte Datei aus.
2. Das System prüft die Preview-Metadaten.
3. Das System zeigt einen Hinweis statt einer Diff-Darstellung.

**Nachbedingung:** Anwender versteht den Grund für die reduzierte Darstellung.

### UC-3: Diff-Ansicht weiter über Route nutzen
**Akteur:** Anwender / Entwickler  
**Vorbedingung:** Ein gültiger DiffResult-Kontext existiert

**Ablauf:**
1. Nutzer öffnet einen vorhandenen Diff-Einstieg.
2. Der Route-Einstieg lädt die gleiche Komponente.
3. Die Darstellung entspricht der eingebetteten Version.

**Nachbedingung:** Beide Einstiegspunkte zeigen konsistent denselben Diff-Inhalt.

---

## 9. Nächste Schritte

1. Komponenten-Schnittstelle für DiffViewer festlegen
2. AufgabeDetail-Layout auf eingebettete Diff-Komponente umstellen
3. Route-Wrapper für Rückwärtskompatibilität definieren
4. Tests für Datei-Auswahl, Sonderfälle und Diff-Darstellung ergänzen
5. UI-Abnahme mit realen DiffResult-Daten durchführen

---

## 10. Approval & Versionierung

| Version | Datum | Änderung | Status |
|---------|-------|----------|--------|
| 1.0.0 | 2026-05-23 | Erstfassung der Anforderungsanalyse | 📋 Entwurf |

**Freigabe:** offen  
**Erstellt für:** DiffViewer-Integration in `AufgabeDetail`

