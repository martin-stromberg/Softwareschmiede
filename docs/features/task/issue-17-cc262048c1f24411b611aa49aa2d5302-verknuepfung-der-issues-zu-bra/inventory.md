# Bestandsaufnahme: Verknuepfung der Issues zu Branches

Diese Bestandsaufnahme analysiert den vorhandenen Weg von GitHub-Issue-Auswahl ueber Aufgabenstart und Branch-Erstellung bis zur Pull-Request-Erstellung.

## Zusammenfassung

| Aspekt | Status | Anmerkung |
|--------|--------|-----------|
| Aufgabenmodell | Vorhanden | `Aufgabe` enthaelt `BranchName` und optionale `IssueReferenz`. |
| Issue-Referenzmodell | Vorhanden | `IssueReferenz.IssueNummer` ist nullable und fuer Closing-Direktiven geeignet. |
| Persistenz | Vorhanden | EF-Core modelliert `Aufgabe` zu `IssueReferenz` als 1:1-Beziehung. |
| Issue-Aufgabenerstellung | Vorhanden | `AufgabeService.CreateFromIssueAsync(...)` legt Aufgabe und Referenz gemeinsam an. |
| Manuelle Issue-Aktualisierung | Vorhanden | `AufgabeService.UpdateIssueReferenzAsync(...)` setzt, aktualisiert oder entfernt die Referenz. |
| Branch-Start | Vorhanden | `EntwicklungsprozessService` erzeugt Issue-basierte Branch-Namen und `AufgabeService.StartenAsync(...)` persistiert sie. |
| Pull-Request-Erstellung | Vorhanden | `GitOrchestrationService.PullRequestErstellenAsync(...)` baut Titel/Body und ruft `IGitPlugin.CreatePullRequestAsync(...)` auf. |
| Closing-Direktive | Teilweise bereits vorhanden | Im aktuellen Arbeitsbaum existiert bereits `BuildPullRequestBody(...)` mit `Closes #<IssueNummer>` und Duplikatpruefung. |
| GitHub-Plugin | Vorhanden | `GitHubPlugin.CreatePullRequestAsync(...)` uebergibt den Body unveraendert an `gh pr create --body`. |
| Tests | Teilweise vorhanden | Mehrere PR-Body-Tests existieren bereits; ein Test fuer Aufgabe mit Issue-Referenz ohne Issue-Nummer fehlt sichtbar. |

## Relevante Detaildokumente

- [Datenmodell und Persistenz](inventory/models.md)
- [Service- und Prozesslogik](inventory/services.md)
- [Plugin-Vertrag und GitHub-Integration](inventory/plugins.md)
- [Tests und Testluecken](inventory/tests.md)

## Aktueller Datenfluss

1. Die UI waehlt ein Git-Provider-Issue und erstellt daraus ueber `AufgabeService.CreateFromIssueAsync(...)` eine Aufgabe.
2. Die Aufgabe speichert die Issue-Daten in `IssueReferenz`, insbesondere `IssueNummer`.
3. Beim Starten liest `EntwicklungsprozessService.ProzessStartenAsync(...)` die Aufgabe inklusive `IssueReferenz`.
4. `EntwicklungsprozessService.ErstelleTaskBranchName(...)` verwendet die Issue-Nummer fuer Branch-Namen wie `task/issue-55-...`.
5. `AufgabeService.StartenAsync(...)` persistiert den erzeugten oder ausgewaehlten Branch an derselben Aufgabe.
6. Bei der PR-Erstellung laedt `GitOrchestrationService.PullRequestErstellenAsync(...)` die Aufgabe per `GetDetailAsync(...)` inklusive `IssueReferenz` und `GitRepository`.
7. Der PR-Body wird im Service gebaut und an das aufgeloeste Git-Plugin uebergeben.
8. Das GitHub-Plugin ruft `gh pr create` mit `--body` auf; GitHub kann dadurch `Closes #<IssueNummer>` beim Merge auswerten.

## Wichtige Beobachtungen

- Die Anforderung passt zur vorhandenen Architektur: Domain-Daten bleiben in `Aufgabe`/`IssueReferenz`; Provider-spezifische CLI-Ausfuehrung bleibt im Plugin.
- Die automatische Closing-Direktive sollte in `GitOrchestrationService` bleiben, weil dort Aufgabe, Issue-Referenz, Repository-Aufloesung und PR-Erstellung zusammenlaufen.
- Im aktuellen Arbeitsbaum ist die erwartete Kernlogik bereits in `GitOrchestrationService.BuildPullRequestBody(...)` vorhanden. Die weitere Planung sollte deshalb pruefen, ob noch Vollstaendigkeit, Tests und Review-Korrekturen offen sind, statt dieselbe Logik erneut einzubauen.
- Die Methode `EntwicklungsprozessService.PullRequestErstellenAsync(Guid, string, string, string, ...)` ist ein aelterer/alternativer PR-Pfad ohne Issue-Referenz-Auswertung. Vor Aenderungen muss geklaert werden, ob dieser Pfad noch produktiv genutzt wird oder historisch ist.

## Risiken

- Provider-Neutralitaet: `Closes #...` ist GitHub-kompatibel. Bei anderen Plugins kann derselbe Body harmlos sein, aber nicht zwingend semantisch korrekt.
- Duplikat-Erkennung: Die vorhandene Regex deckt `close/closes/closed`, `fix/fixes/fixed` und `resolve/resolves/resolved` mit optionalem `owner/repo#num` ab. Mehrere Issue-Nummern in einer Direktive wie `Closes #1, #2` werden fuer `#2` nicht zwingend als Closing-Direktive erkannt.
- Nullable Issue-Nummer: Aufgaben mit `IssueReferenz`, aber `IssueNummer == null` muessen explizit unveraendertes PR-Verhalten behalten; dafuer ist noch ein eigener Test sinnvoll.
