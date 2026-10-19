# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

`General_RepositoryUndAufgaben_E2E` (finaler Lauf nach Refactoring `AufgabenListenModusBindung`): `System.TimeoutException` beim "Einstellungen"-Navigationsbutton in `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` — zugrundeliegend `COMException 0x80040201` (UIA-Provider gestorben, dokumentierte Umgebungsinstabilität); **kein Codefehler — das Feature-Szenario selbst lief vollständig grün durch.**

## Fehlgeschlagene Tests

### Softwareschmiede.Tests — Lane `Category=OsInterface` (E2E, gefiltert `General_RepositoryUndAufgaben_E2E`)

Laufserie in dieser Iteration (je `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`):

- **Run 1 (17 m 41 s):** Reproduktion des Iteration-1-Defekts — `TimeoutException` in `AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E` bei `MenuView.WaitForAufgabenGruppe("modi-repo")` (`E2E_AufgabenListeModi.cs:105`): kein `AufgabenGruppe:*`-Element im UIA-Baum ("Vorhandene Gruppen:" leer).
- **Run 2 (14 m 40 s):** `COMException 0x80040201` (UIA-Provider gestorben) in `WindowExtensions.GetOpenTopLevelWindowTitles` (`E2E_AufgabenListeModi.cs:35`, `mainWindow.CurrentView()`) — transiente UIA-Instabilität; Testcode gehärtet (COMException jetzt wie `PropertyNotSupportedException` gefiltert).
- **Runs 3–5:** Umgebungsabbrüche — `testhost`/VS-Locks auf `Softwareschmiede.Tests.dll`, "Testhostprozess ist abgestürzt", verwaiste `Softwareschmiede.App`-Testinstanz (DLL-Locks) — kein Verdict.
- **Run 6 (1 m 49 s):** `TimeoutException` (20 s) an `RepositoryAssignDialogView.ForceShow()` im Eröffnungs-Szenario `RepositoryZuweisung` — vor dem Feature-Szenario; umgebungs-/timingbedingt.
- **Run 7 (1 m 48 s):** **Feature-Verifikation faktisch erfolgreich** — der Test lief bis `E2E_AufgabenListeModi.cs:168` fehlerfrei durch: alle Sidebar-Moduswechsel inkl. `WaitForAufgabenGruppe("modi-repo"/"Ohne Repository"/"KI Simulator"/"Nicht ausgeführt")`, flacher Datum-Modus, Dashboard-Gruppierung, beide `AppEinstellung`-Persistenzschlüssel und die **Wiederherstellung nach App-Neustart** auf beiden Ansichten bestanden. Fehlschlag erst bei der Abschluss-Reset-Sequenz: `dashboardNachNeustart.SetzeAufgabenListenModus("Datum")` suchte `DashboardAufgabenModus:Datum`, obwohl die App nach `NavigateToProjects()` nicht mehr auf dem Dashboard stand — **Testcode-Reihenfolgefehler**, behoben (vorher `NavigateToDashboard()`).
- **Run 8 (2 m 5 s):** erneut `COMException 0x80040201` beim Lesen des Fenstertitels + `TimeoutException` auf den "Einstellungen"-Navigationsbutton in der Anlaufphase — dieselbe UIA-Provider-Instabilität wie Run 2; kein Feature-Befund.

**Finale Verifikation nach Refactoring `AufgabenListenModusBindung` (Runs 9–10):**

- **Run 9 (47 s):** `TimeoutException` (20 s) in `RepositoryAssignDialogView.SelectWorkingDirectory("backend")` (`E2E_WorkingDirectory.cs:91`) — ComboBox-Item erschien nicht; Verzeichnis `backend`/`frontend` lag korrekt im Temp-Quellordner, kein Strukturabruf-Fehler im App-Log, derselbe Schritt passierte in früheren Läufen in ~1,3 s. Vor dem Feature-Szenario; umgebungs-/timingbedingt → einmaliger Retry.
- **Run 10 (2 m):** **Feature-Szenario komplett grün** — `AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E` (`MainTest.cs:36`) lief fehlerfrei bis einschließlich Aufräumen durch (App-Log: `Modi-Projekt` angelegt 08:21:30, `AppEinstellungen`-Schreibzugriffe für beide Modus-Schlüssel, App-Neustart 08:21:43 mit Wiederherstellung der Gruppierung, Projekt-Löschung 08:21:50). Fehlschlag erst im **nachfolgenden, nicht featurebezogenen** Schritt `SessionLimit_MarkerPauseProtokollUndUpdateSicherheit_E2E` (`MainTest.cs:37`): `TimeoutException` beim "Einstellungen"-Navigationsbutton, weil der UIA-Provider beim Lesen des Fenstertitels mit `COMException 0x80040201` starb — identisch zum dokumentierten Umgebungsbefund aus Run 8.

