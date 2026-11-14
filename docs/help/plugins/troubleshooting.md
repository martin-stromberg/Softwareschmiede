← [Zurück zur Übersicht](index.md)

# Plugin-System — Fehlerbehebung

## Klon schlägt fehl: „Es werden nur HTTPS-Repository-URLs verwendet"

**Symptom:** Das Klonen eines verknüpften Repositories bricht ab, im Protokoll steht die Meldung „git clone abgebrochen: Es werden nur HTTPS-Repository-URLs verwendet; SSH-URLs werden automatisch konvertiert." (GitHub- und Bitbucket-Plugin).

**Ursache:** Die SCM-Plugins führen Git-Operationen ausschließlich über HTTPS aus — der früher verwendete SSH-Transport mit abgeschalteter Server-Identitätsprüfung wurde entfernt. Repository-Adressen im SSH-Format (`git@host:pfad`, `ssh://[git@]host/pfad` mit Standard-Port) werden dabei automatisch in die HTTPS-Form umgewandelt; nur Adressen, die sich nicht eindeutig überführen lassen — etwa `ssh://` mit einem abweichenden Port (z. B. Port 7999 eines Self-Hosted-Bitbucket-Servers), `http://`-, `git://`-Schemata oder lokale Pfade — werden abgelehnt.

**Lösung:**
1. Prüfen Sie die beim Repository bzw. Projekt hinterlegte Repository-URL und tragen Sie die HTTPS-Adresse des Repositories ein (bei GitHub/Bitbucket Cloud: in der Weboberfläche über „Clone" → HTTPS kopierbar; bei Self-Hosted-Bitbucket z. B. `https://<server>/scm/<PROJEKT>/<repo>.git`).
2. Bei einer SSH-Adresse mit Standard-Port ist keine Umstellung nötig — sie wird transparent konvertiert.
3. Bereits geklonte Arbeitskopien müssen nicht angepasst werden: eine dort noch in SSH-Form hinterlegte Verbindungsadresse sowie ein früher vom Plugin gesetzter, die Prüfung aushebelnder SSH-Konfigurationseintrag werden beim nächsten Push, Pull oder Pull-Request-Bezug automatisch bereinigt.

## Manuell gepflegte `github.com`-Einträge in `_netrc` wurden entfernt

**Symptom:** Nach einem Programmupdate fehlen in der Datei `_netrc` (Windows) bzw. `.netrc` im Benutzerprofil die Einträge für `github.com` und `api.github.com`. Externe Werkzeuge, die sich auf diese Einträge verlassen hatten, melden Authentifizierungsfehler.

**Ursache:** Seit der Umstellung der GitHub-Anmeldung auf eine nicht-persistierende Variante legt die Anwendung den GitHub-Token nicht mehr in dieser Datei ab — frühere Versionen hatten ihn dort im Klartext hinterlegt. Beim Programmstart entfernt das GitHub-Plugin einmalig alle `github.com`-/`api.github.com`-Einträge aus einer vorhandenen Datei, einschließlich manuell gepflegter, da diese von den früher automatisch angelegten nicht unterscheidbar sind.

**Lösung:**
1. Für die Softwareschmiede selbst ist keine Aktion nötig — Klonen, Push, Pull und das Abrufen von Branches funktionieren weiterhin mit dem in den Einstellungen hinterlegten GitHub-Token.
2. Nutzen andere Werkzeuge (z. B. ein manuell gestartetes `git` in einem eigenen Terminal) dieselbe Datei für github.com, richten Sie deren Anmeldung unabhängig ein — etwa über den Git Credential Manager (`git config --global credential.helper manager`) oder einen anderen Mechanismus des jeweiligen Werkzeugs. Die `github.com`-Einträge sollten nicht erneut in `_netrc` angelegt werden: Sie würden beim nächsten Programmstart wieder entfernt.
3. Einträge anderer Anbieter (z. B. `bitbucket.org` oder Self-Hosted-Hosts) sind von der Bereinigung nicht betroffen und können weiterhin in derselben Datei gepflegt werden — das BitBucket-Plugin nutzt sie aktiv weiter.

> **Hinweis:** Enthielt die Datei nur GitHub-Einträge, wurde sie beim Programmstart komplett gelöscht. Das ist beabsichtigt — eine leere Zugangsdaten-Datei hat keinen Nutzen.
