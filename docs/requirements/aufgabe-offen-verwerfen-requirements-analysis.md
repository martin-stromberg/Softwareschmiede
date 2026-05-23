# Anforderungsanalyse – Offene Aufgabe archivieren oder löschen (Verwerfen-Funktion)

> **Dokument-Typ:** Requirements Analysis  
> **Status:** 📋 Geplant  
> **Version:** 1.0.0  
> **Datum:** 2026-05-23  
> **Thema:** Eine Aufgabe im Status `Offen` soll direkt archiviert oder gelöscht werden können, ohne sie vorher starten zu müssen – als bewusste „Verwerfen"-Aktion mit expliziter Bestätigung.

---

## ⚠️ Widerspruchsanalyse – Offene Entscheidung

Die Feature-Anfrage enthält zwei direkt widersprüchliche Aussagen:

| # | Aussage | Interpretation |
|---|---------|----------------|
| **A** | *„Eine Aufgabe mit Status `Offen` darf nicht archiviert oder gelöscht werden, ohne zuvor gestartet worden zu sein."* | Archivieren/Löschen soll für `Offen`-Aufgaben **gesperrt** sein. |
| **B** | *„Es soll auch möglich sein, eine offene Aufgabe direkt zu archivieren oder zu löschen, ohne sie vorher zu starten."* | Archivieren/Löschen soll für `Offen`-Aufgaben **erlaubt** sein. |

**Gewählte Interpretation / Entscheidung:**

> Die Aussagen werden als Beschreibung von **zwei unterschiedlichen Pfaden** verstanden, nicht als sich ausschließende Regeln:
>
> - **Pfad 1 – Workflow-Schutz:** Der reguläre `Archivieren`-Button und der reguläre `Löschen`-Button bleiben für Aufgaben im Status `Offen` **nicht erreichbar** (wie bisher). Damit wird verhindert, dass neue Aufgaben versehentlich über denselben Weg entsorgt werden wie abgeschlossene oder fehlgeschlagene.
>
> - **Pfad 2 – Explizites Verwerfen:** Eine **neue, dedizierte „Verwerfen"-Aktion** ermöglicht es, eine `Offen`-Aufgabe direkt zu archivieren **oder** dauerhaft zu löschen. Diese Aktion erfordert einen eigenständigen Bestätigungsdialog, der explizit darauf hinweist, dass die Aufgabe nie gestartet wurde.
>
> **Sollte die fachliche Absicht eine andere sein** (z. B. dass Aussage A gänzlich entfällt und offene Aufgaben über den normalen Archivieren/Löschen-Weg erreichbar sein sollen), ist dies als **offene Entscheidung** durch den Product Owner vor der Implementierung zu klären und diese Anforderungsanalyse entsprechend anzupassen.

---

## 1. Überblick und Projektkontext

Aufgaben entstehen initial im Status `Offen`. Der bestehende Entwicklungslebenszyklus sieht vor, dass eine Aufgabe zunächst *gestartet* wird (`Offen → InBearbeitung`), bevor sie den Weg zu `Abgeschlossen`, `Fehlgeschlagen` und schließlich `Archiviert` nehmen kann. Erst in diesen Endstatus sind `Archivieren` und `Löschen` in der UI erreichbar.

**Problem:** Aufgaben, die nie begonnen werden (z. B. weil sie obsolet wurden, doppelt angelegt wurden oder sich die Anforderung erledigt hat), können derzeit nicht ohne Umweg entfernt werden. Das Starten nur zum Zweck des Verwerfens wäre ein fachlicher Missbrauch des Entwicklungsprozesses.

**Lösung:** Eine neue **„Verwerfen"**-Aktion erlaubt es, eine `Offen`-Aufgabe direkt zu archivieren oder dauerhaft zu löschen. Die Aktion ist im Statusmodell als eigenständiger Kurzschluss-Pfad (`Offen → Archiviert` bzw. `Offen → gelöscht`) definiert und erfordert eine dedizierte Nutzerbestätigung.

