# Umsetzungsplan: Zuverlässige Anwendung des Arbeitsverzeichnisses

## Übersicht

Der Plan umfasst die konsistente Anwendung des konfigurierten Arbeitsverzeichnisses (`RepositoryStartKonfiguration.WorkingDirectoryRelativePath`) in drei Abläufen: (1) KI-Ausführung (CLI-Start) — bereits funktional, Prüfung erforderlich; (2) Öffnen des Arbeitsverzeichnisses über Ribbon-Aktion — teilweise vorhanden, muss `WorkingDirectoryResolver` nutzen; (3) Starten von Visual Studio Code über Ribbon-Aktion — teilweise vorhanden, muss `WorkingDirectoryResolver` nutzen und die Solution-Suche im aufgelösten Verzeichnis durchführen.

Die Implementierung konzentriert sich auf die Anpassung der `TaskDetailViewModel`-Methoden, um `WorkingDirectoryResolver` zu nutzen, sowie auf das Schreiben von Unit- und E2E-Tests. Die bestehenden Services (`WorkingDirectoryResolver`, `ArbeitsverzeichnisOeffnenService`, `IdeOeffnenService`) benötigen keine Änderungen.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Async-Handling für Ribbon-Aktionen** | Umwandlung von `OeffneArbeitsverzeichnis()` und `OeffneVisualStudioCodeFallback()` zu async-Methoden (`async void` als Command-Handler) | `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` ist async. Ein Blocking-Aufruf (`.GetAwaiter().GetResult()`) blockiert den UI-Thread unnötig. Modernes MVVM nutzt async-Methoden für Command-Handler, insbesondere bei I/O-abhängigen Operationen. Synchrones Blocking widerspricht den Best Practices von WPF und kann zu UI-Verzögerungen führen. |
| **GitPlugin-Parameter in Ribbon-Aktionen** | Übergabe von `gitPlugin: null` an `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` | Die Ribbon-Aktionen arbeiten nur mit konfigurierten Dateisystem-Pfaden und benötigen nicht die Git-Plugin-Logik zur Auflösung des echten Repository-Pfads (wie z. B. bei `LocalDirectoryPlugin.InSourceDirectory`). Dieser Parameter wird in `KiAusfuehrungsService` für spezialisierte Plugin-Modi genutzt; für UI-Aktionen ist `null` ausreichend. |
| **Fehlerbehandlung in Ribbon-Aktionen** | Try-Catch mit aussagekräftigen Fehlermeldungen in der UI; `DirectoryNotFoundException` wird zu benutzerfreundlicher Meldung im ViewModel | Die Anforderung verlangt, dass ungültige oder fehlende Arbeitsverzeichniskonfigurationen zu aussagekräftigen Fehlermeldungen führen. `WorkingDirectoryResolver.ValidateWorkingDirectory()` wirft `DirectoryNotFoundException`, wenn das aufgelöste Verzeichnis nicht existiert — diese muss der ViewModel in eine UI-Meldung übersetzen. |
| **Solution-Suche im aufgelösten Arbeitsverzeichnis** | `IdeOeffnenService.FindeSolutions()` wird mit dem aufgelösten Arbeitsverzeichnis (nicht dem Repository-Root) aufgerufen | Wenn Lösungen im Arbeitsverzeichnis `src/solutions/` liegen, muss die Suche dort stattfinden, nicht im Repository-Root. Das aufgelöste Verzeichnis wird an `FindeSolutions()` übergeben. |

---

## Programmabläufe

### 1. Arbeitsverzeichnis öffnen mit Auflösung (Ribbon-Aktion)

1. Benutzer klickt auf „Arbeitsverzeichnis öffnen" in der Ribbon-Leiste
2. `TaskDetailViewModel.OeffneArbeitsverzeichnis()` wird aufgerufen (async void)
3. ViewModel prüft, ob `Aufgabe.LokalerKlonPfad` vorhanden ist
4. ViewModel ermittelt die `RepositoryStartKonfiguration` aus `Aufgabe.GitRepository.Repositories` (gefiltert nach dem korrekten `LokalerKlonPfad`)
5. ViewModel ruft `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(lokalerKlonPfad, startConfig, gitPlugin: null, ct)` auf
6. `WorkingDirectoryResolver` kombiniert `LokalerKlonPfad` mit `startConfig.WorkingDirectoryRelativePath`, validiert das resultierende Verzeichnis und gibt es zurück
7. ViewModel übergibt das aufgelöste Arbeitsverzeichnis an `ArbeitsverzeichnisOeffnenService.Oeffne(effectiveWorkdir)`
8. Service startet `explorer.exe` mit dem aufgelösten Pfad
9. Bei Fehler (`DirectoryNotFoundException`, `InvalidOperationException` etc.) wird `FehlerMeldung` gesetzt und dem Benutzer angezeigt

