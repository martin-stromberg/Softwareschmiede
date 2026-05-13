# Anforderungsanalyse – LocalDirectoryPlugin (Projekt-Repository-Linking)

> **Dokument-Typ:** Requirements Analysis  
> **Status:** ✅ Umsetzung verifiziert (Code- und Testabgleich)  
> **Version:** 1.3.0  
> **Datum:** 2026-05-13

---

## 1. Überblick und Projektkontext

Das Feature adressiert die projektbezogene Repository-Verknüpfung für unterschiedliche SCM-Plugins. Ziel ist eine plugin-gesteuerte Eingabemaske mit Standardplugin-Vorauswahl, konsistenter Feldvalidierung und verständlicher UI-Darstellung für Workspace-Optionen.

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **FR-1** | **SCM-Plugin-Auswahl im Projekt:** Beim Verknüpfen ist ein SourceCode-Plugin auswählbar und kein GitHub-Hardcoding mehr vorhanden. → [Blueprint](../architecture/lokales-verzeichnis-plugin-architecture-blueprint.md) | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-2** | **Standardplugin-Vorauswahl:** Auswahl nutzt bestehende Default-Auflösung über `PluginSelectionService`. | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-3** | **Dynamische Repository-Felder:** Feldschema kommt aus `GetRepositoryLinkFields()` des aktiven Plugins. | UX / Accessibility | MUST HAVE | ✅ Umgesetzt |
| **FR-4** | **LocalDirectory Pflichtfeld:** Für `LocalDirectoryPlugin` ist `SourceDirectory` beim Linking Pflicht. | Datenverwaltung | MUST HAVE | ✅ Umgesetzt |
| **FR-5** | **GitHub Pflichtfelder:** Für GitHub sind `RepositoryUrl` und `RepositoryName` Pflichtfelder. | Datenverwaltung | MUST HAVE | ✅ Umgesetzt |
| **FR-6** | **WorkspaceMode verständlich anzeigen:** UI rendert fachliche Labels statt technischer Enum-Namen. | UX / Accessibility | MUST HAVE | ✅ Umgesetzt |
| **FR-7** | **Kein WorkingDirectory-Pluginsetting:** LocalDirectory-Einstellungen enthalten kein konfigurierbares `WorkingDirectory` mehr. | Wartbarkeit | MUST HAVE | ✅ Umgesetzt |

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---|---|---|---|---|
| **NFR-1** | Rückwärtskompatible Default-Auflösung für SCM-Standardplugin. | Zuverlässigkeit | MUST HAVE | ✅ Umgesetzt |
| **NFR-2** | Konsistente Validierung je Plugin ohne Feldmischung. | Robustheit | MUST HAVE | ✅ Umgesetzt |
| **NFR-3** | Keine technischen WorkspaceMode-Bezeichner in der UI. | UX / Accessibility | MUST HAVE | ✅ Umgesetzt |
| **NFR-4** | Dynamische Feldumschaltung ohne merkliche Verzögerung. | Performance | HIGH | 🔄 In Arbeit (noch ohne expliziten Performance-Nachweis) |

## 4. Akzeptanzkriterien

- **AC-1:** Repository-Dialog zeigt ein SCM-Plugin-Auswahlfeld und lädt pluginabhängige Eingabefelder.
- **AC-2:** Für LocalDirectory wird `SourceDirectory` als Pflichtfeld erzwungen.
- **AC-3:** Für GitHub werden `RepositoryUrl` und `RepositoryName` als Pflichtfelder erzwungen.
- **AC-4:** WorkspaceMode-Optionen werden in der UI als fachliche Labels angezeigt.
- **AC-5:** `WorkingDirectory` wird im LocalDirectoryPlugin nicht als Einstellungsfeld angezeigt.

## 5. Annahmen und Abhängigkeiten

| Typ | Eintrag | Bewertung |
|---|---|---|
| Abhängigkeit | `IGitPlugin.GetRepositoryLinkFields()` liefert konsistente Felddefinitionen | Erfüllt |
| Abhängigkeit | `PluginSelectionService` bleibt zentrale Auflösung für Standardplugin | Erfüllt |
| Annahme | Persistenz bleibt vorerst auf `GitRepository` fokussiert | Bestätigt |

## 6. Scope und Out-of-Scope

### In-Scope ✅
- Plugin-gesteuerte Repository-Verknüpfung in `ProjektDetail`
- Default-SCM-Plugin-Vorauswahl
- Pflichtfeldvalidierung je Plugin
- WorkspaceMode-Label-Mapping in Einstellungen

### Out-of-Scope ❌
- Neue SCM-Plugin-Typen
- Umstellung auf neue Datenbanktabellen für dynamische Feldwerte
- Vollständiger UI-Redesign der Seiten

## 7. Domänenmodell und Glossar

- **Repository-Linking:** Prozess zur Zuordnung eines Projekts zu einem SCM-Repository inkl. pluginabhängiger Felder.
- **PluginPrefix:** Technische Plugin-ID, die im Projekt gespeichert wird.
- **WorkspaceModeDisplayMap:** UI-Mapping technischer Enum-Werte auf verständliche Labels.

## 8. Nutzungsfälle

- **UC-1:** Nutzer öffnet Repository-Dialog, Standardplugin ist vorausgewählt, Felder werden dynamisch geladen.
- **UC-2:** Nutzer wählt LocalDirectory, gibt `SourceDirectory` ein und speichert erfolgreich.
- **UC-3:** Nutzer sieht WorkspaceMode als verständliche Optionen in den Einstellungen.

## 9. Nächste Schritte

1. Performance-Nachweis für dynamische Feldumschaltung explizit messen.
2. Optionale End-to-End-UI-Interaktionstests ergänzen.
3. Entscheidung dokumentieren, ob dynamische Feldwerte langfristig normalisiert persistiert werden.

## 10. Approval & Versionierung

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.3.0 | 2026-05-13 | GitHub Copilot Agent | Status auf „Umsetzung verifiziert“ aktualisiert; FR/NFR/AC mit Codeabgleich konsolidiert |
| 1.2.0 | 2026-05-13 | GitHub Copilot Agent | Erweiterte Planung für plugin-gesteuertes Repository-Linking |

