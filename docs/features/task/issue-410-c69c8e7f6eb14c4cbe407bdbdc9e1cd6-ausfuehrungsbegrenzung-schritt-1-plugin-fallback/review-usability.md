# Usability-Review

## Ergebnis

**Status:** Keine Befunde

Keine UI-Änderungen — Review nicht anwendbar. Der Diff gegen den Basisbranch `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung` enthält keine `*.xaml`-, `*.razor`-, `*.cshtml`- oder ViewModel-/Dialog-Änderungen; geändert wurden ausschließlich `PluginSelectionService`, `ScmEventOrchestrator` (jeweils `src/Softwareschmiede/Application/Services/`) sowie Test-Code. Die neue Wartemeldung erscheint über den bestehenden `Reason`-Mechanismus der Ereignisübersicht und ist ein verständlicher deutscher Text mit benannten Plugin-Zuordnungen und konkretem Handlungshinweis („Plugin in den Einstellungen aktivieren oder Ereignis öffnen und CLI auswählen") — keine internen Kennungen als Nutzereingabe erforderlich.