**Beteiligte Klassen/Komponenten:** `TaskDetailViewModel`, `WorkingDirectoryResolver`, `ArbeitsverzeichnisOeffnenService`, `RepositoryStartKonfiguration`, `Aufgabe`

### 2. Visual Studio Code öffnen mit Auflösung (Ribbon-Aktion Fallback)

1. Benutzer versucht, IDE zu öffnen, es wird aber keine Solution gefunden
2. `TaskDetailViewModel.OeffneVisualStudioCodeFallback()` wird aufgerufen (async void)
3. ViewModel prüft, ob `_openVisualStudioCodeWhenNoSolutionFound` gesetzt ist
4. ViewModel prüft, ob `Aufgabe.LokalerKlonPfad` vorhanden ist
5. ViewModel ermittelt die `RepositoryStartKonfiguration` aus `Aufgabe.GitRepository.Repositories`
6. ViewModel ruft `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(lokalerKlonPfad, startConfig, gitPlugin: null, ct)` auf
7. ViewModel übergibt das aufgelöste Arbeitsverzeichnis an `IdeOeffnenService.OeffneVisualStudioCode(effectiveWorkdir)`
8. Service öffnet VS Code mit dem aufgelösten Verzeichnis als Arbeitsverzeichnis
9. Bei Fehler wird `FehlerMeldung` gesetzt

**Beteiligte Klassen/Komponenten:** `TaskDetailViewModel`, `WorkingDirectoryResolver`, `IdeOeffnenService`, `RepositoryStartKonfiguration`, `Aufgabe`

### 3. IDE öffnen mit Auflösung (Ribbon-Aktion, Solution-Suche)

1. Benutzer klickt auf „IDE öffnen" in der Ribbon-Leiste
2. `TaskDetailViewModel.OeffneIdeAsync()` wird aufgerufen (async)
3. ViewModel ermittelt die `RepositoryStartKonfiguration` und löst das Arbeitsverzeichnis auf (analog Ablauf 2)
4. ViewModel ruft `IdeOeffnenService.FindeSolutions(effectiveWorkdir)` mit dem **aufgelösten** Arbeitsverzeichnis auf (nicht mit `LokalerKlonPfad`)
5. Service sucht nach `*.sln` und `*.slnx` Dateien im aufgelösten Verzeichnis
6. Falls Solutions gefunden werden, wird die erste geöffnet; falls keine, wird `OeffneVisualStudioCodeFallback()` aufgerufen
7. Bei Fehler wird `FehlerMeldung` gesetzt

**Beteiligte Klassen/Komponenten:** `TaskDetailViewModel`, `WorkingDirectoryResolver`, `IdeOeffnenService`, `RepositoryStartKonfiguration`, `Aufgabe`

### 4. CLI-Start mit Arbeitsverzeichnis (bereits funktional, Prüfung)

1. Benutzer startet KI-Ausführung für eine Aufgabe
2. `TaskDetailViewModel.StartenCommand` ruft `KiAusfuehrungsService.StartCliAsync(aufgabeId, kiPlugin, localRepoPath, startConfig, gitPlugin, ct)` auf
3. `KiAusfuehrungsService` ruft `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` auf
4. Das aufgelöste Arbeitsverzeichnis wird an `kiPlugin.StartCliAsync(effectiveWorkingDirectory, ...)` übergeben
5. CLI-Prozess startet im konfigurierten Arbeitsverzeichnis

**Status:** Bereits implementiert. Bestehende E2E-Tests (`E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`) validieren dies.

**Beteiligte Klassen/Komponenten:** `TaskDetailViewModel`, `KiAusfuehrungsService`, `WorkingDirectoryResolver`

---

## Neue Klassen

Keine neuen Klassen erforderlich. Alle notwendigen Klassen und Services bestehen bereits.

---

## Änderungen an bestehenden Klassen

### `TaskDetailViewModel` (UI ViewModel)

