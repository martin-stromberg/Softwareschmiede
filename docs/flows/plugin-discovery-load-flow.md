# Ablauf – Plugin-Discovery und Laden

## Kontext

Beim Start registriert der Host den `PluginManager`.  
Die eigentliche Discovery läuft lazy beim ersten Zugriff auf `IGitPlugin` oder `IKiPlugin`.

## Flow

```mermaid
flowchart TD
    A[Host startet] --> B[Program.cs registriert IPluginManager]
    B --> C[Erster Zugriff auf IGitPlugin/IKiPlugin]
    C --> D[PluginManager.EnsureInitialized]
    D --> E{plugins Ordner vorhanden?}
    E -- Nein --> E1[Warnung loggen und Ende]
    E -- Ja --> F[Alle *.dll aus AppBase/plugins scannen]
    F --> G[Assembly laden]
    G --> H{Ladbar?}
    H -- Nein --> H1[Warnung loggen und DLL überspringen]
    H -- Ja --> I[Exportierte Plugin-Typen finden]
    I --> J[Instanz per ActivatorUtilities erzeugen]
    J --> K{PluginType + Interface passen?}
    K -- SCM --> L[Als IGitPlugin registrieren]
    K -- DevAutomation --> M[Als IKiPlugin registrieren]
    K -- Ungültig --> N[Warnung loggen und überspringen]
    L --> O[Weiter mit nächster DLL]
    M --> O
    N --> O
    O --> P[Discovery abgeschlossen]
```

## Fehlerverhalten

- Defekte oder nicht ladbare DLLs blockieren den Start nicht.
- Ungültige Plugin-Typ/Interface-Kombinationen werden übersprungen.
- Fehlt ein Default-Plugin, wirft `GetDefault...` eine `InvalidOperationException`.

## Relevante Dateien

- `src/Softwareschmiede/Program.cs`
- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
- `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`
- `src/Softwareschmiede/Softwareschmiede.csproj`
