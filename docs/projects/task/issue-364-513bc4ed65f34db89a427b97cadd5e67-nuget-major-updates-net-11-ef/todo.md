# Projekt-Todo – NuGet-Major-Updates (.NET 11 / EF Core 11, xunit.v3, SQLitePCLRaw 3.x)

Basisbranch: `task/issue-364-513bc4ed65f34db89a427b97cadd5e67-nuget-major-updates-net-11-ef`
Issue: #364 (aufgeschobene Major-Updates aus #361)
Abweichung vom Skill-Standard: Keine Unteragenten in dieser Umgebung verfügbar/freigegeben —
alle Phasen (Anforderung, Bestandsaufnahme, Planung, Planprüfung, Lifecycle-Schritte, Abnahmen)
werden vom Orchestrator selbst in getrennten, nacheinander ausgeführten Phasen bearbeitet.

## Checkliste

- [x] Branchprüfung (kein Hauptbranch, sauberer Git-Zustand)
- [x] Verzeichnis/Tracking (`docs/projects/{branchname}/`, diese todo.md)
- [x] Einstiegspunkt ermittelt (neues Projekt — keine Artefakte vorhanden)
- [ ] Anforderung aufbereitet (`requirement.md`)
- [ ] Bestandsaufnahme (`inventory.md`)
- [ ] Projektplan (`project-plan.md`)
- [ ] Offene Punkte geklärt
- [ ] Planprüfung (`project-plan-check.md`, Status „Projektplan vollständig")
- [ ] Planungscommit
- [ ] Entwicklungsschritte-Tracking (`steps.md`, `status.html`)
- [ ] Entwicklungsschleife (Schritte 1..n mit Lifecycle + Abnahme + Merge)
- [ ] Aufräumen (Projektverzeichnis per `git rm -r` entfernen)
- [ ] Abschlusscommit

## Vorbereitende SCM-Sequenz (aus Issue-Kommentar, vor Entwicklungsstart)

- [ ] Release-Lauf zu Promotion #418 abwarten (Run 44268624839)
- [ ] Backmerge-PR #419 (main → staging) per Admin-Merge schließen — Kontext: martin-stromberg
- [ ] Task-Branch auf aktualisiertes staging rebasen
