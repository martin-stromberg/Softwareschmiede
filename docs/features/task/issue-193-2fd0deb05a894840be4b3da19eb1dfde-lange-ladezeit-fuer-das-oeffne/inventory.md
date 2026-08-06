# Bestandsaufnahme: Asynchrones Laden von Aufgabenprotokollen

Diese Bestandsaufnahme analysiert den bestehenden Code bezogen auf die Anforderung **Issue 193**: Das Laden von Aufgaben mit großem Protokoll blockiert die UI. Das Protokoll soll asynchron nachgeladen werden, damit die Aufgabenbasisinformation schnell angezeigt wird.

---

## Zusammenfassung

### Vorhanden

1. **Datenmodell ist bereits entkoppelbar:**
   - `Aufgabe`-Entity hat Navigationseigenschaft `Protokolleintraege`
   - `Protokolleintrag`-Entity hat Navigationseigenschaft `TestErgebnisse`
   - Keine Änderungen am Datenmodell erforderlich

2. **Zwei separate Service-Methoden existieren bereits:**
   - `AufgabeService.GetDetailAsync()` lädt Aufgabe mit Include für Protokolleinträge
   - `ProtokollService.GetByAufgabeAsync()` lädt Protokolleinträge separat
   - Entkopplung ist technisch möglich

3. **Redundante Abfrage in TaskDetailViewModel:**
   - `TaskDetailViewModel.LadenAsync()` ruft zuerst `GetDetailAsync()` auf (mit Protokoll-Include)
   - Dann ruft es `GetByAufgabeAsync()` auf (lädt Protokoll erneut)
   - Dies ist ineffizient und blockiert sequenziell

4. **UI-Binding ist bereits flexibel:**
   - `Protokolleintraege` ist eine `ObservableCollection<Protokolleintrag>`
   - Kann jederzeit gefüllt werden (nicht nur nach vollständigem Laden der Aufgabe)

### Fehlt / blockiert derzeit

1. **`AufgabeService.GetDetailAsync()` lädt Protokoll blockierend:**
   - `Include(a => a.Protokolleintraege).ThenInclude(p => p.TestErgebnisse)` wird IMMER ausgeführt
   - Keine Möglichkeit, Protokoll auszulassen
   - Blockiert die Aufgabenbasis-Ansicht bei großen Protokollen

2. **Synchrone sequenzielle Koordinierung in TaskDetailViewModel:**
   - `LadenAsync()` wartet vollständig auf `GetDetailAsync()` + `GetByAufgabeAsync()`
   - Erst danach laufen andere asynchrone Operationen (Pull Requests, Plugins, etc.)
   - Keine fire-and-forget für Protokoll-Laden

3. **Keine Optimierung für Basisinformation:**
   - `IsLoading` ist `true`, bis Protokoll komplett geladen ist
   - UI-Responsivität bleibt blockiert, bis alles fertig ist

---

## Details

- [Datenmodell](inventory/models.md) — Aufgabe, Protokolleintrag, Include-Struktur
- [Logik-Services](inventory/logic.md) — AufgabeService.GetDetailAsync, ProtokollService.GetByAufgabeAsync, deren Blockierungsverhalten
- [ViewModel und Präsentation](inventory/viewmodel.md) — TaskDetailViewModel.LadenAsync, Protokolleintraege Collection, sequenzielle Blockierung
- [Tests](inventory/tests.md) — AufgabeServiceTests, TaskDetailViewModelTests, betroffene Test-Methoden

---

## Implementierungsansatz (aus requirement.md)

Der bestehende Code unterstützt bereits die vorgesehene Aufteilung:

1. **Aufgabe ohne Protokoll laden:** `GetDetailAsync()` soll `.Include(a => a.Protokolleintraege)` NICHT mehr enthalten
2. **Protokoll asynchron laden:** `LadenAsync()` soll `LadeProtokolleAsynch()` als Fire-and-Forget aufrufen (`_ = LadeProtokolleAsynch(ct)`)
3. **UI bleibt responsiv:** `IsLoading` für Basisinformation kann früher `false` gesetzt werden; Protokoll lädt parallel

Alle notwendigen Services und Methoden existieren bereits; es ist eine Umstrukturierung ohne neuen Funktionscode.
