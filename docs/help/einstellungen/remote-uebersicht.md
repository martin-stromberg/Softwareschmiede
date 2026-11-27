← [Zurück zur Übersicht](index.md)

# Einstellungen — Remote-Übersicht

Der Abschnitt **„Remote-Übersicht"** in der Registerkarte **„Automatisierung"** bindet
diese App-Instanz an einen externen Status-Server an. Sobald der Schalter aktiv ist und
die Konfiguration vollständig ist, sendet die App regelmäßig einen Status-Snapshot
(laufende Aufgaben, wartende Anwenderaktionen, letzte SCM-Ereignisse) an den Server,
der daraus eine öffentlich abrufbare, rein lesende Status-Webseite rendert.

## Felder

| Feld | Bedeutung |
|------|-----------|
| **Funktionsschalter** | Aktiviert/deaktiviert die Remote-Übersicht. Deaktiviert bleiben die Eingabefelder sichtbar, aber gesperrt — Instanz-ID und Verbindungsstatus bleiben einsehbar. |
| **Server-URL** | Adresse des Status-Servers, z. B. `http://localhost:5055` oder `https://status.example.org`. `http(s)` und `ws(s)` werden akzeptiert und intern auf den Hub-Pfad `/relay` normalisiert. |
| **Anzeigename** | Frei wählbarer Name, unter dem diese Instanz auf der Status-Seite erscheint (z. B. `Martin-PC`). |
| **Relay-Token** | Shared Secret, mit dem sich die Instanz am Server authentifiziert. Der Token wird in der Admin-UI des Status-Servers (`/admin/tokens`) erzeugt und dort **genau einmal** im Klartext angezeigt. Er wird beim Speichern im Credential Store abgelegt und **nie wieder angezeigt** — das Feld bleibt beim erneuten Öffnen leer; der Hinweis „Token hinterlegt" zeigt an, dass bereits eines gespeichert ist. Ein neuer Eintrag überschreibt den bisherigen Token. |
| **Instanz-ID** | Eindeutige, unveränderliche Kennung dieser Installation. Sie wird beim ersten Speichern automatisch erzeugt und ist schreibgeschützt. |
| **Verbindungsstatus** | Ehrlicher Live-Status: `deaktiviert`, `Konfiguration unvollständig`, `Verbinde…`, `Verbunden` (inkl. Zeitpunkt des letzten Snapshot-Versands) oder `Fehler` mit Grund. Fehler werden rot dargestellt. |

## Einrichtung

1. Öffnen Sie **Einstellungen → Automatisierung** und klappen Sie **„Remote-Übersicht"** auf.
2. Tragen Sie **Server-URL** und optional einen **Anzeigenamen** ein.
3. Hinterlegen Sie das **Relay-Token**, das der Server-Betreiber für diese Instanz
   in der Admin-UI des Status-Servers (`/admin/tokens`) erzeugt hat — Details zur
   Token-Erzeugung und -Verwaltung auf der Webseite beschreibt die Hilfe zur
   [Status-Webseite](../status-webseite/index.md), der Betrieb des Servers
   `server/README.md`.
4. Aktivieren Sie den **Funktionsschalter** und klicken Sie **Speichern**.
5. Der Verbindungsstatus meldet wenige Sekunden später „Verbunden"; auf der
   Status-Seite erscheint die Instanz mit ihrem Anzeigenamen.

## Lokale Prüfung

Zum Ausprobieren genügt ein lokal gestarteter Server:

```bash
cd server/Softwareschmiede.StatusServer
dotnet run   # lauscht auf http://localhost:5055
```

Als Server-URL `http://localhost:5055` eintragen. Beim ersten Aufruf der Seite landen
Sie im Setup (`/register`): Legen Sie dort das Administratorkonto an und erzeugen Sie
anschließend unter `/admin/tokens` ein Relay-Token für die App. Die Seite ist danach
unter `http://localhost:5055` abrufbar, die maschinelle Sicht unter
`http://localhost:5055/status.json`.

> **Hinweis:** Bei aktivem Schalter sendet die App Snapshots (Projekttitel,
> Issue-/PR-Verweise, SCM-Ereignisse) an den konfigurierten Server. Richten Sie den
> Endpunkt daher auf einen Server Ihres Vertrauens — die Status-Seite ist für jeden
> zugänglich, der sich am Server mit einem Benutzerkonto (Stufe `Administration`
> oder `Ueberwacher`) anmelden kann.
