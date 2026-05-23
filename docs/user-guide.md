# Benutzerleitfaden – Softwareschmiede

> **Zielgruppe:** Softwareentwickler, die KI-gestützt an Projekten arbeiten möchten.
> Dieser Leitfaden erklärt die Softwareschmiede ohne technischen Fachjargon.

---

## Was ist die Softwareschmiede?

Die Softwareschmiede ist ein persönliches Werkzeug für Softwareentwickler.
Sie läuft direkt im Browser auf Ihrem eigenen Rechner – ohne Cloud, ohne Fremdzugriff.
Die Anwendung verbindet Ihre Projekte, Ihre GitHub-Ablage und eine KI (GitHub Copilot) zu einem gemeinsamen Arbeitsbereich.
Sie beschreiben eine Aufgabe – die KI erledigt die Umsetzung und Sie beobachten die Arbeit in Echtzeit.
Alles, was die KI tut, wird lückenlos protokolliert.

![Startseite der Softwareschmiede](./images/startseite.png)

---

## Hauptfunktionen im Überblick

### Projektverwaltung

Sie legen Ihre Softwareprojekte an und verwalten sie an einem Ort.
Jedes Projekt hat einen Namen, eine Beschreibung und einen Status (Aktiv oder Archiviert).
Aktive Projekte erscheinen im Dashboard, archivierte verschwinden aus der Ansicht.

👉 Mehr dazu: [F001 – Projektverwaltung](./business/features/F001-projektverwaltung.md)

---

### Arbeitsverzeichnis für lokale Klone

Sie können in den **Einstellungen** festlegen, in welchem Basisordner lokale Arbeitskopien Ihrer Repositories angelegt werden.
Wenn keine eigene Angabe gespeichert ist oder der Pfad nicht nutzbar ist, verwendet die Softwareschmiede automatisch einen sicheren Default auf Basis des Temp-Verzeichnisses.
Im Modus `SeparateWorkingDirectory` wird die Quelle nur kopiert und anschließend im Arbeitsverzeichnis als eigenes Git-Repository initialisiert.

👉 Mehr dazu: [F009 – Arbeitsverzeichnis konfigurieren](./business/features/F009-arbeitsverzeichnis-konfigurieren.md)

---

### Plugin-Prinzip für Integrationen

Die Anbindungen für GitHub und Copilot laufen als ausgelagerte Plugins.
Die Softwareschmiede erkennt diese Plugins beim Start automatisch.
Sie müssen keine Dateien selbst nachkopieren.
Eine manuelle DLL-Kopie ist nicht nötig.

👉 Mehr dazu: [F010 – Plugin-Prinzip für Integrationen](./business/features/F010-plugin-prinzip-integrationen.md)

---

### Claude-CLI-Integration

Sie können die Softwareschmiede auch mit Claude nutzen.
Dafür hinterlegen Sie Ihren Schlüssel einmal in den **Einstellungen**.
Danach läuft die Bearbeitung wie gewohnt mit **🤖 KI starten** und **📜 Protokoll**.
So bleibt Ihr Tagesablauf gleich, auch wenn Sie Claude einsetzen.

👉 Mehr dazu: [F013 – Claude-CLI-Integration](./business/features/F013-claude-cli-integration.md)

---

### Aufgabenverwaltung

Innerhalb eines Projekts legen Sie Aufgaben an.
Eine Aufgabe beschreibt genau, was umgesetzt werden soll – zum Beispiel „Login-Funktion hinzufügen".
Sie können Aufgaben aus Ihrem GitHub-Aufgabensystem übernehmen oder frei formulieren.
Jede Aufgabe durchläuft klare Stadien, vom Anlegen bis zum Abschluss.

👉 Mehr dazu: [F002 – Aufgabenverwaltung](./business/features/F002-aufgabenverwaltung.md)

---

### KI-gestützter Entwicklungsprozess

Sie starten die KI mit einem Klick auf **KI starten**.
Die KI liest Ihre Aufgabenbeschreibung und beginnt selbstständig, Code zu schreiben.
Sie sehen die Ausgabe der KI in Echtzeit auf dem Bildschirm.
Bei Bedarf können Sie der KI weitere Hinweise geben, ohne den Vorgang zu unterbrechen.

