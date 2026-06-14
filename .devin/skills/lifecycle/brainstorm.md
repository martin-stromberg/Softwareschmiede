# Anforderungs-Brainstorming

Du bekommst eine (möglicherweise unvollständige) Anforderung als Eingabe. Deine Aufgabe ist es, durch gezielte Rückfragen alle notwendigen Informationen zu ermitteln, um die Anforderung vollständig zu beschreiben.

**Eingabe:**
- Die Anforderung als Text (Pflicht)
- Optional: Pfad zu einer bestehenden `requirement.md`-Datei

---

## Schritt 1: Anforderung analysieren

Lies die gegebene Anforderung sorgfältig. Identifiziere, welche der folgenden Dimensionen unklar, unvollständig oder widersprüchlich sind:

| Dimension | Leitfragen |
|-----------|-----------|
| **Ziel** | Was soll am Ende anders sein als vorher? Was ist der Mehrwert? |
| **Auslöser** | Wann/Wodurch wird das Feature genutzt? Wer oder was startet den Ablauf? |
| **Akteure** | Wer sind die Benutzer oder Systeme, die mit dem Feature interagieren? |
| **Funktionalitätsumfang** | Welche konkreten Funktionen soll das Feature bieten? Gibt es Varianten, Konfigurationsoptionen oder bewusst ausgeschlossene Fähigkeiten? |
| **Eingaben** | Welche Daten oder Ereignisse gehen in das Feature ein? |
| **Ausgaben** | Was sind die sichtbaren Ergebnisse oder Seiteneffekte? |
| **Fehlerfall** | Was passiert, wenn etwas schiefläuft? |
| **Grenzen** | Was soll das Feature ausdrücklich *nicht* tun? |
| **Architektonische Entscheidungen** | Wie wird das Feature eingebunden (z. B. API, Bibliothek, CLI, Hintergrunddienst)? Synchron oder asynchron? Werden Daten persistiert – wenn ja, wo und wie lange? Gibt es Abhängigkeiten zu bestehenden Diensten, die genutzt oder vermieden werden sollen? |
| **Priorität** | Welche Teile sind unverzichtbar (Must-have), welche optional (Nice-to-have)? |
| **Akzeptanzkriterien** | Woran erkennt man, dass das Feature korrekt umgesetzt wurde? |

## Schritt 2: Rückfragen stellen

Stelle dem Benutzer **nur die Fragen, die sich nicht eindeutig aus der Anforderung ableiten lassen**. Keine Fragen zu Punkten, die bereits klar beschrieben sind.

Gruppiere die Fragen thematisch. Nummeriere jede Frage, damit der Benutzer seine Antworten den Fragen zuordnen kann. Formuliere jede Frage präzise und verständlich — keine Mehrfachfragen in einer Zeile.

**Beispiel-Struktur der Rückfragen:**

```
Ich habe folgende Rückfragen zur Anforderung:

**Ablauf**
1. ...
2. ...

**Fehlerbehandlung**
3. ...

**Abgrenzung**
4. ...
```

Warte auf die Antworten des Benutzers, bevor du mit Schritt 3 fortfährst.

## Schritt 3: Antworten einarbeiten

Sobald der Benutzer die Rückfragen beantwortet hat, entscheide anhand der Eingabe:

### Fall A — Keine `requirement.md` angegeben

Gib die vervollständigte Anforderung direkt im Chat aus, strukturiert nach folgendem Schema:

```
# Anforderung: {Titel}

## Zusammenfassung
Kurze Beschreibung (2–4 Sätze): Was soll das Feature tun?

## Auslöser und Akteure
- **Auslöser:** ...
- **Akteure:** ...

## Beschreibung
Detaillierte Beschreibung des gewünschten Verhaltens, in Fließtext oder als nummerierte Schritte.

## Eingaben und Ausgaben
- **Eingaben:** ...
- **Ausgaben/Ergebnisse:** ...

## Fehlerbehandlung
Was passiert bei ungültigen Eingaben, fehlenden Daten oder technischen Fehlern?

## Abgrenzung
Was ist ausdrücklich nicht Teil dieser Anforderung?

## Akzeptanzkriterien
- [ ] ...
- [ ] ...

## Offene Punkte
Punkte, die noch nicht abschließend geklärt sind (falls vorhanden).
```

### Fall B — `requirement.md` wurde angegeben

Lies die bestehende Datei. Ergänze sie um die im Brainstorming gewonnenen Informationen und strukturiere sie sauber nach dem Schema aus Fall A, sofern die Datei noch keine klare Struktur hat.

Überschreibe die Datei mit dem vollständigen, aktualisierten Inhalt. Gib danach im Chat eine kurze Zusammenfassung der vorgenommenen Änderungen aus.

---

## Hinweise

- Stelle keine Fragen, die sich durch Recherche im Projektkontext selbst beantworten lassen.
- Wenn die Anforderung bereits vollständig genug ist, um direkt umgesetzt zu werden, teile das dem Benutzer mit und überspringe Schritt 2.
- Technische Umsetzungsfragen (Klassenstruktur, Implementierungsansatz) gehören nicht in dieses Brainstorming — diese werden in `/translate-requirements` und `/plan` behandelt.
- Bewerte nach dem Brainstorming kurz: Welche Punkte sind jetzt klar, und was bleibt eventuell noch offen?
