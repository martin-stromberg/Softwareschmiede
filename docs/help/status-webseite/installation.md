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

**Entfernte Einträge (ohne Migration/Fallback):** `Page:Username`,
`Page:Password`, `Relay:Tokens`, `Relay:DevToken` — der Seitenzugriff läuft
über die Cookie-Anmeldung, Relay-Tokens kommen aus der Datenbank. Altbestände
werden ignoriert.

## Umgebungsvariablen

Alle `appsettings`-Schlüssel sind wie üblich per
`__`-Konvention übersteuerbar, z. B. `GitHub__Token`. Die Sitzungsdauer ist
**keine** Konfigurationsdatei-Einstellung, sondern liegt in der Datenbank
(`ServerSettings["SessionDurationMinutes"]`, nur über die Admin-UI änderbar,
Default 480 Minuten, Bereich 5–43200).

## Datenablage und Recovery

- Alle Zustände (Benutzer, Rollen, Sperren, Deaktivierungen, Token-Hashes,
  Einstellungen) liegen in `data/statusserver.db`; das `data`-Verzeichnis gehört
  in Sicherung und Monitoring.
- **Recovery:** Es gibt bewusst keinen In-App-Recovery-Weg. Sind alle
  Administratoren ausgesperrt, das `data`-Verzeichnis löschen und den Server
  neu starten — der Setup-Lauf (`/register`) beginnt von vorn.

## Überprüfung

- `GET /` ohne Session → Redirect auf `/register` (Setup) bzw. `/login`.
- Nach dem Setup: Anmeldung, Status-Seite sichtbar; Admin-Links nur bei Stufe
  `Administration`.
- `data/statusserver.db` existiert und wächst mit angelegten Benutzern/Tokens.
