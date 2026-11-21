← [Zurück zur Übersicht](index.md)

# Plugin-System — Business Rules

## Aktivierungsstatus-Verwaltung

### Regel: Fehlende Einträge gelten als aktiviert

**Beschreibung:** Wenn für ein Plugin kein Aktivierungseintrag in der `AppSetting`-Tabelle existiert, wird das Plugin als aktiviert behandelt. Dies ermöglicht Rückwärtskompatibilität und macht neue Plugins automatisch verfügbar.

**Bedingungen:**
- Plugin existiert im System
- Kein Eintrag mit Schlüssel `plugins.enabled.<PluginPrefix>` in `AppSetting`

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

**Umsetzung:** `PluginSelectionService.GetAvailableAiPluginPrefixesAsync()` — ruft `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync()` auf, die deaktivierte Plugins filtert

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
- Fehlermeldung: `ValidatePluginActivation()` → `FehlerMeldung = "Mindestens ein Quellcodeverwaltungs-Plugin muss aktiv bleiben."` oder `"Mindestens ein KI-Plugin muss aktiv bleiben."`
- `SaveAsync()` bricht ab, keine Änderungen werden persistiert

**Umsetzung:** `SettingsViewModel.ValidatePluginActivation()` prüft vor Speichern, ob noch mindestens ein Plugin aktiv ist

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
- `TaskDetailViewModel.LoadAvailablePluginsAsync()` — setzt `ZeigeKiPluginAuswahl` basierend auf Anzahl aktiver Plugins
- `PluginSelectionDialogService.ShowPluginSelectionDialogAsync()` — wird übergangen, wenn nur ein Plugin aktiv
- `RepositoryAssignViewModel.LoadAsync()` — setzt `HasMultipleScmPlugins` basierend auf Anzahl aktiver Plugins

### Regel: Deaktivierte Plugins verschwinden aus allen Auswahllisten

**Beschreibung:** Deaktivierte Plugins werden aus allen UI-Elementen, die Plugin-Auswahl ermöglichen, entfernt.

**Bedingungen:**
- Plugin ist deaktiviert (`IsPluginEnabledAsync()` gibt `false` zurück)

**Verhalten:**
- Plugin erscheint nicht in Plugin-Auswahl-ComboBoxen (Projekt-/Aufgabenbearbeitung)
- Plugin erscheint nicht in Plugin-Auswahldialogen (Aufgabenstart)
- Plugin kann nicht als Default gesetzt werden
- Plugin wird von Issue-Text-Generatoren ausgeschlossen

**Umsetzung:** `PluginSelectionService.GetAvailableAiPluginPrefixesAsync()` — filtert via `PluginActivationService.GetEnabledDevelopmentAutomationPluginsAsync()`

## Persistierung und Konsistenz

### Regel: Aktivierungsstatus wird sofort persistiert

**Beschreibung:** Wenn der Benutzer in den Einstellungen den Status über die CheckBox „Plugin aktiviert" im rechten Bereich wechselt und speichert, wird dieser Status sofort in die `AppSetting`-Tabelle geschrieben und bleibt nach Neustart erhalten.

**Bedingungen:**
- Benutzer wählt ein Plugin in der Liste aus
- Benutzer ändert den Status via CheckBox „Plugin aktiviert" im rechten Bereich
- Speichern-Button wird geklickt
- Validierung erfolgreich (mindestens ein Plugin aktiv)

**Verhalten:**
- `PluginActivationService.SetPluginEnabledAsync(pluginPrefix, enabled)` wird aufgerufen
- Eintrag wird in `AppSetting` gespeichert mit Schlüssel `plugins.enabled.<PluginPrefix>` und Wert `"true"` oder `"false"`
- Beim nächsten Öffnen der Einstellungen ist der Status noch aktiv
- Nach Neustart der Anwendung ist der Status nach Reload noch aktiv

**Umsetzung:** `SettingsViewModel.SaveAsync()` — iteriert über alle Einträge in `SourceCodeManagementPlugins` und `DevelopmentAutomationPlugins`, deren `IsEnabled`-Status sich geändert hat, und ruft `SetPluginEnabledAsync()` auf

## Kompatibilität und Migration

### Regel: Bestehende Konfigurationen ohne Aktivierungseinträge bleiben funktional

**Beschreibung:** Ältere Konfigurationen, die vor Einführung des Aktivierungs-Features erstellt wurden, haben keine `plugins.enabled.*`-Einträge. Diese Plugins werden automatisch als aktiviert behandelt und funktionieren wie gehabt.

**Bedingungen:**
- `AppSetting` hat keinen Eintrag für `plugins.enabled.<PluginPrefix>`
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
- Kein entsprechender `plugins.enabled.<PluginPrefix>`-Eintrag in `AppSetting`

