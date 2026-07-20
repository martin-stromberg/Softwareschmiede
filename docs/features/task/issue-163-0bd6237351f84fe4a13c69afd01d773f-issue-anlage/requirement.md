# Übersetzte Anforderung: Issue-Anlage aus der Aufgabendetailansicht

## Ziel

Anwender sollen aus der Aufgabendetailansicht heraus ein neues Issue im zum Repository gehörenden Issue-System anlegen und dieses automatisch der aktuellen Aufgabe zuweisen können, sofern der Aufgabe noch kein Issue zugeordnet ist.

## Nutzerrolle

- Anwender mit Zugriff auf die Aufgabendetailansicht und ausreichenden Berechtigungen zum Anlegen von Issues im Repository.

## Funktionale Anforderungen

### FA-1: Aktion in der Aufgabendetailansicht

- In der Aufgabendetailansicht wird ein neuer Ribbon-Aktionsbutton zum Anlegen eines Issues angeboten.
- Die Aktion ist nur verfügbar, wenn der Aufgabe noch kein Issue zugeordnet ist.
- Ist bereits ein Issue zugeordnet, wird kein weiteres Issue über diese Aktion angelegt.

### FA-2: Dialog zur Issue-Anlage

- Beim Auslösen der Aktion öffnet sich ein Dialog zur Vorbereitung und Übermittlung des neuen Issues.
- Die Anforderungsbeschreibung der Aufgabe wird beim Öffnen als initiale Issue-Beschreibung übernommen.
- Der Anwender kann die Issue-Beschreibung im Dialog vor dem Absenden vollständig bearbeiten.
- Der Dialog bietet eine Abbruchmöglichkeit, ohne ein Issue anzulegen oder die Aufgaben-Zuordnung zu verändern.

### FA-3: Auswahl eines Issue-Templates

- Der Dialog zeigt verfügbare Issue-Templates des Repository-Providers an, sofern dieser Templates bereitstellt und sie abgerufen werden können.
- Der Anwender kann ein Template auswählen.
- Wenn kein Template verfügbar ist, kann die Issue-Anlage ohne Template fortgesetzt werden.
- Bei Auswahl eines Templates wird dessen Inhalt als Grundlage der Issue-Beschreibung übernommen.
- Die ursprüngliche Anforderungsbeschreibung der Aufgabe wird unterhalb des Template-Inhalts ergänzt.
- Template-Inhalt und Originalanforderung werden durch eine Trennlinie getrennt.
- Der Abschnitt mit der ursprünglichen Anforderung trägt den Titel `Originalanforderung:`.
- Der durch Template und Originalanforderung erzeugte Inhalt kann vor dem Absenden weiter bearbeitet werden.

### FA-4: KI-Unterstützung für Templates

- Der Dialog bietet eine KI-Aktion an, mit der ein ausgewähltes Template anhand der Originalanforderung der Aufgabe ausgefüllt werden kann.
- Die KI erhält mindestens den Inhalt des ausgewählten Templates und die Originalanforderung als Eingaben.
- Das KI-Ergebnis wird in die bearbeitbare Issue-Beschreibung übernommen.
- Der Anwender kann das KI-Ergebnis vor dem Absenden prüfen und ändern.
- Die Issue-Anlage bleibt auch ohne Nutzung der KI möglich.

### FA-5: Provider-Unterstützung

- Die Issue-Anlage nutzt den für das Repository konfigurierten Provider.
- Für GitHub soll die Verfügbarkeit und Auswahl von Issue-Templates unterstützt werden.
- Für Bitbucket und Jira ist zu verifizieren, ob und über welche Schnittstellen Issue-Templates verfügbar und nutzbar sind.
- Nicht unterstützte oder nicht verfügbare Template-Funktionen dürfen die Issue-Anlage ohne Template nicht verhindern.

### FA-6: Issue erstellen und Aufgabe zuweisen

