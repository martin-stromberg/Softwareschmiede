# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### EntwicklungsprozessService.cs (EntwicklungsprozessService)

- **Doppelter Code / Fehlende Kapselung** — Die beiden geänderten Methoden `CreateIssueFileAsync` (Zeilen 616–618) und `UpdateGitignoreAsync` (Zeilen 638–640) enthalten denselben zweizeiligen Block zur Auflösung und Erstellung des effektiven Arbeitsverzeichnisses:

  ```csharp
  var effektivesVerzeichnis = WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(
      lokalerKlonPfad, startKonfiguration?.WorkingDirectoryRelativePath);
  Directory.CreateDirectory(effektivesVerzeichnis);
  ```

  Beide Methoden werden in `FinalizeStartAsync` unmittelbar nacheinander (Zeilen 502–503) mit denselben Argumenten (`lokalerKlonPfad`, `repository.StartKonfiguration`) aufgerufen, sodass die identische Auflösung zweimal ausgeführt und der Codeblock wörtlich dupliziert wird.

  Empfehlung: Die Auflösung in eine private Hilfsmethode auslagern, z. B. `private static string EnsureEffectiveWorkingDirectory(string lokalerKlonPfad, RepositoryStartKonfiguration? startKonfiguration)`, die `ResolveEffectiveWorkingDirectory` aufruft, das Verzeichnis anlegt und den Pfad zurückgibt. Beide Methoden rufen dann nur noch diese Hilfsmethode auf. Alternativ das effektive Verzeichnis einmal in `FinalizeStartAsync` ermitteln und als Parameter an beide Methoden übergeben, um die doppelte Auflösung zu vermeiden.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
