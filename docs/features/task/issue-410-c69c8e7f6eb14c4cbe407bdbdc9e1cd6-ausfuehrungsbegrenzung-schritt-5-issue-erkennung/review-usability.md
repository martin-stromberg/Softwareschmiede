# Usability-Review: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5)

**Status: Keine Befunde**

## UI-relevante Änderungen

Einzige UI-Berührung: `ScmEventQueueItem.From` (`src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs` Z. 402-403) — die Kind-Spalte der SCM-Ereignisübersicht zeigt für interne Aufbereitungsaufträge „Issue-Aufbereitung" statt „Beschreibung". Keine XAML-Änderung, kein neuer Benutzerfluss, keine neue Einstellungs-UI (bewusst Schritt 6 vorbehalten).

## Prüfung gegen die UI-Aspekte

- **Sichtbarkeit:** Der interne Auftrag erscheint regulär in der Ereignisübersicht (Anforderung „sichtbar"); Status „Aufgenommen" bzw. nach Prüfung „Wartend" mit ehrlichem Grund („Interner Aufbereitungsauftrag für Issue #n." / „Die Ausführung der Issue-Aufbereitung ist noch nicht verfügbar.") — konsistent zur Terminologie der Übersicht.
- **Bedienbarkeit:** Abbrechen bleibt möglich (`CanCancel` — korrekt: interne Aufträge sollen verwaltbar sein). „Erneut prüfen"/Queue-Auswahl führt über den Orchestrator-Guard erneut in den ehrlichen Wartezustand statt in eine irreführende Zustellung — kein falscher Handlungsvorschlag. `CanConfirmReady`/`CanResend` greifen nicht (kein `CliRunId`/`AttemptId`, kein bestätigbarer Wartegrund).
- **Konsistenz:** Zweck-Markierung und Bezeichnung folgen den deutschen Fachbegriffen der Oberfläche; die Aufbereitungs-Aufgabe erscheint als normale Aufgabe in der Aufgabenliste — für Schritt 6 gewollt (sie ist der spätere Ausführungsträger); ein manueller Start ist nicht gesperrt, führt aber aktuell nur in den regulären Entwicklungsfluss — als Restrisiko dokumentiert, nicht als Befund.
- **Keine Einstellungs-UI:** Die drei neuen Schlüssel sind nur persistiert — Absicht laut Anforderung (UI folgt in Schritt 6).

## Befunde

Keine.
