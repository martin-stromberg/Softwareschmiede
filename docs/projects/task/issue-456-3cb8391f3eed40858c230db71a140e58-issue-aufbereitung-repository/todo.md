# Projekt-Todo – SCM-Ereignisverarbeitung & Abschlussüberwachung

Basisbranch: `task/issue-456-3cb8391f3eed40858c230db71a140e58-issue-aufbereitung-repository`
Issues: #456, #452, #451, #436, #433, #457

## Checkliste

- [x] Branchprüfung (aktueller Task-Branch = Projekt-Basisbranch, Stand = origin/staging)
- [x] Verzeichnis/Tracking angelegt
- [x] Einstiegspunkt: neues Projekt
- [x] Anforderung aufbereitet (`requirement.md`)
- [ ] Bestandsaufnahme (`inventory.md`)
- [ ] Projektplan (`project-plan.md`)
- [ ] Offene Punkte geklärt (Nutzerentscheidungen)
- [ ] Planprüfung (`project-plan-check.md`)
- [ ] Planungscommit
- [ ] Entwicklungsschritte-Tracking (`steps.md`)
- [ ] Entwicklungsschleife (je Schritt: lifecycle-Skill, Abnahme, Merge)
- [ ] Aufräumen + Abschlusscommit
- [ ] Issue-Kommentare zum Bearbeitungsstatus (vom Nutzer autorisiert)
- [ ] PR gegen `staging` mit Closing-Keywords für alle 6 Issues, Checks grün

## Hinweise

- Abweichung zum Skill: Keine Unteragenten verfügbar (Umgebungsrestriktion) — alle delegierbaren Arbeitsaufträge (Anforderung, Bestandsaufnahme, Planung, Planprüfung, Lifecycle, Abnahme) werden von der Leitinstanz selbst als getrennte Phasen ausgeführt.
- PR-Ziel: `staging` (Staging-Workflow; Issues schließen erst bei Promotion nach main — per Repo-Regel gewünscht).
