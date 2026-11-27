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
