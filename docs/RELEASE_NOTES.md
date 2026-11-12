# Release Notes

## Important Notes Before Update

- There are no special notices.

## What's New

- SCM event monitoring now detects requests already picked up by a second installation running in parallel (e.g. a prerelease with its own database): a comment your event account has already reacted to — including the 👀 receipt reaction or any manually added reaction — is treated as already handled and creates no new event. This requires both installations to use the same GitHub account; editing such a comment does not retrigger it either.
- New "Automatisierung" settings tab: all automation settings moved out of "Allgemein" into three collapsible panels ("Aufgaben ausführen", "Aufgaben beenden", "Prozessabläufe") that show their enabled state in the title bar. Fields that have no effect are now hidden or disabled (e.g. Trigger-Keyword, Prompt-Anhänge, Wartezeit).
- Backmerge pull requests are now checked in their own cycle — independent of the SCM event monitoring switch and the Überwachungsmodus, always under the user account, so they are discovered in "Separates Konto" mode as well. Two new opt-in options: draft backmerge PRs can be activated automatically before merging, and a blocked regular merge can optionally be retried as an admin merge.
- New "Prüfmodus" for the automatic task completion: choose between monitoring only event-driven tasks or all tasks. Tasks without a linked issue and without a linked pull request are never ended automatically.
- Fixed: the task tile in the task list (sidebar menu and dashboard) could show "✓ Bereit" while the CLI execution was still running — the tile status now follows the live state of the CLI session and consistently shows "▶ Läuft" / "⏸ Wartet" in sync with the task footer.
- Missing run data for a running CLI process is now restored automatically (self-healing), so the status stays correct after restarts and reused sessions.
- New error message when stopping a task fails: if the CLI process keeps running, the app reports "Der CLI-Prozess konnte nicht beendet werden und läuft weiter. Bitte erneut versuchen." instead of wrongly showing the task as ready.

## Wichtige Hinweise vor dem Update

- Es gibt keine besonderen Hinweise.

## Neuerungen

- Die SCM-Ereignisüberwachung erkennt jetzt Anfragen, die bereits von einer parallel betriebenen zweiten Installation (z. B. einer Vorabversion mit eigener Datenbank) übernommen wurden: Ein Kommentar, auf den das eigene Konto bereits reagiert hat — sei es die 👀-Empfangsreaktion oder eine manuell gesetzte Reaktion —, gilt als bereits bearbeitet und erzeugt kein neues Ereignis. Voraussetzung ist, dass beide Installationen dasselbe GitHub-Konto verwenden; auch ein späteres Bearbeiten eines solchen Kommentars löst kein neues Ereignis aus.
- Neues Einstellungs-Register „Automatisierung": Alle Automatisierungs-Einstellungen sind aus „Allgemein" in drei auf-/zuklappbare Panels umgezogen („Aufgaben ausführen", „Aufgaben beenden", „Prozessabläufe"), die ihren Aktivierungsstatus in der Titelleiste anzeigen. Felder ohne Wirkung werden jetzt ausgeblendet oder deaktiviert (z. B. Trigger-Keyword, Prompt-Anhänge, Wartezeit).
- Backmerge-Pull-Requests werden jetzt in einem eigenen Zyklus geprüft — unabhängig vom Schalter der SCM-Ereignisüberwachung und vom Überwachungsmodus, immer unter dem Benutzerkonto, sodass sie auch im Modus „Separates Konto" entdeckt werden. Zwei neue Optionen: Als Entwurf markierte Backmerge-PRs können vor dem Merge automatisch aktiviert werden, und ein am Branch-Schutz gescheiterter regulärer Merge kann optional als Admin-Merge wiederholt werden.
- Neuer „Prüfmodus" für das automatische Beenden von Aufgaben: wahlweise nur ereignisgesteuerte oder alle Aufgaben beobachten. Aufgaben ohne verknüpftes Issue und ohne verknüpften Pull Request werden nie automatisch beendet.
- Behoben: Die Aufgabenkachel in der Aufgabenliste (Menü-Seitenleiste und Dashboard) konnte „✓ Bereit" anzeigen, obwohl die CLI-Ausführung noch lief — der Kachel-Status folgt jetzt dem Live-Zustand der CLI-Session und zeigt konsistent zur Aufgaben-Fußzeile „▶ Läuft" bzw. „⏸ Wartet".
- Fehlende Laufdaten zu einem laufenden CLI-Prozess werden jetzt selbstheilend ergänzt, sodass der Status auch nach Neustarts und wiederverwendeten Sitzungen korrekt bleibt.
- Neue Fehlermeldung beim Stoppen: Läuft der CLI-Prozess nach dem Stoppen weiter, meldet die App „Der CLI-Prozess konnte nicht beendet werden und läuft weiter. Bitte erneut versuchen." statt die Aufgabe fälschlich als bereit anzuzeigen.
