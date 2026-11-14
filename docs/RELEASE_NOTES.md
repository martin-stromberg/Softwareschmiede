# Release Notes

## Important Notes Before Update

- GitHub and Bitbucket plugins no longer use SSH for git operations: SSH remote URLs (`git@…`, `ssh://…`) are automatically converted to HTTPS, and the previously written insecure SSH configuration (`GIT_SSH_COMMAND` environment variable / `core.sshCommand` git config with disabled host key checking) is removed. Repositories that are only reachable via SSH can no longer be used through the plugins.

## What's New

- Security: the GitHub and Bitbucket plugins now use HTTPS-only git transport — the insecure SSH configuration with disabled host key verification (a man-in-the-middle attack vector) has been removed. Existing managed working copies are migrated automatically: on the next push, pull, or PR checkout the remote URL is switched to HTTPS and the plugin-written `core.sshCommand` entry is unset.

## Wichtige Hinweise vor dem Update

- Die GitHub- und Bitbucket-Plugins verwenden für Git-Operationen kein SSH mehr: SSH-Remote-URLs (`git@…`, `ssh://…`) werden automatisch auf HTTPS umgestellt, und die zuvor geschriebene unsichere SSH-Konfiguration (`GIT_SSH_COMMAND`-Umgebungsvariable / `core.sshCommand`-Git-Konfiguration mit deaktivierter Host-Key-Prüfung) wird entfernt. Repositories, die nur über SSH erreichbar sind, können über die Plugins nicht mehr genutzt werden.

## Neuerungen

- Sicherheit: Die GitHub- und Bitbucket-Plugins nutzen jetzt ausschließlich HTTPS als Git-Transport — die unsichere SSH-Konfiguration mit deaktivierter Host-Key-Prüfung (ein Man-in-the-Middle-Angriffsvektor) wurde entfernt. Bestehende verwaltete Arbeitskopien werden automatisch migriert: Beim nächsten Push, Pull oder PR-Checkout wird die Remote-URL auf HTTPS umgestellt und der vom Plugin geschriebene `core.sshCommand`-Eintrag entfernt.
