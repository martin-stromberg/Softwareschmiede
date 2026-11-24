# Einstellungen — Installation und Konfiguration

## Arbeitsverzeichnis einrichten

1. In der Seitenleiste auf **Einstellungen** klicken.
2. Registerkarte **Allgemein** öffnen.
3. Im Abschnitt „Arbeitsverzeichnis" einen absoluten Pfad eingeben, z.B. `C:\Dev\Workdir`.
4. Auf **Speichern** klicken.

Das Verzeichnis muss existieren und beschreibbar sein. Unterverzeichnisse werden von der Anwendung automatisch angelegt (`softwareschmiede/<aufgabe-id>/`).

## KI-Kontext-Limits konfigurieren

Die Grenzen für die automatische Kontextkomprimierung können in `appsettings.json` überschrieben werden:

| Parameter | Standardwert | Beschreibung |
|-----------|-------------|--------------|
| `KiKontext:SoftLimitChars` | `12000` | Ab dieser Zeichenzahl wird Komprimierung ausgelöst |
| `KiKontext:HardLimitChars` | `20000` | Ab dieser Zeichenzahl erscheint Warnung |

Beispiel:

```json
{
  "KiKontext": {
    "SoftLimitChars": 15000,
    "HardLimitChars": 25000
  }
}
```

## Benachrichtigungen konfigurieren

Benachrichtigungseinstellungen werden global in der Einstellungsseite verwaltet.

| Modus | Beschreibung |
|-------|-------------|
| `Disabled` | Keine Benachrichtigungen |
| `Banner` | Windows-Toast-Benachrichtigung nach Statuswechsel |
| `Ton` | Audiodatei abspielen (MP3 oder WAV) |

Audiodateien für Ton-Benachrichtigungen werden über den Einstellungsschlüssel `NotificationAudioPath` hinterlegt. Die Wiedergabe erfolgt über den WPF-`MediaPlayer` — kein separates Backend erforderlich.

## Design (Dark Mode) konfigurieren

Das Design wird in den Einstellungen geändert:

1. In der Seitenleiste auf **Einstellungen** klicken.
2. Registerkarte **Allgemein** öffnen.
3. Im Abschnitt „Design" die gewünschte Option aus der Dropdown-Liste wählen:
   - **Hell** — helles Erscheinungsbild
   - **Dunkel** — dunkles Erscheinungsbild
4. Auf **Speichern** klicken.

Die Änderung wird sofort wirksam und beim nächsten Start automatisch beibehalten.

## Automatisierungs-Einstellungen konfigurieren

Die drei zyklischen Automatisierungsfunktionen werden auf der Registerkarte „Automatisierung" in drei auf-/zuklappbaren Panels gesteuert („Aufgaben ausführen", „Aufgaben beenden", „Prozessabläufe"); jede Panel-Titelleiste zeigt den Schalterzustand („aktiviert"/„deaktiviert"):

