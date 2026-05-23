# F016 – Fehlerbehandlung & Recovery

## Einleitung

Diese Funktion beschreibt, wie Sie bei Störungen sicher weiterarbeiten – insbesondere bei festhängenden Aufgaben in der Detailansicht.
Neu ist die **manuelle Recovery-Aktion „Aufgabe wiederherstellen“**.

---

## Wer nutzt es?

- Anwender, die eine Aufgabe aktiv bearbeiten und bei Fehlern schnell weiterarbeiten müssen
- Support/Teamleitung, die Recovery-Fälle nachvollziehen und bewerten

---

## Fokus: Manuelle Wiederherstellung festhängender Aufgaben

### Wann ist die Aktion verfügbar?

Die Aktion erscheint in **AufgabeDetail** nur, wenn die Aufgabe auf:
- **KI aktiv**
- **Tests laufen**

steht.

### Was macht die Aktion?

- Setzt die Aufgabe auf **In Bearbeitung**
- nur nach **expliziter Bestätigung**
- nur wenn **keine Verarbeitung mehr läuft**
- schreibt einen nachvollziehbaren **Audit-Eintrag** ins Protokoll

### Was macht sie bewusst nicht?

- Keine automatische Selbstheilung im Hintergrund
- Keine Recovery aus beliebigen anderen Status
- Kein Erzwingen gegen laufende Verarbeitungen

---

## Schritt-für-Schritt: Aufgabe wiederherstellen

1. Öffnen Sie die betroffene Aufgabe in der Detailseite.
2. Prüfen Sie den Hinweis unter der Aktionsleiste.
3. Klicken Sie auf **🩹 Aufgabe wiederherstellen**.
4. Bestätigen Sie mit **Ja, wiederherstellen**.
5. Die Seite lädt neu; bei Erfolg steht die Aufgabe auf **In Bearbeitung**.

---

## Grenzen und Fehlerszenarien

| Situation | Ergebnis |
|---|---|
| Verarbeitung läuft noch | Recovery ist gesperrt („Wiederherstellung nicht möglich, Verarbeitung läuft noch.“) |
| Laufzeitstatus nicht prüfbar | Recovery bleibt gesperrt („Prüfung der Laufzeit war nicht möglich.“) |
| Status nicht recoverbar | Recovery wird abgewiesen |
| Parallel hat jemand/etwas den Status geändert | Konfliktmeldung, Seite wird neu geladen |

---

## Operative Hinweise (für Support)

- Erfolgreiche Recovery erzeugt einen Statusprotokoll-Eintrag mit:
  - „Manuelle Wiederherstellung: … → InBearbeitung“
  - `ReasonCode: RecoveryManual`
  - `CorrelationId`
- Laufzeitlogs enthalten Ereignisse `TaskRecoveryRequested`, `TaskRecoveryRejected`, `TaskRecoverySucceeded`.
- Bei Fehlermeldungen zuerst prüfen, ob noch ein aktiver KI-/Testlauf existiert.

---

## FAQ

**Verliere ich Protokolldaten durch Recovery?**  
Nein, bestehende Protokolle bleiben erhalten. Zusätzlich wird ein Audit-Eintrag ergänzt.

**Warum kann ich manchmal nicht auf „Aufgabe wiederherstellen“ klicken?**  
Entweder läuft noch eine Verarbeitung oder der Laufstatus ist gerade nicht zuverlässig prüfbar.

**Kann ich Recovery mehrfach nacheinander auslösen?**  
Nur wenn die Statusbedingungen wieder erfüllt sind. Parallelkonflikte werden automatisch abgefangen.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Verhalten bei hängenden Läufen
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Recovery-Audit nachvollziehen
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md) – Alternative bei laufender, aber nicht gewünschter Verarbeitung
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Recovery angrenzender Plugin-Fehlerpfade
- [Zurück zur Übersicht](../features.md)
