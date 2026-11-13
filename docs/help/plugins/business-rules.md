← [Zurück zur Übersicht](index.md)

# Plugin-System — Business Rules

## Aktivierungsstatus-Verwaltung

### Regel: Fehlende Einträge gelten als aktiviert

**Beschreibung:** Wenn für ein Plugin kein Aktivierungseintrag in der `AppEinstellung`-Tabelle existiert, wird das Plugin als aktiviert behandelt. Dies ermöglicht Rückwärtskompatibilität und macht neue Plugins automatisch verfügbar.

**Bedingungen:**
- Plugin existiert im System
- Kein Eintrag mit Schlüssel `plugins.enabled.<PluginPrefix>` in `AppEinstellung`

**Verhalten:**
- `PluginActivationService.IsPluginEnabledAsync(pluginPrefix)` gibt `true` zurück
- Plugin wird in allen Auswahllisten angezeigt
- Plugin wird bei Filterung einbezogen

**Umsetzung:** `PluginActivationService.IsEnabledValue()` — wenn Wert `null` oder leer ist, wird `true` zurückgegeben

### Regel: Explizite Deaktivierung hat Vorrang

**Beschreibung:** Ein Plugin mit gespeichertem Status `false` wird als deaktiviert behandelt, unabhängig davon, ob es neu entdeckt wurde.

**Bedingungen:**
- Eintrag mit Schlüssel `plugins.enabled.<PluginPrefix>` existiert
- Wert ist `false`

**Verhalten:**
- Plugin verschwindet aus allen Auswahllisten
- Plugin wird nicht als Default vorgeschlagen
- Plugin wird von Filter-Methoden ausgeschlossen

**Umsetzung:** `PluginSelectionService.GetAvailableKiPluginPrefixesAsync()` — ruft `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync()` auf, die deaktivierte Plugins filtert

## Mindestanforderung für funktionsfähige Kategorien

### Regel: Mindestens ein Plugin je Kategorie muss aktiv sein

**Beschreibung:** Die Anwendung erfordert für sichere Operationen mindestens ein aktives SCM-Plugin und ein aktives KI-Plugin pro Kategorie. Das Deaktivieren des letzten Plugins einer Kategorie wird durch Validierung beim Speichern verhindert.

**Bedingungen:**
- Benutzer versucht, den letzten aktiven Plugin einer Kategorie zu deaktivieren
- Speichern-Button wird geklickt

**Verhalten:**
- Validierungsfehlermeldung wird angezeigt: „Mindestens ein <Kategorie>-Plugin muss aktiv bleiben."
- Status wird nicht geändert
- Checkbox bleibt aktiviert

**Fehlerfall:**
- Fehlermeldung: `ValidierePluginAktivierung()` → `FehlerMeldung = "Mindestens ein Quellcodeverwaltungs-Plugin muss aktiv bleiben."` oder `"Mindestens ein KI-Plugin muss aktiv bleiben."`
- `SpeichernAsync()` bricht ab, keine Änderungen werden persistiert

**Umsetzung:** `SettingsViewModel.ValidierePluginAktivierung()` prüft vor Speichern, ob noch mindestens ein Plugin aktiv ist

## UI-Verhalten bei Auswahl

### Regel: Single-Plugin-Verhalten für vereinfachte Bedienung

**Beschreibung:** Wenn nach Filterung nur ein Plugin einer Kategorie aktiv ist, wird das Plugin automatisch ohne Auswahldialog oder Selector verwendet.

**Bedingungen:**
- Genau ein Plugin einer Kategorie ist aktiv (nach Filterung durch `PluginActivationService`)
- In KI-Auswahldialog oder SCM-Selektor

**Verhalten:**

| Kontext | Verhalten |
|---------|-----------|
| Aufgabenstart | Dialog wird nicht angezeigt; Plugin wird automatisch gewählt |
| Aufgabendetail KI-Selector | Sichtbarkeit `ZeigeKiPluginAuswahl = false`; Plugin wird automatisch verwendet |
| Projektbearbeitung SCM-Selector | `HasMultipleScmPlugins = false`; Plugin wird automatisch selektiert |

