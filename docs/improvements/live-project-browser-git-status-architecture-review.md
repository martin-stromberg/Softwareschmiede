# Architecture-Review – Live Project Browser mit Git-Status

> **Dokument-Typ:** Architecture Review  
> **Status:** ✅ Auflagen umgesetzt  
> **Version:** 1.1.0  
> **Datum:** 2026-05-18

---

## 1. Referenzen

- [Requirements Analysis](../requirements/live-project-browser-git-status-requirements-analysis.md)
- [Architecture Blueprint](../architecture/live-project-browser-git-status-architecture-blueprint.md)
- [ERM](../architecture/live-project-browser-git-status-entity-relationship-model.md)

---

## 2. Fazit

Der Ansatz ist fachlich stimmig: Die Anzeige bleibt lokal, derivativ und ohne Persistenzaufwand. Die Trennung aus Aufgabenseite, Repository-Service und Vergleichsansicht ist wartbar und passt zur bestehenden Architektur.

**Freigabeentscheidung:** ✅ **Go bestätigt**

---

## 3. Priorisierte Findings

| ID | Priorität | Finding | Empfehlung |
|---|---|---|---|
| AR-01 | MAJOR | Die Statusableitung muss Staged- und Unstaged-Zustände deterministisch unterscheiden. | Statuscodes testgetrieben aus `git status --porcelain` ableiten und Sonderfälle (deleted, untracked) explizit absichern. |
| AR-02 | MAJOR | Die ursprüngliche Version gelöschter Dateien darf nicht aus dem Arbeitsverzeichnis gelesen werden. | Für deleted files konsequent `git show HEAD:path` verwenden. |
| AR-03 | MAJOR | Große und binäre Dateien dürfen die UI nicht blockieren. | Inline-Vorschau strikt begrenzen und Download/Hint als Fallback festlegen. |
| AR-04 | MINOR | Der View-Toggle per Query-Parameter muss mit Refreshs und Navigation kompatibel bleiben. | View-State zentralisieren und bei Reload den selben Kontext rekonstruieren. |
| AR-05 | MINOR | Diff-Whitespace-Option kann bei komplexen Dateien zu Missverständnissen führen. | Default ausblenden, aber klar kennzeichnen und testseitig abdecken. |

---

## 4. Risiken und Trade-offs

### Risiken

1. Repositoryzustand kann sich während der Betrachtung ändern.
2. Staged/unstaged Mischfälle sind fehleranfällig, wenn Statuscodes nicht sauber normalisiert werden.
3. Sehr große oder binäre Dateien können die Nutzererfahrung verschlechtern.

### Trade-offs

| Entscheidung | Vorteil | Nachteil |
|---|---|---|
| Keine Persistenz | Kein Schemaaufwand | Kein historischer Verlauf |
| Query-Parameter für View | Kontext bleibt erhalten | Mehr UI-Zustandslogik |
| Inline-Diff + Download-Fallback | Gute Lesbarkeit für Text | Zusätzliche Komplexität in der Vorschauklassifikation |

---

## 5. Umsetzung der Auflagen

1. Statuszuordnung ist mit expliziten Tests für staged/unstaged/deleted/untracked abgesichert.
2. File-Preview-Grenzen für Größe und Binärdateien sind verbindlich festgelegt.
3. Separate Pfade für Arbeitskopie und Ursprungsversion sind in der Service-Schicht implementiert.
4. Refresh-Trigger nach KI-Ausführung und Protokollaktualisierung sind nachweisbar.

---

## 6. Ergebnis

Alle priorisierten Findings wurden in der Implementierung berücksichtigt. Verbleibende Trade-offs sind dokumentiert und bewusst akzeptiert.

---

## 7. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-18 | review-architecture | Initiales Architektur-Review für Live Project Browser mit Git-Status |
| 1.1.0 | 2026-05-18 | documentation-orchestrator | Review nach Implementierung geschlossen; Auflagen und Trade-offs dokumentiert |
