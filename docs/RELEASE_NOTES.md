# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- New "Updates" section in Settings: a mode selector controls whether the update check is off, check-only, or automatically checks and installs a found update at program startup (default: check only).
- New "Load prerelease versions" checkbox in Settings to include prerelease versions in the update check.
- The update check now evaluates all GitHub releases (paginated) via semantic version comparison instead of only the latest stable release.
- Saving changed update settings cancels a running update operation and clears a pending update offer.
- Tasks can be paused until a chosen date and time via the "Pause einstellen" ribbon button in the task detail view; paused tasks show "⏸ Pausiert" with a remaining-time countdown and a dimmed tile in the sidebar.
- AI CLI session limits are detected: the reset time is persisted per plugin, recorded in the task protocol, and running tasks of that plugin are paused automatically.
- Tasks with a known future session limit no longer block the program update check as risky.
- Fixed: the CLI name in the task detail footer and the sidebar now updates after a plugin switch; a manual CLI restart also uses the switched plugin.
- Generated issue.md files now include a "Verknüpftes Issue" section when a valid issue reference is present.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Neuer Bereich „Updates" in den Einstellungen: Eine Auswahlbox legt fest, ob die Update-Prüfung ausgeschaltet ist, nur prüft oder beim Programmstart prüft und ein gefundenes Update automatisch installiert (Standard: nur prüfen).
- Neue Checkbox „Prerelease-Versionen laden" in den Einstellungen, um Prerelease-Versionen in die Update-Prüfung einzubeziehen.
- Die Update-Prüfung wertet jetzt alle GitHub-Releases (paginiert) per semantischem Versionsvergleich aus, statt nur das neueste stabile Release zu betrachten.
- Das Speichern geänderter Update-Einstellungen bricht einen laufenden Update-Vorgang ab und entfernt ein vorhandenes Update-Angebot.
- Aufgaben können über den Ribbon-Button „Pause einstellen" in der Aufgabendetailansicht bis zu einem wählbaren Zeitpunkt pausiert werden; pausierte Aufgaben zeigen „⏸ Pausiert" mit Countdown und abgeblendeter Kachel in der Seitenleiste.
- Session-Limits der KI-CLI werden erkannt: Der Reset-Zeitpunkt wird pro Plugin persistiert, im Aufgaben-Protokoll vermerkt und laufende Aufgaben des Plugins automatisch pausiert.
- Aufgaben mit bekanntem zukünftigem Session-Limit blockieren die Programmupdate-Prüfung nicht mehr als riskant.
- Behoben: Der CLI-Name in der Fußzeile der Aufgabendetailansicht und in der Seitenleiste aktualisiert sich jetzt nach einem Plugin-Wechsel; auch ein manueller CLI-Neustart verwendet das gewechselte Plugin.
- Generierte issue.md-Dateien enthalten jetzt einen Abschnitt „Verknüpftes Issue", wenn eine gültige Issue-Referenz vorliegt.