**Ist-Zustand im Code:**
- `AufgabeService.ArchivierenAsync` enthält einen expliziten Guard: nur `Abgeschlossen` oder `Fehlgeschlagen` sind erlaubt.
- `AufgabeService.DeleteAsync` besitzt keinen Status-Guard, ist jedoch in der UI nur für `Abgeschlossen`, `Fehlgeschlagen` und `Archiviert` erreichbar (`AufgabeDetail.razor`).
- Für `Offen`-Aufgaben werden in der aktuellen UI weder Archivieren- noch Löschen-Buttons angezeigt.

**Kein Datenmodell-Change erforderlich:** Der Status `Archiviert` ist bereits vorhanden. Die Entität `Aufgabe` braucht keine neuen Felder.

**Stakeholder:** Anwender, Product Owner, Entwicklung, QA

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Verwerfen-Aktion für `Offen`-Aufgaben:** In `AufgabeDetail` wird für Aufgaben im Status `Offen` eine eigenständige Aktion **„Verwerfen"** angeboten. Diese Aktion ist separat von den bestehenden `Archivieren`- und `Löschen`-Aktionen und nur bei `Offen` sichtbar. | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-1.1** | **Verwerfen – Variante Archivieren:** Die Verwerfen-Aktion ermöglicht es, die Aufgabe in den Status `Archiviert` zu überführen (`Offen → Archiviert`). | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-1.2** | **Verwerfen – Variante Löschen:** Die Verwerfen-Aktion ermöglicht es, die Aufgabe dauerhaft zu löschen (`Offen → gelöscht`). | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-2** | **Pflicht-Bestätigungsdialog:** Vor dem Ausführen der Verwerfen-Aktion wird ein dedizierter Bestätigungsdialog gezeigt. Der Dialog macht explizit deutlich, dass die Aufgabe **nie gestartet** wurde, und fragt, ob archiviert oder gelöscht werden soll. | Sicherheit / Bedienung | MUST HAVE | 📋 Geplant |
| **FR-3** | **Service-Methode `VerwerfenAsync`:** `AufgabeService` erhält eine neue Methode `VerwerfenAsync(Guid id, VerwerfenAktion aktion)`, die ausschließlich für Status `Offen` zugelassen ist und je nach Aktion archiviert oder löscht. | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-3.1** | **Status-Guard in `VerwerfenAsync`:** Die Methode wirft `InvalidOperationException`, wenn die Aufgabe nicht im Status `Offen` ist. | Datenkonsistenz | MUST HAVE | 📋 Geplant |
| **FR-4** | **Bestehende Archivieren/Löschen-Pfade bleiben unverändert:** Die regulären Aktionen `Archivieren` und `Löschen` (für `Abgeschlossen`, `Fehlgeschlagen`, `Archiviert`) behalten ihre bisherige Logik und Sichtbarkeit ohne Modifikation. | Stabilität | MUST HAVE | 📋 Geplant |
| **FR-5** | **Weiterleitung nach Verwerfen:** Nach erfolgreichem Verwerfen (sowohl Archivieren als auch Löschen) wird der Anwender zur Projektübersicht weitergeleitet. | UX / Accessibility | HIGH | 📋 Geplant |
| **FR-6** | **Fehlermeldung bei Race Condition:** Wird `VerwerfenAsync` aufgerufen und die Aufgabe wurde zwischenzeitlich gestartet, erhält der Nutzer eine verständliche Fehlermeldung; kein stiller Datenverlust. | Datenkonsistenz | MUST HAVE | 📋 Geplant |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Kein Datenmodell-Change:** Für dieses Feature sind keine neuen Tabellen, Spalten, Migrationen oder ERM-Änderungen erforderlich. Der Status `Archiviert` und die Lösch-Infrastruktur existieren bereits. | Wartbarkeit | MUST HAVE | 📋 Geplant |
| **NFR-2** | **Klare UI-Trennung:** Die „Verwerfen"-Aktion ist visuell und semantisch klar von den bestehenden `Archivieren`- und `Löschen`-Aktionen abgegrenzt (eigene Schaltfläche, eigener Bestätigungsdialog). | UX / Accessibility | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Testbarkeit:** `VerwerfenAsync` und der zugehörige UI-Pfad sind durch Unit- und Integrationstests abzusichern – mindestens Happy Path (`Offen → Archiviert`, `Offen → gelöscht`) und Guard-Prüfung (falsche Status). | Qualität | HIGH | 📋 Geplant |
| **NFR-4** | **Bestehende Tests dürfen nicht brechen:** Änderungen an `AufgabeService` und `AufgabeDetail` dürfen keine bestehenden Tests beeinflussen. | Stabilität | MUST HAVE | 📋 Geplant |
| **NFR-5** | **Logging:** `VerwerfenAsync` schreibt strukturierte Log-Einträge (Aufgabe-ID, gewählte Aktion, Statusübergang) auf `Information`-Level analog zu den bestehenden Service-Methoden. | Observability | HIGH | 📋 Geplant |

