# Anforderungsanalyse – AufgabeDetail: Projektanzeige unter dem Titel

> **Dokument-Typ:** Requirements Analysis  
> **Status:** ✅ Umgesetzt  
> **Version:** 1.0.0  
> **Thema:** In der Aufgabendetailseite wird unter dem Titel der reine Text `Projekt: <Name>` angezeigt.

---

## 1. Überblick

Die Detailseite einer Aufgabe zeigt jetzt direkt unter dem Titel den Projektbezug als Klartext.  
Damit ist ohne Zusatznavigation sofort sichtbar, zu welchem Projekt die Aufgabe gehört.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Priorität | Status |
|---|---|---|---|
| FR-1 | Unterhalb des Aufgabentitels wird `Projekt: <Name>` angezeigt. | MUST HAVE | ✅ Umgesetzt |
| FR-2 | Angezeigt wird ausschließlich `Project.Name`. | MUST HAVE | ✅ Umgesetzt |
| FR-3 | Ist kein Projektname vorhanden (null/leer/Whitespace), wird `Projekt: ohne projekt` angezeigt. | MUST HAVE | ✅ Umgesetzt |
| FR-4 | Die Anzeige ist für alle Anwender sichtbar (kein rollenbasiertes Gate). | MUST HAVE | ✅ Umgesetzt |
| FR-5 | Die Ausgabe erfolgt als reiner Text (kein HTML-Rendering von Projektnamen). | MUST HAVE | ✅ Umgesetzt |

---

## 3. Akzeptanzkriterien

- AC-1: In AufgabeDetail steht direkt unter `h2` ein Text `Projekt: ...`.
- AC-2: Bei Projektname `Alpha` erscheint exakt `Projekt: Alpha`.
- AC-3: Bei leerem/Whitespace-Projektnamen erscheint `Projekt: ohne projekt`.
- AC-4: HTML im Projektnamen wird escaped dargestellt.

---

## 4. Umsetzung und Nachweis

- UI: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- ViewModel/Code-Behind: `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- Datenbereitstellung (Projekt eager geladen): `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- Testergänzungen: `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionsBunitTests.cs`

---

## 5. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-24 | documentation-orchestrator | Erstfassung für umgesetzte Projektanzeige inkl. Testnachweis |

