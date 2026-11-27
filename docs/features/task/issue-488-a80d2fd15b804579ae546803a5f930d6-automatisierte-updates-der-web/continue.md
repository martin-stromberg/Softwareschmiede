# Offene Aufgaben

Erstellt am: 2026-11-27
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Task 40 — Manuelle E2E-Abnahme der echten Installation: Akzeptanzszenarien 4+5 aus `plan.md` („Jetzt installieren" mit Installationsskript-Lauf: Binäraustausch der Programmdateien, Service-/Prozess-Neustart, Erhalt von `data/` + `appsettings*.json`, `updates/update.log`) sind in dieser Umgebung nicht durchführbar — es existiert kein Service-/IIS-/systemd-Lifecycle für einen produktiven Serverlauf. Abgenommen ist der Pfad bis `ReadyToInstall` inkl. echtem Prozess-Neustart mit persistierten Einstellungen (siehe `test-results.md`, Abschnitt „Reale End-to-End-Abnahme"); die finale Installation muss auf einem realen Deployment (Windows-Dienst oder systemd) abgenommen werden.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Keine.
