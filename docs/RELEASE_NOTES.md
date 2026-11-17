# Release Notes

## Important Notes Before Update

- GitHub and Bitbucket plugins no longer use SSH for git operations: SSH remote URLs (`git@…`, `ssh://…`) are automatically converted to HTTPS, and the previously written insecure SSH configuration (`GIT_SSH_COMMAND` environment variable / `core.sshCommand` git config with disabled host key checking) is removed. Repositories that are only reachable via SSH can no longer be used through the plugins.

## What's New

- SCM event tasks can request a delayed execution: writing the configurable delay keyword (default `#verzögert`, Settings → Automation → Run tasks) followed by a duration such as `30min`, `4h`, `2t`, `1w` or `m1` (calendar day of month) in the same text as the trigger defers the event — the event stays in the queue as "Verzögert", the task is paused until the target time, the receipt is commented on GitHub, the persisted timer survives restarts and blocks the automatic shutdown (maximum 32 days).
- SCM event monitoring now also accepts the GitHub `github-actions` account (`github-actions[bot]` / `app/github-actions`) as a trusted author/editor in addition to the repository owner — issues and comments containing a trigger that were created by GitHub Actions workflows are executed as tasks automatically.
- Security: downloaded update packages are now verified against the SHA-256 hash published in the update manifest (`update.json`) before being installed — a tampered or corrupted download is discarded instead of applied. The update progress dialog shows a new "Verification" phase.
- Security: the GitHub and Bitbucket plugins now use HTTPS-only git transport — the insecure SSH configuration with disabled host key verification (a man-in-the-middle attack vector) has been removed. Existing managed working copies are migrated automatically: on the next push, pull, or PR checkout the remote URL is switched to HTTPS and the plugin-written `core.sshCommand` entry is unset.

## Wichtige Hinweise vor dem Update

- Die GitHub- und Bitbucket-Plugins verwenden für Git-Operationen kein SSH mehr: SSH-Remote-URLs (`git@…`, `ssh://…`) werden automatisch auf HTTPS umgestellt, und die zuvor geschriebene unsichere SSH-Konfiguration (`GIT_SSH_COMMAND`-Umgebungsvariable / `core.sshCommand`-Git-Konfiguration mit deaktivierter Host-Key-Prüfung) wird entfernt. Repositories, die nur über SSH erreichbar sind, können über die Plugins nicht mehr genutzt werden.

## Neuerungen

- SCM-Aufgaben können eine verzögerte Ausführung anfordern: Steht im selben Text wie der Trigger das konfigurierbare Verzögerungs-Keyword (Standard `#verzögert`, Einstellungen → Automatisierung → Aufgaben ausführen) gefolgt von einer Angabe wie `30min`, `4h`, `2t`, `1w` oder `m1` (Kalendertag im Monat), ruht das Ereignis als „Verzögert" in der Queue — die Aufgabe wird bis zum Zielzeitpunkt pausiert, der Empfang wird auf GitHub kommentiert und der persistierte Timer übersteht Neustarts und blockiert den Auto-Shutdown (maximal 32 Tage).
- Die SCM-Ereignisüberwachung akzeptiert neben dem Repository-Owner jetzt auch das GitHub-Konto `github-actions` (`github-actions[bot]` / `app/github-actions`) als vertrauenswürdigen Autor/Bearbeiter — durch GitHub-Actions-Workflows angelegte Issues und Kommentare mit Trigger werden automatisch als Aufgaben ausgeführt.
- Sicherheit: Heruntergeladene Update-Pakete werden vor der Installation jetzt gegen den im Update-Manifest (`update.json`) veröffentlichten SHA-256-Hash geprüft — ein manipulierter oder beschädigter Download wird verworfen statt installiert. Der Update-Fortschrittsdialog zeigt dafür eine neue Phase „Verifikation".
- Sicherheit: Die GitHub- und Bitbucket-Plugins nutzen jetzt ausschließlich HTTPS als Git-Transport — die unsichere SSH-Konfiguration mit deaktivierter Host-Key-Prüfung (ein Man-in-the-Middle-Angriffsvektor) wurde entfernt. Bestehende verwaltete Arbeitskopien werden automatisch migriert: Beim nächsten Push, Pull oder PR-Checkout wird die Remote-URL auf HTTPS umgestellt und der vom Plugin geschriebene `core.sshCommand`-Eintrag entfernt.
