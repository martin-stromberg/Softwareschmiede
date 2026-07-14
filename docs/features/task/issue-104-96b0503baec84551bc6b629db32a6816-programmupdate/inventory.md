# Bestandsaufnahme - Programmupdate

## Kurzfazit

Die Anwendung hat bereits eine passende WPF-Shell mit linker Sidebar, eine DI-basierte Service-Architektur und eine vergleichsweise gute CLI-Laufstatus-Infrastruktur. Der Update-Button kann technisch am unteren Rand der bestehenden Sidebar ergaenzt und an ein neues ViewModel-Command gebunden werden. Fuer die Sicherheitsabfrage kann `IDialogService.BestaetigenDialog` wiederverwendet werden.

Fuer die eigentliche Update-Funktion fehlen jedoch zentrale Bausteine: Es gibt keinen Update-Service, keinen HTTP/GitHub-Release-Client, keine lokale Versionsquelle in der WPF-Assembly, keinen Download-/Entpack-/Austauschprozess und kein vorhandenes Updater-Skript. Die Release-Pipeline erzeugt bereits ein GitHub-Release-Asset `release.zip`; dieses ist der naheliegende Download-Kandidat.

## Detaildokumente

- [WPF-Shell, Sidebar und MainWindowViewModel](inventory/wpf-shell-sidebar.md)
- [CLI-Ausfuehrung und Laufstatus](inventory/cli-runtime-status.md)
- [Release, Publish und Versionierung](inventory/release-publish-versioning.md)
- [HTTP- und GitHub-Release-Zugriff](inventory/http-github-release-access.md)
- [DI, Tests und Testluecken](inventory/di-tests.md)
- [Update-Ablauf: vorhandene Anknuepfpunkte und fehlende Bausteine](inventory/update-flow-gaps.md)

## Relevante vorhandene Anknuepfpunkte

| Bereich | Befund | Relevante Dateien |
|---|---|---|
| WPF-Shell/Sidebar | Sidebar ist direkt in `MainWindow.xaml` aufgebaut; aktuell nur Toggle, Dashboard, Projekte, Einstellungen und aktive Aufgaben. Kein Footer-Bereich vorhanden. | `src/Softwareschmiede.App/Views/MainWindow.xaml` |
| MainWindowViewModel | Zentrale Navigation, aktive Aufgabenliste, Timer/Event-Refresh, DI-Zugriff auf ViewModels. Kein Update-State/Command vorhanden. | `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` |
| CLI-Laufstatus | `KiAusfuehrungsService` verwaltet laufende Prozesse; `CliProcessManager` persistiert `Aufgabe.AktiveRunId`, Heartbeat und `Aufgabe.LaufStatus`. | `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`, `src/Softwareschmiede/Application/Services/CliProcessManager.cs` |
| Wartet-auf-Eingabe | `PseudoConsoleSession` leitet `CliRuntimeStatus.WartetAufEingabe` aus fehlender I/O-Aktivitaet ab; Domain-Substatus ist `AufgabeLaufStatus.WartetAufEingabe`. | `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`, `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs` |
| Aktive Aufgaben | `AufgabeService.GetAktiveAufgabenAsync()` liefert aktive/wartende Aufgaben inkl. `LaufStatus`; geeignet fuer die Update-Risikopruefung, wenn die Regel konkretisiert wird. | `src/Softwareschmiede/Application/Services/AufgabeService.cs` |
| Dialoge | `IDialogService.BestaetigenDialog` zeigt Yes/No-Warnungen und ist bereits in DI registriert. | `src/Softwareschmiede.App/Services/IDialogService.cs`, `src/Softwareschmiede.App/Services/WpfDialogService.cs` |
| Release-Artefakt | GitHub Actions erstellt `release.zip`, benennt `Softwareschmiede.App.exe` im Publish nach `Softwareschmiede.exe` um und prueft Plugins. | `.github/workflows/release.yml`, `.github/actions/build-and-package/action.yml` |
| Versionierung | Semantic Release schreibt `CHANGELOG.md` und `package.json`, erstellt Tags/Releases. WPF-Projekt hat keine explizite Assembly-/File-/Informational-Version. | `.releaserc.json`, `package.json`, `src/Softwareschmiede.App/Softwareschmiede.App.csproj` |
| DI | Services und ViewModels werden zentral in `App.xaml.cs` registriert. Neue Update-Services koennen dort registriert werden. | `src/Softwareschmiede.App/App.xaml.cs` |

## Wichtigste Planungsimplikationen

1. Lokale Versionsquelle festlegen: Aktuell ist `package.json` fuer Semantic Release relevant, aber die WPF-App traegt keine Release-Version in Assembly-Metadaten. Fuer einen robusten Vergleich braucht der Build eine Version in `Softwareschmiede.App` oder eine mitgelieferte Manifestdatei.
2. GitHub-Zugriff neu bauen: Bestehender GitHub-Code nutzt das `gh` CLI fuer Projekt-/Repository-Funktionen. Fuer In-App-Update ist ein direkter `HttpClient`-basierter Release-Client naheliegender, damit Updates ohne Benutzer-GitHub-Token funktionieren.
3. Sicherheitsabfrage auf persistierten Status stuetzen: Kritisch sind aktive Aufgaben mit laufendem Prozess und `LaufStatus != WartetAufEingabe`. `IRunningAutomationStatusSource` reicht dafuer allein nicht aus; `AufgabeService.GetAktiveAufgabenAsync()` oder ein neuer Status-Query-Service ist geeigneter.
4. Sidebar-Layout umbauen: Der Update-Button soll unten erscheinen. Das bestehende Grid hat nur Header und Scrollbereich; ein eigener Footer-Row ist erforderlich.
5. Updater muss extern laufen: Die App kann ihre eigenen Dateien nicht verlaesslich ersetzen. Ein PowerShell-Skript oder kleiner externer Updater-Prozess muss vorbereitet, gestartet und mit App-PID, Zielverzeichnis, Temp-Verzeichnis und Exe-Pfad parametriert werden.
6. Tests sollten auf Services statt echte GitHub-/Dateiaustausch-Nebenwirkungen zielen: HTTP ueber fake Handler, Download/Entpacken mit Temp-Verzeichnissen, Skriptgenerator als String-/File-Output-Test, ViewModel-Kommandos mit Mock-Services.

## Offene technische Entscheidungen

- Welche lokale Versionsquelle gilt verbindlich: AssemblyInformationalVersion, FileVersion oder eine Release-Manifestdatei im Publish?
- Soll `release.zip` immer exakt der Asset-Name sein, oder soll der Client nach einem Muster suchen?
- Soll der Update-Check nur beim Start oder periodisch laufen?
- Soll der finale Dateiaustausch erhoehte Rechte unterstuetzen oder nur beschreibbare Programmverzeichnisse?
- Wie umfangreich soll Rollback sein: Backup + Wiederherstellung oder nur kontrollierter Fehlerzustand mit Log?