👉 Mehr dazu: [F003 – KI-Entwicklungsprozess](./business/features/F003-ki-entwicklungsprozess.md)

---

### Agentenpakete

Agentenpakete sind Sammlungen von Anweisungen für die KI.
Sie legen fest, wie die KI arbeiten soll – zum Beispiel welche Prüfungen sie durchführt oder welchen Stil sie beim Schreiben von Code verwendet.
Sie wählen beim Start einer Aufgabe aus, welches Agentenpaket die KI nutzen soll.

👉 Mehr dazu: [F004 – Agentenpakete](./business/features/F004-agentenpakete.md)

---

### Aufgabenprotokoll

Jede Aktion der KI wird automatisch aufgezeichnet.
Das Protokoll zeigt Ihnen, was die KI geschrieben hat, welche Dateien sie verändert hat und wann Status-Änderungen eingetreten sind.
Sie können das Protokoll jederzeit aufrufen – auch Tage oder Wochen später.

👉 Mehr dazu: [F005 – Aufgabenprotokoll](./business/features/F005-aufgabenprotokoll.md)

---

### Projektverzeichnis auf der Aufgabenseite

Auf der Detailseite einer Aufgabe können Sie das verknüpfte lokale Repository direkt ansehen.
Sie sehen dort die Anzahl der Commits, die lokalen Änderungen und das Projektverzeichnis mit Git-Status.
Mit einem Klick auf eine Datei erhalten Sie Inhalt, Hinweis oder Vergleichsansicht.

👉 Mehr dazu: [F021 – Live Project Browser mit Git-Status](./business/features/F021-live-project-browser-git-status.md)

---

## Typischer Arbeitsablauf

Dieser Ablauf zeigt, wie eine Aufgabe von Anfang bis Ende bearbeitet wird.

1. **Projekt anlegen** – Öffnen Sie die Softwareschmiede und klicken Sie auf **Neues Projekt**. Geben Sie Namen und Beschreibung ein.
2. **GitHub-Ablage verknüpfen** – Tragen Sie im Projekt die Adresse Ihrer GitHub-Ablage ein. Die Softwareschmiede lädt den Code automatisch herunter, wenn eine Aufgabe startet.
3. **Arbeitsverzeichnis prüfen** (optional) – Öffnen Sie **Einstellungen** und setzen Sie bei Bedarf ein eigenes Basis-Arbeitsverzeichnis für lokale Klone.
4. **Claude einrichten** (optional) – Öffnen Sie **Einstellungen**, tragen Sie bei **Anthropic API Key** Ihren Schlüssel ein und klicken Sie **💾 Speichern**.
5. **Aufgabe erstellen** – Wählen Sie Ihr Projekt aus und klicken Sie auf **Neue Aufgabe**. Optional können Sie eine GitHub-Issue auswählen; Titel und Beschreibung werden übernommen.
6. **Agentenpaket wählen** – Wählen Sie aus der Liste das passende Agentenpaket für diese Aufgabe aus.
7. **KI starten** – Klicken Sie auf **KI starten**. Die KI erhält Ihre Aufgabenbeschreibung und beginnt zu arbeiten.
8. **Echtzeit-Ausgabe beobachten** – Verfolgen Sie auf dem Bildschirm, was die KI gerade tut. Sie sehen Nachrichten, Code-Änderungen und Zwischenergebnisse.
9. **Folge-Anweisung geben** (optional) – Wenn Sie der KI etwas ergänzen möchten, tippen Sie eine Nachricht in das Eingabefeld und senden Sie sie ab.
10. **Ergebnis prüfen** – Sobald die KI fertig ist, sehen Sie eine Zusammenfassung. Prüfen Sie, ob das Ergebnis Ihren Vorstellungen entspricht.
11. **Pull Request erstellen** – Klicken Sie auf **Pull Request erstellen**. Die Änderungen werden als Vorschlag in Ihre GitHub-Ablage hochgeladen. Bei verknüpfter Issue ergänzt die Anwendung automatisch eine Closing-Direktive.
12. **Aufgabe abschließen** – Bestätigen Sie den Abschluss. Die lokale Arbeitskopie wird gelöscht, die Aufgabe gilt als erledigt.