**Umsetzung:**
- `TaskDetailViewModel.LadeVerfuegbarePluginsAsync()` — setzt `ZeigeKiPluginAuswahl` basierend auf Anzahl aktiver Plugins
- `PluginSelectionDialogService.ShowPluginSelectionDialogAsync()` — wird übergangen, wenn nur ein Plugin aktiv
- `RepositoryAssignViewModel.LadenAsync()` — setzt `HasMultipleScmPlugins` basierend auf Anzahl aktiver Plugins

### Regel: Deaktivierte Plugins verschwinden aus allen Auswahllisten

**Beschreibung:** Deaktivierte Plugins werden aus allen UI-Elementen, die Plugin-Auswahl ermöglichen, entfernt.

**Bedingungen:**
- Plugin ist deaktiviert (`IsPluginEnabledAsync()` gibt `false` zurück)

**Verhalten:**
- Plugin erscheint nicht in Plugin-Auswahl-ComboBoxen (Projekt-/Aufgabenbearbeitung)
- Plugin erscheint nicht in Plugin-Auswahldialogen (Aufgabenstart)
- Plugin kann nicht als Default gesetzt werden
- Plugin wird von Issue-Text-Generatoren ausgeschlossen

**Umsetzung:** `PluginSelectionService.GetAvailableKiPluginPrefixesAsync()` — filtert via `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync()`

## Persistierung und Konsistenz

### Regel: Aktivierungsstatus wird sofort persistiert

**Beschreibung:** Wenn der Benutzer in den Einstellungen den Status über die CheckBox „Plugin aktiviert" im rechten Bereich wechselt und speichert, wird dieser Status sofort in die `AppEinstellung`-Tabelle geschrieben und bleibt nach Neustart erhalten.

**Bedingungen:**
- Benutzer wählt ein Plugin in der Liste aus
- Benutzer ändert den Status via CheckBox „Plugin aktiviert" im rechten Bereich
- Speichern-Button wird geklickt
- Validierung erfolgreich (mindestens ein Plugin aktiv)

**Verhalten:**
- `PluginActivationService.SetPluginEnabledAsync(pluginPrefix, enabled)` wird aufgerufen
- Eintrag wird in `AppEinstellung` gespeichert mit Schlüssel `plugins.enabled.<PluginPrefix>` und Wert `"true"` oder `"false"`
- Beim nächsten Öffnen der Einstellungen ist der Status noch aktiv
- Nach Neustart der Anwendung ist der Status nach Reload noch aktiv

**Umsetzung:** `SettingsViewModel.SpeichernAsync()` — iteriert über alle Einträge in `SourceCodeManagementPlugins` und `DevelopmentAutomationPlugins`, deren `IsEnabled`-Status sich geändert hat, und ruft `SetPluginEnabledAsync()` auf

## Kompatibilität und Migration

### Regel: Bestehende Konfigurationen ohne Aktivierungseinträge bleiben funktional

**Beschreibung:** Ältere Konfigurationen, die vor Einführung des Aktivierungs-Features erstellt wurden, haben keine `plugins.enabled.*`-Einträge. Diese Plugins werden automatisch als aktiviert behandelt und funktionieren wie gehabt.

**Bedingungen:**
- `AppEinstellung` hat keinen Eintrag für `plugins.enabled.<PluginPrefix>`
- Plugin existiert im System

**Verhalten:**
- Plugin wird als aktiviert behandelt (Default `true`)
- Keine Datenbank-Migration erforderlich
- Bestehende Benutzer sehen keine Verhaltensänderungen

**Umsetzung:** `PluginActivationService.IsEnabledValue()` — fehlender Eintrag = `true`

### Regel: Neu entdeckte Plugins sind standardmäßig aktiviert

**Beschreibung:** Wenn ein neues Plugin zur Anwendung hinzugefügt wird (z. B. nach Software-Update), ist es automatisch für alle Benutzer aktiviert.

**Bedingungen:**
- Plugin existiert in der aktuellen Version
- Kein entsprechender `plugins.enabled.<PluginPrefix>`-Eintrag in `AppEinstellung`

**Verhalten:**
- Plugin wird sofort in Auswahllisten angezeigt
- Benutzer kann es bei Bedarf deaktivieren
- Keine Neustart-Aktion nötig

