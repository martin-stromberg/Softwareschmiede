# Übersetzte Anforderung: PowerShell-Auflösung im UpdateScriptService auf feste Installationspfade beschränken

**Quelle:** Issue #381 / Aufgaben-ID `e4d97073-4748-4d8d-9f93-0cd35776e769` (Prio-2-Befund aus dem Architektur- und Sicherheitsreview 2026-11, Issue #374)
**Branch:** `task/issue-381-e4d9707347484d8d9f930cd35776e769-powershell-aufloesung-im-updat`

## Fachliche Zusammenfassung

Der `UpdateScriptService` löst den PowerShell-Interpreter für den Start des externen Update-Skripts (`updates/update.ps1`) aktuell über eine Kandidatenliste auf, die neben dem vollqualifizierten Windows-PowerShell-Pfad auch die PATH-aufgelösten Namen `powershell.exe`, `pwsh.exe` und `pwsh` enthält (`GetDefaultPowerShellCandidates`, `IsExecutableAvailable` via `PathExecutableResolver.EnumeratePathDirectories` + `PATHEXT`). Da der Skriptstart bei nicht beschreibbarem Programmverzeichnis erhöht erfolgt (`IUpdateProcessLauncher.Start` mit `Verb = "runas"`), kann ein `powershell.exe`/`pwsh.exe`-Eintrag in einem benutzerschreibbaren PATH-Verzeichnis einen Schadcode-Interpreter mit Administratorrechten starten (PATH-Hijacking). Die Anforderung beschränkt die Interpreter-Auflösung auf eine feste Liste administrativ kontrollierter Installationspfade: `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe` und `%ProgramFiles%\PowerShell\7\pwsh.exe`. Die PATH-Suche entfällt beim Update-Skriptstart vollständig.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die *Auflösung des PowerShell-Interpreters* für genau einen Updater-Start — d. h. die Auswahl der ausführbaren Datei, mit der `updates/update.ps1` im Rahmen von `UpdateScriptService.StartScriptAsync` gestartet wird. Zum Scope gehört ausschließlich die Ermittlung dieses Interpreter-Pfads, nicht der Skriptinhalt, nicht der Prozessstart selbst und nicht die Elevation-Entscheidung.
- **Auslösende Ereignisse:**
  - Jeder Aufruf von `StartScriptAsync` am Ende des gemeinsamen Installationspfads (`UpdateService.StartPreparedUpdateAsync`), unabhängig davon, ob die Installation manuell oder über die Startautomatik ausgelöst wurde.
  - Sicherheitskritisch insbesondere der Fall `UpdatePreparationResult.RequiresElevation == true` (Programmverzeichnis nicht beschreibbar, `UpdatePackageService.CanWriteToBaseDirectory` → `false`): Hier erfolgt der Start über `Verb = "runas"` mit UAC-Elevation — ein über PATH eingeschleuster falscher Interpreter würde mit Administratorrechten laufen.