![Typischer Arbeitsablauf](./images/arbeitsablauf.png)

---

## Agentenpakete erklären und verwenden

### Was sind Agentenpakete?

Ein Agentenpaket ist wie ein Regelwerk für die KI.
Es enthält Anweisungen, die bestimmen, wie die KI Aufgaben angeht.
Zum Beispiel kann ein Paket vorgeben, dass die KI immer Tests schreibt oder bestimmte Namensregeln einhält.
Verschiedene Pakete eignen sich für verschiedene Projektarten.

### Wie legt man ein Agentenpaket an?

1. Öffnen Sie den Bereich **Agentenpakete** in der Navigation.
2. Klicken Sie auf **Neues Paket**.
3. Geben Sie dem Paket einen Namen und eine Beschreibung.
4. Fügen Sie die gewünschten Anweisungsdateien hinzu.
5. Speichern Sie das Paket mit **Speichern**.

### Wie wählt man ein Agentenpaket für eine Aufgabe aus?

Beim Anlegen oder Starten einer Aufgabe erscheint ein Auswahlfeld **Agentenpaket**.
Wählen Sie das gewünschte Paket aus der Liste.
Die KI verwendet dann beim Bearbeiten dieser Aufgabe genau die Anweisungen dieses Pakets.

👉 Mehr dazu: [F004 – Agentenpakete](./business/features/F004-agentenpakete.md)

---

## Dashboard und Protokoll nutzen

### Das Dashboard

Das Dashboard zeigt Ihnen alle aktiven Projekte und deren Aufgaben auf einen Blick.
Sie sehen sofort, welche Aufgaben gerade bearbeitet werden und welche abgeschlossen sind.
Farbige Status-Anzeigen helfen Ihnen, den Überblick zu behalten.

![Dashboard-Ansicht](./images/dashboard.png)

### Das Protokoll lesen

Das Protokoll öffnen Sie, indem Sie eine Aufgabe auswählen und auf **Protokoll anzeigen** klicken.

Das Protokoll enthält verschiedene Arten von Einträgen:

| Symbol / Typ | Bedeutung |
|---|---|
| 💬 Nachricht | Die KI hat etwas mitgeteilt oder eine Frage beantwortet. |
| 📝 Code-Änderung | Die KI hat eine Datei bearbeitet oder erstellt. |
| ⚙️ Aktion | Eine automatische Aktion wie das Anlegen der Arbeitskopie. |
| 🔀 Status-Änderung | Die Aufgabe hat einen neuen Status erreicht. |
| ❌ Fehler | Etwas ist schiefgelaufen. Die Fehlermeldung steht daneben. |

Einträge sind chronologisch sortiert – der neueste Eintrag steht unten.
Sie können im Protokoll scrollen und nach bestimmten Begriffen suchen.

👉 Mehr dazu: [F005 – Aufgabenprotokoll](./business/features/F005-aufgabenprotokoll.md)

---

## Claude CLI einrichten und nutzen

### Claude in den Einstellungen hinterlegen

1. Öffnen Sie **Einstellungen** in der Navigation.
2. Suchen Sie die Karte **Claude CLI**.
3. Tragen Sie im Feld **Anthropic API Key** Ihren Schlüssel ein.
4. Klicken Sie auf **💾 Speichern**.
5. Prüfen Sie die Meldung **Einstellungen gespeichert.**

### Claude im Aufgabenablauf verwenden

1. Öffnen Sie eine Aufgabe und klicken Sie auf **🚀 Starten**.
2. Wählen Sie Agentenpaket und Agent wie gewohnt aus.
3. Geben Sie im Bereich **💬 KI-Prompt** Ihre Anweisung ein.
4. Klicken Sie auf **🤖 KI starten**.
5. Verfolgen Sie den Fortschritt im **📜 Protokoll**.

👉 Mehr dazu: [F013 – Claude-CLI-Integration](./business/features/F013-claude-cli-integration.md)

---

## Aufgabe abbrechen

Sie können eine laufende Aufgabe jederzeit abbrechen.
Offene Aufgaben verwerfen Sie direkt, ohne sie vorher zu starten.

**Was passiert beim Abbrechen?**

