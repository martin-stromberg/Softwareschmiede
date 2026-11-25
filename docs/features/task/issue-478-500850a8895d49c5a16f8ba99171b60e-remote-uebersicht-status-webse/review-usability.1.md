# Usability-/UI-Review: Remote-Übersicht – Status-Webseite (Issue #478)

**Status: Befunde vorhanden**

Geprüft wurden die serverseitig gerenderte Status-Seite (`server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs` inkl. eingebettetem CSS, zugehörige Datenwege `GitHub/GitHubStatusService.cs`, `Status/InstanceSnapshotCache.cs`, `Program.cs`) gegen den abgenommenen Entwurf `ui-entwuerfe/01-entwurf-online.html` sowie der neue Einstellungs-Abschnitt „Remote-Übersicht" (`src/Softwareschmiede.App/Views/SettingsView.xaml` Zeilen 719–812, `SettingsView.xaml.cs`, `SettingsViewModel.cs` Zeilen 365–416, `RemoteStatusConnectionInfo.cs`).

## Befunde

### 1. Instanz-Zugehörigkeit fehlt in zwei von drei App-Sektionen (Mittel)

- **Stelle:** `StatusPageComposer.cs` Zeilen 123–173. Nur die `h2` von „Laufende Aufgaben" enthält den `Instanz „Name"`-Span (Zeile 125); „Wartet auf Anwender" (Zeilen 142–144) und „Letzte SCM-Ereignisse" (Zeilen 160–162) nennen die Instanz nicht.
- **Beschreibung:** Multi-Instanz-Anzeige ist laut Plan explizit vorgesehen („je sendender Instanz einen eigenen App-Sektions-Block mit Instanzname/-ID", plan.md Z. 50). Sobald zwei Installationen liefern, erzeugt die Seite sechs gleichartige Sektionen; für die Warteliste und die Ereignisliste ist nicht mehr erkennbar, zu welcher Installation sie gehören.
- **Korrektur:** Den `inst`-Span in allen drei `h2` der Instanz ausgeben oder die drei Sektionen einer Instanz in einen gemeinsamen, beschrifteten Container gruppieren (z. B. Instanz-Band vor den Sektionen).

### 2. Deaktivierte Felder werden ausgeblendet statt gesperrt – ohne Hinweistext (Mittel)

- **Stelle:** `SettingsView.xaml` Zeilen 745–746 (`RemoteStatusFieldsArea` an `Visibility="{Binding IsRemoteStatusFieldVisible ...}"`), `SettingsViewModel.cs` Zeile 413.
- **Beschreibung:** Die Plan-Vorgabe lautet „Felder deaktiviert + Hinweistext" bzw. „deaktiviert → Felder gesperrt mit Hinweis" (plan.md Z. 280, Z. 327). Die Geschwister-Expander setzen das konsequent per `IsEnabled`-Bindung um (Issue-Aufbereitung `IsIssuePreparationFieldEnabled`, Aufgaben beenden `IsTaskCompletionFieldEnabled`, Prozessabläufe `IsProcessRunsFieldEnabled`). Hier verschwinden stattdessen Server-URL, Anzeigename, Token, Instanz-ID und Verbindungsstatus komplett. Folgen: Im deaktivierten Zustand ist weder sichtbar, welche Felder existieren, noch wird erklärt, dass sie nach Aktivierung erscheinen; die Instanz-ID ist nicht einsehbar, obwohl sie für die serverseitige Zuordnung relevant sein kann.
- **Korrektur:** Den Feldbereich wie bei den Geschwistern sichtbar lassen und per `IsEnabled`-Bindung sperren; ergänzend einen Hinweis-TextBlock im gesperrten Zustand, z. B. „Server-URL, Anzeigename, Token und Verbindungsstatus sind nur bei aktivierter Remote-Übersicht verfügbar."

### 3. Gespeicherter Relay-Token erscheint nicht in der PasswordBox (Mittel)

- **Stelle:** `SettingsView.xaml.cs` Zeilen 93–97 (`OnRemoteStatusTokenLoaded`), `SettingsViewModel.cs` Zeilen 639–688 (`LoadAsync`, Token erst in Z. 686–687 aus dem CredentialStore gelesen).
- **Beschreibung:** `PasswordBox.Loaded` feuert beim Einfügen in den Visual Tree – deutlich bevor die asynchrone `LoadAsync` den Token aus `ICredentialStore` nachgeladen hat. `passwordBox.Password = vm.RemoteStatusToken` setzt damit eine leere Zeichenfolge; den nachgeladenen Wert schiebt nichts mehr in die Box (PasswordBox ist nicht bindbar, kein Folge-Update). Ergebnis: Die Box zeigt leer an, obwohl ein Token hinterlegt ist – „Token gesetzt" ist von „kein Token" nicht unterscheidbar. Beim Speichern bleibt der im ViewModel gepufferte echte Token erhalten (kein Datenverlust), die Anzeige ist aber falsch-ehrlich.
- **Korrektur:** Den Box-Inhalt nach Abschluss von `LoadAsync` (bzw. bei `PropertyChanged` auf `RemoteStatusToken`) aus dem Code-behind erneut setzen – z. B. über ein `PropertyChanged`-Abonnement in der View oder einen View-seitigen Aktualisierungsaufruf nach dem Load-Command.

### 4. Zeitstempel ohne Datum bei datumsübergreifenden Angaben (Niedrig)

- **Stelle:** `StatusPageComposer.cs` Zeile 302 (`FmtTime` → `HH:mm:ss`), verwendet u. a. für „offline seit" (Z. 98), „liefert seit" (Z. 104), Quellen-Badges (Z. 121, 236), „pausiert bis" (Z. 191), „Heartbeat" (Z. 193), SCM-Ereigniszeiten (Z. 219).
- **Beschreibung:** Reine Uhrzeit-Angaben sind über Tagesgrenzen hinweg mehrdeutig – gerade im Kern-Szenario „von unterwegs nachsehen". „Instanz offline seit 22:15 UTC" kann den Vortag meinen; ein Anwender im Urlaub/in anderer Zeitzone kann das Alter nicht sicher ablesen. Die UTC-Kennzeichnung ist lobenswert vorhanden, ersetzt aber kein Datum.
- **Korrektur:** Bei abweichendem Kalendertag Datum mit ausgeben (`dd.MM. HH:mm`, UTC bleibt gekennzeichnet) oder generell kurzes Datum voranstellen.

### 5. `white-space: nowrap` auf Status-Pills verletzt die Mobile-Vorgabe (Niedrig)

- **Stelle:** `StatusPageComposer.cs` Zeile 336 (`.pill { ... white-space: nowrap; }`).
- **Beschreibung:** Die Anwender-Vorgabe „keine nowrap-Muster" (plan.md Z. 45, Test-Notiz Z. 312) wird an dieser Stelle gebrochen. Praktisch bleibt das Risiko klein: Pill-Texte sind kurz („Wartet auf Eingabe" ≈ 120 px) und die übrigen Zellen können über `overflow-wrap: anywhere` schrumpfen, sodass die Tabelle auf 320 px noch passt. Der Fallback-Pill (Z. 214) gibt aber rohe `RunStatus`-Strings aus – bei unerwartet langen Werten bläht der `nowrap`-Span seine Spalte auf und kann rechts überragen.
- **Korrektur:** `white-space: nowrap` entfernen oder ergänzend `max-width`/`text-overflow: ellipsis` setzen; alternativ die Fallback-Anzeige auf bekannte, kurze Statuswerte mappen.

### 6. GitHub-Fehlertext ist rohe, englische Exception-Message (Niedrig)

- **Stelle:** `GitHubStatusService.cs` Zeile 185 (`ex.Message` als `Error` gespeichert), `StatusPageComposer.cs` Zeilen 239–245 (direkte Ausgabe im `stale-note`).
- **Beschreibung:** Auf der deutschsprachigen Seite erscheinen technische Rohmeldungen wie „Response status code does not indicate success: 403 (Forbidden)." oder Socket-Fehlertexte – für die Status-Kernaussage „GitHub gerade nicht abrufbar" unnötig kryptisch.
- **Korrektur:** Deutsche Kategorie-Texte ausgeben (z. B. „HTTP 403 – vermutlich Rate-Limit erschöpft", „Zeitüberschreitung beim Abruf"); die Rohmeldung ggf. nur in `title`-Attribut oder Log.

### 7. Quellen-Badge überschätzt Datenfrische nach Folgefehlern (Niedrig)

- **Stelle:** `GitHubStatusService.cs` Zeilen 201–216 (`StoreAsync` schreibt `FetchedUtc = now` auch für Fehler-Einträge) und `ToResult` Zeilen 219–226; Anzeige in `StatusPageComposer.cs` Zeilen 235–237.
- **Beschreibung:** Beim ersten Fehler liefert `EndpointResult.Failed` korrekt die letzte Erfolgszeit (`previous?.FetchedUtc`). Der Fehler-Eintrag wird aber mit „Jetzt" als `FetchedUtc` gespeichert; jeder Folge-Request innerhalb der TTL erhält aus `ToResult` die *Fehlversuchszeit* als `FetchedUtc`. Das Quellen-Badge „GitHub · HH:mm:ss UTC" suggeriert dann frisch abgerufene Daten, obwohl die angezeigten Werte ggf. deutlich älter sind. Der Fehler-Hinweisbalken entschärft das, die Kombination „frischer" Zeitstempel + veraltete Daten bleibt aber irreführend.
- **Korrektur:** Letzte Erfolgszeit separat im `CacheEntry` führen und als `FetchedUtc`/`Letzter-Erfolg` melden; Badge ggf. „GitHub · Stand <Erfolg>, Fehler seit <Fehler>".

### 8. Gedimmte Stale-Sektionen unterhalb guter Lesbarkeit (Niedrig)

- **Stelle:** `StatusPageComposer.cs` Zeile 347 (`.dim td { color: #9ca3af; }`).
- **Beschreibung:** `#9ca3af` auf Weiß ergibt ca. 2,8:1 Kontrast – unterhalb üblicher Lesbarkeits-Richtwerte (4,5:1). Ausgerechnet die als veraltet markierten Daten, die ein Anwender noch ablesen will, werden damit schwer lesbar. (Gleiche Farbe im `footer`, dort unkritisch da rein dekorativ.)
- **Korrektur:** Dimmung abschwächen (z. B. `#6b7280` wie `.muted`, ca. 4,8:1); die `stale-note` trägt die Veraltungs-Botschaft bereits eindeutig.

### 9. „Keine Daten"-Zustand ohne Handlungshinweis (Niedrig)

- **Stelle:** `StatusPageComposer.cs` Zeilen 50–54.
- **Beschreibung:** Ohne je verbundene Instanz steht nur „Keine App-Instanz hat bisher Daten geliefert." Der abgenommene Entwurf `03-entwurf-keine-daten.html` sah zusätzlich einen erklärenden Hinweisbalken vor („Sobald in den App-Einstellungen die Remote-Übersicht aktiviert ist, erscheinen hier die App-Sektionen."). Ohne diesen Hinweis kann der Betreiber nicht ablesen, ob die Konfiguration fehlt oder ein Fehler vorliegt.
- **Korrektur:** `stale-note`-Hinweis wie im Entwurf ergänzen (Aktivierungsort: Einstellungen → Automatisierung → Remote-Übersicht).

