← [Zurück zur Übersicht](index.md)

# Status-Webseite — Ablauf für Anwender

## Voraussetzungen

- Die Adresse des Status-Servers ist bekannt (vom Betreiber).
- Für Anmeldungen nach der Ersteinrichtung: ein Benutzerkonto mit Benutzername
  und Passwort, das die Administration angelegt hat.

## Schritt-für-Schritt-Anleitung

### 1. Ersteinrichtung (einmalig, nur beim allerersten Aufruf)

1. Öffne die Adresse der Status-Webseite im Browser. Solange noch kein
   Benutzerkonto existiert, erscheint automatisch die Seite **Ersteinrichtung**.
2. Vergib einen **Benutzernamen** — erlaubt sind die Zeichen a–z, A–Z, 0–9
   sowie `-._@+` (keine Umlaute, keine Leerzeichen).
3. Vergib ein **Passwort** (mindestens 8 Zeichen) und wiederhole es im Feld
   **Passwort wiederholen**.
4. Klicke **Administratorkonto anlegen**. Du bist sofort angemeldet und landest
   auf der Status-Übersicht.

> **Hinweis:** Das so angelegte Konto erhält die Stufe **Administration** und
> ist der einzige Weg, ein erstes Konto zu erzeugen. Danach ist die
> Registrierung dauerhaft geschlossen — die Ersteinrichtung erscheint nie
> wieder.

### 2. Anmelden

1. Öffne die Adresse der Status-Webseite. Ohne bestehende Anmeldung erscheint
   die Seite **Anmeldung**.
2. Gib **Benutzername** und **Passwort** ein und klicke **Anmelden**.
3. Bei Erfolg öffnet sich die Status-Übersicht.

> **Hinweis:** Nach drei falschen Versuchen ist das Konto eine Stunde gesperrt.
> Entweder abwarten oder die Administration bitten, die Sperrung zurückzusetzen.

### 3. Eigenes Passwort ändern

1. Klicke in der Navigationszeile auf **Profil**.
2. Gib unter **Passwort ändern** das **Aktuelle Passwort** sowie zweimal das
   **Neue Passwort** (mindestens 8 Zeichen) ein.
3. Klicke **Passwort ändern** — die Bestätigung „Das Passwort wurde geändert."
   erscheint; die Anmeldung bleibt bestehen.

### 4. Benutzer verwalten (Stufe Administration)

1. Klicke in der Navigationszeile auf **Benutzer**.
2. **Anlegen:** Unten im Abschnitt **Benutzer anlegen** Benutzername, Passwort
   und Berechtigungsstufe wählen und **Benutzer anlegen** klicken. Die
   Zugangsdaten dem Benutzer mitteilen.
3. **Stufe ändern:** In der Zeile des Benutzers **Zu Überwacher herabstufen**
   bzw. **Zu Administration befördern** klicken.
4. **Deaktivieren / Aktivieren:** **Deaktivieren** sperrt das Konto sofort
   (bestehende Anmeldungen enden), **Aktivieren** gibt es wieder frei.
5. **Sperrung zurücksetzen:** Nur bei gesperrten Konten verfügbar — hebt die
   einstündige Sperre sofort auf.
6. **Löschen:** Auf **Löschen** klicken — es erscheint eine Bestätigungsseite,
   erst **Endgültig löschen** entfernt das Konto unwiderruflich; **Abbrechen**
   führt zurück.

> **Hinweis:** Beim letzten aktiven Administrator sind Deaktivieren, Löschen
> und Herabstufen abgeblockt — das Konto ist mit „letzter Admin" markiert.

### 5. Relay-Token für eine App-Instanz anlegen (Stufe Administration)

1. Klicke in der Navigationszeile auf **Tokens**.
2. Gib im Abschnitt **Token anlegen** eine **Bezeichnung** ein (z. B. den Namen
   der App-Instanz) und klicke **Token anlegen**.
3. Der Klartext des Tokens wird **genau jetzt einmalig** in einem gelben Block
   angezeigt — sofort kopieren und in der App unter
   **Einstellungen → Automatisierung → Remote-Übersicht → Relay-Token**
   hinterlegen.

> **Hinweis:** Nach dem Verlassen der Seite ist der Klartext nicht mehr
> abrufbar. Ein Refresh zeigt stattdessen den Hinweis, dass die Anzeige bereits
> erfolgt ist.

### 6. Relay-Token widerrufen (Stufe Administration)

1. Klicke auf **Tokens** und in der Zeile des Tokens auf **Widerrufen**.
2. Bestätige auf der Bestätigungsseite mit **Token endgültig widerrufen** —
   oder **Abbrechen**.
3. Das Token bleibt als „widerrufen" in der Liste sichtbar; App-Instanzen, die
   es weiterhin senden, werden abgelehnt.

### 7. Sitzungsdauer einstellen (Stufe Administration)

1. Klicke auf **Einstellungen**.
2. Trage unter **Sitzungsdauer** die Dauer der Anmeldesession in Minuten ein
   (erlaubt: 5–43200) und klicke **Speichern**.
3. Der neue Wert gilt ab der nächsten Ausstellung bzw. Verlängerung der
   Anmeldung — laufende Sitzungen werden bei ihrer nächsten Erneuerung
   angepasst.

### 8. Abmelden

Klicke in der Navigationszeile auf **Abmelden** — die Anmeldung endet und die
Anmeldeseite erscheint.

## Ergebnis

Nach der Anmeldung steht die Status-Übersicht (und die maschinelle Ansicht)
zur Verfügung — Inhalt abhängig von der Berechtigungsstufe: Überwacher lesen,
Administratoren verwalten zusätzlich Benutzer, Tokens und die Sitzungsdauer.
Alle Zustände bleiben über Server-Neustarts hinweg erhalten.

## Barrierefreiheit

Die Seiten sind vollständig tastaturbedienbare HTML-Formulare ohne JavaScript:
Beschriftete Eingabefelder, Pflichtfeld- und Längenprüfung direkt im Browser,
Links und Schaltflächen sind per Tab erreichbar.
