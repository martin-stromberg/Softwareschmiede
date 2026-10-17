← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Business Rules

## Trigger und Repository-Owner

Ein Text ist nur gültig, wenn er `$Softwareschmiede` ohne Beachtung der Groß-/Kleinschreibung enthält und der authentifizierte GitHub-Nutzer der Repository-Owner ist. Bei unverändertem Inhalt zählt der Autor; bei bearbeitetem Inhalt muss der aktuelle Bearbeiter bestimmbar sein und dem Owner entsprechen. `ScmEventValidationService.GetRejection` erzwingt diese Regeln. Nicht bestimmbarer Bearbeiter und eigene Antwortmarker werden abgewiesen.

## Eindeutige Aufgabenreferenz

Beobachtet werden alle Referenzen aktivierter Projekt-Repositories (`GitRepository.Aktiv`) sowie die an Aufgaben verknüpften Referenzen. Eine Remote-Referenz, die mehreren Aufgaben zugeordnet ist („Mehrdeutige Zuordnung zu Aufgaben"), wird nicht willkürlich zugestellt; der Konflikt bleibt als Diagnose sichtbar. Referenzen ohne zuordenbare Aufgabe erhalten die Diagnose „Keine Aufgabe zugeordnet" und erzeugen keinen zustellbaren Eintrag; die Beobachtung wird weitergeführt, damit eine später entstehende Aufgabenverknüpfung nahtlos greift.

## Aufgabenstatus und CLI

Beendete, archivierte und pausierte Aufgaben werden nicht automatisch aktiviert. Eine lebende unterstützte CLI hat Vorrang vor einem Standard. Ohne Sitzung wird die konfigurierte Aufgaben-, Projekt- oder globale CLI verwendet; fehlt eine geeignete CLI, bleibt das Ereignis wartend. Die manuelle Queue-Auswahl kann eine neue, nicht beendete Aufgabe aktivieren.

## Zustellung und Abschluss

Kommentare erhalten eine Empfangsreaktion, Beschreibungen nicht. Eine erfolgreiche Prompt-Eingabe markiert ausschließlich die Zustellung. „Verarbeitet“ setzt korrelierte KI-Annahme, Abschluss und Erfolg aller angeforderten Remote-Aktionen voraus. Nach einem ungewissen Schreibversuch erfolgt kein automatischer Neuversand; die UI verlangt dafür eine bestätigte Warnung.
