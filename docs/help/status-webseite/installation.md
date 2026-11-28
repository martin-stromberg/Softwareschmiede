← [Zurück zur Übersicht](index.md)

# Status-Webseite — Installation und Konfiguration

## Voraussetzungen

- Self-contained Publish des `Softwareschmiede.StatusServer` (aus dem Release:
  `statusserver-linux-x64.zip` / `statusserver-win-x64.zip`) oder eine
  .NET-11-Laufzeit samt Build-Ausgabe — siehe `server/README.md` für
  systemd-Unit und nginx-Beispiel.
- Schreibrecht auf ein `data`-Verzeichnis unter dem Content-Root (wird beim
  Start automatisch angelegt).

## Installationsschritte

1. Server bereitstellen und starten — beim ersten Start wird
   `data/statusserver.db` angelegt und die Migration
   `InitialCreate` ausgeführt (`StatusServerDatabaseInitializer`); die Rollen
   `Administration` und `Ueberwacher` werden geseedet.
2. Die Adresse im Browser öffnen → Weiterleitung auf `/register`; das
   Administratorkonto anlegen.
3. Pro App-Instanz unter `/admin/tokens` ein Relay-Token erzeugen und in der
   App hinterlegen (Remote-Übersicht).
4. Hinter einem HTTPS-Reverse-Proxy `X-Forwarded-Proto` weiterreichen, damit
   das Anmelde-Cookie als Secure markiert wird (Forwarded-Headers sind
   konfiguriert, `ForwardedHeaders.All`, Trusted-Proxy-Liste geleert).

## Konfiguration

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `Page:RefreshSeconds` | int | 15 | Meta-Refresh-Intervall der Status-Seite |
| `App:StaleAfterSeconds` | int | 300 | Veraltungsschwelle verbundener Snapshots |
| `GitHub:Repositories` | Liste | `[]` | Repos für die GitHub-Sektionen (`owner/repo`) |
| `GitHub:Token` | string? | `null` | Optionales PAT für höhere Rate-Limits |
| `GitHub:CacheTtlSeconds` | int | 60 | Mindestabstand zwischen GitHub-Abrufen |
| `Data:DatabasePath` | string? | `<ContentRoot>/data/statusserver.db` | Interner Override des SQLite-Pfads (primär Test-Isolation) |
| `PathBase` | string? | `null` | Optionaler URL-Unterpfad-Präfix für den Betrieb hinter einem pfadbasierten Reverse Proxy (z. B. `/softwareschmiede` statt eigener Subdomain/Domain-Root) |
| `Update:Enabled` | bool | `false` | Master-Schalter des Update-Subsystems; der persistierte Admin-Modus überschreibt ihn (manuelle Aktionen laufen unabhängig davon) |
| `Update:HostedServicesEnabled` | bool | `true` | Registriert die Bibliotheks-Hintergrunddienste (Prüf-/Installations-Scheduler) |
| `Update:SourceType` | string | `Github` | `Github` oder `LocalFolder` (Tests/Offline-Betrieb) |
| `Update:RepositoryOwner` / `Update:RepositoryName` | string | `martin-stromberg` / `Softwareschmiede` | GitHub-Repository der Release-Quelle |
| `Update:ManifestAssetName` | string | `statusserver-update.json` | Manifest-Asset im Release (separat vom Desktop-`update.json`) |
| `Update:LocalFolderPath` | string? | `null` | Verzeichnis der LocalFolder-Quelle (Manifest + Paket) |
| `Update:SourceCheckStartTime` / `Update:SourceCheckEndTime` | TimeOnly | `20:00` / `06:00` | Tägliches Prüfzeitfenster (Über-Mitternacht-Fenster wird intern je Tag in zwei Ranges aufgeteilt) |
| `Update:WorkingDirectory` | string | `updates` | Workspace relativ zum ContentRoot (Pakete, Status, Lock, Logs) |
| `Update:ServiceName` / `Update:ExecutablePath` | string? | `null` | Neustart-Ziel: Windows-Dienst-Name bzw. direkter Exe-Pfad (Linux/systemd und IIS werden automatisch erkannt) |
| `Update:HealthTimeoutSeconds` | int | `120` | Altersschwelle, ab der ein Installations-Lock als verwaist gilt |
| `Update:MaxAssetBytes` | long | `536870912` | Maximale Paketgröße |
| `Update:EnableAutomaticDownload` / `Update:EnableAutomaticInstallation` | bool | `true` / `false` | Auto-Download/-Installation — werden vom persistierten Admin-Modus überschrieben |
| `Update:StopHostAfterScriptStart` | bool | `false` | Default: Das Installationsskript steuert Stopp/Start des Servers selbst |

**Entfernte Einträge (ohne Migration/Fallback):** `Page:Username`,
`Page:Password`, `Relay:Tokens`, `Relay:DevToken` — der Seitenzugriff läuft
über die Cookie-Anmeldung, Relay-Tokens kommen aus der Datenbank. Altbestände
werden ignoriert.

## Automatische Updates

