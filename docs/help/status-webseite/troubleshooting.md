← [Zurück zur Übersicht](index.md)

# Status-Webseite — Fehlerbehebung (Betrieb)

## Anmeldung hinter HTTPS-Reverse-Proxy schlägt fehl / Cookie wird nicht gesetzt

**Symptom:** Der Login hinter einem HTTPS-Proxy kehrt sofort zur Anmeldeseite
zurück; lokal über HTTP funktioniert es.

**Ursache:** Das Anmelde-Cookie nutzt `CookieSecurePolicy.SameAsRequest`.
Kommt `X-Forwarded-Proto` nicht am Server an, erscheint die Anfrage als
`http` — der Cookie wird ohne `Secure`-Flag ausgestellt bzw. vom Browser
hinter `https` verworfen.

**Lösung:**

1. Im Reverse Proxy `X-Forwarded-Proto: $scheme` setzen (siehe nginx-Beispiel
   in `server/README.md`).
2. Der Server wertet `ForwardedHeaders.All` aus (Trusted-Proxy-Liste ist für
   Self-Hosting geleert) — der Header muss nur ankommen.

## Alle Administratoren ausgesperrt — kein Zugang mehr möglich

**Symptom:** Kein Konto mit Stufe `Administration` kann sich anmelden (alle
gesperrt, deaktiviert oder Passwort unbekannt).

**Ursache:** Es existiert bewusst kein In-App-Recovery (kein Passwort-Reset,
keine Recovery-Codes). Der Letzter-Admin-Schutz verhindert nur das *Erschaffen*
des Zustands über die UI — nicht z. B. einen Lockout aller Admins.

**Lösung:**

1. Server stoppen.
2. Das `data`-Verzeichnis unter dem Content-Root löschen (damit gehen Benutzer,
   Tokens und Einstellungen verloren; Snapshot-Daten sind ohnehin flüchtig).
3. Server starten → Migrationen laufen erneut, `/register` bietet die
   Ersteinrichtung an.
4. Administratorkonto neu anlegen, Benutzer und Relay-Tokens neu erzeugen und
   in den App-Instanzen umstellen.

## `/relay` lehnt alle Verbindungen ab

**Symptom:** App-Instanzen melden Verbindungsfehler; die Token-Liste ist leer.

**Ursache:** Ohne mindestens ein gültiges Token gibt es keine Zugangsberechtigung —
die frühere Konfigurationsliste `Relay:Tokens`/`Relay:DevToken` existiert nicht
mehr.

**Lösung:** In der Admin-UI unter **Tokens** ein Token anlegen und den
einmalig angezeigten Klartext in den App-Einstellungen (Remote-Übersicht)
hinterlegen. App-Instanzen mit widerrufenem Token neu verbinden: neues Token
anlegen → in der App umstellen → altes widerrufen.

## Token-Klartext nicht notiert

**Symptom:** Nach dem Anlegen steht nur der Hinweis „Der Klartext des Tokens
wurde bereits angezeigt oder ist nicht mehr abrufbar".

**Ursache:** Der Klartext liegt nur im In-Memory-`RelayTokenPlaintextStore`
(einmaliger `Take`, TTL 2 Minuten) und nie in der Datenbank — ein Refresh des
`?created=`-Links oder ein Server-Neustart entfernt ihn.

**Lösung:** Token widerrufen, neu anlegen, Klartext sofort übernehmen.

## Alte Konfigurationseinträge werden ignoriert

**Symptom:** `Page:Username`/`Page:Password` oder `Relay:Tokens`/`Relay:DevToken`
in `appsettings.json` haben keine Wirkung mehr.

**Ursache:** Die Schlüssel wurden mit der Benutzerverwaltung entfernt —
bewusst ohne Migrationspfad oder Fallback.

**Lösung:** Einträge entfernen; Zugriff über die Cookie-Anmeldung und
datenbankgestützte Tokens einrichten.

## „Formular abgelaufen" häufig bei lang geöffneten Seiten

**Symptom:** POST-Formulare schlagen mit der 400-Seite „Formular abgelaufen"
fehl.

**Ursache:** Das Antiforgery-Token ist zeitlich gebunden; sehr alt geöffnete
Formularseiten (oder veraltete Tabs) senden ein ungültiges Token
(`EndpointSecurityExtensions.RequireAntiforgery` → `RenderFormExpired`).

**Lösung:** Seite neu laden, Formular erneut absenden. Kein Datenverlust — die
Eingaben wurden nicht übernommen.

## Sitzungsdauer-Änderung scheint nicht zu greifen

**Symptom:** Nach dem Speichern einer kürzeren Sitzungsdauer bleiben Benutzer
länger angemeldet.

**Ursache:** Der Wert gilt ab der nächsten Cookie-Ausstellung bzw. Erneuerung —
bestehende Tickets werden erst bei ihrer Sliding-Erneuerung (halbe
Ticket-Laufzeit) angepasst.

