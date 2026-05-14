# F019 – Issue-, Branch- und PR-Verknüpfung

## Einleitung

Diese Funktion verbindet GitHub-Issues sauber mit dem technischen Umsetzungsweg.  
Wenn Sie eine Aufgabe aus einer Issue anlegen, bleibt diese Verbindung bis zum Pull Request erhalten.  
Dadurch wird der Branch klar benannt und das Issue beim Merge des PR automatisch geschlossen.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender und Entwickler, die Aufgaben über GitHub-Issues steuern.  
Sie hilft besonders in Teams, die Nachvollziehbarkeit und saubere Ticket-Automation benötigen.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen **Neue Aufgabe** und wählen eine vorhandene GitHub-Issue aus.
2. Titel und Beschreibung werden übernommen und die Aufgabe wird mit der Issue verknüpft.
3. Sie starten den Entwicklungsprozess.
4. Die Anwendung erstellt automatisch einen issuebezogenen Branch.
5. Beim Erstellen des Pull Requests wird die passende Closing-Direktive ergänzt.
6. Nach dem Merge schließt GitHub das verknüpfte Issue automatisch.

---

## Beispiel

Sie wählen Issue **#123** „Filter für Dashboard erweitern“.  
Beim Start entsteht ein Branch wie `task/issue-123-...`.  
Beim PR-Erstellen ergänzt die Anwendung automatisch `Closes #123`.  
Nach dem Merge ist Issue #123 automatisch geschlossen.

---

## Was passiert im Hintergrund?

- Die Issue wird als `IssueReferenz` an der Aufgabe gespeichert.
- Der Branchname enthält bei vorhandener Issue die Issue-Nummer.
- Beim PR-Beschreibungstext prüft die Anwendung, ob schon eine passende Closing-Direktive existiert.
- Nur wenn sie fehlt, wird `Closes #<IssueNummer>` ergänzt.
- Der Protokolleintrag dokumentiert, dass Auto-Close für die Issue aktiv ist.

---

## Häufige Fragen (FAQ)

**Muss ich immer eine Issue auswählen?**  
Nein. Sie können Aufgaben weiterhin ohne Issue anlegen.

**Wird die Closing-Direktive doppelt eingefügt?**  
Nein. Eine bereits passende Direktive bleibt unverändert.

**Was passiert bei leerer PR-Beschreibung?**  
Dann wird bei verknüpfter Issue nur `Closes #<IssueNummer>` verwendet.

**Schließt sich das Issue sofort?**  
Nein. Das Issue wird beim **Merge** des Pull Requests geschlossen.

---

## Verwandte Funktionen

- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md)
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F006 – Aufgabe abschließen](./F006-aufgabe-abschliessen.md)
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md)
- [Zurück zur Übersicht](../features.md)
