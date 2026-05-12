# F016 – Fehlerbehandlung & Recovery

## Einleitung

Diese Querschnittsfunktion beschreibt, wie Nutzer bei typischen Störungen sicher weiterarbeiten.
Ziel ist ein robuster Ablauf: Probleme früh erkennen, verständlich anzeigen und mit klaren Schritten beheben.

---

## Wer nutzt es?

Alle Fachanwender im Tagesbetrieb.
Teamleitungen und Stakeholder nutzen diese Sicht, um Ausfallrisiken und Wiederanlaufzeiten zu bewerten.

---

## Typische Störungen

- Gespeicherter Pfad ist nicht mehr erreichbar
- Gewähltes Plugin ist nicht verfügbar
- Zugangsdaten fehlen oder sind ungültig
- Externe Dienste antworten verzögert oder mit Fehlern

---

## Recovery im Alltag

1. Die Anwendung zeigt eine verständliche Fehlermeldung mit Handlungshinweis.
2. Sie korrigieren die Ursache in den Einstellungen oder wählen eine Alternative.
3. Sie starten den betroffenen Schritt erneut.
4. Wenn möglich, nutzt die Anwendung automatisch einen Fallback (z. B. anderes verfügbares Plugin).
5. Der Ablauf bleibt im Protokoll nachvollziehbar.

---

## Beispiel

Sie senden einen Prompt, aber das gespeicherte Standard-KI-Plugin ist nicht verfügbar.
Die Anwendung wechselt auf ein verfügbares Fallback-Plugin und zeigt einen Hinweis.
Sie können den Prompt fortsetzen und später in den Einstellungen ein neues Standardplugin festlegen.

---

## Häufige Fragen (FAQ)

**Gehen meine bisherigen Ergebnisse bei einem Fehler verloren?**  
In der Regel nein. Bereits protokollierte Schritte bleiben sichtbar.

**Muss ich nach jeder Störung die Aufgabe neu anlegen?**  
Nein. Meist reicht das Korrigieren der Ursache und ein erneuter Start des Schritts.

**Was passiert, wenn ein Standardwert nicht nutzbar ist?**  
Die Anwendung nutzt, wenn möglich, einen sicheren Fallback und informiert Sie darüber.

**Wo sehe ich, was genau passiert ist?**  
Im Aufgabenprotokoll und in den Hinweisen der betroffenen Ansicht.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Fehler im laufenden KI-Ablauf behandeln
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md) – Verlauf und Fehlersituationen nachvollziehen
- [F007 – Aufgabe abbrechen](./F007-aufgabe-abbrechen.md) – kontrollierter Abbruch bei nicht lösbarer Störung
- [F009 – Arbeitsverzeichnis konfigurieren](./F009-arbeitsverzeichnis-konfigurieren.md) – Pfadfehler erkennen und korrigieren
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Fallback bei Plugin-Auswahl
- [F015 – Einstellungen & Persistenz](./F015-einstellungen-und-persistenz.md) – gespeicherte Werte gezielt anpassen
- [Zurück zur Übersicht](../features.md)
