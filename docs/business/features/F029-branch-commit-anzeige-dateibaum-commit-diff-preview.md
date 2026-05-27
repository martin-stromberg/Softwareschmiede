# F029 – Branch-Commit-Anzeige im Dateibaum + Commit-Diff-Preview

## Einleitung

Mit dieser Funktion sehen Sie eigene Commits Ihres Arbeits-Branches direkt im **🗂️ Repository-Explorer**.
Sie klappen einen Commit auf und sehen die enthaltenen Dateien im selben Baum.
Sie öffnen eine Datei aus dem Commit und prüfen die Vorschau ohne Seitenwechsel.
So erkennen Sie schneller, was genau in einem einzelnen Commit geändert wurde.
Das hilft bei Freigaben und bei Rückfragen im Team.

---

## Nutzen im Alltag

- Sie prüfen Änderungen je Commit statt nur als große Gesamtliste.
- Sie finden schneller die richtige Datei zu einem Commit.
- Sie sehen alte und neue Inhalte direkt in der Vorschau.
- Sie können Fehler in einem Commit früher erkennen.

---

## Wer nutzt es?

Diese Funktion nutzen Mitarbeitende, die Aufgaben prüfen und freigeben.
Sie ist auch für Teamleitungen nützlich, die den Stand eines Branches nachvollziehen.
Neue Kolleginnen und Kollegen verstehen damit schneller, was ein Commit wirklich geändert hat.

---

## Schritt-für-Schritt-Anleitung

1. Öffnen Sie eine Aufgabe und wechseln Sie auf **🗂️ Projektverzeichnis**.
2. Klicken Sie im Bereich **🗂️ Repository-Explorer** auf einen Commit mit Kurzkennung wie `[abc1234]`.
3. Warten Sie kurz, bis die Commit-Dateien geladen sind.
4. Klicken Sie auf eine Datei im aufgeklappten Commit-Baum.
5. Lesen Sie den Inhalt in **Dateivorschau**.
6. Wechseln Sie bei Bedarf zu weiteren Dateien oder zu einem anderen Commit.
7. Klicken Sie bei einer Fehlermeldung auf **Retry**.

---

## Beispiel

Sie haben zwei eigene Commits in einem Feature-Branch.
Sie klappen zuerst den neueren Commit auf.
Dort wählen Sie `commit-file.cs` aus.
In **Dateivorschau** sehen Sie direkt den Stand aus diesem Commit.
Bei einem Ladefehler klicken Sie **Retry** und laden die Commit-Dateien erneut.

---

## Was passiert im Hintergrund?

Die Anwendung lädt beim Öffnen des Explorers die Commits Ihres Branches.
Sie vergleicht dafür den Branch mit einer Basislinie wie `origin/main`.
Beim Aufklappen eines Commits lädt sie die Dateiliste erst dann.
Beim Klick auf eine Commit-Datei lädt sie die Vorschau für genau diesen Commit.
Wenn Sie sehr schnell klicken, bleibt nur die letzte Auswahl sichtbar.

---

## Grenzen

- Die Ansicht bleibt schreibgeschützt.
- Ohne gültige Basislinie zeigt die Anwendung keine Branch-Commit-Liste.
- Für Ordner zeigt die Vorschau nur einen Hinweis.
- Für Binärdateien zeigt die Vorschau einen Hinweis statt Inhalt.
- Bei Ladefehlern sehen Sie eine Meldung und nutzen **Retry**.

---

## Abhängigkeiten

- Die Aufgabe braucht einen gültigen lokalen Klonpfad.
- Das Verzeichnis muss ein Git-Repository sein.
- Die lokale Git-Installation muss erreichbar sein.
- Die Funktion nutzt den bestehenden Explorer und die Dateivorschau der Aufgabenseite.

---

## Testabdeckung

Die Funktion ist automatisiert getestet.
Wichtige Tests liegen in folgenden Dateien:

- [`CommitTreePresenterTests.cs`](../../../src/Softwareschmiede.Tests/Components/Pages/Aufgaben/CommitTreePresenterTests.cs)
- [`AufgabeDetailWorkspacePreviewBunitTests.cs`](../../../src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailWorkspacePreviewBunitTests.cs)
- [`GitWorkspaceBrowserServiceTests.cs`](../../../src/Softwareschmiede.Tests/Application/Services/GitWorkspaceBrowserServiceTests.cs)

Zusätzlich dokumentieren diese Testpläne den angrenzenden Bereich:

- [Testplan – Changed Artifact Detection & Agentendefinitions-Compliance](../../tests/testplan-changed-artifact-detection-agent-compliance.md)
- [Testplan – DiffViewer für geänderte Dateien](../../tests/testplan-diffviewer-geaenderte-dateien.md)

---

## Häufige Fragen (FAQ)

**Warum sehe ich keine Commit-Liste?**  
Die Aufgabe braucht ein gültiges Git-Repository und eine erkennbare Basislinie.

**Was bedeutet die Kennung in eckigen Klammern, zum Beispiel `[abc1234]`?**  
Das ist die Kurzkennung des Commits.

**Warum zeigt die Vorschau manchmal nur einen Hinweis?**  
Bei Ordnern oder Binärdateien zeigt die Anwendung bewusst keinen Inhalt.

**Kann ich Commit-Dateien hier direkt bearbeiten?**  
Nein. Der Bereich dient nur zur Prüfung.

**Warum sehe ich bei Commit-Dateien keinen direkten Diff-Link?**  
Die Commit-Vorschau wird direkt im Explorer angezeigt und bleibt im Commit-Kontext.

---

## Verwandte Funktionen

- [F021 – Live Project Browser mit Git-Status](./F021-live-project-browser-git-status.md)
- [F022 – Diff-Vergleichskomponente](./F022-diff-vergleichskomponente.md)
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md)
- [Technischer Contract: Branch-Commit-Anzeige + Commit-Diff-Preview](../../api/branch-commit-diff-preview.md)
- [Flow-Dokumentation: Branch-Commit-Laden und Retry](../../flows/branch-commit-tree-expansion-flow.md)
- [Technischer Contract: Live Project Browser mit Git-Status](../../api/live-project-browser-git-status.md)
- [Technische Detailseite: Diff Viewer](../../api/diff-viewer.md)
- [Flow-Dokumentation: Live Project Browser](../../flows/live-project-browser-git-status-flow.md)
- [Requirements Analysis](../../requirements/live-project-browser-git-status-requirements-analysis.md)
- [Architecture Blueprint](../../architecture/live-project-browser-git-status-architecture-blueprint.md)
- [Zurück zur Übersicht](../features.md)