---

## 4. Akzeptanzkriterien

### US-1 – Offene Aufgabe verwerfen (Archivieren)

- **AC-1:** Gegeben eine Aufgabe im Status `Offen`, wenn `AufgabeDetail` geöffnet wird, dann ist die Schaltfläche **„Verwerfen"** sichtbar.
- **AC-2:** Die bestehenden `Archivieren`- und `Löschen`-Buttons sind für `Offen`-Aufgaben **weiterhin nicht sichtbar**.
- **AC-3:** Ein Klick auf „Verwerfen" öffnet einen Bestätigungsdialog mit dem Hinweis, dass die Aufgabe nie gestartet wurde.
- **AC-4:** Im Dialog kann der Nutzer zwischen **„Archivieren"** und **„Dauerhaft löschen"** wählen (oder der Dialog bietet beide als separate Buttons an – Implementierungsentscheid).
- **AC-5:** Nach Bestätigung von „Archivieren" hat die Aufgabe den Status `Archiviert` und der Nutzer wird zur Projektübersicht weitergeleitet.

### US-2 – Offene Aufgabe verwerfen (Dauerhaft löschen)

- **AC-6:** Nach Bestätigung von „Dauerhaft löschen" ist die Aufgabe nicht mehr in der Datenbank vorhanden und der Nutzer wird zur Projektübersicht weitergeleitet.
- **AC-7:** Die Löschoperation aus dem Verwerfen-Pfad nutzt `AufgabeService.DeleteAsync` (kein eigener Löschpfad nötig), nach Statusprüfung durch `VerwerfenAsync`.

### US-3 – Guard: Verwerfen nicht möglich, wenn Status nicht `Offen`

- **AC-8:** `VerwerfenAsync` wirft `InvalidOperationException`, wenn die Aufgabe nicht im Status `Offen` ist.
- **AC-9:** Wird die Aufgabe zwischen dem Öffnen der Detailseite und der Bestätigung gestartet (Race Condition), erhält der Nutzer eine Fehlermeldung; es findet kein versehentliches Archivieren/Löschen statt.

### US-4 – Bestehende Pfade unberührt

- **AC-10:** Aufgaben im Status `Abgeschlossen`, `Fehlgeschlagen` und `Archiviert` zeigen weiterhin die bisherigen `Archivieren`- und `Löschen`-Buttons und der Verwerfen-Button ist für diese Status **nicht sichtbar**.
- **AC-11:** `AufgabeService.ArchivierenAsync` akzeptiert weiterhin nur `Abgeschlossen` und `Fehlgeschlagen`; an seinem Guard wird nichts geändert.

---

## 5. Annahmen und Abhängigkeiten

