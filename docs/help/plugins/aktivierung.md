← [Zurück zur Übersicht](index.md)

# Plugin-System — Aktivierung und Deaktivierung

## Zweck

Das Aktivierungs-Feature ermöglicht es Benutzern, einzelne Plugins selektiv zu deaktivieren und dadurch aus der Anwendung zu verbergen. Dies ist sinnvoll, wenn:

- Ein Plugin vorhanden ist, aber nicht benötigt wird und die Plugin-Auswahl-UIs vereinfacht werden sollen
- Bei nur einem aktiven Plugin der Auswahl-Dialog vollständig entfallen soll
- Ein Plugin mit ungültigen Konfigurationen vorerst ausgeblendet werden soll, ohne es zu entfernen

## Funktionsweise

### Wo Plugins aktiviert/deaktiviert werden

Die Plugin-Aktivierung erfolgt zentral im Einstellungsfenster (Menü → Einstellungen), im neuen Register „Plugins":

1. **Zwei Spalten-Layout:**
   - **Linke Spalte:** Zwei gruppierte Listen
     - SCM-Plugins (Quellcodeverwaltung)
     - KI-Plugins (Entwicklungsautomatisierung)
   - **Rechte Spalte:** Einstellungsgruppen des ausgewählten Plugins

2. **Aktivierungsstatus im rechten Bereich:** Der Aktivierungsstatus wird nicht direkt in der Liste umgeschaltet, sondern über eine CheckBox „Plugin aktiviert" im rechten Einstellungsbereich des ausgewählten Plugins konfiguriert.

3. **Plugin-Details:** Wählt der Benutzer ein Plugin aus der Liste, werden dessen Plugin-Namen als Kopfzeile, die Aktivierungs-CheckBox „Plugin aktiviert" und die Einstellungsgruppen rechts angezeigt

### Persistierung

Der Aktivierungsstatus wird pro Plugin persistiert:

- Speicherort: `AppEinstellung`-Datenbanktabelle (Key-Value-Store, verwaltet über `AppEinstellungService`)
- Schlüsselformat: `plugins.enabled.<PluginPrefix>` (z. B. `plugins.enabled.Softwareschmiede.GitHub`)
- Wert: `true` (aktiviert) oder `false` (deaktiviert)
- **Default bei fehlendem Schlüssel:** `true` — neue oder bisher nicht konfigurierte Plugins sind automatisch aktiviert

### Single-Plugin-Verhalten

Wenn nach Filterung nur ein Plugin einer Kategorie aktiv ist:

- **In der Aufgabenbearbeitung:** Der KI-Plugin-Selector wird ausgeblendet; das Plugin wird automatisch verwendet
- **In der Projektbearbeitung:** Der SCM-Plugin-Selector wird ausgeblendet; das Plugin wird automatisch verwendet
- **Im Plugin-Auswahldialog beim Aufgabenstart:** Der Dialog wird nicht angezeigt, wenn nur ein KI-Plugin aktiv ist

### Validierung

**Regel:** Es muss mindestens ein Plugin pro Kategorie (SCM oder KI) aktiv bleiben.

Versucht der Benutzer, das letzte aktive Plugin einer Kategorie zu deaktivieren:

1. Der Benutzer versucht, die CheckBox „Plugin aktiviert" im rechten Bereich zu deaktivieren und klickt „Speichern"
2. Beim Speichern wird eine Validierungsfehlermeldung angezeigt: „Mindestens ein <Kategorie>-Plugin muss aktiv bleiben."
3. Die Deaktivierung wird nicht gespeichert
4. Die CheckBox im rechten Bereich bleibt aktiviert

## Beispiele

### Szenario 1: Codex-Plugin ausblenden

Ein Benutzer hat GitHub, BitBucket und Codex CLI installiert. Codex wird aber nicht benötigt:

1. Einstellungen → Tab „Plugins" öffnen
2. In der KI-Plugins-Liste (linke Spalte) „Codex CLI" anklicken/auswählen
3. Im rechten Bereich wird die CheckBox „Plugin aktiviert" angezeigt — diese deaktivieren (☑ → ☐)
4. Button „Speichern" klicken
5. **Ergebnis:** Beim nächsten Aufgabenstart wird die KI-Plugin-Auswahl nur noch Claude CLI und GitHub Copilot zeigen; Codex CLI verschwindet aus allen Auswahldialogen

### Szenario 2: Single-Plugin-Verhalten mit GitHub

Ein Benutzer möchte nur GitHub als SCM-Plugin nutzen; BitBucket und Local Directory sind vorhanden, sollen aber ausgeblendet werden:

