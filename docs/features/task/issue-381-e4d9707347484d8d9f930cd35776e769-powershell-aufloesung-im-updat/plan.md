# Umsetzungsplan: PowerShell-Auflösung im UpdateScriptService auf feste Installationspfade beschränken

## Übersicht

Der `UpdateScriptService` löst den PowerShell-Interpreter für `updates/update.ps1` künftig ausschließlich über eine feste Liste administrativ kontrollierter Installationspfade auf (`%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`, `%ProgramFiles%\PowerShell\7\pwsh.exe`) — die PATH-/PATHEXT-Suche und der `"powershell.exe"`-Letztfallback entfallen vollständig. Schlägt die Auflösung fehl, wird der Update-Versuch über eine `InvalidOperationException` kontrolliert abgebrochen statt einen unqualifizierten Dateinamen an `ProcessStartInfo.FileName` zu übergeben. Betroffen ist nur `UpdateScriptService.cs` (plus eine XML-Doc-Korrektur in `PathExecutableResolver.cs` und Tests) — keine Interface-, Konfigurations- oder UI-Änderung.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Auflösung des PowerShell-Interpreters für genau einen Updater-Start innerhalb `UpdateScriptService.StartScriptAsync` — nicht Skriptinhalt, nicht Prozessstart, nicht Elevation-Entscheidung. |
| Auslösende Ereignisse | Jeder Aufruf von `StartScriptAsync` am Ende des gemeinsamen Installationspfads (`UpdateService.StartPreparedUpdateAsync`), unabhängig von manuellem oder Startautomatik-Auslöser; sicherheitskritisch bei `UpdatePreparationResult.RequiresElevation == true` (`Verb = "runas"`). |
| Erwartete Wirkung | Der gestartete Updater-Prozess verwendet ausschließlich einen Interpreter aus der festen Kandidatenliste (Windows PowerShell 5.1 zuerst, dann PowerShell 7). PATH-/PATHEXT-Manipulation kann keinen Interpreter mehr einschleusen; ohne Treffer erfolgt kontrollierter Abbruch statt Fallback. |
| Umgang mit fehlenden Objekten | Windows PowerShell am festen Pfad nicht vorhanden → **wiederverwenden** des nächsten Kandidaten (`pwsh.exe` unter `%ProgramFiles%\PowerShell\7`). PowerShell 7 nicht installiert → fällt fehlerfrei weg, sofern Windows PowerShell existiert. Kein fester Kandidat vorhanden → Update-Versuch **verwerfen** (`InvalidOperationException`, kein Prozessstart, kein `Shutdown()`). `%SystemRoot%`/`%ProgramFiles%` manipuliert oder ungesetzt → Pfade werden über `Environment.GetFolderPath` (`SpecialFolder.System` bzw. `SpecialFolder.ProgramFiles`) aufgelöst, nicht über die rohen Umgebungsvariablen. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein PATH-/Basisnamen-Fallback: Existiert keiner der zwei festen Kandidaten, schlägt das Update fehl, statt — wie bisher — einen über PATH auflösbaren Namen zu starten. | Jeder PATH-Fallback würde die PATH-Hijacking-Lücke (besonders kritisch unter `Verb = "runas"`) wieder öffnen. | bestätigt (2026-11-13, implizit durch Issue-Text: „Auf feste Kandidaten beschränken") |
| 2 | Exakt zwei feste Installationspfade: `%ProgramFiles(x86)%`-PowerShell-7, Per-User-/MSI-Abweichpfade, Microsoft-Store-Installation und `7-preview` werden nicht erkannt; Systeme ohne die zwei Pfade können nicht updaten. | Empfehlung des Sicherheitsreviews nennt genau diese zwei Pfade; jeder weitere Ort wäre ein neuer, separat zu bewertender Befund. Windows PowerShell 5.1 ist fester Windows-Bestandteil, der Normalfall ist abgedeckt. | bestätigt (2026-11-13, implizit durch Issue-Text: exakt die zwei genannten Pfade) |
| 3 | Keine Konfigurierbarkeit der Interpreter-Pfade (kein `UpdateOptions`-Eintrag, keine Anwendungseinstellung, keine UI-Option). | Die Kandidatenliste ist eine Sicherheitsfestlegung; konfigurierbare Pfade wären selbst ein Angriffsvektor. | bestätigt (2026-11-13, implizit durch Issue-Text: „feste Kandidaten") |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Quelle für `%ProgramFiles%` (geklärte Offene Frage 2) | `Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)` | Die Sicherheitsmotivation ist leitend: `GetFolderPath` fragt das Betriebssystem (Known-Folder-API) statt einer beliebig setzbaren Umgebungsvariablen — analog zum bestehenden `SpecialFolder.System`-Ansatz für den Windows-PowerShell-Pfad. `GetEnvironmentVariable("ProgramFiles")` wäre durch denselben Manipulationsvektor unterlaufbar, den die Änderung schließt. |
| Fehlschlag ohne Treffer (geklärte Offene Frage 1) | `InvalidOperationException` mit aussagekräftiger Meldung in `FindPowerShellExecutable`, kein PATH-/Basisnamen-Fallback | Die Fehlerkette existiert bereits (`StartScriptAsync` → `UpdateService.StartPreparedUpdateAsync` → `MainWindowUpdateFlow` → `SetError`/`UpdateHinweis`); der Abbruch verhindert `Shutdown()` und Prozessstart. Ein Fallback würde die Sicherheitslücke wieder öffnen. |
| Kandidatenreihenfolge (geklärte Offene Frage 3) | Windows PowerShell 5.1 zuerst, PowerShell 7 als zweiter Kandidat | Bestehende Priorisierung bleibt erhalten: `update.ps1` ist auf Windows PowerShell ausgelegt (immer vorhanden, maximale Kompatibilität); pwsh ist Zusatzoption für abgespeckte Installationen. |
| Umfang der festen Liste (geklärte Offene Frage 4) | Exakt die zwei empfohlenen Pfade, keine weiteren Installationsorte | Wortlaut der Review-Empfehlung; Erweiterungen wären ein neuer Befund mit eigener Sicherheitsbewertung. |
| Härtung der Kandidatenprüfung (geklärte Offene Frage 5) | Einheitlicher Prüfpunkt in `FindPowerShellExecutable`: `Path.IsPathFullyQualified(candidate) && File.Exists(candidate)` — gilt für Standard- wie injizierte Kandidaten gleichermaßen, keine separate Konstruktor-Validierung | Ein einziger Durchsetzungspunkt ist einfacher und lückenloser als zwei Prüfstellen: Da injizierte Listen denselben Auflösungspfad durchlaufen, kann per DI/Test kein unqualifizierter Name zum Launcher durchkommen. |
| `IsExecutableAvailable` | Methode entfällt ersatzlos; die Vollqualifiziert-/Existenzprüfung zieht inline in `FindPowerShellExecutable` | Bei ausschließlich festen Kandidaten ist der gesamte PATH-/PATHEXT-Codepfad tot; eine vereinfachte Restmethode hätte keinen Mehrwert gegenüber der Inline-Bedingung. |
| Testbarkeit der Standard-Kandidatenliste | `GetDefaultPowerShellCandidates` wird von `private static` auf `internal static` geöffnet (Zugriff über bestehendes `InternalsVisibleTo("Softwareschmiede.Tests")` in `AssemblyInfo.cs`) | Die Anforderung verlangt einen Test auf den Listeninhalt (nur vollqualifizierte, nicht-PATH-aufgelöste Pfade). Ein `internal`-Seam ist die projektkonforme Testöffnung; Reflection oder indirekte Behauptungen wären fragiler. Kein neuer Laufzeit-Erweiterungspunkt — die Liste bleibt fest kodiert. |

## Programmabläufe

### Interpreter-Auflösung und Skriptstart (`StartScriptAsync`, erfolgreicher Pfad)

1. `UpdateService.StartPreparedUpdateAsync` ruft `IUpdateScriptService.StartScriptAsync(preparation)` auf.
2. `StartScriptAsync` ruft `FindPowerShellExecutable(_powerShellCandidates)` auf; `_powerShellCandidates` stammt aus der injizierten Liste (Testkonstruktor) oder aus `GetDefaultPowerShellCandidates()` (Produktion und Update-E2E-Modus).
3. `FindPowerShellExecutable` prüft die Kandidaten in Reihenfolge: Der erste Kandidat mit `Path.IsPathFullyQualified(candidate) && File.Exists(candidate)` gewinnt — unter Windows normalerweise der Windows-PowerShell-Pfad aus `Environment.GetFolderPath(SpecialFolder.System)`; ist dieser nicht vorhanden, der PowerShell-7-Pfad aus `Environment.GetFolderPath(SpecialFolder.ProgramFiles)`. Unqualifizierte oder nicht existierende Einträge (auch injizierte) werden übersprungen.
4. `StartScriptAsync` baut die Argumentliste unverändert (`-NoProfile -ExecutionPolicy Bypass -File <script> -AppPid … -TargetDirectory … -ExtractedDirectory … -ExecutablePath … -LogPath …`) und ruft `_processLauncher.Start(executable, arguments, _baseDirectory, preparation.RequiresElevation)` — `executable` ist garantiert ein vollqualifizierter Pfad.
5. Rückgabe `true` → `StartPreparedUpdateAsync` ruft `IApplicationShutdownService.Shutdown()` auf.

Beteiligte Klassen/Komponenten: `UpdateScriptService`, `UpdateService`, `IUpdateProcessLauncher`/`UpdateProcessLauncher`, `IApplicationShutdownService`, `UpdatePreparationResult`

### Kontrollierter Abbruch bei fehlendem Interpreter

1. Schritte 1–3 wie oben; kein Kandidat erfüllt die Prüfung.
2. `FindPowerShellExecutable` wirft `InvalidOperationException` mit aussagekräftiger Meldung, die die geprüften festen Pfade benennt (z. B. „Kein PowerShell-Interpreter unter den festen Installationspfaden gefunden: <Pfad 1>, <Pfad 2>.").
3. Die Exception propagiert über `UpdateService.StartPreparedUpdateAsync` (kein `Shutdown()`) nach `MainWindowUpdateFlow` → `progressViewModel.SetError("Update konnte nicht vorbereitet werden. Details wurden protokolliert.")` / `UpdateHinweis`. Es findet kein Prozessstart statt.

Beteiligte Klassen/Komponenten: `UpdateScriptService`, `UpdateService`, `MainWindowUpdateFlow`

## Neue Klassen

Keine — reine Verhaltensänderung innerhalb von `UpdateScriptService`.

## Änderungen an bestehenden Klassen

### `UpdateScriptService` (Klasse, `src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs`)

- **Geänderte Methoden:**
  - `GetDefaultPowerShellCandidates()` — Sichtbarkeit `private static` → `internal static` (Test-Seam über bestehendes `InternalsVisibleTo`). Rückgabe reduziert auf zwei vollqualifizierte Pfade: (1) `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")` (unverändert), (2) `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe")` (neu). Die Einträge `"powershell.exe"`, `"pwsh.exe"`, `"pwsh"` werden entfernt.
  - `FindPowerShellExecutable(IReadOnlyList<string> candidates)` — Kandidatenprüfung auf `Path.IsPathFullyQualified(candidate) && File.Exists(candidate)` umgestellt (inline, ohne `IsExecutableAvailable`). Letztfallback `return "powershell.exe"` entfällt; stattdessen `InvalidOperationException` mit Meldung, die die geprüften Kandidatenpfade benennt.
- **Entfernte Methoden:**
  - `IsExecutableAvailable(string executable)` — vollständig entfernt; der PATH-/PATHEXT-Suchpfad über `PathExecutableResolver.EnumeratePathDirectories` entfällt. `UpdateScriptService` verwendet `PathExecutableResolver` danach nicht mehr.
- **Unverändert:** Produktions- und Testkonstruktor (die injizierbare `powerShellCandidates`-Liste bleibt bestehen), `CreateScriptAsync`, `BuildScript`, `StartScriptAsync`-Aufrufstruktur.

### `PathExecutableResolver` (Klasse, `src/Softwareschmiede/Infrastructure/Services/PathExecutableResolver.cs`)

- **Geändert:** nur der XML-Doc-Kommentar (Zeilen 3–13) — die Verweise auf `Updates.UpdateScriptService` als Nutzer der PATH-Zerlegung werden entfernt; als Nutzer bleiben `RepositoryScriptExecutor` (`bash.exe`-Suche) und `VisualStudioCodeLocator` dokumentiert. Kein Code-Eingriff.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| PowerShell-Kandidat (Standardliste und injizierte Liste gleichermaßen) | Kandidat zählt nur als Interpreter, wenn `Path.IsPathFullyQualified(candidate)` **und** `File.Exists(candidate)` gelten. | Kandidat wird übersprungen; erfüllt kein Kandidat die Regel, wirft `FindPowerShellExecutable` eine `InvalidOperationException` mit den geprüften Pfaden. |

## Konfigurationsänderungen

Keine — die Kandidatenliste ist eine Sicherheitsfestlegung und bleibt fest kodiert; weder `UpdateOptions` noch `AppEinstellungen` noch eine UI-Option werden ergänzt. Einzige „Konfigurationsfläche" bleibt der bestehende Testkonstruktor (`powerShellCandidates`) für Unit-Tests.

## Seiteneffekte und Risiken

- **Update-Verfügbarkeit auf exotischen Systemen:** Auf Windows-Installationen ohne Windows PowerShell am Standardort und ohne PowerShell 7 unter `%ProgramFiles%\PowerShell\7` schlägt das Update künftig fehl, wo es bisher u. U. über PATH startete. Bewusst in Kauf genommen (Sicherheitsfestlegung); Fehlerbild ist ein verständlicher Dialog statt eines stillen Fehlstarts.
- **`PathExecutableResolver`:** Verliert einen Nutzer (`UpdateScriptService`), bleibt aber für `RepositoryScriptExecutor` und `VisualStudioCodeLocator` vollständig erhalten — keine Verhaltensänderung dort.
- **Update-E2E-Modus:** `App.xaml.cs` registriert `UpdateScriptService` ohne Kandidateninjektion — die E2E-Pipeline nutzt weiter die echte `GetDefaultPowerShellCandidates()`; der aufgezeichnete `fileName` im `UpdateProcessStartRecorded`-Ereignis wird künftig immer ein vollqualifizierter Pfad sein (wird als Assertion ergänzt).
- **Nicht-Windows-Ausführung:** Der bisherige `"pwsh"`-Kandidat ohne Erweiterung entfällt; das Update-Feature ist Windows-only (WPF), `SpecialFolder`-Auflösung auf anderen Plattformen ohnehin nicht einsatzrelevant.

## Umsetzungsreihenfolge

1. **`UpdateScriptService`-Umstellung**
   - Voraussetzungen: Keine (alle benötigten APIs — `Environment.GetFolderPath`, `Path.IsPathFullyQualified`, `File.Exists` — sind BCL).
   - Beschreibung: `GetDefaultPowerShellCandidates` auf die zwei festen Pfade reduzieren und `internal static` machen; `FindPowerShellExecutable` auf die Vollqualifiziert-+`File.Exists`-Prüfung umstellen und den `"powershell.exe"`-Fallback durch `InvalidOperationException` ersetzen; `IsExecutableAvailable` entfernen.
2. **XML-Doc-Korrektur `PathExecutableResolver`**
   - Voraussetzungen: Schritt 1 (nur damit der Kommentar nicht wieder veraltet).
   - Beschreibung: `UpdateScriptService`-Verweise aus dem `<summary>`-Block entfernen.
3. **Unit-Tests in `UpdateScriptServiceTests`**
   - Voraussetzungen: Schritt 1 (insbesondere `internal`-Öffnung für den Listeninhalt-Test).
   - Beschreibung: Neue Tests für Listeninhalt, Fehlschlag-Semantik und Unqualifiziert-Härtung; bestehenden Argument-Test um Vollqualifiziert-Assert erweitern (Details siehe Tests).
4. **E2E-Assertion im bestehenden Update-Fixture-Test**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: In `E2E_UpdateFixture.Fixture_UsesIsolatedRealUpdatePipeline` (ausgeführt über `End2EndTest.General_UpdateFixtureSmoke_E2E`) Assertion auf das protokollierte `fileName` des `UpdateProcessStartRecorded`-Ereignisses ergänzen (vollqualifiziert, einer der zwei festen Pfade).
5. **Verifikation**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet format Softwareschmiede.slnx --verify-no-changes`, `dotnet build Softwareschmiede.slnx`, Regulär-Lane `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` sowie das Update-E2E-Subset `--filter "FullyQualifiedName~End2EndTest.General_Update"` — in dieser Sandbox jeweils mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` und synchron (nicht im Hintergrund).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetDefaultPowerShellCandidates_ShouldReturnOnlyFixedFullyQualifiedPaths` | `UpdateScriptServiceTests` | Die Standardliste enthält genau zwei Einträge: den Windows-PowerShell-Pfad unter `SpecialFolder.System` und den PowerShell-7-Pfad unter `SpecialFolder.ProgramFiles`; beide `Path.IsPathFullyQualified`, kein PATH-/Basisnamen-Eintrag. |
| `StartScriptAsync_ShouldThrow_WhenNoFixedPowerShellCandidateExists` | `UpdateScriptServiceTests` | Injizierte Liste nur mit nicht existierenden vollqualifizierten Pfaden → `InvalidOperationException` mit aussagekräftiger Meldung; `CapturingLauncher`/`FailingLauncher` wird nicht aufgerufen (kein `FileName` gesetzt). |
| `StartScriptAsync_ShouldIgnoreUnqualifiedCandidates` | `UpdateScriptServiceTests` | Injizierte Liste mit relativem/Basisnamen-Eintrag vor einem existierenden vollqualifizierten Pfad → `FileName` ist der vollqualifizierte Pfad; Variante mit ausschließlich unqualifizierten Einträgen → `InvalidOperationException`. Nachweis, dass injizierte Kandidaten derselben Vollqualifiziert-Prüfung unterliegen. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `StartScriptAsync_ShouldPassArgumentsToLauncher` (`UpdateScriptServiceTests`) | Um `Path.IsPathFullyQualified(launcher.FileName)`-Assertion erweitern — verankert, dass am `IUpdateProcessLauncher` nie ein relativer/Basisname ankommt. |

`StartScriptAsync_ShouldFallbackToPwshCandidate_WhenPowerShellCandidateIsMissing` und `StartScriptAsync_ShouldThrow_WhenLauncherReportsNoProcess` bleiben unverändert gültig (injizierte vollqualifizierte Pfade bzw. Launcher-Rückgabe sind von der Umstellung nicht betroffen). `UpdateServiceTests.StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails` deckt die Shutdown-Semantik des neuen Fehlschlagpfads bereits ab.

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung ändert kein UI-Verhalten, aber einen sicherheitsrelevanten Schritt im realen Update-Benutzerfluss (Skriptstart mit aufgelöstem Interpreter). Der bestehende konsolidierte E2E-Test startet die App mit der echten `UpdateScriptService`-Instanz und protokolliert den aufgelösten `fileName` über `RecordingUpdateProcessLauncher` — genau dort wird die neue Sicherheitseigenschaft nachgewiesen. Gemäß der E2E-Konsolidierungsregel wird **kein** neuer Testmethoden-App-Start ergänzt, sondern die Assertion in den bestehenden konsolidierten Test eingehängt.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Update-Start im E2E-Modus verwendet einen vollqualifizierten, festen Interpreter-Pfad | `E2E_UpdateFixture.cs` → `Fixture_UsesIsolatedRealUpdatePipeline` (läuft in `End2EndTest.General_UpdateFixtureSmoke_E2E`) | Erwartete Wirkung: ausschließlich Interpreter aus der festen Kandidatenliste | Nur der E2E-Lauf nutzt die produktive `GetDefaultPowerShellCandidates()`-Liste in der realen DI-Verkabelung; ein Unit-Test mit injizierter Liste kann die Standardliste am Prozessstart nicht nachweisen. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Fixture_UsesIsolatedRealUpdatePipeline` (`E2E_UpdateFixture.cs`, via `End2EndTest.General_UpdateFixtureSmoke_E2E`) | Assertion ergänzen: `fileName` des `UpdateProcessStartRecorded`-Ereignisses ist `Path.IsPathFullyQualified` und gleich einem der zwei festen Installationspfade (Windows-PowerShell-Pfad bzw. PowerShell-7-Pfad). |

Übrige Update-E2E-Szenarien (`General_UpdateStartvarianten_E2E`, E-01 bis E-07) sind unverändert gültig — der aufgelöste Interpreter-Pfad wird dort nicht assertiert, und der Skriptstart gelingt in der Windows-Testumgebung weiter über den Windows-PowerShell-Pfad.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Update-Start mit aufgelöstem Windows-PowerShell-Interpreter | Nutzer bestätigt Update im Fortschrittsdialog (oder Startautomatik) | Update vorbereitet (`UpdatePreparationResult` mit `ScriptPath`, `RequiresElevation` beliebig); Windows PowerShell am festen Pfad vorhanden; beliebig manipulierbarer PATH (z. B. `powershell.exe` in benutzerschreibbarem Verzeichnis ganz vorn) | `UpdateService.StartPreparedUpdateAsync` → `StartScriptAsync` → `FindPowerShellExecutable` liefert `…\System32\WindowsPowerShell\v1.0\powershell.exe` → `IUpdateProcessLauncher.Start(fileName, …)` → `Shutdown()` | Updater-Prozess wird mit dem vollqualifizierten System-PowerShell gestartet — nie mit dem PATH-Eintrag; App beendet sich für den Austausch | E2E: `General_UpdateFixtureSmoke_E2E` (fileName-Assertion); Unit: `StartScriptAsync_ShouldPassArgumentsToLauncher` |
| 2 | Windows PowerShell fehlt, PowerShell 7 vorhanden | Wie #1 | Windows-PowerShell-Pfad existiert nicht; `%ProgramFiles%\PowerShell\7\pwsh.exe` existiert | Auflösung überspringt Kandidat 1, wählt Kandidat 2 | Updater-Prozess wird mit vollqualifiziertem pwsh-Pfad gestartet | Unit: `StartScriptAsync_ShouldFallbackToPwshCandidate_WhenPowerShellCandidateIsMissing` |
| 3 | Kein fester Kandidat vorhanden | Wie #1 | Weder Windows-PowerShell- noch PowerShell-7-Pfad existieren | `FindPowerShellExecutable` wirft `InvalidOperationException` → `UpdateService` propagiert → `MainWindowUpdateFlow.SetError`/`UpdateHinweis` | Fortschrittsdialog zeigt „Update konnte nicht vorbereitet werden."; kein Prozessstart, kein Shutdown | Unit: `StartScriptAsync_ShouldThrow_WhenNoFixedPowerShellCandidateExists` + `UpdateServiceTests.StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails` |
| 4 | Unqualifizierte Kandidaten können nicht einschleusen | Interne Auflösung (Test/DI) | Injizierte Liste enthält relative Namen/Basisnamen | Vollqualifiziert-Prüfung überspringt sie; ggf. `InvalidOperationException` | Am `IUpdateProcessLauncher` kommt ausschließlich ein vollqualifizierter Pfad an — oder gar nichts | Unit: `StartScriptAsync_ShouldIgnoreUnqualifiedCandidates` |

## Offene Punkte

Keine — alle fünf offenen Fragen der `requirement.md` wurden durch die Vorgaben des Orchestrators geklärt und sind als Designentscheidungen eingearbeitet: (1) `InvalidOperationException` statt Fallback, (2) `Environment.GetFolderPath(SpecialFolder.ProgramFiles)`, (3) Windows PowerShell 5.1 zuerst, (4) exakt zwei feste Pfade, (5) injizierte Kandidaten unterliegen derselben Vollqualifiziert-Prüfung.
