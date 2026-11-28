# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- The status website now supports Light and Dark mode: the color scheme follows `prefers-color-scheme` automatically (fallback Light) and can be overridden per user in the profile (Automatic / Light / Dark, persisted in SQLite via a new `Theme` column on the user); implemented server-side via a `data-theme` attribute on `<html>` plus CSS custom properties — no JavaScript required.
- Fixed: the assign trigger now also works on processed issues and pull requests — descriptions rewritten by issue processing under the monitored account (carrying the `<!-- softwareschmiede:action:<id> -->` marker) are no longer discarded as an "own response" when the assignment comes from an authorized assigner (repository owner, monitored account, or trusted automation account); the plugin's marker pre-filter now only applies to comments, while the recursion guard of the regular mention/keyword trigger path remains unchanged.
- The status server can now update itself automatically via msTools.Updater: the update source is the repository's GitHub releases with a separate `statusserver-update.json` manifest asset (packages verified via SHA-256 and ZIP integrity); release channel (Stable/Pre-Release) and automation mode (off / check only / check and install at a configurable daily time) are managed in the admin UI under Settings → Updates, together with manual actions ("check for update now", "install update now" with downtime confirmation, orphaned-lock reset) and an update status table. `appsettings*.json` and the `data/` directory are preserved during the file exchange, the installed version is read from the new `release-metadata.json` in the package, and the restart runs platform-dependently (systemd transient unit, Windows service via `Update:ServiceName`, IIS `app_offline.htm`); the release pipeline publishes each status server package including `release-metadata.json` plus the `statusserver-update.json` manifest asset.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Die Status-Webseite unterstützt jetzt Light- und Darkmode: Das Farbschema folgt automatisch `prefers-color-scheme` (Fallback Light) und kann pro Benutzer im Profil überschrieben werden (Automatisch / Light / Dark, in SQLite über eine neue `Theme`-Spalte am Benutzer persistiert); server-seitig umgesetzt über ein `data-theme`-Attribut am `<html>`-Tag plus CSS Custom Properties — ganz ohne JavaScript.
- Behoben: Der Assign-Auslöser funktioniert jetzt auch bei aufbereiteten Issues und Pull Requests — Beschreibungen, die von der Issue-Aufbereitung unter dem Überwachungskonto neu geschrieben wurden (mit `<!-- softwareschmiede:action:<id> -->`-Marker), werden bei berechtigter Zuweisung (Repository-Owner, Überwachungskonto oder vertrauenswürdiges Automatisierungskonto) nicht mehr als „Eigene Antwort" verworfen; der Marker-Vorfilter des Plugins greift nur noch bei Kommentaren, der Rekursionsschutz des regulären Erwähnungs-/Keyword-Triggers bleibt unverändert.
- Der StatusServer kann sich jetzt über msTools.Updater selbst aktualisieren: Update-Quelle sind die GitHub-Releases des Repositories mit separatem Manifest-Asset `statusserver-update.json` (Pakete per SHA-256 und ZIP-Integrität verifiziert); Release-Kanal (Stable/Pre-Release) und Automatisierung (aus / nur prüfen / prüfen und täglich zur eingestellten Uhrzeit installieren) werden in der Admin-UI unter Einstellungen → Updates verwaltet — zusammen mit manuellen Aktionen („Jetzt nach Update suchen", „Update jetzt installieren" mit Downtime-Bestätigung, Zurücksetzen einer verwaisten Sperre) und einer Update-Statustabelle. `appsettings*.json` und das `data/`-Verzeichnis bleiben beim Dateiaustausch geschützt, die installierte Version stammt aus der neuen `release-metadata.json` im Paket, und der Neustart läuft plattformabhängig (systemd-Transient-Unit, Windows-Dienst über `Update:ServiceName`, IIS `app_offline.htm`); die Release-Pipeline veröffentlicht jedes StatusServer-Paket samt `release-metadata.json` plus dem Manifest-Asset `statusserver-update.json`.
