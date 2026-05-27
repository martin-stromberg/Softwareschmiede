# F026 – KI-Plugin-spezifische Agenten-Discovery und -Auswahl

## Einleitung

Diese Funktion sorgt für eine klare und sichere Auswahl vor dem Aufgabenstart.
Sie wählen zuerst das **KI-Plugin**.
Danach sehen Sie nur passende Agentenpakete und Agenten.
Das **KI-Plugin** ist Pflicht.
Agentenpaket und Agent bleiben optional.

---

## Wer nutzt es?

Diese Funktion nutzen Fachanwender auf der Aufgabenseite.
Sie hilft beim schnellen Start ohne unnötige Pflichtfelder.
Stakeholder nutzen sie, um einheitliche Regeln für den Start festzulegen.

---

## Schritt-für-Schritt-Anleitung

1. Sie öffnen eine Aufgabe.
2. Sie wählen ein **KI-Plugin**.
3. Sie prüfen bei Bedarf das Feld **Agentenpaket** und wählen optional ein Paket.
4. Sie prüfen bei Bedarf das Feld **Agent** und wählen optional einen Agenten.
5. Sie klicken auf **KI starten** oder senden eine Folge-Anweisung.

Wichtig:
- Ohne **KI-Plugin** bleibt **KI starten** deaktiviert.
- Sie dürfen **Agentenpaket** und **Agent** leer lassen.
- Bei einem Pluginwechsel setzt die Oberfläche Paket und Agent zurück.

---

## Beispiel

Sie möchten eine kleine Textanpassung umsetzen.
Sie wählen zuerst **KI-Plugin: Copilot**.
Die Felder **Agentenpaket** und **Agent** lassen Sie leer.
Sie klicken auf **KI starten**.
Die Aufgabe startet sofort, weil das Pflichtfeld erfüllt ist.

---

## Was passiert im Hintergrund?

Die Anwendung merkt sich Ihr gewähltes KI-Plugin pro Aufgabe.
So bleibt die Auswahl beim nächsten Öffnen erhalten.
Passende Pakete und Agenten werden auf dieses Plugin abgestimmt angezeigt.
Beim Start gelten dieselben Regeln wie bei Folge-Anweisungen.

---

## Häufige Fragen (FAQ)

**Muss ich immer ein Agentenpaket wählen?**
Nein. Das Feld ist optional.

**Muss ich immer einen Agenten wählen?**
Nein. Auch dieses Feld ist optional.

**Was ist beim Start zwingend?**
Die Auswahl eines **KI-Plugins**.

**Warum werden manche Pakete oder Agenten nicht angezeigt?**
Die Liste zeigt nur Einträge, die zum gewählten KI-Plugin passen.

---

## Verwandte Funktionen

- [F003 – KI-Entwicklungsprozess](./F003-ki-entwicklungsprozess.md) – Aufgabe starten und Verlauf verfolgen
- [F004 – Agentenpakete](./F004-agentenpakete.md) – Pakete anlegen und optional nutzen
- [F011 – Agent-Auswahl bei Folgeanweisungen](./F011-agent-auswahl-bei-folgeanweisungen.md) – Agent bei Folge-Anweisungen wählen
- [F014 – Standardplugin je Pluginart & KI-Plugin-Auswahl](./F014-standardplugin-ki-plugin-auswahl.md) – Standardwerte und Plugin-Auswahl verstehen
- [F028 – Startvalidierung beim Aufgabenstart](./F028-startvalidierung-aufgabenstart.md) – Pflicht- und optionale Felder im Überblick
- [Zurück zur Übersicht](../features.md)