Der Server aktualisiert sich über die Bibliothek `msTools.Updater` aus den
GitHub-Releases des Repositories; das Update-Manifest ist das separate
Release-Asset `statusserver-update.json` (nicht das Desktop-`update.json` —
die Paketwahl läuft nur über Plattform + Runtime-Identifier und würde dort
mit `release.zip` kollidieren). Gefundene Pakete werden per SHA-256 gegen
das Manifest verifiziert.

- **Admin-Einstellungen:** Kanal (`stable`/`prerelease`), Automatisierungsmodus
  (`off`/`check`/`install`) und Installationsuhrzeit (`HH:mm`, Default
  `03:00`) liegen in der `ServerSettings`-Tabelle (Schlüssel `UpdateChannel`,
  `UpdateAutomationMode`, `UpdateInstallTime`) und werden bei jedem Start auf
  die Laufzeitoptionen angewendet — persistierte Werte gewinnen gegenüber
  `appsettings.json`. Änderbar nur über die Admin-UI (Seite „Einstellungen",
  Sektion „Updates").
- **Neustart je Plattform:** Die Installation läuft über ein detached
  gestartetes Skript, das den Server stoppt, das Paket einspielt und wieder
  startet — Linux/systemd als Transient-Unit
  `SoftwareschmiedeStatusServerUpdate` (Unit-Erkennung automatisch); unter
  Windows `Update:ServiceName` für den Dienstbetrieb bzw.
  `Update:ExecutablePath` für den direkten Exe-Start; unter IIS greifen
  `app_offline.htm`-Mechanik und automatische Erkennung
  (`ServiceName`/`ExecutablePath` werden dann mit Warnung überschrieben).
  Laufende `/relay`-Verbindungen brechen beim Neustart kurz ab; die
  App-Instanzen verbinden sich selbstständig neu.
- **Datenerhalt:** `appsettings*.json` und `data/*` werden beim
  Dateiaustausch gesichert und restauriert (`PreserveFile`); ein per
  `Data:DatabasePath` konfigurierter SQLite-Pfad innerhalb des
  Installationsverzeichnisses, der außerhalb der `data/*`-Abdeckung liegt,
  wird samt `-wal`/`-shm`-Seitendateien zusätzlich geschützt.
- **Workspace:** `Update:WorkingDirectory` (Default `updates/`) enthält
  `update.log` (Protokoll des Installationsskripts), `status.json`
  (persistierter Update-Status über Neustarts), `update.lock`
  (Installations-Lock) sowie die Paketverwaltung (`staging/`/`pending/`).
- **GitHub-Token:** Das optionale `GitHub:Token` wird vom Update-HttpClient
  als Bearer mitverwendet (Rate-Limit der anonymen Release-Abfragen); es ist
  kein separater Update-Token nötig.
- **Voraussetzung `release-metadata.json`:** Die installierte Version stammt
  aus dieser Datei im Installationsverzeichnis — Pakete aus Releases vor der
  Update-Pipeline-Erweiterung enthalten sie nicht. Dann meldet die
  Admin-Seite „unbekannt" und die Update-Prüfung bleibt wirkungslos; erst
  Pakete aus der aktuellen Release-Pipeline sind update-fähig.

Details stehen in `server/README.md`, Abschnitt „Automatische Updates".

## Umgebungsvariablen

Alle `appsettings`-Schlüssel sind wie üblich per
`__`-Konvention übersteuerbar, z. B. `GitHub__Token` oder
`Update__SourceType`. Die Sitzungsdauer ist **keine**
Konfigurationsdatei-Einstellung, sondern liegt in der Datenbank
(`ServerSettings["SessionDurationMinutes"]`, nur über die Admin-UI änderbar,
Default 480 Minuten, Bereich 5–43200) — ebenso der Update-Kanal, der
Automatisierungsmodus und die Installationsuhrzeit (`ServerSettings`-Schlüssel
`UpdateChannel`, `UpdateAutomationMode`, `UpdateInstallTime`).

## Datenablage und Recovery

- Alle Zustände (Benutzer, Rollen, Sperren, Deaktivierungen, Token-Hashes,
  Einstellungen) liegen in `data/statusserver.db`; das `data`-Verzeichnis gehört
  in Sicherung und Monitoring. Der Update-Workspace `updates/` (`status.json`,
  `update.log`, Locks, Pakete) ist ablaufbezogen und muss nicht gesichert
  werden.
- **Recovery:** Es gibt bewusst keinen In-App-Recovery-Weg. Sind alle
  Administratoren ausgesperrt, das `data`-Verzeichnis löschen und den Server
  neu starten — der Setup-Lauf (`/register`) beginnt von vorn.

## Überprüfung

- `GET /` ohne Session → Redirect auf `/register` (Setup) bzw. `/login`.
- Nach dem Setup: Anmeldung, Status-Seite sichtbar; Admin-Links nur bei Stufe
  `Administration`.
- `data/statusserver.db` existiert und wächst mit angelegten Benutzern/Tokens.
