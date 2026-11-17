# Todo: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5, #412 Teil 1)

- [x] Feature-Verzeichnis + todo.md anlegen (Schritte 1-2)
- [x] requirement.md schreiben (Schritt 3)
- [x] inventory.md schreiben inkl. Code-Erkundung (Schritt 4)
- [x] plan.md + plan-check.md + Planungscommit `73057e5f` (Schritte 5-5c)
- [x] Implementierung: Entities+Migration `AddIssueAufbereitung`, Erkennung, interne Einreihung,
      Resolver-Ausschluss, Settings-Keys (Schritt 6)
- [x] Tests schreiben + ausführen (Unit + Integration, ScmDispatchFixture) — 10 neue Integrationstests,
      7 neue Unit-Tests; Integration-Suite 346/346, Unit-Lane 2669/2672 (3 Umgebungs-Skips)
- [x] Reviews: review.md, review-usability.md, review-code.md (Schritte 7-9) — alle „Keine Befunde"/vollständig
- [x] test-results.md mit realem Testlauf (Schritt 10)
- [x] Doku: docs/help (beschreibung/business-rules/datenmodell/ablauf-technisch/architektur),
      README, RELEASE_NOTES, dokumentation.md (Schritte 12-12c)
- [x] Abschlusscommit + Format-Check (Format-Check sauber; Build 0 Warnungen/0 Fehler)

## Abweichung Lifecycle

Die Lifecycle-Schritte sehen delegierte Sub-Agenten vor (Plan, Inventur, Reviews, Tests);
die Umgebung bietet keinen Subagenten-Start — alle Schritte wurden wie in Schritt 4 dokumentiert
direkt ausgeführt und die Reviews selbst gegengeprüft.
