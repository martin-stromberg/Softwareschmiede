# Enums – Bestandsaufnahme IDE-Plugin-System

## Plugin-Kategorisierung

### `PluginKategorie`
Datei: `src/Softwareschmiede/Domain/Enums/PluginKategorie.cs`

Kategorisiert Plugin-Typen für interne Verwaltung.

| Wert | Bedeutung |
|------|-----------|
| `Git` | Git-Provider Plugin (z.B. GitHub, GitLab) |
| `Ki` | KI-Plugin (z.B. GitHub Copilot, Claude CLI) |

**Zu erweitern laut Anforderung:**
- `Ide` – IDE-Integration Plugin (z.B. Visual Studio, Visual Studio Code)

---

### `PluginType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

Plugin-Typen für Discovery und Registry im Host.

| Wert | Bedeutung |
|------|-----------|
| `SourceCodeManagement` | Source-Code-Management-Plugin (entspricht `Git` in `PluginKategorie`) |
| `DevelopmentAutomation` | Development-Automation-Plugin (entspricht `Ki` in `PluginKategorie`) |

**Hinweis:** `PluginType` wird vom `PluginManager` bei der Plugin-Registrierung verwendet (`switch`-Statement in `TryCreateAndRegister`). Es ist unklar, ob für IDE-Plugins ein neuer `PluginType`-Wert erforderlich ist oder ob sie unter einen bestehenden fallen. Die Anforderung spezifiziert dies nicht explizit.

---

## Zu implementierende neue Enums

### `IdePluginCompatibility` (NEU)
Laut Anforderung zu erstellen in: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/IdePluginCompatibility.cs` (oder als ValueObject)

Prüfergebnis für IDE-Plugin-Kompatibilität.

| Wert | Bedeutung |
|------|-----------|
| `Explicit` | IDE ist explizit kompatibel (z.B. `.sln` gefunden) – höchste Priorität |
| `Fallback` | IDE wird als Rückfall verwendet – wird nur verwendet, wenn kein `Explicit` verfügbar ist |
| `Incompatible` | IDE ist nicht kompatibel (wird nicht berücksichtigt) – wird von `ResolveIdePluginAsync()` ignoriert |

