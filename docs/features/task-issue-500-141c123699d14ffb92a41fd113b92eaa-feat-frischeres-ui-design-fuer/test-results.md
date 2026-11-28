# Testergebnisse — Design-Entwurf Status-Webseite (Issue #500)

Datum: 2026-11-28

## Kontext

Dieser Lauf ändert keinen Produktivcode — Ergebnis sind statische Design-Artefakte
(HTML/CSS-Mockups) plus 16 Composite-Screenshots unter
`docs/design/status-webseite/screenshots/`. Der Funktionsnachweis ist daher die
gerenderte, einzeln sichtgeprüfte Screenshot-Menge (kein Unit-/E2E-Testlauf
erforderlich; Baseline der StatusServer-Tests unverändert grün, siehe
`inventory/tests.md`).

## Automatisierte Tests

Keine ausgeführt — kein Produktivcode geändert (Scope-Ausschluss 1, bestätigt).
Baseline aus der Bestandsaufnahme: 165 StatusServer-Tests + 2880 reguläre Tests +
437 Integrationstests, 0 Fehlschläge.

## Reale End-to-End-Abnahme

Nicht anwendbar im klassischen Sinn (kein ausführbares Feature). Äquivalente
Abnahme: Die Mockups wurden mit Headless-Chrome real gerendert und jedes PNG
einzeln sichtgeprüft (siehe nächster Abschnitt) — das ist der für diesen
Design-Lauf zugesagte Nachweis gegenüber dem Anwender.

## Sichtprüfung der Abnahme-Screenshots

Alle 16 PNGs wurden einzeln geöffnet und auf Layout-Defekte geprüft
(Orchestrator-Sichtprüfung, zusätzlich zur Eigenkontrolle des
Implementierungsagenten):

