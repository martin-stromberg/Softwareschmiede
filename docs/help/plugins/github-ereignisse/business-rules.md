← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Business Rules

## Überwachung ein- und ausschalten

Die SCM-Ereignisüberwachung lässt sich unter **Einstellungen → Allgemein → SCM-Ereignisse** global deaktivieren (Einstellung `scm.monitoring.enabled`, Standard aktiviert; wirkt ohne Neustart). Im deaktivierten Zustand werden keine Quellen beobachtet oder enumeriert — also keine GitHub-Abfragen und keine `gh`-Aufrufe — und keine Ereignisse automatisch zugestellt oder abgeglichen (keine CLI-Starts, keine Prompts, keine GitHub-Kommentare/-Reaktionen aus den Hintergrunddiensten `ScmEventMonitoringService` und `ScmEventExchangeService`). Bereits in der Inbox vorhandene Ereignisse behalten ihren Status; Checkpoints und Deduplikation bleiben unverändert, sodass nach der Reaktivierung alles normal weiterläuft. Explizite Benutzeraktionen in der Ereignisübersicht — Öffnen/CLI auswählen, Erneut prüfen, Status prüfen, Neu senden, Abbrechen — bleiben auch bei deaktivierter Überwachung funktionsfähig, da sie einen ausdrücklichen Benutzerwunsch darstellen. Lediglich „Quellen erneut prüfen" ist wirkungslos, solange die Überwachung aus ist: Diese Aktion löst ausschließlich eine Remote-Beobachtung aus und ist damit Teil der deaktivierten Überwachung.

## Trigger und Repository-Owner

Ein Text ist nur gültig, wenn er das konfigurierte Trigger-Keyword (Standard `$Softwareschmiede`, einstellbar unter **Einstellungen → Allgemein → SCM-Ereignisse**; Einstellung `scm.trigger.keyword`) ohne Beachtung der Groß-/Kleinschreibung enthält und der authentifizierte GitHub-Nutzer der Repository-Owner ist. Das Keyword darf nicht leer sein; ein leerer oder nur aus Leerraum bestehender gespeicherter Wert gilt nicht und es gilt der Standard. Bei unverändertem Inhalt zählt der Autor; bei bearbeitetem Inhalt muss der aktuelle Bearbeiter bestimmbar sein und dem Owner entsprechen. `ScmEventValidationService.GetRejection` erzwingt diese Regeln. Nicht bestimmbarer Bearbeiter und eigene Antwortmarker (`<!-- softwareschmiede:` — ein vom Trigger-Keyword unabhängiges Konzept) werden abgewiesen. Eine spätere Keyword-Änderung entwertet bereits angenommene Ereignisse nicht nachträglich: Bei der Zustellung wird nur die Identitätsprüfung wiederholt.

## Eindeutige Aufgabenreferenz