- **Erwartete Wirkung:**
  - Der gestartete Updater-Prozess verwendet ausschließlich einen Interpreter aus der festen Kandidatenliste (Windows PowerShell 5.1 unter `%SystemRoot%\System32\WindowsPowerShell\v1.0\` bzw. PowerShell 7 unter `%ProgramFiles%\PowerShell\7\`). Eine über PATH- oder `PATHEXT`-Manipulation eingeschleuste Datei in benutzerschreibbaren Verzeichnissen kann nicht mehr als Update-Interpreter gestartet werden — weder erhöht noch une erhöht.
  - Die Kandidaten werden in definierter Reihenfolge geprüft (Windows PowerShell zuerst, dann PowerShell 7 — Annahme: bestehende Priorisierung bleibt erhalten); der erste existierende Kandidat gewinnt.
  - Existiert keiner der festen Kandidaten, wird das Update mit einer verständlichen Fehlermeldung abgebrochen (Fehlerpropagation wie beim bisherigen Startfehler über `InvalidOperationException` → `UpdateService` → Fortschrittsdialog/`UpdateHinweis`). Es erfolgt **kein** stiller Fallback auf einen PATH-aufgelösten Namen (Annahme — ein solcher Fallback würde die Lücke wieder öffnen, siehe Offene Fragen).
- **Umgang mit fehlenden Objekten:**
  - *Windows PowerShell nicht unter dem festen Pfad vorhanden (theoretisch: Server Core / abgespeckte Installationen):* **Wiederverwenden** des nächsten Kandidaten (`%ProgramFiles%\PowerShell\7\pwsh.exe`); nicht „erzeugen" oder PATH-suchen.
  - *PowerShell 7 nicht installiert:* fällt ohne Fehler weg, sofern Windows PowerShell existiert (Normalfall unter Windows).
  - *Kein fester Kandidat vorhanden:* Update-Versuch **verwerfen** — Fehler an den Aufrufer melden, kein Prozessstart, kein `Shutdown()` (der Shutdown erfolgt erst nach erfolgreichem Skriptstart).
  - *Umgebungsvariablen `%SystemRoot%`/`%ProgramFiles%` manipuliert oder ungesetzt:* Auflösung der festen Pfade muss über betriebssystemseitig verlässliche API erfolgen (für System32 bereits heute `Environment.GetFolderPath(Environment.SpecialFolder.System)`; für ProgramFiles analog `Environment.SpecialFolder.ProgramFiles` bzw. `ProgramFiles`-Variable — Entwurfsentscheidung, siehe Offene Fragen). Eine Pfadableitung über beliebig setzbare Umgebungsvariablen darf die Schutzwirkung nicht unterlaufen.
- **Scope-Ausschlüsse:**
  - Keine Änderung am generierten Skript `update.ps1` (Parameter, Kopier-, Warte- und Neustartlogik bleiben unverändert).
  - Keine Änderung an der Elevation-Entscheidung (`RequiresElevation`-Probe in `UpdatePackageService`, `Verb = "runas"`, UAC-Dialog).
  - Keine Beschränkung der PATH-Auflösung in anderen Komponenten: `RepositoryScriptExecutor` (bash-Suche) und `VisualStudioCodeLocator` bleiben unverändert — sie starten keine erhöhten Prozesse und sind nicht Teil dieses Befunds.
  - Keine Unterstützung weiterer PowerShell-Installationsorte über die zwei genannten hinaus (z. B. `%ProgramFiles(x86)%`-PowerShell-7, Per-User-/MSI-Abweichpfade, Microsoft-Store-Installation, `7-preview`) — nicht Teil der Empfehlung; ggf. Offene Frage.
  - Keine neue Benutzer-/Anwendungskonfiguration für Interpreter-Pfade (die Liste ist fest kodiert, siehe Konfiguration).
  - Kein Linux/macOS-Fall: Das Update-Feature ist Windows-only (WPF-Anwendung); der bisherige `pwsh`-Kandidat ohne Erweiterung entfällt ersatzlos.

## Betroffene Klassen und Komponenten

### Zu ändernde Artefakte

- `src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs`
  - `GetDefaultPowerShellCandidates()` (Zeilen 92–101): Kandidatenliste auf die zwei vollqualifizierten Pfade reduzieren (`powershell.exe` aus `SpecialFolder.System` + `WindowsPowerShell\v1.0`, `pwsh.exe` aus `%ProgramFiles%\PowerShell\7`); Entfernen der Einträge `"powershell.exe"`, `"pwsh.exe"`, `"pwsh"`.
  - `IsExecutableAvailable` (Zeilen 114–136): PATH-/PATHEXT-Suche entfällt für die Update-Auflösung; bei nur noch vollqualifizierten Kandidaten reduziert sich die Prüfung auf `File.Exists` (ggf. entfällt die Methode oder wird vereinfacht).
  - `FindPowerShellExecutable` (Zeilen 103–112): Rückgabe `"powershell.exe"` als Letztfallback muss entfallen bzw. durch einen kontrollierten Fehlschlag ersetzt werden (kein unqualifizierter Dateiname an `ProcessStartInfo.FileName` mehr).
- `src/Softwareschmiede/Infrastructure/Services/PathExecutableResolver.cs`
  - Kein Code-Eingriff erforderlich (weiterhin von `RepositoryScriptExecutor` und `VisualStudioCodeLocator` genutzt), aber der XML-Doc-Kommentar (Zeilen 3–13) nennt `UpdateScriptService` als Nutzer der PATH-Zerlegung — nach der Änderung ist dieser Verweis zu korrigieren.
- `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateScriptServiceTests.cs`
  - Bestehende Tests bleiben weitgehend nutzbar (der Testkonstruktor mit injizierbarer `powerShellCandidates`-Liste ermöglicht bereits kontrollierte Kandidaten); der Test `StartScriptAsync_ShouldFallbackToPwshCandidate_WhenPowerShellCandidateIsMissing` bleibt fachlich gültig, sofern die Injektion fester Pfade weiter möglich ist (Annahme).
  - Neue Tests: (a) Standard-Kandidatenliste enthält ausschließlich vollqualifizierte, nicht-PATH-aufgelöste Pfade; (b) Fehlschlag mit Fehlermeldung, wenn kein fester Kandidat existiert — statt Rückgabe von `"powershell.exe"`; (c) ggf. Absicherung, dass `FileName` am `IUpdateProcessLauncher` niemals ein relativer/Basisname ist.
- Dokumentation (Lifecycle-Schritt 12, nicht Teil der Code-Änderung): `docs/help/programmupdate/ablauf-technisch.md` (Abschnitt „Gemeinsamer Installationspfad", Schritt 10 beschreibt den PowerShell-Start) und `docs/help/programmupdate/architektur.md` (Rolle `UpdateScriptService`) erhalten einen Hinweis auf die feste Interpreter-Auflösung.

### Nicht betroffene Artefakte

- `UpdateProcessLauncher` (`IUpdateProcessLauncher`) — erhält weiterhin nur den aufgelösten `fileName`; keine Änderung.
- `UpdatePackageService`, `UpdateService`, `UpdateOptions`, `UpdatePreparationResult` — unverändert.
- `RepositoryScriptExecutor`, `VisualStudioCodeLocator` — eigene PATH-Auflösung bleibt bestehen (siehe Scope-Ausschlüsse).
- Interfaces: keine neuen oder geänderten Verträge (`IUpdateScriptService`-Signatur bleibt); die Fehlschlag-Semantik läuft über die bestehende `InvalidOperationException`-Konvention.

## Implementierungsansatz

- Reine Verhaltensänderung innerhalb von `UpdateScriptService` — keine neue Klasse, kein neues Interface, kein neuer Erweiterungspunkt nötig.
- `GetDefaultPowerShellCandidates` liefert zwei vollqualifizierte Pfade:
  1. `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")` — wie heute bereits implementiert.
  2. PowerShell 7: `Path.Combine(<ProgramFiles>, "PowerShell", "7", "pwsh.exe")` — Quelle für `<ProgramFiles>`: `Environment.GetEnvironmentVariable("ProgramFiles")` (direkteste Entsprechung der Empfehlung `%ProgramFiles%`) oder `Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)` (verlässlicher gegen Variablenmanipulation); Entwurfsentscheidung, siehe Offene Fragen.
- `FindPowerShellExecutable` prüft Kandidaten ausschließlich per `Path.IsPathFullyQualified` + `File.Exists`; der PATHEXT-/PATH-Codepfad in `IsExecutableAvailable` wird entfernt (für diese Aufrufstelle — `PathExecutableResolver` selbst bleibt für andere Nutzer erhalten).
- Fehlschlag ohne Treffer: statt `return "powershell.exe"` eine `InvalidOperationException` mit aussagekräftiger Meldung (z. B. „Kein PowerShell-Interpreter unter den festen Installationspfaden gefunden.") — sie läuft über die bestehende Fehlerkette (`StartScriptAsync` → `UpdateService.StartPreparedUpdateAsync` → `MainWindowUpdateFlow` → `SetError`/Dialog), sodass weder `Shutdown()` noch ein Prozessstart erfolgt.
- Der injizierbare `powerShellCandidates`-Parameter des Testkonstruktors bleibt bestehen (Annahme), um die Auflösung hermetisch testen zu können; die Injektion unterliegt derselben Vollqualifiziert-Prüfung, damit keine relative Umgehung möglich ist (Annahme/Entwurfsentscheidung).
- Abhängigkeiten: keine neuen; die Änderung ist auf `Infrastructure.Services.Updates` lokal begrenzt.

## Konfiguration

- **Keine Konfigurierbarkeit vorgesehen:** Die Kandidatenliste ist eine Sicherheitsfestlegung und wird fest im Code gehalten (Empfehlung des Reviews: „auf feste Kandidaten beschränken"). Es wird weder ein `UpdateOptions`-Eintrag noch eine Anwendungseinstellung (`AppEinstellungen`) noch eine UI-Option ergänzt.
- Einzige Konfigurationsfläche bleibt der bestehende Testkonstruktor (`powerShellCandidates`) für Unit-Tests — keine Laufzeitkonfiguration.

## Offene Fragen

1. **Verhalten bei fehlendem festen Kandidaten:** Annahme ist ein kontrollierter Abbruch des Update-Versuchs mit Fehlermeldung (statt des bisherigen `"powershell.exe"`-Fallbacks). Ist das so gewünscht, oder soll — da Windows PowerShell 5.1 fester Windows-Bestandteil ist und `SpecialFolder.System` praktisch immer liefert — der zweite Kandidat (`pwsh`) überhaupt nur als Zusatzoption gelten?
2. **Quelle für `%ProgramFiles%`:** Soll der Pfad über `Environment.GetEnvironmentVariable("ProgramFiles")` (wortgetreue Umsetzung der Empfehlung) oder über `Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)` (robuster gegenüber manipulierten Umgebungsvariablen) aufgelöst werden? Sicherheitsmotivation spricht für Letzteres — beide Varianten sind plausibel.
3. **Reihenfolge der Kandidaten:** Bleibt Windows PowerShell 5.1 der bevorzugte Interpreter (Kompatibilität, immer vorhanden), PowerShell 7 nur Fallback — oder soll pwsh bevorzugt werden, wenn installiert? Annahme: bestehende Reihenfolge bleibt.
4. **Weitere feste Kandidaten:** Sollen neben den zwei empfohlenen Pfaden weitere feste Orte aufgenommen werden (z. B. `%ProgramFiles%\PowerShell\7-preview\pwsh.exe`, 32-bit-`ProgramFiles(x86)`)? Annahme: nein — exakt die zwei genannten Pfade, Erweiterungen wären ein neuer Befund.
5. **Härtung des Testkonstruktors:** Soll die injizierbare Kandidatenliste zusätzlich validiert werden (nur vollqualifizierte Pfade zulässig), damit auch per DI/Test keine unqualifizierten Namen eingebracht werden können?