- **Geänderte Methode:** `OeffneArbeitsverzeichnis()` 
  - **Änderung:** Umwandlung von synchroner zu `async void`-Methode
  - **Neue Logik:** 
    - Ermittelung der `RepositoryStartKonfiguration` aus `Aufgabe.GitRepository.Repositories` basierend auf `LokalerKlonPfad`
    - Aufruf von `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(lokalerKlonPfad, startConfig, gitPlugin: null, CancellationToken.None)`
    - Übergabe des aufgelösten Arbeitsverzeichnisses an `ArbeitsverzeichnisOeffnenService.Oeffne(effectiveWorkdir)`
  - **Fehlerbehandlung:** Try-Catch für `DirectoryNotFoundException`, `InvalidOperationException`, `ArgumentException`; Fehler wird in `FehlerMeldung` angezeigt
  - **Abhängigkeit:** Benötigt `WorkingDirectoryResolver` (statischer Service, keine DI-Änderung)

- **Geänderte Methode:** `OeffneVisualStudioCodeFallback()`
  - **Änderung:** Umwandlung von synchroner zu `async void`-Methode
  - **Neue Logik:** Analog zu `OeffneArbeitsverzeichnis()` — Auflösung des Arbeitsverzeichnisses, dann Übergabe an `IdeOeffnenService.OeffneVisualStudioCode(effectiveWorkdir)`
  - **Fehlerbehandlung:** Try-Catch mit aussagekräftigen Fehlermeldungen
  - **Abhängigkeit:** Benötigt `WorkingDirectoryResolver`

- **Geänderte Methode:** `OeffneIdeAsync()`
  - **Änderung:** Bereits async, aber muss angepasst werden
  - **Neue Logik:** 
    - Auflösung des Arbeitsverzeichnisses (wie oben)
    - Aufruf von `IdeOeffnenService.FindeSolutions(effectiveWorkdir)` mit dem **aufgelösten** Arbeitsverzeichnis statt mit `LokalerKlonPfad`
    - Rest des Ablaufs bleibt gleich (Solutions öffnen oder VSCode-Fallback)
  - **Abhängigkeit:** Benötigt `WorkingDirectoryResolver`

---

## Datenbankmigrationen

Keine. Das Datenmodell `RepositoryStartKonfiguration` und `Aufgabe` sind bereits vorhanden und benötigen keine Schema-Änderungen.

---

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Die Validierung erfolgt in `WorkingDirectoryResolver.ValidateWorkingDirectory()`, die bereits implementiert ist:
- Prüfung auf Path-Traversal-Angriffe
- Validierung, dass das aufgelöste Verzeichnis existiert (wirft `DirectoryNotFoundException` bei Fehler)

Diese Validierung wird durch die Ribbon-Aktionen indirekt genutzt, wenn sie `DetermineEffectiveWorkingDirectoryAsync()` aufrufen. Die Fehlerbehandlung muss im ViewModel erfolgen.

---

## Konfigurationsänderungen

Keine. Die Funktionalität baut auf der bereits existierenden `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` auf, die über die UI konfigurierbar ist (`ArbeitsverzeichnisBearbeitenDialog.xaml`).

---

## Seiteneffekte und Risiken

- **Ribbon-Aktionen Async/Await:** Die Umwandlung von `OeffneArbeitsverzeichnis()` und `OeffneVisualStudioCodeFallback()` zu async void-Methoden (Command-Handler) ist ein bekanntes Muster in MVVM. Risiko: minimal, da diese Methoden nicht auf Rückgabewerte warten müssen; die Fehlerbehandlung erfolgt über `FehlerMeldung`.

- **Solution-Suche im aufgelösten Verzeichnis:** Wenn ein Projekt kein `WorkingDirectoryRelativePath` konfiguriert hat, wird der Repository-Root verwendet — das Verhalten ändert sich nicht. Wenn konfiguriert, wird die Solution-Suche im neuen Verzeichnis durchgeführt. **Potentielles Risiko:** Wenn Solutions in mehreren Verzeichnissen existieren (Root und Arbeitsverzeichnis), wird jetzt die im Arbeitsverzeichnis bevorzugt. Das ist gewünscht, sollte aber bei Dokumentation erwähnt werden.

- **Fehlerbehandlung:** Wenn die `RepositoryStartKonfiguration` `null` ist oder `WorkingDirectoryRelativePath == null`, wird der Repository-Root verwendet (aktuelles Verhalten bleibt erhalten).

- **CLI-Ausführung:** Keine Änderungen; bereits funktional und getestet.

---

## Umsetzungsreihenfolge

1. **Anpassung von `TaskDetailViewModel.OeffneArbeitsverzeichnis()`**
   - Voraussetzungen: `WorkingDirectoryResolver` (bereits vorhanden), `ArbeitsverzeichnisOeffnenService` (bereits vorhanden), `RepositoryStartKonfiguration` und `Aufgabe` (bereits vorhanden)
   - Beschreibung: Methode von synchron zu `async void` umwandeln, `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` aufrufen, aufgelöstes Verzeichnis übergeben, Fehlerbehandlung implementieren

