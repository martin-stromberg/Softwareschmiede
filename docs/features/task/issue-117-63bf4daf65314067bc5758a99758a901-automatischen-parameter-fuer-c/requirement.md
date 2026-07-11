# Strukturierte Anforderung

## Kontext

Für das Codex-Plugin werden wiederholt Kommandozeilenparameter gesetzt, die nicht vom Anwender angegeben wurden. Diese automatisch festgelegten beziehungsweise halluzinierten Parameter überschreiben oder machen vom Anwender vorgenommene Änderungen rückgängig.

## Problem

Anwenderänderungen an den Parametern des Codex-Plugins bleiben nicht zuverlässig erhalten. Stattdessen wird mindestens ein Parameter automatisch auf einen Wert gesetzt, der nicht aus der Anwenderkonfiguration stammt.

## Ziel

Vom Anwender geänderte Kommandozeilenparameter für das Codex-Plugin müssen dauerhaft erhalten bleiben. Das System darf keine Parameter automatisch hinzufügen, ändern oder zurücksetzen, wenn diese Änderung nicht ausdrücklich durch den Anwender ausgelöst wurde.

## Funktionale Anforderungen

1. Änderungen des Anwenders an den Kommandozeilenparametern des Codex-Plugins müssen gespeichert und beim nächsten Zugriff unverändert wiederverwendet werden.
2. Automatische Logik darf keine anwenderdefinierten Codex-Plugin-Parameter überschreiben.
3. Automatische Logik darf keine Codex-Plugin-Parameter hinzufügen, die nicht aus einer expliziten Anwenderaktion, einer bestehenden gespeicherten Konfiguration oder einer dokumentierten technischen Vorgabe stammen.
4. Bereits vorhandene automatisch gesetzte, nicht anwenderdefinierte Parameter müssen entfernt oder so behandelt werden, dass sie Anwenderänderungen nicht erneut rückgängig machen.
5. Falls Standardparameter benötigt werden, dürfen sie nur verwendet werden, solange der Anwender keine eigenen Parameter gesetzt hat.

## Nicht-Funktionale Anforderungen

1. Das Verhalten muss deterministisch sein: gleiche gespeicherte Anwenderparameter führen zu gleichen ausgeführten Codex-Plugin-Parametern.
2. Die Korrektur darf keine anderen Plugin-Konfigurationen unbeabsichtigt verändern.
3. Die Persistenz der Anwenderparameter muss nachvollziehbar und testbar sein.

## Akzeptanzkriterien

1. Wenn ein Anwender die Codex-Plugin-Parameter ändert, bleiben diese Werte nach Speichern, Neuladen und erneutem Öffnen der Konfiguration unverändert erhalten.
2. Wenn ein Anwender einen zuvor automatisch gesetzten Parameter entfernt, wird dieser Parameter nicht automatisch wieder hinzugefügt.
3. Wenn ein Anwender einen Codex-Plugin-Parameter auf einen eigenen Wert setzt, wird dieser Wert nicht durch einen automatisch bestimmten Wert ersetzt.
4. Wenn keine Anwenderparameter existieren, verhält sich das System weiterhin gemäß der dokumentierten Standardlogik.
5. Ein Regressionstest weist nach, dass anwenderdefinierte Codex-Plugin-Parameter nicht durch automatische Parameterfestlegung überschrieben werden.

## Abgrenzung

Diese Anforderung betrifft ausschließlich die Behandlung von Kommandozeilenparametern für das Codex-Plugin. Sie fordert keine allgemeine Änderung am Verhalten anderer Plugins oder an nicht parameterbezogenen Plugin-Einstellungen.

## Offene Punkte

Keine.