**Lösung:** Abwarten oder Benutzer ab-/neu anmelden lassen; deaktivierte
Konten verlieren die Session dagegen sofort (`ValidatePrincipal` je Request).

## „Installierte Version: unbekannt" — Update-Prüfung findet nichts

**Symptom:** Die Update-Statustabelle zeigt als installierte Version
„unbekannt"; die Prüfung meldet kein Update.

**Ursache:** Die installierte Version stammt aus `release-metadata.json` im
Installationsverzeichnis — Pakete aus Releases vor der Update-Pipeline-
Erweiterung enthalten diese Datei nicht. Ohne bekannte Ausgangsversion kann
keine neuere Version bestimmt werden.

**Lösung:**

1. Ein aktuelles StatusServer-Paket (`statusserver-win-x64.zip` /
   `statusserver-linux-x64.zip`) aus der Release-Pipeline manuell
   einspielen — das Paket enthält `release-metadata.json`.
2. Danach zeigt die Tabelle die installierte Version und die Update-
   Prüfung arbeitet regulär.

## Update-Prüfung oder Download schlägt fehl

**Symptom:** „Die Update-Prüfung ist fehlgeschlagen" bzw. „Letzter Fehler"
in der Update-Sektion.

**Ursache:** Typischerweise die Update-Quelle nicht erreichbar (Netzwerk,
GitHub-Ausfall), das GitHub-Rate-Limit anonymer API-Aufrufe erschöpft oder
die Integritätsprüfung des geladenen Pakets fehlgeschlagen.

**Lösung:**

1. Den Fehlertext in der Sektion **Updates** und im Server-Log lesen;
   das Skript-Protokoll steht unter dem angezeigten
   **Update-Protokoll**-Pfad (`updates/update.log`).
2. Bei Rate-Limit-Fehlern `GitHub:Token` konfigurieren (der Update-Client
   nutzt es als Bearer mit).
3. Der Server läuft unverändert weiter — die nächste reguläre Prüfung
   erfolgt automatisch, oder „Jetzt nach Update suchen" erneut auslösen.

## „Die Installation wurde nicht gestartet"

**Symptom:** Nach „Update jetzt installieren" erscheint die Meldung „Die
Installation wurde nicht gestartet — es liegt kein bereites Update vor oder
der Vorgang wurde übersprungen bzw. abgebrochen."

**Ursache:** Es liegt kein installationsbereites Paket vor (keine Prüfung
gelaufen, kein Update gefunden oder Download noch nicht abgeschlossen) —
oder die Downtime-Bestätigung fehlte.

**Lösung:**

1. Erst „Jetzt nach Update suchen" ausführen und warten, bis die
   Statustabelle „Bereit zur Installation" bzw. eine verfügbare Version
   zeigt.
2. Beim Installieren die Checkbox **Downtime bestätigen** setzen — ohne
   sie startet nichts.

## Verwaiste Installations-Sperre blockiert Updates

**Symptom:** Die Statustabelle zeigt bei „Installationssperre" „verwaist —
Reset möglich"; weitere Update-Aktionen laufen nicht.

**Ursache:** Ein Installationsversuch wurde abgebrochen (z. B.
Prozessabbruch) und hat `update.lock` im Workspace `updates/` liegen
lassen — der Lock blockiert alle weiteren Update-Läufe.

**Lösung:**

1. Prüfen, dass wirklich keine Installation mehr läuft.
2. In der Sektion **Updates** auf **Update-Sperre zurücksetzen** klicken —
   der Button erscheint nur bei verwaistem Lock (älter als
   `Update:HealthTimeoutSeconds`, Default 120 s) und löscht den Lock samt
   Status-Korrektur.

> **Hinweis:** Zeigt die Sperre „aktiv", läuft gerade eine Installation —
> dann nicht zurücksetzen, sondern den Lauf zu Ende bringen lassen.

## Server nach Update-Neustart nicht erreichbar

**Symptom:** Nach „Der Server startet neu" bleibt die Seite dauerhaft
unerreichbar.

**Ursache:** Das detached laufende Installationsskript konnte den Server
nicht wieder starten (z. B. fehlende Rechte, Dienst-/Unit-Konfiguration,
dauerhaft gesperrte Dateien). Der Fehlversuch hinterlässt den Marker
`update.failed`; geschützte Dateien werden aus dem Backup restauriert.

**Lösung:**

1. `updates/update.log` und `updates/status.json` im
   `Update:WorkingDirectory` auswerten — dort steht, an welchem Schritt
   (Stopp, Austausch, Start) es scheiterte.
2. Den Dienst/Prozess manuell starten (systemd-Unit, Windows-Dienst, IIS
   oder direkter Exe-Aufruf) — danach meldet die Update-Sektion den
   Fehlerzustand samt `LastError`.
3. Bei Windows-Dienstbetrieb `Update:ServiceName` bzw. bei direktem Start
   `Update:ExecutablePath` korrekt setzen; unter IIS die automatische
   Erkennung wirken lassen.