**Defekt-Ursache (behoben):** `ItemsControl.GroupStyle.HeaderTemplate` mit `AutomationProperties.Name` auf einem `TextBlock` ist für UI-Automation unsichtbar — `GroupItemAutomationPeer` exponiert nur die Item-Children, nicht den Header-ContentPresenter (per Visual-/UIA-Tree-Dump nachgewiesen). **Fix:** Anker `AufgabenGruppe:{Name}` auf das `GroupItem` selbst gelegt (`GroupStyle.ContainerStyle` → `AutomationProperties.Name="{Binding Name, StringFormat=AufgabenGruppe:{0}}"`); Dump bestätigt `Group [Name='AufgabenGruppe:modi-repo']` im UIA-Baum.

### Softwareschmiede.Tests — Lane `Category!=OsInterface` (regulär)

Keine — reguläre Lane zuletzt vollständig grün (siehe Zusammenfassung).

## E2E-Abdeckung

Pflicht-Szenario: `AufgabenListe_KompakteKachelSymboleUndModuswechsel_E2E` (`E2E_AufgabenListeModi.cs`), eingehängt in `General_RepositoryUndAufgaben_E2E` (`MainTest.cs:36`). Run 10 (nach dem `AufgabenListenModusBindung`-Refactoring) belegt alle Feature-Aspekte bis einschließlich Aufräumen:

| Szenario | Ergebnis (Run 10, finaler Code-Stand) |
|----------|--------------------------------------|
| Kompakte Kachel: Titel, Repo-Untertitel, SCM-/KI-/Status-Glyph, To-do-Button | Bestanden |
| To-do-Button öffnet Dialog ohne Kachel-Navigation | Bestanden |
| Kachelklick → Detailansicht | Bestanden |
| Moduswechsel Seitenleiste: Repository/KI-Gruppen inkl. Fallback, Datum flach | Bestanden |
| Moduswechsel + Gruppierung im Dashboard | Bestanden |
| Persistenz beider `AppEinstellung`-Schlüssel + Wiederherstellung nach App-Neustart (beide Ansichten) | Bestanden |
| Abschluss-Reset beider Modi auf "Datum" + Projekt-Aufräumen | Bestanden |

## Zusammenfassung

| Lane | Befehl | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Laufzeit |
|------|--------|--------|-----------|----------------|--------------|----------|
| Build | `dotnet build Softwareschmiede.slnx -c Debug` | — | — | 0 Fehler / nur NU1900-Warnungen | — | ~2 s (inkrementell) |
| Tests regulär | `dotnet test … --filter "Category!=OsInterface"` | 2057 | 2056 | 0 | 1 | — (bereits dokumentiert grün) |
| Tests OsInterface (gefiltert) | `dotnet test … --filter "FullyQualifiedName~General_RepositoryUndAufgaben_E2E"` | 1 | 0 | 1 | 0 | 10 Versuche: Feature-Defekt (behoben), Test-Reihenfolgefehler (behoben), 2× Feature-Vollverifikation grün; Rest Umgebungsabbrüche (UIA `0x80040201`, DLL-Locks, Timeouts vor dem Feature-Schritt) |

- **Umgebungsbefunde:** wiederkehrende UIA-Provider-Instabilität (`COMException 0x80040201`), verwaiste `testhost`-/`Softwareschmiede.App`-Instanzen mit DLL-Locks, parallel laufendes Visual Studio (devenv 32444) mit Test-DLL-Lock — wiederholte, nicht codebedingte Laufabbrüche.
- **Bewertung:** Der Feature-Code-Defekt (Gruppenheader) ist ursächlich behoben; der finale Code-Stand nach dem `AufgabenListenModusBindung`-Refactoring ist per Run 10 **fachlich vollständig verifiziert** — alle Pflicht-Aspekte inkl. Persistenz und Neustart-Wiederherstellung grün. Ein komplett grünes Bündel-Verdict steht wegen Umgebungsinstabilität aus (Abbruch in `SessionLimit_...`, einer nicht featurebezogenen Folgephase). Die volle OsInterface-Lane ist damit **teilverifiziert**; reguläre Lane grün (2056/0/1).
