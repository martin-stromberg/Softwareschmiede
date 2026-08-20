# Interfaces und Contracts

## `IIdePlugin`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs`

Interface für IDE-Plugins. Definiert die Contract für die Kompatibilitätsprüfung und Öffnung von Repositories in IDEs.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckCompatibilityAsync` | `repositoryPath: string`, `ct: CancellationToken` | `Task<IdePluginCompatibility>` | Prüft, ob das Plugin das Repository unterstützt (Explicit/Fallback/None) |
| `OpenRepositoryAsync` | `repositoryPath: string`, `ct: CancellationToken` | `Task` | Öffnet das Repository in der IDE (Legacy-Methode, wird durch `OpenEntryPointAsync` ersetzt) |
| `FindEntryPointsAsync` | `repositoryPath: string`, `ct: CancellationToken` | `Task<IReadOnlyList<IdeEntryPoint>>` | **Kern-Methode:** Ermittelt alle verfügbaren Einstiegspunkte für das Repository (z. B. `.sln`-Dateien) |
| `OpenEntryPointAsync` | `entryPoint: IdeEntryPoint`, `ct: CancellationToken` | `Task` | **Kern-Methode:** Öffnet einen spezifischen Einstiegspunkt in der IDE |

**Notizen:**
- Das Interface erbt von `IPlugin` (basis Plugin-Interface mit `PluginPrefix` und `PluginName`).
- `IdeEntryPoint` ist ein Record mit `Path` und optionalem `DisplayName`.
- Die Methoden `FindEntryPointsAsync` und `OpenEntryPointAsync` sind neu für die Mehrfach-Einstiegspunkt-Unterstützung.
- `OpenRepositoryAsync` ist Legacy und wird nicht mehr verwendet.

---

## `IDialogService`
Datei: `src/Softwareschmiede.App/Services/IDialogService.cs`

Service zur Abstraktion von UI-Dialogen für das MVVM-Muster.

### Relevante Methoden

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ShowSolutionSelectionDialogAsync` | `solutionPfade: IReadOnlyList<string>`, `ct: CancellationToken` | `Task<string?>` | Zeigt einen Dialog zur Auswahl einer Solution-Datei aus einer Liste; gibt den gewählten Pfad oder `null` bei Abbruch zurück |

**Notizen:**
- Diese Methode wird derzeit von `TaskDetailViewModel.OeffneIdeAsync` aufgerufen, um den Anwender zwischen mehreren `IdeEntryPoint`-Pfaden wählen zu lassen.
- Sie arbeitet nur mit Pfad-Strings; hat keinen Zugriff auf die vollständigen `IdeEntryPoint`-Objekte (inkl. `DisplayName` und Plugin-Info).
- Ein Dialog-ViewModel existiert nicht speziell für IDE-Auswahl; die Logik ist derzeit in `TaskDetailViewModel` inline implementiert.