| PNG | Inhalt geprüft | Ergebnis |
|-----|----------------|----------|
| `status.png` | Befüllte Übersicht: Topbar+Brand, Gesamtstatus-Badge „teilweise veraltet", Instanz-Namen, Meta-Zeile mit `/status.json`-Link, 3 Sektionen je Instanz (Zähler-Chips, Quellen-Badges, Pills, Links), stale-note + `dim`-Sektionen der stale Instanz, 2 GitHub-Sektionen inkl. Abruffehler-Hinweis | Bestanden — beide Schemata vollständig, keine Abschnitte |
| `status-leer.png` | Badge „keine Instanz", Anleitungs-Hinweisblock (Aktivierung der Remote-Übersicht), leere App-Instanzen-Sektion, GitHub-Sektion weiterhin befüllt | Bestanden |
| `login.png` | `.msg err`-Sperr-Box mit Restdauer-Text, Benutzername/Passwort-Felder, Anmelden-Button, kein Registrierungs-Link (Anwender-Entscheidung) | Bestanden (nach Review-Fix erneut geprüft: einzelne h1) |
| `register.png` | Ersteinrichtungs-Erklärtext, Zeichensatz-Hinweis, Passwort-Regel, Button „Administratorkonto anlegen" | Bestanden |
| `profil.png` | Konto-Info mit Stufe, `.msg`-Erfolgsbox, Farbschema-Auswahl (Automatisch (System)/Light/Dark), Passwort-ändern-Formular | Bestanden |
| `admin-benutzer.png` | Tabelle 3 Benutzer: letzter-Admin-Pill, deaktivierte Schutz-Aktionen **mit sichtbarer Begründung** „Letztes Administratorkonto — Aktionen gesperrt.", gesperrter Benutzer (Datum UTC) mit aktivem „Sperrung zurücksetzen", deaktivierter Benutzer, „—" statt nicht auslösbarer Aktionen, Anlegen-Formular mit Stufen-Auswahl | Bestanden (nach Review-Fix erneut geprüft) |
| `admin-benutzer-loeschen.png` | Bestätigungszwischenseite: unwiderruflich-Warnung, Danger-Button „Endgültig löschen", Abbrechen-Link | Bestanden |
| `admin-tokens.png` | `.token-once`-Klartextblock mit einmaligem Token (43 Zeichen), Tabelle gültig + widerrufen („—" statt nicht auslösbarem Widerrufen-Button beim widerrufenen), Anlegen-Formular | Bestanden (nach Review-Fix erneut geprüft) |
| `admin-tokens-leer.png` | Leer-Zustand mit `/relay`-Ablehnungshinweis, Anlegen-Formular | Bestanden |
| `admin-token-widerrufen.png` | Bestätigungszwischenseite: Zugangsverlust-Warnung, Danger-Button, Abbrechen-Link | Bestanden |
| `admin-einstellungen.png` | Sitzungsdauer, Update-Statustabelle (Version, Zustand, Fehler-Pill, verwaiste Installationssperre + Reset-Formular, Protokollpfad), Kanal/Automatisierungs-Formular, Aktionen mit Downtime-Checkbox + Danger-Button | Bestanden |
| `admin-update-installation.png` | Statische Ergebnisseite: Neustart-Hinweis, Update-Protokoll-Verweis | Bestanden |
| `zugriff-verweigert.png` | Fehlerseite mit Erklärung + Link zur Status-Übersicht; Überwacher-Nav ohne Admin-Links/Rollenzusatz | Bestanden |
| `formular-abgelaufen.png` | CSRF-/Antiforgery-Erklärung, Neuladen-Hinweis, Link zur Status-Übersicht | Bestanden |
| `status-mobil.png` | 920-px-Viewport: Tabellen brechen zu gestapelten Karten mit Feld-Labels um, kein horizontaler Scroll, Nav umbrochen | Bestanden |
| `admin-benutzer-mobil.png` | Benutzertabelle als Label/Wert-Stapel, Aktions-Buttons umbrochen, Formular voll sichtbar | Bestanden |

**Ergebnis der Sichtprüfung:** Alle 16 Bilder einzeln geprüft, keine
Layout-Defekte (keine abgeschnittenen Inhalte, keine Iframe-Artefakte, korrekte
Light/Dark-Zuordnung je Hälfte, lesbare Kontraste in beiden Schemata, kein
Textüberlauf). Vom Implementierungsagenten in der Eigenkontrolle behobene
Defekte: Wortumbruch mitten in „Administration" (`overflow-wrap`), übermäßiger
Leerraum unter Seiten (Iframe-Höhen je Seite abgestimmt), fehlender Abstand
Formular→`h2` im Profil, Hex-Literal in generierten Composites.

## Fehlgeschlagene Tests

Keine.

## Gesamtstatus

Keine Fehler — Screenshot-Nachweis vollständig, Sichtprüfung dokumentiert.

## Iteration 3 — Stitch-Entwurf (Nachforderung via Issue-Kommentar)

Nach dem Anwender-Kommentar vom 2025-… (Datenverständlichkeit, expliziter Wunsch nach
Google Stitch) wurde ein zweiter, vom bisherigen Layout unabhängiger Entwurf komplett
mit Google Stitch generiert (Projekt `projects/7540435568831394111`, Design-System
"Precision Operations" `assets/25e6f8dbb3604bef8ac4be3fa1415efc`).

| Nachweis | Ergebnis |
|---|---|
| 14 Light-Screens (Stitch PNG) | einzeln sichtgeprüft — konsistente Navigation, deutsche Texte, gute Lesbarkeit |
| 14 Dark-Varianten (`generate_variants`, COLOR_SCHEME) | einzeln sichtgeprüft — konsistentes Dark-Theme, keine Layoutdefekte |
| 14 Composite-PNGs (Light\|Dark) | lokal erzeugt; `status.composite.png` stichprobenartig verifiziert |
| Generierte HTML-Exporte | 13× vorhanden (`status.html` fehlt, nur visueller Referenzwert) |

Artefakte: `docs/design/status-webseite-stitch/` (README mit allen Stitch-IDs).
