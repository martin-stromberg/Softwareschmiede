# Bestandsaufnahme: GitHub Actions zusammenfassen

## Zielbereich

Die Anforderung betrifft den Pull-Request-Workflow gegen `staging` in `.github/workflows/pr.yml`. Die bisher getrennten Jobs `test` und `validate` sollen als gemeinsame Action `validate & test` erscheinen. Entscheidend ist die Ausfuehrungsreihenfolge: Formatcheck, Security-Dependency-Check und statische Codeanalyse muessen vor den Tests abgeschlossen sein.

## Ist-Zustand

`pr.yml` ist ein `pull_request`-Workflow fuer `staging` mit den Ereignissen `opened`, `synchronize` und `reopened`. Fuer normale Feature-PRs existieren zwei unabhaengige Jobs:

- `test`: Restore, Debug-Build und vier Testschritte.
- `validate`: Formatpruefung, NuGet-Security-Scan und statische Analyse.

Beide Jobs wiederholen Checkout, .NET-Setup und Restore und besitzen keine `needs`-Beziehung. Dadurch laufen sie parallel; die geforderte Gate-Reihenfolge ist im Ist-Zustand nicht garantiert. Der Sonderfall eines PRs von `main` nach `staging` wird weiterhin durch `back-merge-skip` behandelt.

## Relevante Kontextabgrenzung

`.github/workflows/test.yml` prueft Pull Requests gegen `main` sowie Pushes nach `main` und bleibt ein eigenstaendiger Main-Testworkflow. `.github/workflows/staging-ci.yml` reagiert auf Pushes nach `staging` und umfasst neben Build und Tests die Staging-/Release-Candidate-Automatisierung. Beide Workflows sind daher nicht Teil der Zusammenlegung.

Der separate Workflow `.github/workflows/security-scan.yml` hat eigene Main- und Schedule-Trigger. Sein Security-Scan wird nicht mit dem `staging`-PR-Gate zusammengelegt; `pr.yml` besitzt bereits einen eigenen Scan-Schritt.

## Aenderungsgrenze fuer die Planung

Voraussichtlich ist nur `.github/workflows/pr.yml` fachlich zu aendern. Die gemeinsame Jobstruktur sollte die bestehenden Trigger, Bedingungen, Runner, Timeout-Werte, Berechtigungen, Testfilter, Fehlertoleranz und Artefakte erhalten und lediglich die gemeinsamen Setup-Schritte deduplizieren sowie die Validierungsschritte vor die Tests verschieben.

## Detaildokumente

- [Pull Request Workflow](inventory/pr-workflow.md): Trigger, Jobs, aktuelle Reihenfolge und konkrete Risiken.
- [Kontext-Workflows](inventory/context-workflows.md): Abgrenzung von `test.yml`, `staging-ci.yml` und `security-scan.yml`.
- [Workflow-Vertraege](inventory/workflow-contracts.md): zu erhaltende Konfiguration, Gates und Artefakte.

## Offene Punkte fuer die Planung

- Es ist zu entscheiden, ob `validate & test` als Job-ID, Job-Anzeigename (`name`) oder beides modelliert wird. Fuer die sichtbare GitHub-Actions-Anzeige ist ein Job-`name` mit diesem Text erforderlich; eine Job-ID mit `&` ist als YAML-Schluessel zwar moeglich, aber fuer Referenzen unhandlicher.
- Es ist zu pruefen, ob die beiden Upload-Schritte an ihren bisherigen Stellen verbleiben oder jeweils nach den zugehoerigen Schritten im gemeinsamen Job angeordnet werden. Beide muessen weiterhin mit `if: always()` ausgefuehrt werden.