### 10. Verbindungsstatus im Fehlerfall farblich nicht hervorgehoben (Niedrig)

- **Stelle:** `SettingsView.xaml` Zeilen 804–809 (`RemoteStatusConnectionText` in `SecondaryTextBrush`).
- **Beschreibung:** „verbunden", „verbinde…", „Konfigurationsfehler: …" und „Fehler: …" stehen alle in derselben dezenten Grau-Schrift. Der ehrliche Fehlerzustand (Anforderung Z. 22) ist vorhanden, fällt aber optisch nicht auf.
- **Korrektur:** `Foreground` an den Zustand binden (z. B. `ErrorBrush` bei `Error`/`ConfigurationError`) oder ein Statuszeichen/Farb-Pill voranstellen.

### 11. Checks-Spalte des abgenommenen Entwurfs nicht umgesetzt (Niedrig / Klärung)

- **Stelle:** Entwurf `01-entwurf-online.html` Zeilen 86–91 zeigt „Checks: ✓/✗ Blocked" je PR; `GitHubStatusService` holt keine Check-Runs, die Seite enthält keine Checks-Information.
- **Beschreibung:** Falls das Weglassen der PR-Checks eine bewusste v1-Entscheidung war (analog zu den Aktionsbuttons), ist es kein Mangel – dann sollte die Entwurfs-HTML/die Entscheidungsliste das festhalten. Falls es übersehen wurde, fehlt eine im Entwurf abgenommene Information.
- **Korrektur:** Entscheidung dokumentieren (entweder Checks nachrüsten oder Entwurf/Beschluss um „keine Checks in v1" ergänzen).

## Geprüfte Bereiche (ohne Befund)

- **Sektions-Struktur/-Reihenfolge:** Header mit Gesamt-Badge → Meta-Zeile (Stand, Refresh-Intervall, `/status.json`-Link) → je Instanz „Laufende Aufgaben" → „Wartet auf Anwender" (inkl. wartender SCM-Ereignisse) → „Letzte SCM-Ereignisse" → je Repository GitHub-Sektion (Offene PRs, Letzte Releases, offene Issues) → Footer. Entspricht dem Entwurf.
- **Quellen-/Zeitstempel je Sektion:** `src app`/`src gh`-Badges mit UTC-Abrufzeit in jeder Sektion vorhanden; GitHub-Sektionen bleiben bei getrennter App live (nur App-Sektionen werden gedimmt). Erfüllt.
- **Ehrliche Zustände:** Badge online/offline/teilweise veraltet/„keine Instanz"; `stale-note` bei getrennter oder veralteter Instanz; GitHub-Fehler mit Zeitstempel als Hinweisbalken statt stillem Veralten; „Noch kein Snapshot empfangen" bei verbundener, aber datenloser Instanz. Erfüllt (Einschränkungen siehe Befunde 7, 9).
- **Lesbarkeit:** Segoe-UI/system-ui, 13,5 px Tabellen, deutsche Texte durchgehend (`lang="de"`, UTF-8); Kontraste der Pills/Badges ausreichend; Escaping via `WebUtility.HtmlEncode` auf allen Benutzerinhalten (Test `Render_UserContent_IsHtmlEncoded` belegt).
- **Links:** Alle referenzierten GitHub-Objekte sind echte `<a href>` zur `html_url` (Issue-, PR-, Release-, SCM-Referenz- und Repo-Links) – Pflichtvorgabe erfüllt (Test `Render_References_RenderedAsGitHubLinks`).
- **Responsiv:** `viewport`-Meta vorhanden; `.page` fluide mit `max-width`/`auto`-Margin; keine festen Inhaltsbreiten außer `td.num` 70 px, das unter 600 px auf `auto` zurückgesetzt wird; `overflow-wrap: anywhere` auf Zellen; Media-Query <600 px reduziert Paddings; `h2` und Header flex-wrap. Einzige nowrap-Ausnahme: `.pill` (Befund 5).
- **Meta-Refresh:** `<meta http-equiv="refresh" content="{RefreshSeconds}">` (Default 15 s, `appsettings.json` Z. 17) vorhanden.
- **Keine Aktionsbuttons in v1:** Kein `<button>` im Ausgabe-HTML; rein lesend umgesetzt (Test-Zusicherung `NotContain("<button")`).
- **Einstellungs-Platzierung:** Expander-Muster identisch zu Geschwistern (`RemoteStatusExpander`, `IsExpanded="False"`, Header mit Titel + Status-TextBlock `aktiviert`/`deaktiviert`); letzter Abschnitt im Tab „Automatisierung" im `ScrollViewer` mit `StackPanel MaxWidth="640"` (Z. 355–356); Einzüge der Unterfelder (Margin 22) konsistent.
- **AutomationProperties.Name:** Alle interaktiven Elemente benannt (`RemoteStatusExpander`, `RemoteStatusStatus`, `IsRemoteStatusEnabled`, `RemoteStatusUrl`, `RemoteStatusInstanceName`, `RemoteStatusToken`, `RemoteStatusInstanceId`, `RemoteStatusConnectionText`, `RemoteStatusFieldsArea`) – Konvention erfüllt; `HelpText` auf Status-Texten gebunden.
- **Instanz-ID-Anzeige:** Read-only-TextBox, kopierbar; ID wird erst bei aktivierter Übersicht erzeugt (`GetOrCreateRemoteStatusInstanceIdAsync`, VM Z. 683–685, 887–888).
- **Verbindungsstatus:** Verständliche deutsche Texte aus `RemoteStatusConnectionInfo.BuildStatusText` („deaktiviert/verbinde…/verbunden/verbinde erneut…/Konfigurationsfehler/Fehler" + Detail + letzter Snapshot-Zeitpunkt, UTC) mit `StateChanged`-Live-Update.
- **TabIndex:** Im gesamten View keine expliziten `TabIndex`-Werte; die implizite Tab-Reihenfolge folgt der visuellen Reihenfolge (Schalter → URL → Name → Token → Instanz-ID) und ist logisch.
- **Token-Ablage:** PasswordBox → `ICredentialStore` (nicht als `AppSetting`), Leer-Löschen entfernt den Eintrag, Trimming beim Speichern – Handling korrekt; Anzeigeproblem separat als Befund 3.