| Typ | Eintrag | Auswirkung |
|-----|---------|------------|
| Annahme | Der `Archiviert`-Status eignet sich als Endzustand für verworfene `Offen`-Aufgaben; ein separater Status `Verworfen` ist fachlich nicht erforderlich. | Kein neuer Enum-Wert und keine Migration nötig. |
| Annahme | Eine `Offen`-Aufgabe hat noch keinen `BranchName`, `LokalerKlonPfad` oder aktive `AktiveRunId`; Aufräumen dieser Felder ist beim Verwerfen nicht nötig. | Vereinfacht `VerwerfenAsync`; sollte beim Coding per Assertion verifiziert werden. |
| Annahme | Die endgültige Auswahl zwischen „Archivieren" und „Löschen" wird dem Nutzer im Bestätigungsdialog angeboten. Ob beide Optionen in einem Dialog oder via zwei separater Flows präsentiert werden, ist ein UI-Implementierungsdetail. | Kein Einfluss auf die Service-API. |
| Abhängigkeit | `AufgabeDetail.razor` und `AufgabeDetail.razor.cs` steuern Sichtbarkeit und Handler der neuen Schaltfläche. | Änderungen liegen in der UI-Komponente. |
| Abhängigkeit | `AufgabeService` erhält die neue Methode `VerwerfenAsync`. | Schnittstelle bleibt additive Erweiterung; bestehende Methoden unberührt. |
| Offene Entscheidung | Falls die fachliche Absicht aus Aussage B bedeutet, dass auch der reguläre `Archivieren`-Button für `Offen` zugänglich sein soll (ohne separaten Verwerfen-Pfad), muss `AufgabeService.ArchivierenAsync` angepasst und die UI-Bedingung erweitert werden. → **Product Owner klären.** | Höherer Scope; andere Guard-Logik. |

---

## 6. Scope und Out-of-Scope

**In-Scope ✅**
- Neue „Verwerfen"-Schaltfläche in `AufgabeDetail` für `Offen`-Aufgaben
- Bestätigungsdialog mit Hinweis auf nicht gestarteten Zustand und Wahl zwischen Archivieren/Löschen
- Neue Service-Methode `AufgabeService.VerwerfenAsync` (Status-Guard für `Offen`)
- Statusübergang `Offen → Archiviert` (Verwerfen-Archivieren)
- Löschpfad `Offen → gelöscht` (Verwerfen-Löschen) unter Nutzung von `DeleteAsync`
- Unit- und Integrationstests für den neuen Pfad
- Strukturiertes Logging im Service

**Out-of-Scope ❌**
- Neue Datenbankmigrationen oder Schemaänderungen
- Neuer `AufgabeStatus`-Wert (z. B. `Verworfen`)
- Änderungen am regulären `Archivieren`- oder `Löschen`-Button für andere Status
- Automatisches Verwerfen von Aufgaben nach Ablauf einer Frist
- Massenverwerfen mehrerer `Offen`-Aufgaben
- Audit-Log / `Protokolleintrag` für den Verwerfen-Vorgang (kann als separate Erweiterung folgen, wird hier nicht gefordert)
- Änderungen an Recovery-, Abbrechen- oder KI-Aktiv-Pfaden

---

## 7. Domänenmodell und Glossar

### Relevante Entitäten (keine Änderungen erforderlich)

```
Aufgabe
├── Id: Guid
├── Status: AufgabeStatus   ← Offen | InBearbeitung | KiAktiv | TestsLaufen | Abgeschlossen | Fehlgeschlagen | Archiviert
├── Titel: string
├── BranchName: string?      ← null bei Offen (Invariante)
├── LokalerKlonPfad: string? ← null bei Offen (Invariante)
└── AktiveRunId: string?     ← null bei Offen (Invariante)
```

### Erweiterter Statusgraph (Änderungen fett markiert)

```
Offen ──► InBearbeitung ──► KiAktiv ──► ...
  │                                       
  │  (neu: Verwerfen-Pfad)               
  ├──► Archiviert   [VerwerfenAsync + Aktion=Archivieren]
  └──► (gelöscht)   [VerwerfenAsync + Aktion=Loeschen → DeleteAsync]
```

### Neues Value Object / Enum

```csharp
// Vorschlag – Implementierungsdetail
public enum VerwerfenAktion
{
    Archivieren,
    Loeschen
}
```

### Glossar

| Begriff | Definition |
|---------|------------|
| **Offen** | Initialer Status einer neu angelegten Aufgabe; noch nicht gestartet. |
| **Verwerfen** | Fachliche Aktion, die eine `Offen`-Aufgabe direkt archiviert oder dauerhaft löscht, ohne den normalen Entwicklungsprozess zu durchlaufen. |
| **Archivieren (regulär)** | Bestehende Aktion für `Abgeschlossen`/`Fehlgeschlagen`-Aufgaben; durch diese Anforderung nicht verändert. |
| **Löschen (regulär)** | Bestehende Aktion ohne Status-Guard im Service, in der UI aber nur für Endstatus erreichbar; durch diese Anforderung nicht verändert. |
| **VerwerfenAsync** | Neue Service-Methode, die den Verwerfen-Pfad für `Offen`-Aufgaben kapselt. |
| **Guard** | Status-Prüfung im Service, die bei unzulässigem Status eine `InvalidOperationException` auslöst. |

