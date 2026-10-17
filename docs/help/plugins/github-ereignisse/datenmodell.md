← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Datenmodell

## Entitäten

### `ScmIncomingEvent`

Enthält den dauerhaft angenommenen Ereignistext und Ursprung, Aufgabe, Status, Wartegrund, Versuchs- und Laufkennung sowie Zustellzeiten und Wiederholungsinformationen.

### `ScmObservationCheckpoint`

Speichert Wasserstand, Fehlerzahl, nächste Prüfzeit und Diagnose je Repository-/Referenz-/Quellenart.

### `ScmContentSnapshot`

Speichert Inhaltshash und Revision einer beobachteten Textfassung sowie den Auflösungsstatus ihrer Identität.

### `ScmOutgoingAction`

Verfolgt Empfangsreaktionen, Wartehinweise und angeforderte Antworten einschließlich Aktionstyp, Remote-ID, Versuchszahl und Fehlerstatus.

### `ScmTaskLease`

Koordiniert die exklusive Verarbeitung von Ereignissen einer Aufgabe über Worker und UI-Aktionen hinweg.

## Beziehungen

Ein Ereignis gehört fachlich zu einer Aufgabe und kann mehrere ausgehende Aktionen enthalten. Checkpoints und Snapshots sind unabhängig nach Quelle beziehungsweise Textversion indiziert. Eine Aufgaben-Lease serialisiert Ereignisse desselben Aufgabenbereichs. Die konkreten Zuordnungen und Fremdschlüssel werden in `SoftwareschmiededDbContext` konfiguriert.
