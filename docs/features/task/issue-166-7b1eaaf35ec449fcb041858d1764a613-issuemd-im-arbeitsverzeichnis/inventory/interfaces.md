# Interfaces

## `IArbeitsverzeichnisResolver`
Datei: `src/Softwareschmiede/Domain/Interfaces/IArbeitsverzeichnisResolver.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ResolveAsync` | `CancellationToken ct = default` | `Task<ArbeitsverzeichnisResolutionResult>` | Löst das effektive Basis-Arbeitsverzeichnis zur Laufzeit auf. |

### Zweck und Kontext

Das Interface wird von `EntwicklungsprozessService` verwendet, um das Basis-Arbeitsverzeichnis für Klon-Operationen aufzulösen (z.B. Temp-Ordner oder konfigurierter Pfad). Es wird aufgerufen in der `PrepareCloneDirectoryAsync`-Methode (Zeile 419).

**Rückgabewert:** `ArbeitsverzeichnisResolutionResult` enthält das aufgelöste Pfad sowie Informationen darüber, ob ein Fallback verwendet wurde.

---

## `IGitPlugin`
Datei: Verschiedene (verwendet von `EntwicklungsprozessService` und `GitOrchestrationService`)

Nicht Teil der direkten Anforderung, aber verwendet für Git-Operationen (Clone, Branch, Commit, Push, Pull, etc.).

---

## Weitere Interfaces (optional, für Kontext)

- **`IKiPlugin`:** Für KI-Automation (Pseudo-Console Start).
- **`IPluginManager`:** Für Plugin-Selektion.
