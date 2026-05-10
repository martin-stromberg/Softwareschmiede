# F001 – Projektverwaltung

## Einleitung

Die Projektverwaltung ist der Ausgangspunkt für Ihre Arbeit in der Softwareschmiede.
Hier legen Sie alle Ihre Softwareprojekte an und behalten den Überblick.
Jedes Projekt fasst zusammen, worum es bei einem Vorhaben geht, und verbindet es mit der GitHub-Ablage, wo der Code gespeichert wird.
Archivierte Projekte verschwinden aus der aktiven Ansicht, bleiben aber erhalten.
So bleibt Ihr Arbeitsbereich übersichtlich.

![Projektübersicht](../images/F001-projektübersicht.png)

---

## Wer nutzt es?

**Softwareentwickler**, die mehrere Projekte gleichzeitig betreuen.
Sie legen zu Beginn eines neuen Vorhabens ein Projekt an und verknüpfen es mit ihrer GitHub-Ablage.
Über die Projektverwaltung steuern sie, was aktiv bearbeitet wird und was bereits abgeschlossen ist.

---

## Schritt-für-Schritt-Anleitung

### Neues Projekt anlegen

1. Sie öffnen die Softwareschmiede und sehen das **Dashboard**.
2. Sie klicken auf die Schaltfläche **Neues Projekt**.
3. Sie geben im Formular einen **Projektnamen** ein (z.B. „Meine Webseite").
4. Sie tragen eine kurze **Beschreibung** ein, die erklärt, worum es geht.
5. Sie fügen die Adresse Ihrer **GitHub-Ablage** ein (z.B. `https://github.com/nutzername/projekt`).
6. Sie klicken auf **Speichern**. Das Projekt erscheint nun im Dashboard.

![Neues Projekt anlegen](../images/F001-neues-projekt.png)

### Projekt bearbeiten

1. Sie klicken im Dashboard auf das gewünschte Projekt.
2. Sie klicken auf **Projekt bearbeiten** (Stift-Symbol).
3. Sie ändern Name, Beschreibung oder die GitHub-Adresse.
4. Sie speichern mit **Speichern**.

### Projekt archivieren

1. Sie öffnen das Projekt.
2. Sie klicken auf **Archivieren**.
3. Sie bestätigen die Nachfrage.
4. Das Projekt wechselt in den Status **Archiviert** und verschwindet aus dem aktiven Dashboard.

### Projekt löschen

1. Sie öffnen das Projekt.
2. Sie klicken auf **Löschen**.
3. Sie bestätigen die Nachfrage mit **Ja, löschen**.

> ⚠️ **Achtung:** Das Löschen eines Projekts entfernt auch alle zugehörigen Aufgaben und Protokolle dauerhaft. Der Code in Ihrer GitHub-Ablage bleibt unberührt.

---

## Beispiel

Sie beginnen ein neues Vorhaben: Sie möchten eine Buchungssoftware für Ihr Büro entwickeln.
Sie öffnen die Softwareschmiede und klicken auf **Neues Projekt**.
Sie nennen das Projekt „Buchungssoftware Büro" und beschreiben kurz den Zweck.
Sie fügen die Adresse der GitHub-Ablage ein, in der der Code gespeichert werden soll.
Nach dem Speichern erscheint das Projekt sofort im Dashboard, und Sie können erste Aufgaben anlegen.

---

## Was passiert im Hintergrund?

Wenn Sie eine GitHub-Ablage-Adresse eintragen, merkt sich die Softwareschmiede diese.
Sobald Sie später eine Aufgabe starten, lädt die Softwareschmiede den Code automatisch auf Ihren Rechner.
Der Speicherort der lokalen Arbeitskopie wird über die globale Einstellung für das Arbeitsverzeichnis gesteuert.
Beim Archivieren oder Löschen wird kein Code verändert – nur die Verwaltungseinträge in der Softwareschmiede werden angepasst.

---

## Häufige Fragen (FAQ)

**Kann ich mehrere Projekte gleichzeitig aktiv haben?**
Ja. Die Softwareschmiede unterstützt beliebig viele aktive Projekte.

**Was passiert mit meinen Aufgaben, wenn ich ein Projekt archiviere?**
Die Aufgaben bleiben erhalten und sind über das archivierte Projekt weiterhin einsehbar. Neue Aufgaben können Sie in einem archivierten Projekt nicht mehr anlegen.

**Kann ich eine GitHub-Ablage mehreren Projekten zuweisen?**
Technisch ist das möglich, wird aber nicht empfohlen. Jede Aufgabe legt eine eigene Arbeitskopie an, was zu Konflikten führen kann.

**Was passiert mit dem Code, wenn ich ein Projekt lösche?**
Ihr Code auf GitHub bleibt vollständig erhalten. Nur die Aufzeichnungen in der Softwareschmiede werden gelöscht.

**Kann ich ein archiviertes Projekt wieder aktivieren?**
Ja. Öffnen Sie das archivierte Projekt und klicken Sie auf **Reaktivieren**.

---

## Verwandte Funktionen

- [F002 – Aufgabenverwaltung](./F002-aufgabenverwaltung.md) – Aufgaben innerhalb eines Projekts anlegen und verwalten
- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – KI für eine Aufgabe starten
- [F009 – Arbeitsverzeichnis konfigurieren](./F009-arbeitsverzeichnis-konfigurieren.md) – Basisverzeichnis lokaler Klone festlegen
- [Zurück zur Übersicht](../features.md)
