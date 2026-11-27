← [Zurück zur Übersicht](index.md)

# Status-Webseite — Beschreibung

## Zweck

Die Status-Webseite macht den Gesamtzustand der Softwareschmiede — laufende Aufgaben,
wartende Anwenderaktionen, letzte SCM-Ereignisse sowie ergänzende GitHub-Daten —
von überall im Browser einsehbar. Damit nicht jeder Aufrufer die Daten lesen kann,
ist die Seite durch eine Anmeldung mit persönlichen Benutzerkonten geschützt.
Die Webseite verwaltet ihre Benutzer selbst: Es gibt keine freie Registrierung —
der erste Benutzer wird bei der Ersteinrichtung angelegt, alle weiteren Konten
legt die Administration an.

## Funktionsweise

### Anmeldung und Berechtigungsstufen

- Wer die Seite aufruft, ohne angemeldet zu sein, landet auf der **Anmeldung** —
  oder beim allerersten Aufruf auf der **Ersteinrichtung**, die das
  Administratorkonto anlegt.
- Jedes Konto besitzt eine **Berechtigungsstufe**:
  - **Überwacher (nur lesen)** darf die Status-Übersicht und die maschinelle
    Ansicht lesen sowie das eigene Passwort im Profil ändern.
  - **Administration (Verwaltung)** erhält zusätzlich die Verwaltungsbereiche
    **Benutzer**, **Tokens** und **Einstellungen** in der Navigationszeile.
- Die Navigationszeile oben auf jeder Seite zeigt **Status**, **Profil**, den
  eigenen Benutzernamen und eine **Abmelden**-Schaltfläche; die Verwaltungslinks
  erscheinen nur bei der Stufe Administration.
- Die Anmeldung bleibt für die eingestellte **Sitzungsdauer** bestehen und
  verlängert sich bei Nutzung automatisch. Wer sich abmeldet oder dessen Konto
  deaktiviert wird, verliert den Zugriff sofort.

### Sperrung und Deaktivierung

- Nach **drei fehlgeschlagenen Anmeldeversuchen** ist ein Konto für **eine
  Stunde** gesperrt; die Administration kann die Sperrung vorzeitig zurücksetzen.
- Ein **deaktiviertes** Konto kann sich nicht anmelden und verliert eine
  bestehende Anmeldung sofort — das Konto bleibt aber erhalten und kann durch
  die Administration wieder aktiviert werden.
- Der **letzte aktive Administrator** ist geschützt: Er kann nicht deaktiviert,
  gelöscht oder herabgestuft werden — so bleibt immer ein Verwaltungszugang
  erhalten.

### Verwaltung (Stufe Administration)

- **Benutzer**: Liste aller Konten mit Stufe und Status (aktiv, gesperrt bis,
  deaktiviert, letzter Admin). Aktionen je Konto: Berechtigungsstufe ändern,
  deaktivieren/aktivieren, Sperrung zurücksetzen, löschen (mit
  Bestätigungsseite). Darunter das Formular **Benutzer anlegen** mit
  Benutzername, Passwort und Stufen-Auswahl.
- **Tokens**: Liste der Relay-Tokens, mit denen sich App-Instanzen am Server
  anmelden. Ein neu angelegtes Token wird **genau einmal** im Klartext
  angezeigt; danach ist es nicht mehr abrufbar. Tokens können über eine
  Bestätigungsseite **widerrufen** werden — widerrufene Tokens bleiben in der
  Liste sichtbar, berechtigen aber nicht mehr zum Verbindungsaufbau.
- **Einstellungen**: Die **Sitzungsdauer** der Anmeldung in Minuten
  (5–43200, voreingestellt 480).

## Beispiele

- Ein Zweitrechner soll die Status-Übersicht lesen dürfen: Die Administration
  legt auf der Seite **Benutzer** ein Konto mit der Stufe „Überwacher (nur
  lesen)" an und gibt die Zugangsdaten weiter. Der neue Benutzer meldet sich an
  und sieht die Übersicht — ohne Verwaltungslinks.
- Eine neue App-Instanz soll Snapshots an den Server liefern: Die Administration
  legt unter **Tokens** ein Token mit der Bezeichnung der Instanz an, kopiert
  den einmalig angezeigten Klartext und hinterlegt ihn in der App unter
  Einstellungen → Automatisierung → Remote-Übersicht.
- Ein Benutzer hat sein Passwort dreimal falsch eingegeben: Das Konto ist eine
  Stunde gesperrt — oder die Administration klickt in der Benutzerliste auf
  **Sperrung zurücksetzen** und der Benutzer kann sich sofort wieder anmelden.

## Einschränkungen

- Es gibt keinen Passwort-Reset und keine Selbstregistrierung: Bei vergessenem
  Passwort kann die Administration das Konto nur löschen und neu anlegen;
  das eigene Passwort kann jeder Benutzer im Profil ändern.
- Der letzte aktive Administrator kann nicht deaktiviert, gelöscht oder
  herabgestuft werden.
- Der Klartext eines Relay-Tokens wird nur einmal angezeigt — geht er verloren,
  muss ein neues Token angelegt und das alte widerrufen werden.
- Sind alle Administratoren ausgesperrt, gibt es keinen Wiederherstellungsweg in
  der Anwendung; der Server-Betreiber muss die gespeicherten Daten verwerfen und
  die Ersteinrichtung neu durchlaufen (siehe
  [Fehlerbehebung](troubleshooting.md)).
