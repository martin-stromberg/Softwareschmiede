# Enums

## `ProjektStatus`
Datei: `src/Softwareschmiede/Domain/Enums/ProjektStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Aktiv` | Projekt ist aktiv in Bearbeitung |
| `Archiviert` | Projekt wurde archiviert |

### Zweck
Definiert den Status eines Projekts. Wird z.B. bei `ArchivierenAsync()` verwendet, um ein Projekt als archiviert zu kennzeichnen.

---

## `AufgabenFilterTyp`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabenFilterTyp.cs` (aus ViewModel referenziert)

Enum für Filterung von Aufgaben nach Status. Wird in `ProjectDetailViewModel` verwendet, um gefilterte Aufgabenlisten zu verwalten (nicht direkt für Repository-Suggestion relevant, aber im gleichen ViewModel vorhanden).

---

## `PluginType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

Definiert Plugin-Kategorien, z.B. `SourceCodeManagement`, `DevelopmentAutomation`. Wird vom `IPluginManager` verwendet, um Plugins zu klassifizieren.