2. **Anpassung von `TaskDetailViewModel.OeffneVisualStudioCodeFallback()`**
   - Voraussetzungen: wie oben
   - Beschreibung: Analog zu Schritt 1, aber für VSCode-Öffnen

3. **Anpassung von `TaskDetailViewModel.OeffneIdeAsync()`**
   - Voraussetzungen: wie oben
   - Beschreibung: `WorkingDirectoryResolver` nutzen für Arbeitsverzeichnisauflösung, aufgelöstes Verzeichnis an `IdeOeffnenService.FindeSolutions()` übergeben

4. **Unit-Tests für `TaskDetailViewModel.OeffneArbeitsverzeichnis()`**
   - Voraussetzungen: Schritt 1 abgeschlossen
   - Beschreibung: Tests schreiben für Szenarien:
     - Mit `RepositoryStartKonfiguration` und `WorkingDirectoryRelativePath` — prüfen, dass aufgelöstes Verzeichnis an Service übergeben wird
     - Ohne `RepositoryStartKonfiguration` — prüfen, dass `LokalerKlonPfad` verwendet wird
     - Mit ungültigem Arbeitsverzeichnis — prüfen, dass `FehlerMeldung` gesetzt wird

5. **Unit-Tests für `TaskDetailViewModel.OeffneVisualStudioCodeFallback()`**
   - Voraussetzungen: Schritt 2 abgeschlossen
   - Beschreibung: Analog zu Schritt 4

6. **Unit-Tests für `TaskDetailViewModel.OeffneIdeAsync()`**
   - Voraussetzungen: Schritt 3 abgeschlossen
   - Beschreibung: Tests schreiben für:
     - Solution-Suche im aufgelösten Arbeitsverzeichnis
     - Fallback zu VSCode mit aufgelöstem Arbeitsverzeichnis

7. **E2E-Test: Arbeitsverzeichnis öffnen mit konfiguriertem Arbeitsverzeichnis**
   - Voraussetzungen: Schritte 1 und 4 abgeschlossen
   - Beschreibung: 
     - Aufgabe mit konfiguriertem Arbeitsverzeichnis (z. B. `src/subfolder`) erstellen
     - Ribbon-Aktion „Arbeitsverzeichnis öffnen" ausführen
     - Prüfen, dass Explorer das konfigurierte Verzeichnis (nicht den Repository-Root) öffnet
     - (Optional: Datei im Arbeitsverzeichnis erstellen und prüfen, dass sie sichtbar ist)

8. **E2E-Test: Visual Studio Code öffnen mit konfiguriertem Arbeitsverzeichnis**
   - Voraussetzungen: Schritte 2 und 5 abgeschlossen, VS Code verfügbar auf Test-System
   - Beschreibung:
     - Aufgabe ohne Solution, aber mit konfiguriertem Arbeitsverzeichnis erstellen
     - Ribbon-Aktion „IDE öffnen" (fallback zu VSCode) ausführen
     - Prüfen, dass VSCode mit dem konfigurierten Arbeitsverzeichnis geöffnet wird