---

## 8. Nutzungsfälle (Use Cases)

### UC-1: Verwerfen – Archivieren

| Feld | Inhalt |
|------|--------|
| **Akteur** | Anwender |
| **Vorbedingung** | Aufgabe existiert mit Status `Offen` |
| **Hauptablauf** | 1. Anwender öffnet `AufgabeDetail` einer `Offen`-Aufgabe. 2. Klick auf „Verwerfen". 3. Bestätigungsdialog erscheint. 4. Anwender wählt „Archivieren". 5. `VerwerfenAsync(id, VerwerfenAktion.Archivieren)` setzt Status auf `Archiviert`. 6. Weiterleitung zur Projektübersicht. |
| **Nachbedingung** | Aufgabe hat Status `Archiviert`; ist in Projektübersicht nicht mehr sichtbar (gefiltert wie bisher). |
| **Ausnahmeablauf** | Status nicht mehr `Offen` → Fehlermeldung, kein Statuswechsel. |

### UC-2: Verwerfen – Dauerhaft löschen

| Feld | Inhalt |
|------|--------|
| **Akteur** | Anwender |
| **Vorbedingung** | Aufgabe existiert mit Status `Offen` |
| **Hauptablauf** | 1. Anwender öffnet `AufgabeDetail`. 2. Klick auf „Verwerfen". 3. Bestätigungsdialog erscheint. 4. Anwender wählt „Dauerhaft löschen". 5. `VerwerfenAsync(id, VerwerfenAktion.Loeschen)` prüft Status `Offen`, ruft dann `DeleteAsync` auf. 6. Weiterleitung zur Projektübersicht. |
| **Nachbedingung** | Aufgabe ist aus der Datenbank entfernt. |
| **Ausnahmeablauf** | Status nicht mehr `Offen` → Fehlermeldung, kein Löschen. |

### UC-3: Verwerfen abbrechen

| Feld | Inhalt |
|------|--------|
| **Akteur** | Anwender |
| **Vorbedingung** | Bestätigungsdialog ist geöffnet |
| **Hauptablauf** | Anwender klickt „Zurück" / schließt Dialog. |
| **Nachbedingung** | Aufgabe unverändert, Dialog geschlossen. |

---

## 9. Nächste Schritte

1. **Product Owner bestätigt** die gewählte Interpretation (zwei Pfade: Workflow-Schutz + explizites Verwerfen) oder gibt eine Korrektur vor.
2. Implementierung `AufgabeService.VerwerfenAsync` (inkl. Guard und Logging).
3. Implementierung UI-Änderungen in `AufgabeDetail.razor` / `AufgabeDetail.razor.cs` (neuer Button + `_showVerwerfenConfirm`-State).
4. Unit-Tests für `VerwerfenAsync` (Happy Path Archivieren, Happy Path Löschen, Guard für Nicht-`Offen`).
5. Integrations-/UI-Tests für den Bestätigungsdialog und die Weiterleitung.

---

## 10. Referenzen

- `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs` (ArchivierenAsync, DeleteAsync)
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `docs/requirements/aufgabe-recovery-wiederherstellung-requirements-analysis.md`
- `docs/requirements/status-zuruecksetzen-ki-aktiv-ohne-lauf-requirements-analysis.md`
- `docs/architecture/architecture-blueprint.md`
- `docs/architecture/entity-relationship-model.md`

---

## 11. Approval & Versionierung

| Version | Datum | Autor | Änderung |
|---------|-------|-------|----------|
| 1.0.0 | 2026-05-23 | planning-requirements-analysis | Erstfassung inkl. Widerspruchsanalyse, zwei-Pfade-Entscheidung, FR/NFR/AC/UC |
