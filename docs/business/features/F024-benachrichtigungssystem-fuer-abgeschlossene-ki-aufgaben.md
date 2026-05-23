# F024 – Benachrichtigungssystem für abgeschlossene KI-Aufgaben

## Einleitung

Diese Funktion informiert Sie, wenn eine KI-Aufgabe fertig ist.  
Sie erhalten auf Wunsch eine kurze Meldung und einen Hinweiston.  
So verpassen Sie auch bei mehreren Aufgaben keinen Abschluss.  
Sie legen selbst fest, wann Hinweise erscheinen sollen.  
Die Hinweise gelten pro Benutzer und bleiben nach dem Speichern erhalten.

---

## Wer nutzt es?

Diese Funktion nutzen vor allem Sachbearbeiter mit parallelen KI-Aufgaben.  
Auch Teamleitungen nutzen sie, um Ergebnisse ohne ständige Seitenwechsel zu sehen.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen die Seite **Einstellungen**.
2. Sie gehen zum Bereich **🔔 KI-Aufgaben-Benachrichtigungen**.
3. Sie wählen bei **Toast-Modus** einen Modus: **Deaktiviert**, **Nur auf Aufgabenseite** oder **Global**.
4. Sie wählen bei **Hinweiston-Modus** ebenfalls einen passenden Modus.
5. Optional laden Sie bei **Benutzerdefinierter Hinweiston (.mp3, .wav, .ogg, max. 10 MB)** eine eigene Datei hoch.
6. Sie speichern mit **💾 Benachrichtigungen speichern**.
7. Optional prüfen Sie den Ton mit **🔊 Testton**.

---

## Beispiel

Sie bearbeiten Angebote und lassen zwei KI-Aufgaben laufen.  
Sie arbeiten in einer anderen Ansicht weiter.  
Eine Aufgabe endet mit Fehler und Sie erhalten sofort einen Hinweis.  
Der Toast zeigt den Status, und ein Ton macht Sie zusätzlich aufmerksam.

---

## Was passiert im Hintergrund?

Nach jedem KI-Lauf wird genau ein Abschlussereignis erzeugt.  
Das System entscheidet je Kanal, ob ein Hinweis gesendet oder unterdrückt wird.  
Die Entscheidung folgt Ihrer Moduswahl: **Deaktiviert**, **Nur auf Aufgabenseite** oder **Global**.  
Jede Entscheidung wird auditierbar protokolliert.  
Doppelte Ereignisse werden je Kanal dedupliziert, damit kein Hinweis doppelt erscheint.

Wenn der Browser Ton sofort blockiert, wird der Ton zurückgestellt.  
Nach Ihrer nächsten Interaktion versucht die Anwendung die Wiedergabe erneut.

---

## Häufige Fragen (FAQ)

**Welche Modi gibt es für Toast und Hinweiston?**  
Es gibt **Deaktiviert**, **Nur auf Aufgabenseite** und **Global**.

**Welche Audio-Dateien darf ich hochladen?**  
Erlaubt sind mp3, wav und ogg bis maximal 10 MB.

**Was passiert, wenn mein Browser den Ton nicht automatisch startet?**  
Die Anwendung meldet die Verzögerung und versucht den Ton nach Ihrer nächsten Interaktion erneut.

**Wird jede Benachrichtigungsentscheidung nachvollziehbar gespeichert?**  
Ja. Gesendet, unterdrückt, zurückgestellt oder fehlgeschlagen wird je Ereignis und Kanal protokolliert.

**Kann ich doppelte Hinweise bei demselben Abschluss bekommen?**  
Nein. Das System dedupliziert pro Ereignis und pro Kanal.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md)
- [F005 – Aufgabenprotokoll](./F005-aufgabenprotokoll.md)
- [F015 – Einstellungen & Persistenz](./F015-einstellungen-und-persistenz.md)
- [F016 – Fehlerbehandlung & Recovery](./F016-fehlerbehandlung-und-recovery.md)
- [Zurück zur Übersicht](../features.md)
