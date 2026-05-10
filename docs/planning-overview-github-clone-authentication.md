# Planungsübersicht – GitHub Clone Authentication Bugfix

> **Dokument-Typ:** Planungsübersicht (Orchestrator-Ergebnis)
> **Status:** ✅ Planungsphase abgeschlossen
> **Datum:** 2026-05-10

## 1. Anlass
Beim Aufgabenstart tritt wiederholt ein Clone-Fehler auf:
`fatal: could not read Username for 'https://github.com': terminal prompts disabled`.

## 2. Erstellte/aktualisierte Artefakte
| Dokument | Zweck | Link |
|---|---|---|
| Requirements Analysis | Ziele, Scope, FR/NFR, ACs | [requirements/github-clone-authentication-requirements-analysis.md](requirements/github-clone-authentication-requirements-analysis.md) |
| Architektur-Blueprint | Zielarchitektur, Auth-Strategie, Risiken, Rollout | [architecture/github-clone-authentication-architecture-blueprint.md](architecture/github-clone-authentication-architecture-blueprint.md) |
| ERM | Fachlich-technisches Modell für Clone/Auth-Fluss | [architecture/github-clone-authentication-entity-relationship-model.md](architecture/github-clone-authentication-entity-relationship-model.md) |
| Architektur-Review | Bewertete Risiken und priorisierte Maßnahmen | [improvements/github-clone-authentication-architecture-review.md](improvements/github-clone-authentication-architecture-review.md) |

## 3. Kernentscheidungen
1. Authentifizierung wird verbindlich **vor** `git clone` hergestellt.
2. Clone bleibt strikt non-interactive (`GIT_TERMINAL_PROMPT=0`).
3. Fehler werden als Auth-/Netzwerk-/sonstige Kategorien klassifiziert und nutzerverständlich ausgegeben.
4. Secrets dürfen zu keinem Zeitpunkt in Logs/Exceptions/UI im Klartext erscheinen.
5. Regression-Schutz erfolgt über gezielte Erweiterung von `GitHubPluginTests`.

## 4. Priorisierte Verbesserungsmaßnahmen aus Review
- **Major:** Sichere, verbindliche Credential-Übergabe standardisieren.
- **Major:** URL-/Schema-Matrix (HTTPS/SSH/unsupported) explizit modellieren.
- **Major:** Fehlerdomäne granularisieren (`MissingToken`, `InvalidToken`, `InsufficientScope`, …).
- **Medium:** UX-Fehlerpfade und Metriken für Qualitätsziele konkretisieren.

## 5. Traceability
Alle Dokumente sind gegenseitig per relativer Links referenziert und bilden gemeinsam die Planungsgrundlage für die Umsetzung.
