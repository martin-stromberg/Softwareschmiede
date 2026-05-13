# Architektur-Review – LocalDirectoryPlugin

> **Dokument-Typ:** Architektur-Review  
> **Status:** ✅ Freigabe mit Restpunkten (keine Blocker)  
> **Version:** 1.3.0  
> **Datum:** 2026-05-13

---

## 1. Executive Summary

Die Planungsartefakte sind mit dem aktuellen Codezustand konsistent. Zentrale Zielpunkte (plugin-gesteuertes Linking, Default-Plugin-Auflösung, Pflichtfeldvalidierung, WorkspaceMode-Label-Mapping, Entfall `WorkingDirectory`) sind umgesetzt.

**Gesamturteil:** ✅ **Freigabe erteilt**

## 2. Bewertete Fokusbereiche

| Bereich | Bewertung | Hinweis |
|---|---|---|
| SCM-Plugin-Auswahl im Projektdialog | ✅ | `ProjektDetail` nutzt Pluginliste + Selektion |
| Standardplugin-Vorauswahl | ✅ | Auflösung über `PluginSelectionService` |
| Dynamisches Feldschema | ✅ | `ApplyRepositoryFieldSchema(...)` aktiv |
| Pflichtfeldregeln je Plugin | ✅ | UI- und Service-Validierung vorhanden |
| WorkspaceMode-UI-Mapping | ✅ | Verständliche Labels in `EinstellungenBase` |
| Entfall WorkingDirectory-Setting | ✅ | Kein entsprechendes Setting-Feld im LocalDirectoryPlugin |

## 3. Findings (priorisiert)

| ID | Priorität | Finding | Empfehlung |
|---|---|---|---|
| F-01 | Major | Für dynamischen Feldwechsel fehlt ein expliziter Performance-Benchmark. | Messbare UI-Latenzgrenze (z. B. <200ms) per Test/Monitoring nachziehen. |
| F-02 | Minor | Persistenz dynamischer Zusatzfelder bleibt auf Kernfelder verdichtet. | Bei wachsender Plugin-Komplexität optional normalisierte Feldwert-Tabelle prüfen. |

## 4. Nachweise (Code)

- `src/Softwareschmiede/Components/Pages/Projekte/ProjektDetail.razor.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Components/Pages/Einstellungen.razor.cs`
- `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
- `src/Softwareschmiede.Tests/Components/Pages/Projekte/ProjektDetailRepositoryFormTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/ProjektServiceTests.cs`

## 5. Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.3.0 | 2026-05-13 | GitHub Copilot Agent | Review auf As-Built aktualisiert; Blocker geschlossen, Restpunkte neu priorisiert |
| 1.2.0 | 2026-05-13 | GitHub Copilot Agent | Delta-Review gegen alten Ist-Zustand |

