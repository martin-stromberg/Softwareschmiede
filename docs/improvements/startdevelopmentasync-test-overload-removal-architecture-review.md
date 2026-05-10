# Architektur-Review: StartDevelopmentAsync Test-Overload Removal

## 1) Executive Summary
Die geplante Konsolidierung auf einen kanonischen `StartDevelopmentAsync`-Vertrag ist architektonisch sinnvoll und reduziert API-Duplizierung.  
Die Umsetzung ist mit geringer technischer Komplexität möglich, sofern die Testmigration vollständig vor der Overload-Entfernung erfolgt.

**Freigabeempfehlung:** ✅ Freigabe mit Auflagen (Major-Findings zuerst schließen).

## 2) Bewertungsmatrix
| Bereich | Bewertung | Kurzbegründung |
|---|---|---|
| Architekturklarheit | Gut | Ein einheitlicher Vertrag reduziert Komplexität. |
| Technologieentscheidungen | Gut | Bestehende kanonische Signatur kann direkt genutzt werden. |
| Testbarkeit | Mittel-Gut | Migration der Testaufrufe muss vollständig erfolgen. |
| Governance/Traceability | Gut | Artefakte und Links vorhanden. |

## 3) Priorisierte Findings
| ID | Priorität | Finding | Risiko |
|---|---|---|---|
| F-01 | Major | Unvollständige Testmigration kann Compile-/Testbrüche erzeugen. | CI-Instabilität |
| F-02 | Major | Overload-Entfernung vor finalem Regressionstest kann Verhaltensdrift verdecken. | Funktionsregression |
| F-03 | Medium | Caller außerhalb der Haupttestdatei könnten Altaufrufe enthalten. | Späte Integrationsfehler |
| F-04 | Medium | Dokumentation in API-/Flow-Dokumenten könnte veralten. | Traceability-Drift |

## 4) Konkrete Maßnahmen
1. **(zu F-01)** Repo-weite Migration aller Kurzaufrufe auf kanonische Signatur.
2. **(zu F-02)** Reihenfolge strikt einhalten: Testmigration → Overload-Entfernung → Volltest.
3. **(zu F-03)** Alle Service- und Integrationstests auf Signaturkonsistenz prüfen.
4. **(zu F-04)** API-/Flow-Dokumente nach Umsetzung synchron aktualisieren.

## 5) Restrisiken
| Risiko | Bewertung | Gegenmaßnahme |
|---|---|---|
| Späte Altaufrufe in Randtests | Mittel | globale Suche + Build-Check |
| Semantikabweichung bei `executionId == null` | Niedrig-Mittel | gezielte Regressionstests |

## 6) Freigabekriterien
- Keine Kurzsignatur-Aufrufe mehr in Tests.
- Kanonischer Vertrag in Interface und Implementierung konsistent.
- Vollständiger Testlauf grün.
- Dokumentlinks auf Requirements/Blueprint/ERM/Review auflösbar.

## 7) Traceability
- Anforderungen: `../requirements/startdevelopmentasync-test-overload-removal-requirements-analysis.md`
- Architektur: `../architecture/startdevelopmentasync-test-overload-removal-architecture-blueprint.md`
- ERM: `../architecture/startdevelopmentasync-test-overload-removal-entity-relationship-model.md`

