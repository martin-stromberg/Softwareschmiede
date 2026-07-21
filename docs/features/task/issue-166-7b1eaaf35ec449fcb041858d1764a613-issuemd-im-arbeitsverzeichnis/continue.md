# Offene Aufgaben

Erstellt am: 2026-07-21
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (1 offener Punkt → 2 offene Punkte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine (Plan-Review-Status: „Vollständig umgesetzt").

## Code-Review-Befunde

- [ ] **Fehlermeldung ohne aussagekräftigen Kontext** — In `UpdateGitignoreAsync` (`EntwicklungsprozessService.cs`) referenzieren die Log-Meldungen (Zeile 654 und 662) weiterhin `lokalerKlonPfad` (Repository-Root), obwohl die `.gitignore` seit dieser Änderung in `effektivesVerzeichnis` (dem konfigurierten Arbeitsunterverzeichnis) geschrieben/gelesen wird. Bei konfiguriertem `WorkingDirectoryRelativePath` zeigt die Diagnose auf das falsche Verzeichnis. Empfehlung: In beiden Log-Aufrufen `lokalerKlonPfad` durch `effektivesVerzeichnis` ersetzen.
- [ ] **Kopplung/Konsistenz** — Der neue Helper `EnsureEffectiveWorkingDirectory` löst das Arbeitsverzeichnis ohne das Git-Plugin auf (`WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory`), während `KiAusfuehrungsService` und `GitOrchestrationService` die plugin-fähige `DetermineEffectiveWorkingDirectoryAsync(..., gitPlugin, ...)` verwenden. Für Pointer-Datei-Plugins (z. B. `LocalDirectoryPlugin` im `InSourceDirectory`-Modus) könnten `issue.md`/`.gitignore` dadurch in einem anderen Verzeichnis landen als dem, in dem später die CLI ausgeführt wird. **Hinweis:** Dies ist eine bewusste, im Plan dokumentierte Design-Entscheidung (Antwort auf offene Frage #2 in `plan.md`, Zeile 12) — die Divergenz bestand laut Code-Review bereits vor dieser Änderung für `issue.md`. Vor einer Umsetzung sollte geprüft werden, ob die Plan-Entscheidung revidiert werden soll oder ob dieser Befund bewusst zurückgestellt wird.

## Fehlgeschlagene Tests

Keine (stabile Testlane: 981/982 bestanden, 0 Fehler, 1 übersprungen — verifiziert nach Korrektur eines fehlerhaften Testlaufs, der versehentlich `Category=OsInterface`-Tests ohne Sandbox-Umgebungseinstellungen einschloss).
