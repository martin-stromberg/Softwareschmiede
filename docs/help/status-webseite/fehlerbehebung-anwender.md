← [Zurück zur Übersicht](index.md)

# Status-Webseite — Fehlerbehebung

## „Benutzername oder Passwort ist falsch."

**Symptom:** Die Anmeldung schlägt fehl, obwohl die Zugangsdaten richtig
scheinen.

**Ursache:** Tippfehler im Benutzernamen oder Passwort — aus Sicherheitsgründen
zeigt die Seite nicht an, welches der beiden falsch war.

**Lösung:**

1. Eingaben sorgfältig wiederholen (Groß-/Kleinschreibung beachten).
2. Bei wiederholtem Fehlschlag nicht weiterprobieren — nach drei Versuchen
   wird das Konto eine Stunde gesperrt.
3. Passwort vergessen: Die Administration kann kein Passwort zurücksetzen —
   sie kann das Konto löschen und neu anlegen.

## „Das Konto ist gesperrt."

**Symptom:** Nach mehreren Fehlversuchen meldet die Anmeldung eine Sperrung.

**Ursache:** Nach drei fehlgeschlagenen Anmeldeversuchen sperrt das Konto
automatisch für eine Stunde.

**Lösung:**

1. Eine Stunde warten — die Sperrung endet automatisch.
2. Oder die Administration bitten, in der Benutzerliste auf **Sperrung
   zurücksetzen** zu klicken — dann ist die Anmeldung sofort wieder möglich.

## „Dieses Konto ist deaktiviert."

**Symptom:** Trotz richtiger Zugangsdaten erscheint die Meldung „Dieses Konto
ist deaktiviert." — oder eine bestehende Anmeldung endet plötzlich.

**Ursache:** Die Administration hat das Konto deaktiviert. Deaktivierung wirkt
sofort, auch auf bereits angemeldete Sitzungen.

**Lösung:** Die Administration bitten, das Konto auf der Seite **Benutzer**
wieder zu **aktivieren**. Danach ist die Anmeldung sofort wieder möglich.

## „Zugriff verweigert"

**Symptom:** Die Seite meldet „Zugriff verweigert — Deine Berechtigungsstufe
reicht für diesen Bereich nicht aus."

**Ursache:** Ein Verwaltungsbereich (Benutzer, Tokens, Einstellungen) wurde mit
der Stufe „Überwacher" aufgerufen — diese Stufe darf nur lesen.

**Lösung:** Zur Status-Übersicht zurückkehren. Wird Verwaltungszugriff benötigt,
die Administration bitten, die Stufe auf „Administration (Verwaltung)" zu
ändern — die neue Stufe gilt spätestens beim nächsten Seitenaufruf, ohne
erneute Anmeldung.

## „Formular abgelaufen"

**Symptom:** Beim Absenden eines Formulars erscheint die Seite „Formular
abgelaufen".

**Ursache:** Das Formular war zu lange geöffnet oder wurde aus einem veralteten
Browser-Tab abgesendet; die Eingaben wurden nicht übernommen.

**Lösung:** Die Seite neu laden und das Formular erneut absenden.

## „Der Klartext des Tokens wurde bereits angezeigt"

**Symptom:** Nach dem Anlegen eines Tokens erscheint beim erneuten Laden der
Seite nur ein Hinweis statt des Klartexts.

**Ursache:** Der Klartext eines Tokens wird aus Sicherheitsgründen genau einmal
angezeigt und nirgends gespeichert.

**Lösung:** Das Token ist angelegt und in der Liste sichtbar. Wurde der
Klartext nicht rechtzeitig kopiert, das Token **widerrufen** und ein neues
anlegen — den neuen Klartext sofort übernehmen.

## Die Seite ist kurzzeitig nicht erreichbar (Server-Update)

**Symptom:** Die Status-Webseite lädt nicht oder der Aufruf bricht ab; kurz
darauf ist sie wieder erreichbar und verlangt ggf. eine erneute Anmeldung.

**Ursache:** Die Administration oder die eingestellte Automatisierung hat
gerade ein Server-Update eingespielt — dafür wird der Server neu gestartet.
Laufende Relay-Verbindungen der App-Instanzen verbinden sich danach
selbstständig neu.

**Lösung:** Typischerweise unter einer Minute warten und die Seite neu
laden. Bleibt der Server dauerhaft unerreichbar, die Administration bzw. den
Server-Betreiber informieren (siehe
[Fehlerbehebung für Betreiber](troubleshooting.md)).

## Das Farbschema wechselt nicht wie erwartet

**Symptom:** Nach dem Speichern einer neuen Anzeige-Einstellung bleibt die
Seite zunächst im alten Farbschema — oder sie folgt weiter der
Systemeinstellung, obwohl Light bzw. Dark gewählt wurde.

**Ursache:** Das Farbschema wird beim Aufruf jeder Seite vom Server
festgelegt; eine bereits geöffnete Seite schaltet nicht im laufenden Bild
um. Steht die Einstellung auf „Automatisch (System)", folgt die Darstellung
außerdem bewusst der Hell-/Dunkel-Einstellung des Geräts — sie ändert sich
nur, wenn sich diese Systemeinstellung ändert.

**Lösung:**

1. Die Seite einmal neu laden — ab dann gilt das gespeicherte Schema.
2. Soll die Seite unabhängig von der Geräteeinstellung hell bzw. dunkel
   bleiben, im Profil unter **Anzeige → Farbschema** „Light" oder „Dark"
   wählen statt „Automatisch (System)".
3. Seiten ohne Anmeldung (z. B. die Anmeldung selbst) folgen immer der
   Systemeinstellung — dort gibt es keine manuelle Auswahl.

## Wann Hilfe nötig ist

- **Passwort vergessen:** Die Administration kann das Konto löschen und neu
  anlegen; ein Zurücksetzen des Passworts ist nicht möglich.
- **Konto gesperrt oder deaktiviert:** Nur die Administration kann freischalten
  bzw. aktivieren.
- **Kein Zugang mehr möglich (alle Administratoren betroffen):** Es gibt keinen
  Wiederherstellungsweg in der Anwendung — der Server-Betreiber muss die
  gespeicherten Daten verwerfen und die Ersteinrichtung neu durchlaufen (siehe
  [Fehlerbehebung für Betreiber](troubleshooting.md)).
