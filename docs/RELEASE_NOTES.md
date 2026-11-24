# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- In separate-account monitoring mode, assigning the monitored account on an issue or pull request (notification reason `assign`) now triggers a task — the reference description becomes the job content without needing a mention or the trigger keyword, an empty description produces no task, and the assignment is only accepted when made by the repository owner, the separate account itself, or a trusted automation account (`github-actions[bot]` / `app/github-actions`); every assignment — including a re-assignment of an already linked reference — creates a new event that reuses and continues the existing task instead of duplicating it, and the delivered prompt explicitly states that it was triggered by an assignee assignment. The trigger applies only in `SeparateAccount` mode (the user-account mode keeps the trigger keyword as its only trigger), removing an assignment fires nothing, and no new configuration is required.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Im Überwachungsmodus „separates Konto" löst die Zuweisung des überwachten Kontos auf Issues und Pull Requests (Notification-Reason `assign`) jetzt eine Aufgabe aus — die Referenzbeschreibung wird ohne Erwähnung oder Trigger-Keyword zum Auftragsinhalt, eine leere Beschreibung erzeugt keine Aufgabe, und die Zuweisung wird nur akzeptiert, wenn sie vom Repository-Owner, vom separaten Konto selbst oder von einem vertrauenswürdigen Automatisierungskonto (`github-actions[bot]` / `app/github-actions`) stammt; jede Zuweisung — auch eine erneute Zuweisung einer bereits verknüpften Referenz — erzeugt ein neues Ereignis, das die bestehende Aufgabe wiederverwendet und weiterführt statt ein Duplikat anzulegen, und der zugestellte Prompt weist erkennbar darauf hin, dass er durch eine Assignee-Zuweisung ausgelöst wurde. Der Auslöser gilt nur im Modus `SeparateAccount` (der Benutzerkonto-Modus behält das Trigger-Keyword als einzigen Auslöser), das Entfernen einer Zuweisung löst nichts aus, und es ist keine neue Konfiguration erforderlich.
