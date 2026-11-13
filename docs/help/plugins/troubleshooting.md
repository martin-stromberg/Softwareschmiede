← [Zurück zur Übersicht](index.md)

# Plugin-System — Fehlerbehebung

## Manuell gepflegte `github.com`-Einträge in `_netrc` wurden entfernt

**Symptom:** Nach einem Programmupdate fehlen in der Datei `_netrc` (Windows) bzw. `.netrc` im Benutzerprofil die Einträge für `github.com` und `api.github.com`. Externe Werkzeuge, die sich auf diese Einträge verlassen hatten, melden Authentifizierungsfehler.

**Ursache:** Seit der Umstellung der GitHub-Anmeldung auf eine nicht-persistierende Variante legt die Anwendung den GitHub-Token nicht mehr in dieser Datei ab — frühere Versionen hatten ihn dort im Klartext hinterlegt. Beim Programmstart entfernt das GitHub-Plugin einmalig alle `github.com`-/`api.github.com`-Einträge aus einer vorhandenen Datei, einschließlich manuell gepflegter, da diese von den früher automatisch angelegten nicht unterscheidbar sind.

**Lösung:**
1. Für die Softwareschmiede selbst ist keine Aktion nötig — Klonen, Push, Pull und das Abrufen von Branches funktionieren weiterhin mit dem in den Einstellungen hinterlegten GitHub-Token.
2. Nutzen andere Werkzeuge (z. B. ein manuell gestartetes `git` in einem eigenen Terminal) dieselbe Datei für github.com, richten Sie deren Anmeldung unabhängig ein — etwa über den Git Credential Manager (`git config --global credential.helper manager`) oder einen anderen Mechanismus des jeweiligen Werkzeugs. Die `github.com`-Einträge sollten nicht erneut in `_netrc` angelegt werden: Sie würden beim nächsten Programmstart wieder entfernt.
3. Einträge anderer Anbieter (z. B. `bitbucket.org` oder Self-Hosted-Hosts) sind von der Bereinigung nicht betroffen und können weiterhin in derselben Datei gepflegt werden — das BitBucket-Plugin nutzt sie aktiv weiter.

> **Hinweis:** Enthielt die Datei nur GitHub-Einträge, wurde sie beim Programmstart komplett gelöscht. Das ist beabsichtigt — eine leere Zugangsdaten-Datei hat keinen Nutzen.