1. Einstellungen → Tab „Plugins" öffnen
2. In der SCM-Plugins-Liste (linke Spalte) jeweils auswählen und deaktivieren:
   - BitBucket: anklicken, im rechten Bereich CheckBox „Plugin aktiviert" deaktivieren (☑ → ☐)
   - Local Directory: anklicken, im rechten Bereich CheckBox „Plugin aktiviert" deaktivieren (☑ → ☐)
   - GitHub: bleibt aktiviert (kann optional anklicken und CheckBox bleibt aktiviert)
3. Button „Speichern" klicken
4. **Ergebnis:** 
   - In der Projektbearbeitung wird kein SCM-Plugin-Selector mehr angezeigt
   - GitHub wird automatisch für alle Repositorys verwendet
   - Die Auswahl „Welches Quellcodeverwaltungs-Plugin?" entfällt

### Szenario 3: Versehentliches Deaktivieren verhindern

Ein Benutzer versucht, alle KI-Plugins zu deaktivieren (weil keines konfiguriert ist):

1. Einstellungen → Tab „Plugins" öffnen
2. In der KI-Plugins-Liste jeweils auswählen und die CheckBox „Plugin aktiviert" im rechten Bereich deaktivieren:
   - Claude CLI: anklicken, CheckBox deaktivieren (☑ → ☐)
   - GitHub Copilot: anklicken, CheckBox deaktivieren (☑ → ☐)
   - Codex CLI: anklicken, CheckBox deaktivieren (☑ → ☐)
   - KI-Simulator: anklicken, CheckBox deaktivieren (☑ → ☐)
3. Button „Speichern" klicken
4. **Ergebnis:** 
   - Fehlermeldung: „Mindestens ein KI-Plugin muss aktiv bleiben."
   - Keine Änderungen werden gespeichert
   - Der Benutzer muss mindestens ein KI-Plugin aktiviert lassen

## Technische Details

### Service-Architektur

- **`PluginActivationService`** (scoped): Zentrale Service-Klasse für Aktivierungsverwaltung
  - `IsPluginEnabledAsync(pluginPrefix)` — prüft, ob Plugin aktiviert ist
  - `SetPluginEnabledAsync(pluginPrefix, enabled)` — speichert Aktivierungsstatus
  - `GetEnabledSourceCodeManagementPluginsAsync()` — liefert nur aktive SCM-Plugins
  - `GetEnabledDevelopmentAutomationPluginsAsync()` — liefert nur aktive KI-Plugins

- **`PluginActivationEntry`** (ViewModel): Darstellbarer Listeneintrag
  - `PluginName` — Anzeigename des Plugins
  - `PluginPrefix` — eindeutige Identität des Plugins
  - `IsEnabled` — Aktivierungsstatus (bindbar für UI)
  - `Plugin` — Referenz zum Plugin-Objekt

### Filterung in der Anwendung

Die Aktivierungsfilterung ist an folgenden Stellen implementiert:

| Komponente | Methode | Quelle |
|-----------|---------|--------|
| `PluginSelectionService` | `GetAvailableKiPluginPrefixesAsync()` | Filtert KI-Plugins für Aufgabenauswahl |
| `TaskDetailViewModel` | `LadeVerfuegbarePluginsAsync()` | Setzt `ZeigeKiPluginAuswahl` basierend auf aktiven Plugins |
| `RepositoryAssignViewModel` | `LadenAsync()` | Lädt SCM-Plugins über `GetEnabledSourceCodeManagementPluginsAsync()` |
| `IssueCreateDialogViewModel` | Konstruktor/Laden | Filtert KI-Plugins für Issue-Text-Generierung |

### Persistierungs-Details

Aktivierungsstatus werden als Key-Value-Einträge persistiert:

**Beispiel:**

```
Schlüssel: plugins.enabled.Softwareschmiede.GitHub
Wert: true

Schlüssel: plugins.enabled.Softwareschmiede.Codex
Wert: false
```

- Keine separaten Tabellen nötig
- Existing `AppEinstellung`-Tabelle wird verwendet
- Keine Datenbank-Migration erforderlich
- Fehlende Einträge gelten als `true` (aktiviert)

## Einschränkungen und Besonderheiten

- **Globale Geltung:** Der Aktivierungsstatus gilt anwendungsweit, nicht pro Projekt
- **Mindestens ein Plugin:** Die Anwendung kann nicht ohne mindestens ein aktives Plugin je Kategorie funktionieren
- **Keine Auswirkung auf bestehende Plugin-Einstellungen:** Deaktivierte Plugins behalten ihre Konfiguration (Token, API-Keys etc.), die Einstellungen können aber in den Einstellungen nicht bearbeitet werden
- **Neu entdeckte Plugins:** Falls ein neues Plugin installiert wird (z. B. nach Update), ist es automatisch aktiviert
