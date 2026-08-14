# Offene Aufgaben

Erstellt am: 2026-08-14
Aktualisiert am: 2026-08-14
Anlass: Follow-up-Idee des Anwenders nach Abschluss und Commit des IDE-Plugin-Features (Commit `4eaca30`). **Kein Fehler, kein Blocker** — die aktuelle Implementierung funktioniert und ist bereits gemergt/committed. Dies ist ein optionaler Architektur-Verbesserungsvorschlag für einen künftigen Lifecycle-Lauf.

## Befund: Type-Check auf `VisualStudioIdePlugin` in `IdeOeffnenService.OpenRepositoryInIdeAsync`

`src/Softwareschmiede/Application/Services/IdeOeffnenService.cs:60` prüft explizit `plugin is VisualStudioIdePlugin`, um zu entscheiden, ob bei mehreren gefundenen Solution-Dateien ein Auswahl-Dialog angezeigt werden soll:

```csharp
if (plugin is VisualStudioIdePlugin && waehleSolutionAsync is not null)
{
    var solutionPfade = FindeSolutions(repositoryPath);
    if (solutionPfade.Count > 1)
    {
        var solutionPfad = await waehleSolutionAsync(solutionPfade, ct);
        ...
```

**Grund für den aktuellen Zustand:** `IIdePlugin.OpenRepositoryAsync(repositoryPath, ct)` kennt nur einen einzigen Öffnen-Vorgang ohne Rückfrage — kein Konzept für "mehrere mögliche Einstiegspunkte, aus denen der Anwender wählen soll". Die Auswahl-Dialog-Logik für mehrere Solutions konnte deshalb nicht in `VisualStudioIdePlugin.OpenRepositoryAsync` selbst wandern, weil Plugins in `Domain/PluginImpl/` bewusst keine UI-Abhängigkeit (`IDialogService`) haben — nur `IProzessStarter`. Der Type-Check in `IdeOeffnenService` ist ein pragmatischer, aber leaky Workaround, um dieses eine Szenario (Visual Studio mit mehreren `.sln`/`.slnx`) ohne Erweiterung des `IIdePlugin`-Contracts zu unterstützen.

## Vorschlag: generischer Mehrfach-Einstiegspunkt-Contract

- [ ] `IIdePlugin` um eine generische Möglichkeit erweitern, mehrere Einstiegspunkte zu ermitteln und darunter auswählen zu lassen, z. B.:
  - `Task<IReadOnlyList<IdeEntryPoint>> FindEntryPointsAsync(string repositoryPath, CancellationToken ct)` — liefert 0..n Kandidaten (bei `VisualStudioIdePlugin`: die gefundenen `.sln`/`.slnx`-Pfade; bei `VisualStudioCodeIdePlugin`: immer genau 1 Kandidat, das Repository-Root selbst).
  - `Task OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct)` (oder Überladung von `OpenRepositoryAsync`, die einen konkreten Einstiegspunkt statt nur den Repository-Pfad entgegennimmt).
- [ ] `IdeOeffnenService.OpenRepositoryInIdeAsync` entsprechend generalisieren: immer `FindEntryPointsAsync` aufrufen → bei genau 1 Kandidat direkt öffnen, bei >1 den `waehleSolutionAsync`-artigen Callback aufrufen (ggf. umbenennen zu `waehleEntryPointAsync`) → gewählten Kandidaten öffnen. Der `is VisualStudioIdePlugin`-Check entfällt vollständig.
- [ ] Prüfen, ob `PluginSelectionServiceTests_IdePlugin.cs`, `IdeOeffnenServiceTests.cs`, `VisualStudioIdePluginTests.cs`, `VisualStudioCodeIdePluginTests.cs` und `TaskDetailViewModelTests_VisualStudioCode.cs` entsprechend angepasst werden müssen.
- [ ] `docs/help/entwicklungsumgebungen/architektur.md` und `ablauf-technisch.md` (beschreiben aktuell den `is VisualStudioIdePlugin`-Sonderfall explizit) entsprechend aktualisieren.

**Abwägung (bereits mit dem Anwender besprochen, noch keine Entscheidung getroffen):** Jedes IDE-Plugin — auch triviale wie Visual Studio Code, das nie mehr als einen Einstiegspunkt hat — müsste künftig zwei Methoden statt einer implementieren, für ein Szenario, das aktuell nur Visual Studio betrifft. Nutzen: Erweiterbarkeit für künftige Plugins mit mehreren Kandidaten (z. B. mehrere `.csproj`/Workspace-Dateien) ohne erneuten Type-Check-Workaround.

## Nächster Schritt

Kein akuter Handlungsbedarf. Bei erneutem `/lifecycle`-Lauf auf diesem Branch (oder einem Folge-Branch) diesen Punkt als Ausgangspunkt für Anforderungsübersetzung/Planung verwenden, falls der Anwender die Idee tatsächlich umsetzen möchte.
