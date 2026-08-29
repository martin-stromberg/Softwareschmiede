# KI-Funktion in Issue-Anlage mit Devin & Copilot

## Ausgangslage

Beim Anlegen einer neuen Aufgabe kann der Anwender über die Ribbon-Aktion **„Issue anlegen"** ein Issue in GitHub erstellen. Der Beschreibungstext kann dabei mit KI-Plugins aufbereitet werden. Aktuell stehen in der Auswahl der KI-Plugins zur Aufbereitung des Beschreibungstexts nur die Plugins **Codex** und **Claude** zur Verfügung.

## Ziel

In der Auswahl der KI-Plugins zur Aufbereitung des Beschreibungstexts bei der Aktion **„Issue anlegen"** sollen zusätzlich die Plugins **Devin** und **Copilot** auswählbar sein, sodass der Anwender zwischen den folgenden vier Plugins wählen kann:

- Codex
- Claude
- Devin
- Copilot

## Akzeptanzkriterien

1. Das Dialogfenster für „Issue anlegen" zeigt in der KI-Plugin-Auswahl alle vier Plugins an.
2. Devin und Copilot können ausgewählt werden, um den Beschreibungstext aufzubereiten.
3. Die bestehende Funktionalität für Codex und Claude bleibt unverändert.
4. Die Auswahl wird korrekt an den Aufruf für die Beschreibungsaufbereitung übergeben.

## Nicht-Ziele

- Keine inhaltliche Veränderung der Aufbereitungslogik der einzelnen Plugins.
- Keine Änderung an der GitHub-Issue-Erstellung selbst.

## Offene Punkte

Keine.