| Einstellung | Schlüssel (`AppSetting`) | Standardwert | Wirkung |
|-------------|------------------------------|--------------|---------|
| Checkbox „Aufgaben automatisch starten" (`SettingsViewModel.IsScmMonitoringEnabled`) | `scm.monitoring.enabled` (`AppSettingService.ScmMonitoringEnabledKey`) | `true` (Fallback, falls kein Eintrag vorhanden) | Wirkt ohne Neustart; bei `false` beobachten `ScmEventMonitoringService` und `ScmEventExchangeService` keine Quellen und stellen keine Ereignisse automatisch zu. Bei deaktiviertem Schalter ist „Überwachungsmodus" deaktiviert und „Trigger-Keyword" sowie „Prompt-Anhänge" sind ausgeblendet |
| Auswahlbox „Überwachungsmodus" (`SettingsViewModel.SelectedAccountMode`) | `scm.monitoring.accountmode` (`AppSettingService.ScmAccountModeKey`) | `UserAccount` | Wirkt ohne Neustart; `UserAccount` = repository-weite Enumeration mit Trigger-Keyword, `Separates Konto` = Notifications-Inbox eines separaten Kontos mit `@`-Erwähnung oder Assignee-Zuweisung an das Konto als Auslöser. Bei `Separates Konto` muss das GitHub-Plugin-Feld „Personal Access Token des separaten Kontos" gefüllt sein, sonst bricht das Speichern mit einer Fehlermeldung ab; im separaten Modus entfällt die repository-weite Enumeration der Ereignisüberwachung (die Backmerge-Prüfung läuft davon unabhängig weiter). Nur bei aktivierter Funktion bedienbar |
| Textfeld „Trigger-Keyword" (`SettingsViewModel.ScmTriggerKeyword`) | `scm.trigger.keyword` (`AppSettingService.ScmTriggerKeywordKey`) | `$Softwareschmiede` (`ScmEventValidationService.DefaultTriggerKeyword`) | Wirkt ohne Neustart; leere/Leerraum-Eingaben werden in der GUI abgelehnt, ein ungültiger gespeicherter Wert fällt auf den Standard zurück. Nur sichtbar bei aktivierter Funktion und Überwachungsmodus `UserAccount` |
| Textfeld „Prompt-Anhänge → Allgemein" (`SettingsViewModel.ScmPromptAppendixGeneral`) | `scm.prompt.append.general` (`AppSettingService.ScmPromptAppendGeneralKey`) | leer | Wirkt ohne Neustart; wird bei jeder SCM-Ereigniszustellung dem Prompt angehängt; leer = nichts anhängen. Die Prompt-Anhänge sind nur bei aktivierter Funktion sichtbar |
| Textfeld „Prompt-Anhänge → Issue" (`SettingsViewModel.ScmPromptAppendixIssue`) | `scm.prompt.append.issue` (`AppSettingService.ScmPromptAppendIssueKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Issue-Beschreibungen (neuer/geänderter Issue-Text) |
| Textfeld „Prompt-Anhänge → Issue-Kommentar" (`SettingsViewModel.ScmPromptAppendixIssueComment`) | `scm.prompt.append.issuecomment` (`AppSettingService.ScmPromptAppendIssueCommentKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Kommentaren auf Issues |
| Textfeld „Prompt-Anhänge → Pull Request" (`SettingsViewModel.ScmPromptAppendixPullRequest`) | `scm.prompt.append.pr` (`AppSettingService.ScmPromptAppendPullRequestKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Pull-Request-Beschreibungen (neuer/geänderter PR-Text) |
| Textfeld „Prompt-Anhänge → Pull-Request-Kommentar" (`SettingsViewModel.ScmPromptAppendixPullRequestComment`) | `scm.prompt.append.prcomment` (`AppSettingService.ScmPromptAppendPullRequestCommentKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Kommentaren, Zeilenkommentaren und veröffentlichten Reviews auf Pull Requests |
| Checkbox „Aufgaben automatisch beenden" (`SettingsViewModel.IsScmTaskCompletionEnabled`) | `scm.taskcompletion.enabled` (`AppSettingService.ScmTaskCompletionEnabledKey`) | `true` (Fallback) | Wirkt ohne Neustart; bei `false` beobachtet `ScmTaskCompletionMonitoringService` keine Referenzzustände und beendet keine Aufgaben. Bei deaktiviertem Schalter sind „Prüfmodus" und „Wartezeit für Beendigung" deaktiviert |
| Auswahlbox „Prüfmodus" (`SettingsViewModel.SelectedScmTaskCompletionCheckmode`) | `scm.taskcompletion.scope` (`AppSettingService.ScmTaskCompletionCheckModeKey`) | `EventDriven` („Nur ereignisgesteuerte Aufgaben") | Wirkt ohne Neustart; `EventDriven` = nur Aufgaben mit mindestens einem SCM-Ereignis, `AllWorkItems` = zusätzlich manuell angelegte Aufgaben. Kandidaten brauchen stets eine SCM-Referenz (Issue oder Pull Request); die Issue-Prüfung läuft kanonisch unter `UserAccount`. Nur bei aktivierter Funktion bedienbar |
| Textfeld „Wartezeit für Beendigung" (`SettingsViewModel.ScmTaskCompletionWaitMinutes`) | `scm.taskcompletion.wait.minutes` (`AppSettingService.ScmTaskCompletionWaitMinutesKey`) | `60` | Ganzzahl in Minuten (0–43200) zwischen erkanntem Schließen/Mergen der verknüpften Referenz und dem automatischen Beenden. Nur bei aktivierter Funktion bedienbar |
| Auswahlbox „Backmerge-Pull-Requests" (`SettingsViewModel.SelectedBackmergeMode`) | `scm.backmerge.mode` (`AppSettingService.ScmBackmergeModeKey`) | `Disabled` | Wirkt ohne Neustart; `Global` = automatischer Merge in allen Projekten, `ProjectScoped` = zusätzlich Freigabe `plugins.backmerge.project.{projectId}` in der Projektdetailansicht nötig. Der eigene Prüfzyklus (`ScmBackmergeMonitoringService`) läuft unabhängig von `scm.monitoring.enabled` und `scm.monitoring.accountmode`, stets unter `UserAccount` |
| Checkbox „Draft-Pull-Requests vor dem Merge automatisch aktivieren" (`SettingsViewModel.IsScmBackmergeDraftActivation`) | `scm.backmerge.draft_activation` (`AppSettingService.ScmBackmergeDraftActivationKey`) | `false` | Bei `true` wird ein als Entwurf erkannter Backmerge-PR vor dem Merge automatisch als bereit markiert (`IGitPlugin.MarkPullRequestReadyAsync` → `gh pr ready`); bei `false` verbleibt er im Warte-Retry. Nur bei Backmerge-Modus ≠ `Disabled` bedienbar |
| Checkbox „Bei blockiertem Merge Admin-Merge erlauben" (`SettingsViewModel.IsScmBackmergeAdminMerge`) | `scm.backmerge.admin_merge` (`AppSettingService.ScmBackmergeAdminMergeKey`) | `false` | Der Merge läuft zuerst regulär (`AllowProtectedBranchBypass: false`); bei `true` wird ein gescheiterter Merge einmalig als Admin-Merge (`gh pr merge --admin`) wiederholt. Nur bei Backmerge-Modus ≠ `Disabled` bedienbar |

Bei der Zustellung wird zuerst der allgemeine, dann der zur Ereignisart passende Anhang mit einer Leerzeile getrennt angehängt (als eigenes `promptSuffix`-Feld im JSON-Prompt). Die Anhänge verändern weder den gespeicherten Ereignistext noch die Trigger-Prüfung; ein leerer spezifischer Anhang ersetzt den allgemeinen nicht.

## Überprüfung

Nach dem Speichern des Arbeitsverzeichnisses zeigt die Einstellungsseite den aufgelösten Pfad an. Ist ein Fallback aktiv, erscheint ein Hinweis mit dem Reason-Code.