- Beim Absenden wird mit den im Dialog erfassten Daten ein neues Issue im Repository erstellt.
- Nach erfolgreicher Erstellung wird die Issue-Referenz der aktuellen Aufgabe zugewiesen.
- Die Zuordnung wird erst nach erfolgreicher Issue-Erstellung gespeichert.
- Bei einem Fehler bei der Erstellung oder Zuordnung wird kein erfolgreiches Ergebnis signalisiert; der Anwender erhält eine verständliche Fehlermeldung und kann den Dialog erneut bearbeiten oder abbrechen.
- Nach erfolgreicher Anlage zeigt die Aufgabendetailansicht die neue Issue-Zuordnung an und bietet die Anlage eines weiteren Issues nicht mehr an.

## Geschäftsregeln

- Pro Aufgabe darf über diese Funktion höchstens ein Issue angelegt und zugeordnet werden.
- Die Originalanforderung ist bei Template-Verwendung zusätzlich zum Template-Inhalt einzufügen und darf dadurch nicht verloren gehen.
- Eine leere oder nicht vorhandene Anforderungsbeschreibung darf die Dialogöffnung nicht verhindern; in diesem Fall wird keine inhaltliche Originalanforderung ergänzt.
- Eine fehlende Berechtigung, eine fehlende Provider-Verbindung oder ein Provider-Fehler darf nicht zu einer falschen Issue-Zuordnung führen.

## Akzeptanzkriterien

1. Wenn einer Aufgabe kein Issue zugeordnet ist, kann der Anwender in der Aufgabendetailansicht den neuen Ribbon-Button auslösen und den Issue-Dialog öffnen.
2. Beim Öffnen des Dialogs ist die Anforderungsbeschreibung der Aufgabe als bearbeitbare Issue-Beschreibung vorausgefüllt.
3. Der Anwender kann die vorausgefüllte Beschreibung ändern und ohne Template ein Issue erstellen.
4. Wenn der Repository-Provider Templates liefert, werden diese im Dialog auswählbar angezeigt.
5. Nach Auswahl eines Templates enthält die Beschreibung den Template-Inhalt, eine Trennlinie und den Abschnitt `Originalanforderung:` mit der ursprünglichen Aufgabenanforderung.
6. Der Anwender kann den zusammengesetzten Inhalt vor dem Absenden ändern.
7. Der Anwender kann ein ausgewähltes Template mit einer KI anhand der Originalanforderung ausfüllen lassen und das Ergebnis vor dem Absenden ändern.
8. Nach erfolgreichem Absenden existiert das Issue im Repository und seine Referenz ist der Aufgabe zugeordnet.
9. Nach erfolgreicher Zuordnung ist die Issue-Anlage für diese Aufgabe nicht erneut verfügbar.
10. Beim Abbrechen oder bei einem Fehler wird kein Issue der Aufgabe zugeordnet.
11. Die Issue-Anlage ohne Template funktioniert auch dann, wenn der Provider keine Templates unterstützt oder keine Templates verfügbar sind.

## Offene Punkte

- Welche Issue-Felder neben der Beschreibung sind erforderlich, insbesondere Issue-Titel, Labels, Projekt, Issue-Typ und Status?
- Wie wird der Initialwert für den Issue-Titel bestimmt, sofern der Provider einen Titel verlangt?
- Welche konkreten Template-Schnittstellen und Formate unterstützen Bitbucket und Jira?
- Welche KI-Funktion und welches bestehende KI-Modell beziehungsweise welcher bestehende KI-Dienst soll für das Ausfüllen verwendet werden?
- Wie wird mit nicht unterstützten Template-Platzhaltern oder Template-Variablen umgegangen?
- Welche Provider-Berechtigungen und Fehlermeldungen müssen im Dialog berücksichtigt werden?

## Nicht Bestandteil dieser Anforderung

- Bearbeiten oder Löschen bereits bestehender Issues.
- Zuordnen eines bereits vorhandenen Issues über diesen neuen Anlage-Dialog.
- Verwaltung oder Erstellung von Issue-Templates im Repository.
