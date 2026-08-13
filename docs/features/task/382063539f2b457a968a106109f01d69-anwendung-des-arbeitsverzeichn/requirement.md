# Anforderung

## Fachliche Zusammenfassung

Bei der Ausführung von KI-Plugins und beim Öffnen von IDE/Dateiexplorer über Ribbon-Aktionen wird das konfigurierte Arbeitsunterverzeichnis eines Projekts aktuell nicht zuverlässig genutzt. Die Anforderung besteht darin, dass drei Abläufe das im `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` angegebene Unterverzeichnis konsistent anwenden: (1) Beim Start einer KI-Ausführung soll der CLI-Prozess im konfigurierten Arbeitsverzeichnis starten (Abhängigkeit: `KiAusfuehrungsService` bereits implementiert, aber Zuverlässigkeit zu prüfen); (2) Das Öffnen des Arbeitsverzeichnisses über die Ribbon-Aktion soll das konfigurierte Unterverzeichnis im Dateiexplorer anzeigen; (3) Das Starten von Visual Studio Code über die Ribbon-Aktion soll das konfigurierte Arbeitsverzeichnis als Working-Directory übergeben.

## Betroffene Klassen und Komponenten

### Logik-Services

- **`Softwareschmiede.Application.Services.KiAusfuehrungsService`** — CLI-Prozessstart für KI-Ausführungen
  - Methode: `StartCliAsync(...)` (Zeile 93–150+)
  - Status: Arbeitsverzeichnisauflösung ist bereits implementiert (Zeile 118: `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync`)
  - Prüfung erforderlich: Zuverlässigkeit und korrekte Übergabe an `kiPlugin.StartCliAsync()`

- **`Softwareschmiede.Application.Services.ArbeitsverzeichnisOeffnenService`** — Öffnet Verzeichnis im OS-Dateiexplorer
  - Methode: `Oeffne(string arbeitsverzeichnis)` (Zeile 13–20)
  - **Änderung erforderlich:** Aktuell wird nur der übergebene Pfad verwendet; Arbeitsverzeichnisauflösung muss hinzugefügt werden oder Caller muss auflösen

- **`Softwareschmiede.Application.Services.IdeOeffnenService`** — Öffnet Solution oder VS Code
  - Methode: `OeffneVisualStudioCode(string arbeitsverzeichnis)` (Zeile 41–56)
  - **Änderung erforderlich:** VSCode wird aktuell mit dem übergebenen Arbeitsverzeichnis als Argument gestartet; der Caller muss das konfigurierte Unterverzeichnis auflösen

- **`Softwareschmiede.Application.Services.WorkingDirectoryResolver`** — Auflösung effektiver Arbeitsverzeichnisse (bereits vorhanden)
  - Methode: `DetermineEffectiveWorkingDirectoryAsync(...)` (Zeile 30–53) — wird bereits für CLI und `.gitignore`/`issue.md` genutzt
  - Wird auch für Ribbon-Aktionen herangezogen

### UI ViewModels

- **`Softwareschmiede.App.ViewModels.TaskDetailViewModel`** — Aufgabendetailansicht mit Ribbon-Aktionen
  - Methode: `OeffneArbeitsverzeichnis()` (Zeile 1768–1784)
    - **Änderung erforderlich:** Nutzt aktuell `_aufgabe?.LokalerKlonPfad` ohne Arbeitsverzeichnisauflösung
  - Methode: `OeffneIdeAsync(...)` (Zeile 1786–1817) und `OeffneVisualStudioCodeFallback()` (Zeile 1819–1842)
    - **Änderung erforderlich:** Nutzen `_aufgabe?.LokalerKlonPfad` ohne Arbeitsverzeichnisauflösung für VSCode-Start

### Datenmodell-Entitäten

- **`Softwareschmiede.Domain.Entities.RepositoryStartKonfiguration`** — Projektkonfiguration
  - Eigenschaft: `WorkingDirectoryRelativePath` — das zu beachtende Unterverzeichnis (bereits vorhanden)

- **`Softwareschmiede.Domain.Entities.Aufgabe`** — Aufgabe
  - Navigation: `LokalerKlonPfad` (Repository-Root)
  - Navigation: `Projekt` → `Repositories` → `RepositoryStartKonfiguration` (optional)

### Tests

- `Softwareschmiede.Tests.Application.Services.KiAusfuehrungsServiceTests_WorkingDirectory` — Arbeitsverzeichnisauflösung für CLI (vorhanden)
- `Softwareschmiede.Tests.E2E.End2EndTest.E2E_WorkingDirectory` — E2E-Tests für Arbeitsverzeichnis-Szenarien
  - `AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E` — CLI mit konfiguriertem Arbeitsverzeichnis
  - Tests für Ribbon-Aktionen fehlen (zu erweitern/zu schreiben)

## Implementierungsansatz

### 1. KI-Ausführung (CLI-Start)

**Status:** `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync` wird bereits in Zeile 118 von `KiAusfuehrungsService.StartCliAsync` aufgerufen. Der effektive Arbeitsverzeichnis wird an `kiPlugin.StartCliAsync(effectiveWorkdir, ...)` übergeben.

**Zu prüfen:**
- Wird die effektive Arbeitsverzeichnisauflösung zuverlässig durchgeführt?
- Sind alle Parameter (`startConfig`, `gitPlugin`) korrekt vorhanden, wenn `StartCliAsync` aufgerufen wird?
- Sind die Unit-Tests und E2E-Tests ausreichend und grün?

