# Bestandsaufnahme: Überwachung von GitHub

## Kurzbefund

Die Anwendung besitzt bereits SCM-Plugins für GitHub und Bitbucket sowie wiederverwendbare Verträge für Repository-, Issue- und Pull-Request-Funktionen. Im vorhandenen `IGitPlugin`-Vertrag und in den SCM-Plugins ist jedoch kein Ereignis- oder Kommentarüberwachungsprozess erkennbar. Das vorhandene Pull-Request-Monitoring fragt Status und Workflow-Runs bereits verknüpfter PRs periodisch ab; es verarbeitet keine Kommentare und ist fachlich ein anderer Ablauf.

Aufgaben, aktive CLI-Läufe und Terminal-Sitzungen werden in Application-Services verwaltet. Der persistierte Aufgabenstatus und der tatsächliche CLI-Prozessstatus sind getrennte Felder. Es gibt keine erkennbare fachliche Ereignis-Queue für eingehende SCM-Aufträge; vorhandene Queues betreffen unter anderem CLI-Ausgabe und dürfen nicht damit verwechselt werden.

## Relevante Bereiche

- [SCM- und GitHub-Bestand](inventory/scm.md): Plugin-Verträge, Provider und bestehendes PR-Monitoring.
- [Aufgaben-, CLI- und Queue-Bestand](inventory/tasks-cli.md): Aufgabenstatus, laufende Prozesse, Startpfade und Persistenz.

## Konsequenzen für die Planung

- Ein providerübergreifender Ereignisvertrag ist erforderlich, wenn weitere SCM-Systeme denselben Prozess unterstützen sollen.
- GitHub benötigt zusätzlich zur vorhandenen Issue-/PR-Abfrage Fähigkeiten zum Empfangen oder Abrufen von Kommentaren sowie zum Setzen der Empfangsreaktion. Die Wahl zwischen Webhook und Polling ist im Bestand nicht vorgeprägt.
- Das Ereignis muss anhand Repository-Owner, Autor und Trigger validiert und einem lokalen Projekt/Aufgabe-Kontext zugeordnet werden. Bestehende Issue-/PR-Referenzen und Repository-Zuordnungen können dabei als Ausgangspunkt dienen.
- Vor einer neuen Ereignis-Queue ist zu entscheiden, welche Einträge dauerhaft gespeichert werden müssen und wie Wiederholung/Deduplizierung nach Neustarts funktioniert.
- Der CLI-Fall muss zwischen aktivem Aufgabenstatus und tatsächlich laufendem Prozess unterscheiden. Für laufende Sitzungen ist die bestehende Terminal-/Prompt-Verarbeitung zu untersuchen; für neu zu startende Sitzungen existieren bereits zentrale Dienste.
- Der Promptversand kann nicht automatisch als Antwort im externen Issue verstanden werden: Ein SCM-seitiger Kontext und die Berechtigung bzw. Fähigkeit, dort zu antworten, müssen mit dem Agentenprozess verfügbar sein.

## Status und Umfang

Die Bestandsaufnahme ist eine Quellcodeanalyse. Es wurden keine Änderungen an Produktcode und keine Tests ausgeführt. Detaillierte Fundstellen und bekannte Lücken sind in den oben verlinkten Dokumenten erfasst.