9. **E2E-Test: Solution-Suche im aufgelösten Arbeitsverzeichnis**
   - Voraussetzungen: Schritt 3 und 6 abgeschlossen
   - Beschreibung:
     - Aufgabe mit Solution im Arbeitsverzeichnis (z. B. `src/solutions/MyApp.sln`) und konfiguriertem `WorkingDirectoryRelativePath: src/solutions` erstellen
     - Ribbon-Aktion „IDE öffnen" ausführen
     - Prüfen, dass die Solution gefunden und geöffnet wird (nicht der Repository-Root durchsucht)

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OeffneArbeitsverzeichnis_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf` | `TaskDetailViewModelTests_Arbeitsverzeichnis` (neu) | Prüfung, dass `WorkingDirectoryResolver` aufgerufen wird und das aufgelöste Verzeichnis an `ArbeitsverzeichnisOeffnenService.Oeffne()` übergeben wird |
| `OeffneArbeitsverzeichnis_OhneKonfiguration_RuftServiceMitRepositoryRootAuf` | `TaskDetailViewModelTests_Arbeitsverzeichnis` | Prüfung, dass bei `null`-Konfiguration der Repository-Root verwendet wird |
| `OeffneArbeitsverzeichnis_MitUngueltigemArbeitsverzeichnis_ZeigtFehlermeldung` | `TaskDetailViewModelTests_Arbeitsverzeichnis` | Prüfung, dass `DirectoryNotFoundException` zu aussagekräftiger `FehlerMeldung` führt |
| `OeffneVisualStudioCodeFallback_MitKonfiguriertemArbeitsverzeichnis_RuftServiceMitAufgeloestemPfadAuf` | `TaskDetailViewModelTests_VisualStudioCode` (neu) | Prüfung analog zu `OeffneArbeitsverzeichnis` |
| `OeffneVisualStudioCodeFallback_OhneKonfiguration_RuftServiceMitRepositoryRootAuf` | `TaskDetailViewModelTests_VisualStudioCode` | Prüfung bei `null`-Konfiguration |
| `OeffneVisualStudioCodeFallback_OhneVsCode_ZeigtFehlermeldung` | `TaskDetailViewModelTests_VisualStudioCode` | Prüfung, dass `InvalidOperationException` behandelt wird |
| `OeffneIdeAsync_FindetSolutionImAufgeloestenArbeitsverzeichnis` | `TaskDetailViewModelTests_IdeOeffnen` (angepasst/erweitert) | Prüfung, dass `FindeSolutions()` mit aufgelöstem Verzeichnis aufgerufen wird |
| `OeffneIdeAsync_OhneLoesungenImArbeitsverzeichnis_FaelltZuVsCodeZurueck` | `TaskDetailViewModelTests_IdeOeffnen` | Prüfung des Fallback-Verhaltens mit aufgelöstem Verzeichnis |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTests` — alle Tests, die `OeffneArbeitsverzeichnis()`, `OeffneIdeAsync()` oder `OeffneVisualStudioCodeFallback()` aufrufen | Diese Methoden werden von synchron zu async umgewandelt; Mocks und Aufrufe müssen angepasst werden (`.Wait()` oder `await` hinzufügen, je nach Teststruktur) |
| `TaskDetailViewModelTests` — Tests für Command-Binding bei IDE-Öffnen | Async void-Commands benötigen besondere Behandlung in Tests; möglicherweise müssen `Task.Delay()` oder ähnliche Mechanismen hinzugefügt werden, um auf Abschluss der async Operation zu warten |

Falls keine Unit-Tests für `TaskDetailViewModel` bestehen, müssen sie neu angelegt werden.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Arbeitsverzeichnis öffnen mit konfiguriertem Arbeitsverzeichnis | `E2E_WorkingDirectory.cs` oder `E2E_RibbonActions_WorkingDirectory.cs` (neu) | Ribbon-Aktion öffnet das konfigurierte Arbeitsverzeichnis (nicht Repository-Root) im Explorer |
| Visual Studio Code öffnen mit konfiguriertem Arbeitsverzeichnis | `E2E_WorkingDirectory.cs` oder `E2E_RibbonActions_WorkingDirectory.cs` (neu) | VSCode-Fallback öffnet das konfigurierte Arbeitsverzeichnis als Working Directory |
| Solution-Suche im aufgelösten Arbeitsverzeichnis | `E2E_WorkingDirectory.cs` oder `E2E_RibbonActions_WorkingDirectory.cs` (neu) | Ribbon-Aktion „IDE öffnen" findet Solutions im konfigurierten Arbeitsverzeichnis (nicht nur im Repository-Root) |

**Betroffene bestehende E2E-Tests:**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E` | Keine Anpassung erforderlich — dieser Test validiert CLI-Start, nicht Ribbon-Aktionen. Kann als Basis für neue E2E-Tests dienen. |

---

## Offene Punkte

Keine. Die Anforderung und Bestandsaufnahme enthalten alle notwendigen Informationen zur Planung.

**Designentscheidungen, die in diesem Plan geklärt wurden:**

1. ✓ **Async in nicht-async Methoden (Requirement Frage 4):** Gelöst durch Umwandlung der Ribbon-Aktionen zu `async void`-Methoden (moderne MVVM-Best-Practice).

2. ✓ **GitPlugin-Parameter bei Ribbon-Aktionen (Requirement Frage 2):** Gelöst durch Festlegung auf `gitPlugin: null` für Ribbon-Aktionen (UI-Kontext benötigt keine Git-Plugin-Logik).

3. ✓ **Fehlerbehandlung (Requirement Frage 3):** Gelöst durch Try-Catch und Anzeige von aussagekräftigen Fehlermeldungen im ViewModel.

4. ✓ **Zuverlässigkeit CLI-Start (Requirement Frage 1):** Bereits vorhanden und getestet; zusätzliche E2E-Tests für Ribbon-Aktionen sind in Plan enthalten.
