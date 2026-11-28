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

### Anzeige (Farbschema)

- Die gesamte Webseite ist in einem hellen (**Light**) oder dunklen (**Dark**)
  Farbschema darstellbar. Jeder angemeldete Benutzer wählt im Profil unter
  **Anzeige → Farbschema** eine von drei Einstellungen:
  - **Automatisch (System)**: Die Seite folgt der Hell-/Dunkel-Einstellung
    des eigenen Betriebssystems bzw. Browsers — voreingestellt für alle
    Konten.
  - **Light** / **Dark**: Erzwingt das gewählte Schema unabhängig von der
    Systemeinstellung.
- Die Auswahl wird dauerhaft zum eigenen Konto gespeichert, bleibt über
  Abmeldungen und Server-Neustarts erhalten und gilt auf allen Seiten der
  Web-Oberfläche ab dem nächsten Seitenaufruf.
- Ohne Anmeldung (Anmeldung, Ersteinrichtung, Hinweisseiten) folgt die
  Darstellung immer der Systemeinstellung — eine manuelle Auswahl gibt es
  nur mit Konto.

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
  (5–43200, voreingestellt 480) sowie die Sektion **Updates** für die
  automatische Aktualisierung des Servers.

### Automatische Updates (Stufe Administration)

Der Status-Server kann sich selbst aus den GitHub-Releases des
Softwareschmiede-Repositories aktualisieren (Bibliothek `msTools.Updater`).
Die Sektion **Updates** auf der Seite **Einstellungen** bündelt dafür:

- **Statustabelle**: Installierte Version (aus der Datei
  `release-metadata.json` des Pakets), aktueller **Zustand**, **verfügbare
  Version**, **letzte Prüfung**, **letzter Fehler**, die
  **Installationssperre** sowie der Pfad des **Update-Protokolls**.
- **Kanal und Automatisierung**: Der **Release-Kanal** bestimmt, ob nur
  endgültige Releases („Stable") oder auch Vorabversionen („Pre-Release")
  berücksichtigt werden. Die **Automatisierung** steht auf „Aus" (nur
  manuelle Aktionen), „Nur prüfen" (verfügbare Updates im Prüfzeitfenster
  melden, nichts laden oder installieren) oder „Prüfen und installieren"
  (zusätzlich täglich zur eingestellten **Installationsuhrzeit**
  einspielen, voreingestellt 03:00).
- **Aktionen**: „Jetzt nach Update suchen" (Prüfung plus Download mit
  Integritätsprüfung), „Update jetzt installieren" — nur mit gesetzter
  **Downtime-Bestätigung**, weil der Server während der Installation neu
  startet — und „Update-Sperre zurücksetzen" (nur sichtbar, wenn ein
  abgebrochener Installationsversuch eine verwaiste Sperre hinterlassen
  hat). Die manuellen Aktionen laufen auch bei Automatisierung „Aus".

Beim Dateiaustausch bleiben das `data`-Verzeichnis (Benutzer, Tokens,
Einstellungen) und die `appsettings*.json`-Konfiguration unberührt — sie
werden vorher gesichert und danach restauriert.

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
- Der Server soll nachts aktuelle Vorabversionen einspielen: Die Administration
  stellt unter **Einstellungen → Updates** den Kanal „Pre-Release" und die
  Automatisierung „Prüfen und installieren" mit der Installationsuhrzeit 03:00
  ein — Updates werden im Prüfzeitfenster gesucht, geladen und täglich um
  03:00 installiert.

## Einschränkungen

- Es gibt keinen Passwort-Reset und keine Selbstregistrierung: Bei vergessenem
  Passwort kann die Administration das Konto nur löschen und neu anlegen;
  das eigene Passwort kann jeder Benutzer im Profil ändern.
- Der letzte aktive Administrator kann nicht deaktiviert, gelöscht oder
  herabgestuft werden.
- Der Klartext eines Relay-Tokens wird nur einmal angezeigt — geht er verloren,
  muss ein neues Token angelegt und das alte widerrufen werden.
- Das Farbschema lässt sich nur mit Benutzerkonto wählen; nicht angemeldete
  Besucher sehen die Seiten im Schema ihrer Systemeinstellung. Ein Wechsel
  wirkt erst beim nächsten Seitenaufruf — die Seite schaltet nicht im
  laufenden Bild um.
- Sind alle Administratoren ausgesperrt, gibt es keinen Wiederherstellungsweg in
  der Anwendung; der Server-Betreiber muss die gespeicherten Daten verwerfen und
  die Ersteinrichtung neu durchlaufen (siehe
  [Fehlerbehebung](troubleshooting.md)).
- Die automatische Update-Funktion setzt ein Server-Paket voraus, das eine
  `release-metadata.json` enthält — Pakete aus Releases vor Einführung dieser
  Pipeline-Erweiterung melden die installierte Version als „unbekannt", und
  die Update-Prüfung bleibt wirkungslos, bis einmal ein aktuelles Paket
  (manuell) installiert wurde.