### 2. Arbeitsverzeichnis-Ribbon-Aktion

**Änderung in `TaskDetailViewModel.OeffneArbeitsverzeichnis()`:**

```csharp
private async void OeffneArbeitsverzeichnis()
{
    if (_aufgabe?.LokalerKlonPfad is not { } lokalerKlonPfad)
        return;

    FehlerMeldung = null;

    try
    {
        // Arbeitsverzeichnis auflösen
        var startConfig = _aufgabe.Projekt?.Repositories
            .FirstOrDefault(r => r.LokalerKlonPfad == lokalerKlonPfad)
            ?.RepositoryStartKonfiguration;
        
        var effectiveWorkdir = await WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(
            lokalerKlonPfad, 
            startConfig, 
            gitPlugin: null, 
            ct: CancellationToken.None)
            .ConfigureAwait(false);

        _arbeitsverzeichnisOeffnenService.Oeffne(effectiveWorkdir);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fehler beim Öffnen des Arbeitsverzeichnisses {LokalerKlonPfad}.", lokalerKlonPfad);
        FehlerMeldung = $"Arbeitsverzeichnis konnte nicht geöffnet werden: {ex.Message}";
    }
}
```

**Abhängigkeit:** `WorkingDirectoryResolver` ist ein statischer Service, daher keine DI-Änderung erforderlich.

### 3. Visual Studio Code-Ribbon-Aktion

**Änderung in `TaskDetailViewModel.OeffneVisualStudioCodeFallback()`:**

```csharp
private async void OeffneVisualStudioCodeFallback()
{
    if (!_openVisualStudioCodeWhenNoSolutionFound)
        return;

    if (_aufgabe?.LokalerKlonPfad is not { } lokalerKlonPfad)
        return;

    FehlerMeldung = null;

    try
    {
        // Arbeitsverzeichnis auflösen
        var startConfig = _aufgabe.Projekt?.Repositories
            .FirstOrDefault(r => r.LokalerKlonPfad == lokalerKlonPfad)
            ?.RepositoryStartKonfiguration;
        
        var effectiveWorkdir = await WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(
            lokalerKlonPfad, 
            startConfig, 
            gitPlugin: null, 
            ct: CancellationToken.None)
            .ConfigureAwait(false);

        _ideOeffnenService.OeffneVisualStudioCode(effectiveWorkdir);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning(ex, "Visual Studio Code wurde für Arbeitsverzeichnis {LokalerKlonPfad} nicht gefunden.", lokalerKlonPfad);
        FehlerMeldung = "Keine Visual-Studio-Solution gefunden und Visual Studio Code wurde nicht gefunden.";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fehler beim Öffnen von Visual Studio Code für Arbeitsverzeichnis {LokalerKlonPfad}.", lokalerKlonPfad);
        FehlerMeldung = $"IDE konnte nicht geöffnet werden: {ex.Message}";
    }
}
```

**Hinweis:** Auch `OeffneIdeAsync()` ruft `OeffneVisualStudioCodeFallback()` auf, daher wird die Fallback-Änderung für beide Fälle wirksam. Falls VS Code als Fallback bei mehreren Solutions aufgerufen wird, wird die Arbeitsverzeichnisauflösung dort auch angewendet.

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Die Funktionalität baut auf der bereits existierenden `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` auf, die über die UI konfigurierbar ist (siehe `ArbeitsverzeichnisBearbeitenDialog.xaml`).

## Offene Fragen

1. **Zuverlässigkeit der KI-Ausführung (CLI-Start):** Sollte eine Regressions-E2E-Test für die Ribbon-Aktionen (Öffnen von Arbeitsverzeichnis/VSCode mit konfiguriertem Arbeitsverzeichnis) hinzugefügt werden, um sicherzustellen, dass die Funktion auch wirklich in der UI nutzbar ist?

2. **GitPlugin-Parameter bei Ribbon-Aktionen:** In `KiAusfuehrungsService.StartCliAsync` wird `gitPlugin` übergeben, um bei lokalem Repository-Modus (z. B. `LocalDirectoryPlugin.InSourceDirectory`) den echten Repository-Pfad aufzulösen. Sollte `gitPlugin` auch in den Ribbon-Aktionen (`OeffneArbeitsverzeichnis`, `OeffneVisualStudioCodeFallback`) berücksichtigt werden, oder ist ein Fallback auf `gitPlugin: null` ausreichend?

3. **Fehlerbehandlung:** Sollte eine fehlende oder ungültige Arbeitsverzeichniskonfiguration (z. B. wenn das konfigurierte Verzeichnis gelöscht wurde) zu einer aussagekräftigen Fehlermeldung in der UI führen? (Aktuell würde `ValidateWorkingDirectory` eine `DirectoryNotFoundException` werfen.)

4. **Asynchrone Auflösung in SyncMethoden:** `OeffneArbeitsverzeichnis()` ist nicht async, aber `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` ist async (insbesondere wegen `gitPlugin?.ResolveEffectiveRepositoryPathAsync()`). Sollte `OeffneArbeitsverzeichnis()` zu einer async-Methode umgewandelt werden, oder sollte die Auflösung synchronisiert werden (mit `GetAwaiter().GetResult()`)?
