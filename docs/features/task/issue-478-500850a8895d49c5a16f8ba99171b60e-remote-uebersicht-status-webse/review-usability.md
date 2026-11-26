# Usability-/UI-Review: Remote-Übersicht – Status-Webseite (Issue #478) — Iteration 2

**Status: Befunde vorhanden**

Nachprüfung der Umsetzung der elf Iteration-1-Befunde (siehe `review-usability.1.md`) im aktuellen Code: `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs`, `server/Softwareschmiede.StatusServer/GitHub/GitHubStatusService.cs`, `src/Softwareschmiede.App/Views/SettingsView.xaml` (Zeilen 719–836), `SettingsView.xaml.cs`, `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` (Zeilen 365–431, 695–703, 899–914), `src/Softwareschmiede/Application/Services/RemoteStatusConnectionInfo.cs`, `server/README.md`.

## Befunde

### 1. Fehler-Farbtrigger am Verbindungsstatus wirkungslos – lokaler `Foreground`-Wert schlägt Style-Trigger (Niedrig)

- **Stelle:** `SettingsView.xaml` Zeilen 817–833 (`RemoteStatusConnectionText`).
- **Beschreibung:** Der Befund-10-Fix (Fehlerzustand farblich hervorheben) ist codiert, funktioniert aber nicht: Das `TextBlock` setzt `Foreground="{DynamicResource SecondaryTextBrush}"` als **lokalen Wert** (Zeile 820). In der WPF-Präzedenzordnung schlägt ein lokaler Wert (Rang 3) Style-Trigger (Rang 6) – der `DataTrigger` auf `IsRemoteStatusConnectionError` kann den lokalen `Foreground` daher niemals überschreiben. `ErrorBrush` wird folglich nie sichtbar; „Konfigurationsfehler" und „Fehler" bleiben weiterhin grau. (Vergleichbarer Fall in `TodoListView.xaml` Z. 73–89 ist korrekt aufgebaut: dort setzt der Trigger nur `TextDecorations`/`Opacity`, die nicht lokal belegt sind.)
- **Korrektur:** Das lokale `Foreground`-Attribut am `TextBlock` entfernen – der bereits vorhandene `Style`-Setter (Zeile 825) liefert denselben `SecondaryTextBrush`-Default, und der Trigger greift dann.

### 2. Gedimmte Status-Pills in Stale-Sektionen weiterhin schwach lesbar (Niedrig)

