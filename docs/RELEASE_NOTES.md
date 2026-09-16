# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- Tasks can be paused until a chosen date and time via the new "Pause einstellen" ribbon button in the task detail view (prefilled with the current time; an active pause can be lifted early). Paused tasks show "⏸ Pausiert" with a remaining-time countdown and a dimmed tile in the program-menu sidebar.
- When the AI CLI reports a session limit, the reset time is persisted per AI plugin, recorded in the task protocol, and all running tasks of that plugin are paused automatically — already running CLI processes are not interrupted.
- Tasks whose plugin has a known future session limit no longer count as risky in the program update check, so updates are not held back by a tolerance window.
- Fixed: the CLI name in the task detail footer and the program-menu sidebar now updates after a plugin switch instead of sticking to the project default CLI; a manual CLI restart also uses the switched plugin.
- Generated issue.md files now include a "Verknüpftes Issue" (linked issue) section when a valid issue reference is present.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Aufgaben können über den neuen Ribbon-Button „Pause einstellen" in der Aufgabendetailansicht bis zu einem wählbaren Datum und einer Uhrzeit pausiert werden (Vorbelegung: aktueller Zeitpunkt; eine aktive Pause lässt sich vorzeitig aufheben). Pausierte Aufgaben zeigen in der Programmmenü-Seitenleiste „⏸ Pausiert" mit Countdown der Restzeit und abgeblendeter Kachel.
- Meldet die KI-CLI ein Session-Limit, wird der Reset-Zeitpunkt pro KI-Plugin persistiert, im Aufgaben-Protokoll vermerkt und alle laufenden Aufgaben desselben Plugins automatisch pausiert — bereits laufende CLI-Prozesse werden dabei nicht unterbrochen.
- Aufgaben, deren Plugin ein bekanntes zukünftiges Session-Limit hat, gelten bei der Programmupdate-Prüfung nicht mehr als riskant; Updates werden dadurch nicht durch eine Toleranzzeit zurückgehalten.
- Behoben: Der CLI-Name in der Fußzeile der Aufgabendetailansicht und in der Programmmenü-Seitenleiste aktualisiert sich jetzt nach einem Plugin-Wechsel, statt auf der Projekt-Standard-CLI hängen zu bleiben; auch ein manueller CLI-Neustart verwendet das gewechselte Plugin.
- Generierte issue.md-Dateien enthalten jetzt einen Abschnitt „Verknüpftes Issue", wenn eine gültige Issue-Referenz vorliegt.
