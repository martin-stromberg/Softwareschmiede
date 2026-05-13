# F018 – Automatisches Herunterfahren

## Einleitung

Diese Funktion hilft bei langen Läufen ohne Aufsicht.  
Sie können festlegen, dass Ihr Rechner nach dem letzten laufenden Vorgang herunterfährt.  
So sparen Sie Energie, wenn die Arbeit am Abend weiterläuft.  
Die Option ist bewusst deutlich gekennzeichnet, damit keine Überraschung entsteht.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender mit längeren KI-Läufen.  
Sie ist besonders hilfreich bei nächtlichen oder unbeaufsichtigten Durchläufen.

---

## Schritt-für-Schritt-Anleitung

1. Sie starten mindestens eine Automatisierung.
2. Sie prüfen in der Seitenleiste den Bereich **Laufende Automatisierungen**.
3. Sie aktivieren **Nach letztem Lauf System automatisch herunterfahren**.
4. Sie lassen die laufenden Aufgaben fertig arbeiten.
5. Nach dem letzten abgeschlossenen Lauf fährt das System automatisch herunter.

---

## Beispiel

Sie starten am Abend drei Aufgaben und verlassen den Arbeitsplatz.  
Vorher aktivieren Sie die automatische Abschaltung in der Seitenleiste.  
Sobald alle Läufe fertig sind, fährt Ihr Rechner selbstständig herunter.

---

## Was passiert im Hintergrund?

Die Anwendung beobachtet laufend die Anzahl aktiver Läufe.  
Wenn die Zahl von größer null auf null fällt, löst sie genau einmal die Abschaltung aus.  
Startet später ein neuer Lauf, beginnt die Überwachung erneut.

---

## Häufige Fragen (FAQ)

**Warum sehe ich die Option nicht immer?**  
Sie erscheint nur, wenn mindestens ein Lauf aktiv ist.

**Bleibt die Einstellung dauerhaft aktiv?**  
Nein. Sie gilt nur für den aktuellen Betrieb.

**Kann ich die Option wieder ausschalten?**  
Ja. Entfernen Sie einfach den Haken.

**Wird sofort heruntergefahren, wenn ich den Haken setze?**  
Nein. Erst wenn keine Läufe mehr aktiv sind.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F008 – Dashboard](./F008-dashboard.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [Zurück zur Übersicht](../features.md)
