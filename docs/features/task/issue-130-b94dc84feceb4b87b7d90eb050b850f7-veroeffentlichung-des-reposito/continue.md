# Folgeaufgaben

Diese Anforderung wurde vollständig umgesetzt (siehe `review.md`, `review-code.md`, `test-results.md`). Die nachträglich vom Anwender gewünschten redaktionellen Korrekturen an `README.md`/`SECURITY.md` (Punkte 1–7 sowie der SECURITY.md-Hinweis) wurden in diesem Zyklus abgeschlossen.

## Erledigt (dieser Zyklus)

- [x] **1. Dokumentationsvollständigkeit geprüft** — alle Punkte des ehemaligen `Implementierungsstatus`-Abschnitts haben eine eigenständige Doku unter `docs/help/*` bzw. `docs/CI_CD.md`. Keine fehlende Dokumentation identifiziert. (`docs/business/*`, `docs/api/*`, `docs/architecture/*` u. a. existieren im Repo bereits seit einer früheren Dokumentations-Restrukturierung nicht mehr — die dorthin zeigenden alten README-Links waren daher ohnehin tot.)
- [x] **2. `Implementierungsstatus`-Abschnitt entfernt** — die darin enthaltenen noch gültigen Doku-Links wurden in die neue Dokumentationstabelle übernommen.
- [x] **3. `Features`-Abschnitt neu strukturiert** — kurze Zweck-Einleitung + 10 Kern-Features, Verweis auf `docs/help/index.md` für Details.
- [x] **4. `Branding & UI-Assets` entfernt.**
- [x] **4b. `start.ps1`-Beschreibung korrigiert** (kein `-Port`-Parameter, keine Env-Var-Steuerung — Portvergabe ist vollautomatisch) und `Usage`-Abschnitt deutlich gekürzt (von ca. 120 auf ca. 20 Zeilen; Detailthemen bleiben in `docs/help/*`).
- [x] **5. Defekte Links behoben** — komplette alte Dokumentationstabelle (>100 Zeilen, überwiegend tote Ziele) durch eine verifizierte, ausschließlich existierende Ziele enthaltende Tabelle ersetzt; tote `docs/tests/*`-Links im Testabschnitt entfernt; zusätzlich bei der Verifikation gefunden und mitkorrigiert: die ASCII-Baumdarstellung im Abschnitt „Projektstruktur" verwies noch auf `docs/requirements/`, `docs/architecture/`, `docs/improvements/`, `docs/tests/` (existieren nicht mehr) — auf den tatsächlichen Stand (`docs/help/`, `docs/features/`, `docs/CI_CD.md`) korrigiert; sowie eine falsche Behauptung im Changelog-Abschnitt („keine separate CHANGELOG.md" obwohl `CHANGELOG.md` existiert und per Semantic Release gepflegt wird) korrigiert.
- [x] **6. Repository-Adresse korrigiert** — `git remote -v` ergab `martin-stromberg/Softwareschmiede`; Platzhalter durch `https://github.com/martin-stromberg/Softwareschmiede.git` ersetzt.
- [x] **7. Alle „Agentenpakete"-Referenzen entfernt** — eigener Abschnitt, TOC-Eintrag, sowie verstreute Erwähnungen in Voraussetzungen/Installation/Usage (KI-Plugin-Auswahl, Issue-58-Abschnitt)/Changelog/Roadmap. `docs/business/features/F004-agentenpakete.md` war bereits vor diesem Zyklus nicht mehr vorhanden.
- [x] **SECURITY.md-Hinweis ergänzt** — neuer Satz im Abschnitt „Responsible Disclosure", der klarstellt, dass eine Rückmeldung auf eine gemeldete Schwachstelle ggf. auf den Anbieter einer eingebetteten Dritt-CLI verweist, falls sich die Ursache dort statt in Softwareschmiede selbst befindet. Meldeweg (GitHub Private Security Advisories) unverändert.

## Bekannte, bewusst nicht behobene Diskrepanz (außerhalb des Auftrags)

Die Architektur-Mermaid-Diagramm-Knoten `AgentPackageReader`/`IAgentPackageService` (Abschnitt „Architektur") sowie die zugehörige Testklassen-Referenz im Abschnitt „Tests" wurden **nicht** entfernt, da die Klassen (`AgentPackageReader`, `IAgentPackageService`, `IAgentPackageFileService`, `AgentPackageFileService`, `AgentPackageInfo`) nachweislich noch im Code existieren (`src/Softwareschmiede/Infrastructure/Services/`, `src/Softwareschmiede/Domain/`). Das widerspricht der Aussage „es gibt keine Agentenpakete mehr in der Anwendung" aus dem Anwenderwunsch. Diese Diskrepanz (toter Code vs. Doku-Aussage) sollte dem Anwender auffallen und ggf. separat geklärt werden (Code entfernen oder Aussage relativieren) — das lag außerhalb des rein dokumentationsbezogenen Auftrags dieses Zyklus.

## Neuer, noch offener Folgepunkt (vom Anwender während dieses Zyklus ergänzt)

- [ ] **Hook zur Link-Validierung bei README-Bearbeitung:** Der Anwender wünscht einen automatisierten Hook (z. B. PreToolUse/PostToolUse in `.claude/hooks/`), der bei jeder Bearbeitung von `README.md` sicherstellt, dass keine Links verwaist sind (Linkziel existiert nicht mehr im Repo). Zweck: Verhindern, dass zukünftige README-Änderungen erneut unbemerkt tote Links einführen, wie es in diesem Zyklus manuell festgestellt und behoben werden musste.
  - Noch zu klären/umzusetzen: Hook-Typ (Pre- vs. PostToolUse auf `Edit`/`Write` mit Pfadfilter `README.md`), Prüflogik (relative Markdown-Links `[text](pfad)` gegen Dateisystem, ggf. Anker-Prüfung), Verhalten bei Fund (blockieren vs. nur warnen), Registrierung in `.claude/settings.json`.
  - Dieser Punkt ist rein werkzeugbezogen (Harness-Konfiguration), nicht inhaltlich Teil der README/SECURITY-Korrekturen selbst, und wurde daher nicht im selben Zyklus umgesetzt.