- **Stelle:** `StatusPageComposer.cs` Zeile 376 (`.dim .pill { opacity: .55; }`).
- **Beschreibung:** Befund 8 wurde für `.dim td` gelöst (`#6b7280`, ≈4,8:1). Die Pill-Texte in gedimmten Sektionen werden aber weiterhin per `opacity: .55` über Weiß geblendet – effektiv grob ≈2,4:1 Kontrast bei 11 px Schrift, also unterhalb des in Befund 8 bemängelten Lesbarkeits-Richtwerts. Ausgerechnet der kompakte Zustandsindikator („läuft", „pausiert", „Wartet auf Eingabe") fällt damit in genau den Sektionen ab, die der Anwender noch ablesen will.
- **Korrektur:** Opacity deutlich abschwächen (z. B. `.85`) oder ganz entfernen – die `stale-note` trägt die Veraltungs-Botschaft bereits eindeutig; die Dimmung der Fließtext-Zellen genügt als Sekundärsignal.

## Geprüfte Bereiche (ohne Befund – Iteration-1-Befunde als behoben verifiziert)

- **Befund 1 – Instanz-Kennung:** `instanceTag` (`Instanz {Name}`-Span) jetzt in allen drei `h2` je Instanz enthalten (`StatusPageComposer.cs` Z. 125, 129, 148, 166); Header listet zusätzlich alle Instanzen mit gekürzter ID (Z. 83–90); der Snapshot-los-Zustand titelt die Sektion selbst „Instanz {Name}" (Z. 115–117). Multi-Instanz-Zuordnung ist durchgehend erkennbar.
- **Befund 2 – Felder sichtbar und gesperrt:** `RemoteStatusFieldsArea` hat keine `Visibility`-Bindung mehr; Server-URL, Anzeigename und Token sind per `IsEnabled="{Binding IsRemoteStatusFieldEnabled}"` gesperrt (XAML Z. 758, 772, 785; VM Z. 428), der Hinweis-TextBlock `RemoteStatusDisabledHint` erklärt im deaktivierten Zustand Sperrung und Sichtbarkeit (Z. 746–751). Instanz-ID (read-only) und Verbindungsstatus bleiben immer einsehbar; die Instanz-ID wird im deaktivierten Zustand auch aus dem gespeicherten Setting geladen (VM Z. 700). Entspricht der Plan-Vorgabe „deaktiviert → Felder gesperrt mit Hinweis".
- **Befund 3 – PasswordBox-Race:** Gelöst über einen alternativen, sauberen Weg: Die Box bleibt bewusst leer (Kommentar `SettingsView.xaml.cs` Z. 92–95), stattdessen signalisiert `RemoteStatusTokenStoredHint` (XAML Z. 789–794) über `HasRemoteStatusToken` (VM Z. 405, 411) „Ein Token ist bereits hinterlegt …". Der Hinweis erscheint nach dem asynchronen `LoadAsync` korrekt per Binding und wird auch beim Leeren der Box sofort ausgeblendet – „Token gesetzt" ist von „kein Token" unterscheidbar.
- **Befund 4 – Datum bei Zeitstempeln:** `FmtTime` gibt jetzt `dd.MM. HH:mm:ss` aus, sobald Kalendertag abweicht oder der Abstand ≥24 h beträgt (Z. 324–331); sämtliche Aufrufstellen übergeben `now`. UTC-Kennzeichnung bleibt an der Aufrufstelle.
- **Befund 5 – `nowrap`:** Aus `.pill` entfernt (Z. 364); lange Fallback-Statuswerte können jetzt über `overflow-wrap: anywhere` umbrechen und sprengen die Spalte nicht mehr.
- **Befund 6 – Fehlertext:** `GitHubStatusService.DescribeError` (Z. 190–215) mappt auf verständliche deutsche Kategorietexte (401/403+429/404/Netzwerk/Timeout/JSON); die Rohmeldung bleibt im Log. Anzeige im `stale-note` inkl. Fehlerzeitpunkt (Composer Z. 249–253).
- **Befund 7 – Datenfrische nach Folgefehlern:** `CacheEntry` trennt jetzt `FetchedUtc` (Abrufversuch, TTL-Steuerung) von `LastSuccessUtc` und `ErrorUtc` (Z. 254–269); `ToResult` meldet bei Fehlern die letzte Erfolgszeit als `FetchedUtc`. Das Quellen-Badge zeigt damit den echten Datenstand, der Fehlerbalken separat den Fehlerzeitpunkt.
- **Befund 8 – `.dim`-Kontrast:** `.dim td` auf `#6b7280` angehoben (Z. 375), ≈4,8:1 – Fließtext ausreichend lesbar. Restrisiko siehe Befund 2 oben (Pills).
- **Befund 9 – „Keine Daten"-Hinweis:** `stale-note`-Balken mit Aktivierungsort „Einstellungen → Automatisierung → Remote-Übersicht" vor dem leeren Zustand ergänzt (Z. 52–54) – entspricht dem abgenommenen Entwurf.
- **Befund 10 – Fehler-Farbhervorhebung:** Mechanismus (`IsRemoteStatusConnectionError`, VM Z. 422–424, `OnPropertyChanged` bei State-Wechsel Z. 1377; `ErrorBrush`-Ressource in beiden Themes vorhanden) ist vollständig angelegt, aber durch das lokale `Foreground`-Attribut wirkungslos – siehe Befund 1 oben.
- **Befund 11 – Checks-Spalte:** Bewusster v1-Verzicht dokumentiert in `server/README.md` Z. 139–140 („Kein Checks-/Actions-Spalten-Platz in v1 … rein lesend"). Klärungsbedarf erledigt.
- **Neue UI-Elemente benannt:** `AutomationProperties.Name` auf `RemoteStatusDisabledHint` und `RemoteStatusTokenStoredHint` gesetzt; `HelpText`-Bindungen an den gebundenen Texten unverändert.
- **Ressourcen vorhanden:** `BoolToVisibilityConverter`/`InverseBoolToVisibilityConverter` in `App.xaml` (Z. 14, 16), `ErrorBrush` in `LightTheme.xaml`/`DarkTheme.xaml` (je Z. 52) definiert – keine `DynamicResource`-Fehler zu erwarten.