**Verhalten:**
- Plugin wird sofort in Auswahllisten angezeigt
- Benutzer kann es bei Bedarf deaktivieren
- Keine Neustart-Aktion nötig

**Umsetzung:** Default-Verhalten durch fehlende DB-Einträge

## GitHub-Credential-Verwaltung

### Regel: Kein persistierter GitHub-Token

**Beschreibung:** Der GitHub-Personal-Access-Token wird ausschließlich im Windows Credential Store (`Softwareschmiede.GitHub.Token`) gehalten und für git-Aufrufe nur in der Prozessumgebung des Kindprozesses übergeben — niemals in Dateien, Remote-URLs oder Kommandozeilenargumenten. Das Verhalten ist sicherheitsrelevant und nicht per Einstellung abschaltbar.

**Bedingungen:**
- Beliebige Host-git-Operation des `GitHubPlugin` (`clone`, `push`, `pull`, `ls-remote`, `fetch`/`checkout` im PR-Checkout) — gegen `github.com` wie gegen GitHub-Enterprise-Hosts

**Verhalten:**
- `GetGitEnvironment` setzt bei vorhandenem Token `GIT_CONFIG_*`-`http.extraheader` (`AUTHORIZATION: basic <base64("x-access-token:"+token)>`) zusätzlich zu `GH_TOKEN`; der Key bindet an den Host der jeweils kontaktierten Remote-URL (Details siehe Regel „Host-Bindung des `http.extraheader`-Keys" weiter unten). Es gibt keinen `NETRC`-Verweis mehr.
- `_netrc`/`.netrc` wird vom Plugin weder gelesen noch beschrieben — Ausnahme ist die einmalige Legacy-Bereinigung (nächste Regel).
- Ohne Token enthält die Umgebung nur `GIT_TERMINAL_PROMPT=0`; HTTPS-Klone ohne Token scheitern früh mit einer verständlichen Meldung (unverändert).

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

## git-Transport (GitHub und Bitbucket)

### Regel: Plugin-geführter git-Transport läuft ausschließlich über HTTPS

**Beschreibung:** Weder das `GitHubPlugin` noch das `BitbucketPlugin` verwenden SSH als git-Transport — die früher gesetzte Prozessumgebungsvariable `GIT_SSH_COMMAND` mit ausgehebelter Host-Key-Prüfung und das vom GitHub-Plugin in betreute Arbeitskopien geschriebene `core.sshCommand` sind vollständig entfernt. Sicherheitsrelevant, nicht konfigurierbar und ohne Opt-out; die Authentifizierung läuft unverändert über die jeweiligen HTTPS-Mechanismen (GitHub: `GIT_CONFIG_*`-`http.extraheader`/`GH_TOKEN`; Bitbucket: eingebettete Credentials bzw. `_netrc`/`http.extraheader`-Args).

**Bedingungen:**
- Beliebige plugin-geführte git-Transportoperation: `clone`, `push`, `pull`, `ls-remote` (Branch-Abfragen, Standard-Branch) sowie `fetch`/`checkout` des PR-Quell-Checkouts

**Verhalten:**
- `GetGitEnvironment` beider Plugins enthält kein `GIT_SSH_COMMAND` mehr; `GIT_TERMINAL_PROMPT=0` bleibt — ohne Credentials schlägt die Auth nicht-interaktiv fehl statt zu blockieren.
- Neue Klone des GitHub-Plugins erhalten kein `core.sshCommand` in `.git/config`; der Schreibblock in `ConfigureGitCredentialsAsync` entfällt.
- SSH-gestützte Umgehung von TLS-/Host-Key-Prüfungen existiert nicht mehr — MITM-Angriffsfläche entfällt.

**Umsetzung:** `GitHubPlugin.GetGitEnvironment`, `BitbucketPlugin.GetGitEnvironment`, `GitHubPlugin.ConfigureGitCredentialsAsync` (entfernte Blöcke)

### Regel: SSH-Repository-URLs werden deterministisch in HTTPS umgewandelt

**Beschreibung:** In SSH-Form hinterlegte Repository-URLs bleiben gültige Verknüpfungsdaten und werden am Verwendungspunkt transparent konvertiert — Bestandsverknüpfungen und -arbeitskopien funktionieren ohne Migration weiter. Nicht umwandelbare URLs werden beim Klon mit verständlicher Fehlermeldung abgelehnt.

**Bedingungen:**
- `GitRepository.RepositoryUrl` bzw. die den Remote-Methoden übergebene URL liegt in SSH- oder anderem Nicht-HTTPS-Format vor

**Verhalten:**
- `git@host:pfad` (SCP-Form) → `https://host/pfad`; `ssh://[git@]host[:22]/pfad` → `https://host/pfad` — nur User `git`/leer, nur Default-Port oder 22, kein Query/Fragment. DNS-, IPv4- und IPv6-Hosts sind zulässig; `.git`-Suffix und beliebig tiefe Pfade bleiben erhalten, nicht URL-taugliche Zeichen werden escaped.
- Bereits HTTPS-URLs bleiben unverändert; es gibt keinen `owner/repo`-Zwang.
- `CloneRepositoryAsync` (beide Plugins): `ToHttpsRepositoryUrl` liefert `null` (`http://`, `git://`, `ssh://` mit Fremd-Port/-User, Pfade) → harte `InvalidOperationException` „Es werden nur HTTPS-Repository-URLs verwendet; SSH-URLs werden automatisch konvertiert." — vor jedem CLI-Aufruf.
- `GetRemoteBranchesAsync`/`GetDefaultBranchAsync` (beide Plugins): nicht konvertierbare URLs bleiben stehen und laufen in den bestehenden Soft-Fail-Pfad (leere Branch-Liste bzw. `main`-Fallback) — die Branch-Vorschlags-UI bleibt tolerant.
- PR-Checkout: die `SourceRepositoryUrl` der `PullRequestCheckoutSpec` (Fork-Fall) wird per `with`-Ausdruck konvertiert — nicht konvertierbare Fork-URLs bleiben stehen und scheitern im normalen git-Fehlerpfad.
- Self-Hosted-Bitbucket-Server mit SSH auf Nicht-Standard-Port (typisch `ssh://git@host:7999/…`) werden bewusst nicht auf einen geratenen HTTPS-Port umgeschrieben — sauberer Fehler statt nichtdeterministischer Konvertierung.

**Umsetzung:** `GitPluginBase<TPlugin>.ToHttpsRepositoryUrl` + `ScpSshUrlPattern` — eine gemeinsame `protected static`-Methode für beide Plugin-Assemblies mit identischen Akzeptanzregeln.

### Regel: Betreute Arbeitskopien werden bei der nächsten Operation hygienisch bereinigt

**Beschreibung:** Von Altversionen hinterlassene SSH-Remote-URLs und (nur GitHub) das unsichere `core.sshCommand` werden beim nächsten plugin-geführten Push, Pull oder PR-Checkout auf der jeweiligen Arbeitskopie entfernt bzw. normalisiert — eine Migration ohne eigenen Nutzertrigger. Der Anwender muss bestehende Arbeitskopien nicht neu klonen.

**Bedingungen:**
- `PushBranchAsync`, `PullAsync` oder `CheckoutPullRequestSourceAsync` läuft auf einer betreuten Arbeitskopie

**Verhalten:**
- SSH-`remote.origin.url` → `git remote set-url origin <https-url>` (nur bei Differenz): GitHub wendet `ToHttpsRepositoryUrl` nach dem bestehenden Legacy-Token-Stripping an; Bitbucket bettet bei vorhandenen Credentials `user:appPassword` wie beim Frisch-Klon ein (`BuildAuthenticatedCloneUrl`), sonst bleibt die URL nackt — `_netrc` authentifiziert hostbezogen.
- `core.sshCommand` (nur GitHub): `git config --unset` ausschließlich wenn der gelesene Wert exakt der plugin-geschriebenen Signatur `ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null` entspricht (getrimmter Ordinal-Vergleich) — ein anwenderseitig gesetztes eigenes `core.sshCommand`, auch mit `StrictHostKeyChecking=no` neben weiteren eigenen Flags, bleibt unangetastet. Das Bitbucket-Plugin hat den Eintrag nie geschrieben → keine Bereinigung.
- Alle Bereinigungsschritte sind best-effort: Lesefehler, nicht-git-Pfade und Schreibfehler erzeugen nur `LogWarning` und brechen die umgebende git-Operation nie ab.

**Umsetzung:** `GitHubPlugin.NormalizeRemoteUrlAsync` + `RemoveInsecureSshCommandAsync` (`LegacyInsecureSshCommand`-Konstante), `BitbucketPlugin.NormalizeRemoteUrlAsync`; Auslöser jeweils am Anfang von `PushBranchAsync`, `PullAsync` und `CheckoutPullRequestSourceAsync`.

### Regel: Host-Bindung des `http.extraheader`-Keys

**Beschreibung:** Der `http.extraheader`-Key des GitHub-Plugins wird pro git-Operation an den Host der tatsächlich kontaktierten Remote-URL gebunden (`http.https://<host>[:port]/.extraheader`) statt fest auf `github.com` verdrahtet zu sein. Damit authentifizieren HTTPS-git-Operationen gegen `github.com` wie gegen GitHub-Enterprise-Hosts. Ein generischer, hostloser `http.extraheader` wird bewusst nicht erzeugt — er würde den Token an jeden kontaktierten Host senden (Leak-Vektor über Fork-`clone_url`/`SourceRepositoryUrl` aus PR-Metadaten).

**Bedingungen:**
- Beliebige plugin-geführte git-Operation des `GitHubPlugin` bzw. die Prozessumgebung einer Agenten-Terminal-Session (`IScmAgentAccess.GetAgentEnvironmentAsync`) mit vorhandenem Token

**Verhalten:**
- Der Zielhost wird je Operation aus der kontaktierten URL bestimmt: `clone`/`ls-remote`/Credential-Konfiguration aus der (per `ToHttpsRepositoryUrl` normalisierten) übergebenen Repository-URL; `push`/`pull` und der Same-Repository-Fall des PR-Checkouts aus der `remote.origin.url`-Rückgabe von `NormalizeRemoteUrlAsync`; der Fork-Fall des PR-Checkouts aus der normalisierten `SourceRepositoryUrl`; Agenten-Sessions aus `repository.RepositoryUrl`, die über `DevelopmentProcessService` → `AiExecutionService` → `IScmAgentAccess.GetAgentEnvironmentAsync(konto, repositoryUrl, ct)` durchgereicht wird.
- SSH-/SCP-Formen der Ziel-URL werden vor der Key-Bildung über `ToHttpsRepositoryUrl` normalisiert; Userinfo/Credentials gelangen nie in den Key. Der Host steht als `IdnHost` (IDN → Punycode) im Key, IPv6-Literale behalten ihre Klammern, ein Nicht-Default-Port wird angehängt.
- Lässt sich kein Zielhost bestimmen (URL fehlt, ist unparsebar oder nicht-HTTPS, `remote.origin.url` nicht lesbar), entsteht kein `GIT_CONFIG_*`-Block — die Operation läuft fail-closed mit `GIT_TERMINAL_PROMPT=0` weiter; es gibt keinen Fallback auf `github.com` oder einen hostlosen Key.
- Bekannte Einschränkung: `GH_TOKEN`/`gh`-CLI adressiert ohne gesetztes `GH_HOST` weiterhin `github.com` — `gh`-Aufrufe gegen GitHub-Enterprise-Hosts sind nicht Teil dieser Anbindung.

**Umsetzung:** `GitHubPlugin.TryBuildHttpExtraHeaderKey` (Key-Bildung aus der Ziel-URL, `null` = fail-closed) + `AddGitHubHttpAuthHeader(environment, token, targetUrl)`; die Ziel-URL reichen `GetGitEnvironment(token, targetUrl)` und `GetAgentEnvironmentAsync(konto, repositoryUrl, ct)` durch; `GitPluginBase.ResolvePullRequestFetchRemote` bestimmt das Fetch-Ziel des PR-Checkouts (`origin` vs. `SourceRepositoryUrl`).

## Sicherheit der Plugin-Discovery

### Regel: Nur whitelisted Plugin-Assemblies werden geladen

**Beschreibung:** Der `PluginManager` lädt ausschließlich Assemblies, deren Dateiname und echter Assembly-Name (Metadaten) auf einer fest kodierten Whitelist der mitgelieferten First-Party-Plugins stehen. Eine fremde, im `plugins`-Verzeichnis abgelegte DLL wird nicht geladen — sie könnte sonst mit vollen App-Rechten inkl. Credential-Store-Zugriff ausgeführt werden.

**Bedingungen:**
- Plugin-Discovery beim Anwendungsstart (`plugins/` und Plugin-Unterordner der ersten Ebene)

**Verhalten:**
- Ladekandidaten: `*.dll` direkt unter `plugins/` sowie je Unterordner nur `<Ordner>\<Ordner>.dll`; Dependency-DLLs in den Unterordnern sind keine Kandidaten.
- Dateiname nicht auf der Whitelist → `LogWarning` + übersprungen.
- Dateiname auf der Whitelist, aber echter Assembly-Name abweichend (Maskerade per Umbenennung) → `LogWarning` + übersprungen.
- Der im Test-Modus greifende Namensfilter bleibt als zusätzliche Einschränkung bestehen.
- Grenze: Eine präparierte *Dependency*-DLL neben einem legitimen Plugin wird durch die Whitelist nicht abgedeckt — dafür wäre Strong-Name-Signierung erforderlich (derzeit nicht eingerichtet).

**Umsetzung:** `PluginManager.AllowedPluginAssemblyNames`, `PluginManager.EnumeratePluginCandidates()`, `PluginManager.IsAllowedPlugin()` (`AssemblyLoadContext.GetAssemblyName` für die Metadatenprüfung).