**Umsetzung:** Default-Verhalten durch fehlende DB-Einträge

## GitHub-Credential-Verwaltung

### Regel: Kein persistierter GitHub-Token

**Beschreibung:** Der GitHub-Personal-Access-Token wird ausschließlich im Windows Credential Store (`Softwareschmiede.GitHub.Token`) gehalten und für git-Aufrufe nur in der Prozessumgebung des Kindprozesses übergeben — niemals in Dateien, Remote-URLs oder Kommandozeilenargumenten. Das Verhalten ist sicherheitsrelevant und nicht per Einstellung abschaltbar.

**Bedingungen:**
- Beliebige Host-git-Operation des `GitHubPlugin` gegen github.com (`clone`, `push`, `pull`, `ls-remote`, `fetch`/`checkout` im PR-Checkout)

**Verhalten:**
- `GetGitEnvironment` setzt bei vorhandenem Token `GIT_CONFIG_*`-`http.extraheader` (`AUTHORIZATION: basic <base64("x-access-token:"+token)>`) zusätzlich zu `GH_TOKEN`; es gibt keinen `NETRC`-Verweis mehr.
- `_netrc`/`.netrc` wird vom Plugin weder gelesen noch beschrieben — Ausnahme ist die einmalige Legacy-Bereinigung (nächste Regel).
- Ohne Token enthält die Umgebung nur `GIT_TERMINAL_PROMPT=0` und `GIT_SSH_COMMAND`; HTTPS-Klone ohne Token scheitern früh mit einer verständlichen Meldung (unverändert).

**Umsetzung:** `GitHubPlugin.AddGitHubHttpAuthHeader` + `GetGitEnvironment` — dasselbe `extraheader`-Muster, das `GetAgentEnvironmentAsync` bereits für Agent-Prozesse nutzt; die Env-Variante vermeidet im Unterschied zu CLI-Args (`-c http.extraheader=…`, BitBucket-Muster) den Token in der Prozess-Kommandozeile.

### Regel: Einmalige `_netrc`-Bereinigung bei Plugin-Start

**Beschreibung:** Beim Instanziieren des `GitHubPlugin` (Plugin-Discovery beim Anwendungsstart) werden die von Altversionen geschriebenen Stanzas `machine github.com` und `machine api.github.com` einmalig aus einer existierenden `_netrc` im Benutzerprofil entfernt — eine Migration für Bestandsinstallationen.

**Bedingungen:**
- `GitHubPlugin` wird instanziiert (pro Instanziierung einmal, idempotent)

**Verhalten:**
- `_netrc` existiert nicht → keine Aktion, die Datei wird nie angelegt.
- Nur exakte `machine github.com`-/`machine api.github.com`-Stanzas (case-insensitiv, getrimmt, inkl. Inline-Tokens) werden entfernt — die `machine`-Zeile plus alle Folgezeilen bis zur nächsten `machine`-/`default`-Zeile.
- Fremd-Einträge (z. B. `machine bitbucket.org`, Self-Hosted-Hosts) bleiben unverändert; ohne Treffer wird die Datei nicht zurückgeschrieben.
- Bestand die Datei ausschließlich aus den entfernten GitHub-Stanzas (Rest leer/whitespace-only) → die Datei wird gelöscht.
- Manuell gepflegte GitHub-Stanzas werden ebenfalls entfernt — sie sind von plugin-geschriebenen nicht unterscheidbar und der Pfad ist für das Plugin obsolet.
- Jeder Fehler (gesperrte/unlesbare Datei, Rechte) → `LogWarning`; Plugin-Instanziierung und spätere git-Operationen laufen normal weiter.

**Umsetzung:** `GitHubPlugin.RemoveLegacyNetrcEntries` / `RemoveNetrcStanzas` — Read-Modify-Write unter exklusivem Dateizugriff (`FileShare.None`), damit ein gleichzeitiger Fremd-Zugriff (z. B. `BitBucketPlugin.UpdateNetrcEntry`) nicht mit einer veralteten Version interleavet, sondern mit `IOException` fehlschlägt. Verbleibende Einschränkung: kein prozessübergreifender Lock — ein Fremd-Writer kann die Datei vor dem Öffnen oder zwischen Handle-Close und `File.Delete` erneut verändern.
