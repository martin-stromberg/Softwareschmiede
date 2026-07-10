# Plugin-Metadaten

## Gespeicherte Plugin-Informationen

SCI/SCM-Plugin:

- Das Repository speichert den Plugin-Typ/Prefix in `GitRepository.PluginTyp`.
- Quelle: `src/Softwareschmiede/Domain/Entities/GitRepository.cs:13`

KI-Plugin:

- Die Aufgabe speichert den KI-Plugin-Prefix in `Aufgabe.KiPluginPrefix`.
- Quelle: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs:39`

## Anzeigenamen

Alle Plugins implementieren `IPlugin`:

- Anzeigename `PluginName`: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs:14`
- eindeutiger Prefix `PluginPrefix`: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs:20`
- Plugin-Typ: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IPlugin.cs:29`

Fuer die Aufgabenpanels ist wahrscheinlich `PluginName` der richtige sichtbare Text. Falls das gespeicherte Plugin aktuell nicht geladen ist, sollte der gespeicherte Prefix als Fallback angezeigt werden.

## Plugin-Aufloesung

`PluginManager` liefert geladene Plugins:

- SCM-Plugins: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs:38`
- KI-Plugins: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs:45`
- Registrierung nutzt `PluginName`: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs:183` und `:188`

`PluginSelectionService` kann vorhandene Prefixe auf konkrete Plugin-Instanzen mappen:

- SCM-Aufloesung: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs:41`
- KI-Aufloesung: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs:68`
- KI-Prefix mit Projektkontext: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs:87`

Fuer eine reine Anzeige in der Liste sollte kein Default-Fallback als scheinbarer Aufgabenwert angezeigt werden, wenn an der Aufgabe kein KI-Plugin gespeichert ist. Die Anforderung verlangt pro Aufgabe korrekte Werte, nicht globale Defaults. Falls ein Fallback fachlich gewuenscht ist, sollte der Text dies nicht verschleiern.

## Query-Bedarf fuer die Seitenleiste

`GetAktiveAufgabenAsync` laedt aktuell nur `Projekt`:

- `src/Softwareschmiede/Application/Services/AufgabeService.cs:540`

Fuer das SCI/SCM-Plugin muss zusaetzlich `GitRepository` geladen werden, weil `PluginTyp` dort liegt. Alternativ kann die Service-Schicht ein dediziertes DTO erzeugen:

```csharp
public sealed record AktiveAufgabePanelItem(
    Guid Id,
    string Titel,
    string ProjektName,
    string? ScmPluginName,
    string? KiPluginName,
    bool IsAktiv,
    DateTimeOffset? LetzterCliStartUtc,
    string StatusText);
```

Ein DTO reduziert XAML-Logik, verhindert Plugin-Aufloesung im Control und macht Tests direkter.

## Namenskonflikt "SCI"

Im Code heisst die Kategorie Source-Code-Management, abgekuerzt SCM:

- `PluginType.SourceCodeManagement` wird in `PluginSelectionService.ResolveSourceCodeManagementPluginAsync` verwendet: `src/Softwareschmiede/Application/Services/PluginSelectionService.cs:45`

Die Anforderung nennt "SCI-Plugin". Das sollte in der Planung als SCM-/Source-Code-Management-Plugin interpretiert werden.

