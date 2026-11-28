← [Zurück zur Übersicht](index.md)

# Status-Webseite — Einrichtung

## Zweck

Diese Seite beschreibt aus Anwendersicht, welche sichtbaren Einstellungen bei
der Inbetriebnahme der Status-Webseite zu prüfen und vorzunehmen sind —
Ersteinrichtung, Benutzerkonten, Relay-Tokens und Sitzungsdauer. Die
technische Installation des Servers (Betrieb, Reverse Proxy, Datenablage)
beschreibt [Installation & Konfiguration](installation.md) sowie
`server/README.md`.

## Einstellungen

| Einstellung | Bedeutung |
|-------------|-----------|
| **Administratorkonto** (Ersteinrichtung) | Das erste Konto, angelegt über die Seite **Ersteinrichtung** beim allerersten Aufruf. Erhält automatisch die Stufe „Administration (Verwaltung)". |
| **Benutzerkonten** (Seite „Benutzer") | Weitere Konten mit frei wählbarem Benutzernamen, Passwort (mindestens 8 Zeichen) und Berechtigungsstufe — „Überwacher (nur lesen)" oder „Administration (Verwaltung)". |
| **Relay-Tokens** (Seite „Tokens") | Zugangstokens für App-Instanzen, die Snapshots an den Server liefern. Jedes Token trägt eine frei vergebene Bezeichnung; der Klartext wird nur einmal angezeigt. |
| **Sitzungsdauer** (Seite „Einstellungen") | Dauer der Anmeldesession in Minuten (erlaubt 5–43200, voreingestellt 480). Gilt anwendungsweit für alle Benutzer. |
| **Updates** (Seite „Einstellungen") | Steuerung der Server-Aktualisierung aus den GitHub-Releases: Release-Kanal („Stable"/„Pre-Release"), Automatisierung („Aus"/„Nur prüfen"/„Prüfen und installieren"), tägliche Installationsuhrzeit sowie manuelle Aktionen (jetzt prüfen, jetzt installieren, Sperre zurücksetzen). |

## Vorgehen

1. **Ersteinrichtung:** Die Adresse der Status-Webseite aufrufen und auf der
   Seite **Ersteinrichtung** das Administratorkonto anlegen (siehe
   [Ablauf für Anwender](ablauf-anwender.md)).
2. **Benutzer anlegen:** Auf der Seite **Benutzer** für jede Person ein Konto
   mit passender Stufe anlegen — lesende Nutzer erhalten „Überwacher (nur
   lesen)", nur Verwalter „Administration (Verwaltung)".
3. **Relay-Token je App-Instanz anlegen:** Auf der Seite **Tokens** pro
   anzubindender App-Installation ein Token mit sprechender Bezeichnung
   erzeugen und den einmalig angezeigten Klartext in der App unter
   **Einstellungen → Automatisierung → Remote-Übersicht** hinterlegen (siehe
   [Remote-Übersicht](../einstellungen/remote-uebersicht.md)).
4. **Sitzungsdauer prüfen:** Auf der Seite **Einstellungen** die
   voreingestellte Sitzungsdauer bei Bedarf anpassen.
5. **Updates konfigurieren (optional):** In der Sektion **Updates**
   derselben Seite den gewünschten Release-Kanal und die Automatisierung
   einstellen — oder alles auf „Aus" belassen und Updates nur manuell
   auslösen (siehe [Ablauf für Anwender](ablauf-anwender.md), Abschnitt
   „Server-Updates verwalten").

## Hinweise

- Die Registrierung schließt sich nach dem ersten Konto dauerhaft — wer zu
  spät kommt, sieht nur noch die Anmeldung und muss sich von der
  Administration ein Konto anlegen lassen.
- Ohne mindestens ein Relay-Token kann sich keine App-Instanz am Server
  anmelden — die Seite **Tokens** weist in dem Fall darauf hin, dass alle
  Verbindungen abgelehnt werden.
- Token-Rotation läuft über „neu anlegen → in der App umstellen → altes Token
  widerrufen"; ein in der App hinterlegtes, widerrufenes Token meldet sich
  dort als Verbindungsfehler.
- Eine verkürzte Sitzungsdauer wirkt erst bei der nächsten Verlängerung
  bestehender Anmeldungen — für sofortige Wirkung müssen sich die Benutzer
  ab- und neu anmelden.
- Das Farbschema (hell/dunkel) ist keine Verwaltungseinstellung: Jeder
  Benutzer wählt es selbst auf der Seite **Profil** unter **Anzeige →
  Farbschema**; voreingestellt ist „Automatisch (System)".
