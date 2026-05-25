# F027 – KI-Protokoll Auto-Scroll

## Einleitung

Diese Funktion sorgt für ruhiges und verlässliches Lesen im **KI-Protokoll**.
Beim Öffnen sehen Sie direkt den neuesten Stand.
Neue Inhalte laufen nur dann automatisch mit, wenn Sie unten bleiben.
Wenn Sie nach oben scrollen, bleibt Ihre Leseposition stabil.
So behalten Sie in langen Protokollen die Kontrolle.

![KI-Protokoll Auto-Scroll](../images/F027-ki-protokoll-auto-scroll.png)

---

## Wer nutzt es?

Diese Funktion nutzen vor allem **Sachbearbeitende** und **Projektverantwortliche**.
Sie verfolgen laufende KI-Arbeit und prüfen spätere Ergebnisse.
Neue Mitarbeitende nutzen sie, um längere Verläufe stressfrei zu lesen.

---

## Schritt-für-Schritt-Anleitung

### Automatisches Scrollen beim Öffnen

1. Sie öffnen eine Aufgabe mit laufendem oder vorhandenem **KI-Protokoll**.
2. Sie wechseln in die Ansicht **Protokoll**.
3. Sie sehen sofort den neuesten Eintrag am Ende des Verlaufs.

### Bedingtes Follow-Scrolling bei neuem Inhalt

1. Sie bleiben am unteren Ende des **KI-Protokolls**.
2. Während neue Einträge erscheinen, bleibt die Ansicht automatisch am Ende.
3. Sie müssen nicht selbst nach unten scrollen.

### Manuelles Lesen ohne Positionsverlust

1. Sie scrollen nach oben, um frühere Einträge zu lesen.
2. Währenddessen kommen neue Einträge hinzu.
3. Ihre aktuelle Leseposition bleibt unverändert.
4. Sie entscheiden selbst, wann Sie wieder nach unten gehen.

---

## Nutzen, Nutzerverhalten, Akzeptanzkriterien und UX-Erwartungen

### 1) Automatisches Scrollen beim Öffnen

- **Nutzen:** Sie starten ohne Suchen beim neuesten Stand.
- **Nutzerverhalten:** Sie öffnen das Protokoll meist mit Fokus auf aktuelle Ergebnisse.
- **Akzeptanzkriterien:** Nach dem Öffnen liegt der sichtbare Bereich am Ende des Protokolls.
- **UX-Erwartung:** Der Einstieg wirkt sofort klar und ohne zusätzlichen Klick.

### 2) Bedingtes Follow-Scrolling bei neuem Inhalt

- **Nutzen:** Live-Aktualisierungen bleiben sichtbar, ohne manuelles Nachführen.
- **Nutzerverhalten:** Sie beobachten neue Einträge oft direkt im laufenden Betrieb.
- **Akzeptanzkriterien:** Automatisches Mitlaufen erfolgt nur, wenn Sie zuvor unten waren.
- **UX-Erwartung:** Der Verlauf wirkt ruhig, vorhersehbar und nicht sprunghaft.

### 3) Manuelles Lesen ohne Positionsverlust

- **Nutzen:** Sie können ältere Inhalte sicher prüfen und vergleichen.
- **Nutzerverhalten:** Sie lesen bei Rückfragen gezielt weiter oben im Verlauf.
- **Akzeptanzkriterien:** Beim Hochscrollen bleibt die Position trotz neuer Einträge erhalten.
- **UX-Erwartung:** Die Ansicht respektiert Ihre Entscheidung und unterbricht das Lesen nicht.

---

## Beispiel

Sie prüfen morgens eine Aufgabe mit vielen neuen Einträgen.
Beim Öffnen landen Sie direkt unten beim aktuellen Stand.
Dann scrollen Sie nach oben, um eine Entscheidung von gestern zu prüfen.
Während neue Einträge eintreffen, bleibt Ihr Blick an der gewählten Stelle.
Erst danach gehen Sie bewusst wieder ans Ende.

---

## Was passiert im Hintergrund?

Die Ansicht merkt sich, ob Sie gerade unten im Verlauf sind.
Nur in diesem Fall folgt sie neuen Einträgen automatisch.
Sobald Sie nach oben gehen, stoppt das automatische Mitlaufen.
So bleibt Ihre Leseposition geschützt.

---

## Häufige Fragen (FAQ)

**Warum springt das Protokoll beim Öffnen direkt nach unten?**
So sehen Sie sofort den neuesten Stand der Aufgabe.

**Warum läuft das Protokoll manchmal nicht automatisch mit?**
Sie befinden sich dann nicht am Ende des Verlaufs.

**Verliere ich meine Position, wenn neue Einträge ankommen?**
Nein. Beim Lesen weiter oben bleibt Ihre Stelle erhalten.

**Wie aktiviere ich das automatische Mitlaufen wieder?**
Sie scrollen einfach wieder ganz nach unten.

**Ist diese Funktion auch bei langen Protokollen hilfreich?**
Ja. Gerade dort spart sie Zeit und verhindert Orientierungverlust.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Laufende KI-Arbeit beobachten
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Protokoll lesen und verstehen
- [F024 – Benachrichtigungssystem für abgeschlossene KI-Aufgaben](./F024-benachrichtigungssystem-fuer-abgeschlossene-ki-aufgaben.md) – Abschlussmeldungen erhalten
- [Zurück zur Übersicht](../features.md)