- Die lokale Arbeitskopie des Codes wird vollständig gelöscht.
- Es werden keine Änderungen in Ihre GitHub-Ablage hochgeladen.
- Die Aufgabe wechselt in den Status **Abgebrochen**.
- Das bisherige Protokoll bleibt erhalten – Sie können es weiterhin einsehen.

**So brechen Sie eine Aufgabe ab:**

1. Öffnen Sie die Aufgabe in der Aufgabenansicht.
2. Klicken Sie auf **Aufgabe abbrechen**.
3. Bestätigen Sie die Nachfrage mit **Ja, abbrechen**.

> ⚠️ **Hinweis:** Das Abbrechen kann nicht rückgängig gemacht werden. Lokale Änderungen der KI gehen verloren.

👉 Mehr dazu: [F007 – Aufgabe abbrechen](./business/features/F007-aufgabe-abbrechen.md)

---

## Offene Aufgabe verwerfen

Wenn eine Aufgabe noch den Status **Offen** hat, können Sie sie direkt verwerfen:

1. Öffnen Sie die Aufgabe in der Detailansicht.
2. Klicken Sie auf **Verwerfen**.
3. Wählen Sie **Archivieren** oder **Dauerhaft löschen**.

Dabei wird keine Arbeitskopie angelegt oder gelöscht, weil die Aufgabe noch nicht gestartet wurde.

👉 Mehr dazu: [F007 – Aufgabe abbrechen](./business/features/F007-aufgabe-abbrechen.md)

---

## Status zurücksetzen bei KI aktiv

Wenn eine Aufgabe im Status **KI aktiv** steht, aber keine Verarbeitung mehr läuft, können Sie sie in der Detailseite wieder in einen bearbeitbaren Zustand setzen.

1. Öffnen Sie die Aufgabe.
2. Klicken Sie auf **↩️ Status zurücksetzen**.
3. Bestätigen Sie mit **Ja, Status zurücksetzen**.

Die Aktion ist nur verfügbar, wenn keine aktive Verarbeitung mehr läuft. Sie ist nicht mit **🩹 Aufgabe wiederherstellen** zu verwechseln.

👉 Mehr dazu: [F023 – Status zurücksetzen bei KI aktiv](./business/features/F023-status-zuruecksetzen-ki-aktiv.md)

---

## Festhängende Aufgabe wiederherstellen

Wenn eine Aufgabe auf **KI aktiv** oder **Tests laufen** stehen bleibt, obwohl nichts mehr arbeitet, können Sie sie in der Detailseite manuell wiederherstellen.

1. Öffnen Sie die Aufgabe.
2. Klicken Sie auf **🩹 Aufgabe wiederherstellen**.
3. Bestätigen Sie mit **Ja, wiederherstellen**.

Die Funktion ist nur verfügbar, wenn keine aktive Verarbeitung mehr läuft. Bei laufender Verarbeitung bleibt die Aktion deaktiviert und zeigt den Grund an.

👉 Mehr dazu: [F016 – Fehlerbehandlung & Recovery](./business/features/F016-fehlerbehandlung-und-recovery.md)

---

## Weiterführende Dokumentation

- [F001 – Projektverwaltung](./business/features/F001-projektverwaltung.md)
- [F002 – Aufgabenverwaltung](./business/features/F002-aufgabenverwaltung.md)
- [F003 – KI-Entwicklungsprozess](./business/features/F003-ki-entwicklungsprozess.md)
- [F004 – Agentenpakete](./business/features/F004-agentenpakete.md)
- [F005 – Aufgabenprotokoll](./business/features/F005-aufgabenprotokoll.md)
- [F006 – Aufgabe abschließen](./business/features/F006-aufgabe-abschliessen.md)
- [F007 – Aufgabe abbrechen](./business/features/F007-aufgabe-abbrechen.md)
- [F009 – Arbeitsverzeichnis konfigurieren](./business/features/F009-arbeitsverzeichnis-konfigurieren.md)
- [F010 – Plugin-Prinzip für Integrationen](./business/features/F010-plugin-prinzip-integrationen.md)
- [F013 – Claude-CLI-Integration](./business/features/F013-claude-cli-integration.md)
- [F021 – Live Project Browser mit Git-Status](./business/features/F021-live-project-browser-git-status.md)
- [Feature-Übersicht](./business/features.md)