Beobachtet werden alle Referenzen aktivierter Projekt-Repositories (`GitRepository.Aktiv`) sowie die an Aufgaben verknüpften Referenzen. Eine Remote-Referenz, die mehreren Aufgaben zugeordnet ist („Mehrdeutige Zuordnung zu Aufgaben"), wird nicht willkürlich zugestellt; der Konflikt bleibt als Diagnose sichtbar.

Referenzen ohne zuordenbare Aufgabe durchlaufen bei einem qualifizierten Ereignis die Aufgabenauflösung (`ScmEventTaskResolver`): Gehört die Referenz zu genau einer Aufgabe, wird diese wiederverwendet; existiert keine, wird sie aus den Ereignisdaten angelegt — Issue-Anlagen über `IssueNummer` plus kanonischem `IssueReferenz.SourceKey`-Dedupschlüssel, Pull-Request-Anlagen über den Referenzschlüssel `Provider` + `RepositoryId` + `PullRequestNumber` mit dem bestehenden Unique-Index. Wiederholte Polls, erneut eintreffende Ereignisse und Anlagerennen erzeugen keine Duplikate. Referenzen, zu denen sich keine Aufgabe anlegen lässt (z. B. remote verschwunden), erhalten weiterhin die Diagnose „Keine Aufgabe zugeordnet". Da repository-weit jede enumerierte Referenz ohne lokale Aufgabe diesen erwarteten Zustand erreicht, gilt die Diagnose als normal: Sie löst weder den Unterbrechungshinweis in der Ereignisübersicht aus noch erscheint sie in den Integrationshinweisen — anders als echte Beobachtungsfehler (Anmeldung, Rechte, Netzwerk) und die mehrdeutige Zuordnung.

## Aufgabenstatus und CLI

Beendete, archivierte und pausierte Aufgaben werden nicht automatisch aktiviert — das gilt auch für wiederverwendete Aufgaben im automatischen Pfad; ihr Ereignis bleibt mit Grund wartend. Neue, nicht blockierte Aufgaben werden pro Zustellversuch automatisch gestartet (`Neu` → `Gestartet`; Klon, Branch und CLI-Start über `EntwicklungsprozessService`). Eine lebende unterstützte CLI hat Vorrang vor einem Standard. Ohne Sitzung wird die konfigurierte Aufgaben-, Projekt- oder globale CLI verwendet; fehlt eine geeignete CLI, bleibt das Ereignis wartend. Die manuelle Queue-Auswahl kann eine neue, nicht beendete Aufgabe aktivieren.

## Anlage bei fehlender Standard-CLI

Die automatische Aufgabenanlage erfolgt **auch dann, wenn keine Standard-CLI auflösbar ist** (fachliche Entscheidung): Die Aufgabe ist sofort in der Aufgabenverwaltung sichtbar, das Ereignis bleibt mit der Aufforderung „Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen." wartend und wird nach CLI-Auswahl oder -Konfiguration (plus „Erneut prüfen" oder dem nächsten Poll) zugestellt. `SourceKey`-/Referenz-Dedup verhindert Duplikatanlagen über die Wartezeit hinweg; Ereignisse gehen nicht verloren.

## Zustellung und Abschluss

Kommentare erhalten eine Empfangsreaktion, Beschreibungen nicht. Eine erfolgreiche Prompt-Eingabe markiert ausschließlich die Zustellung. „Verarbeitet“ setzt korrelierte KI-Annahme, Abschluss und Erfolg aller angeforderten Remote-Aktionen voraus. Nach einem ungewissen Schreibversuch erfolgt kein automatischer Neuversand; die UI verlangt dafür eine bestätigte Warnung.

## Abbruch durch den Anwender

Der Anwender kann jedes nicht abgeschlossene Ereignis in der SCM-Ereignisübersicht nach kurzer Bestätigung endgültig **abbrechen** (Endstatus `Abgebrochen`, gesetzt über `ScmEventOrchestrator.CancelAsync`). „Verarbeitet“ und „Abgebrochen“ sind die einzigen Endstatus; ein abgebrochenes Ereignis wird nie erneut eingelesen, zugestellt, geprüft oder abgeglichen — auch fällige Hostaktionen (Empfangsreaktion, Wartehinweis, KI-Antworten) werden nicht mehr remote ausgeführt. Ein abgebrochenes früheres Ereignis blockiert die Warteschlange seiner Aufgabe nicht weiter. Der Abbruch beendet ausschließlich das Ereignis-Tracking: Eine bereits zugestellte, laufende CLI-Verarbeitung läuft regulär weiter und wird nicht abgebrochen. Tritt der Abbruch während eines laufenden Zustell- oder Abgleichversuchs ein, gewinnt der persistierte Endstatus an den Prüfpunkten des Dispatchers; ein in diesem Moment bereits gesendeter Prompt bleibt zugestellt, ohne den Endstatus zu überschreiben. Der Eintrag bleibt mit Endstatus sichtbar (Filter „Abgebrochen“/„Alle“) und zählt nicht mehr zur Anzahl wartender Ereignisse.
