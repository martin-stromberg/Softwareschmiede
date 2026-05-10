# Planning Overview: StartDevelopmentAsync Test-Overload Removal

## 1. Ziel
Die Planungsrunde definiert die vollständige Grundlage, um den test-spezifischen `StartDevelopmentAsync`-Overload zu entfernen, ohne Laufzeitverhalten oder Testabdeckung zu verlieren.

## 2. Umgesetzte Arbeitspakete
| Schritt | Ergebnis | Artefakt |
|---|---|---|
| 1 | Anforderungen strukturiert | `../requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md` |
| 2 | Zielarchitektur + Vertragsentscheidung dokumentiert | `../architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md` |
| 3 | ERM für Signaturkonsolidierung modelliert | `../architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md` |
| 4 | Risiken/Freigabe in Architektur-Review bewertet | `../improvements/startdevelopmentasync-test-overload-removal-architecture-review.md` |

## 3. Zentrale Entscheidungen
1. Langfristig nur eine kanonische `StartDevelopmentAsync`-Signatur (`executionId` inklusive).
2. Testmigration ist zwingend vor Entfernen des Kurz-Overloads.
3. Semantik `executionId == null` bleibt unverändert.
4. Regression wird über bestehenden Testbestand abgesichert.

## 4. Blocker & nächster Schritt
- **Kein technischer Blocker in der Planungsphase.**
- **Nächster Schritt:** Umsetzung gemäß Reihenfolge aus dem Blueprint und danach vollständige Lifecycle-Orchestrierung (Implementierung → Testabdeckung → Dokumentation).

## 5. Cross-Links
- Requirements: `../requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md`
- Architecture: `../architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md`
- ERM: `../architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md`
- Review: `../improvements/startdevelopmentasync-test-overload-removal-architecture-review.md`
