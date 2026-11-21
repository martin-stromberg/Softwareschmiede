# Einstellungen — Datenmodell

## Entitäten

### `AppSetting`

Schlüssel-Wert-Tabelle für anwendungsweite Einstellungen.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Key` | `string` | Einstellungsschlüssel (Primärschlüssel) |
| `Value` | `string?` | Einstellungswert |

Bekannte Schlüssel:

| Schlüssel | Typ | Beschreibung |
|-----------|-----|--------------|
| `WorkDir` | string | Lokales Arbeitsverzeichnis für Repository-Klons |
| `DefaultScmPlugin` (`scm.plugin.default`) | string | `PluginName` des Standard-SCM-Plugins |
| `DefaultAiPlugin` (`ki.plugin.default`) | string | `PluginName` des Standard-KI-Plugins; letzte Fallback-Stufe der CLI-Auflösung für Aufgaben-Starts und SCM-Ereigniszustellung |
| `DarkModeEnabled` | bool | Dark Mode aktiviert (`"true"` / `"false"`) |
| `WindowPosition.X` | int | Fenster-X-Position (Pixel) |
| `WindowPosition.Y` | int | Fenster-Y-Position (Pixel) |
| `WindowPosition.Width` | int | Fensterbreite (Pixel) |
| `WindowPosition.Height` | int | Fensterhöhe (Pixel) |
| `NotificationMode` | Enum | Benachrichtigungsmodus (`Disabled` / `Banner` / `Ton`) |
| `NotificationAudioPath` | string | Pfad zur Benachrichtigungs-Audiodatei (MP3/WAV) |
| `LogLevel` | Enum | Logging-Granularität (`Debug` / `Information`) |
| `scm.monitoring.enabled` | bool | Laufzeit-Schalter "Aufgaben automatisch starten" (Einstellungen → Automatisierung → Aufgaben ausführen) (`AppSettingService.ScmMonitoringEnabledKey`); Standardwert bei fehlendem Eintrag: `true`. Bei `false` beobachten/enumerieren die Hintergrunddienste keine SCM-Quellen und stellen keine Ereignisse automatisch zu; manuelle Queue-Aktionen bleiben funktionsfähig |
| `scm.trigger.keyword` | string | Trigger-Keyword für SCM-Ereignisse (`AppSettingService.ScmTriggerKeywordKey`); Standard bei fehlendem oder leerem Eintrag: `$Softwareschmiede` (`ScmEventValidationService.DefaultTriggerKeyword`). Leere/Leerraum-Werte sind ungültig und fallen auf den Standard zurück |
| `scm.monitoring.accountmode` | int | Überwachungsmodus der SCM-Ereignisüberwachung (Einstellungen → Automatisierung → Aufgaben ausführen) als `ScmAccountMode`-Wert (`AppSettingService.ScmAccountModeKey`): `0` = `UserAccount` (repository-weite Enumeration, Trigger-Keyword), `1` = `SeparateAccount` (Notifications-Inbox eines eigenen Bot-Kontos, Auslöser `@`-Erwähnung); Standard bei fehlendem/ungültigem Eintrag: `0`. Bei `1` ist der GitHub-Plugin-Eintrag `AgentToken` Pflicht (GUI-Pflichtprüfung beim Speichern) |
| `scm.backmerge.mode` | int | Backmerge-Aktivierungsmodus als `ScmBackmergeMode`-Wert (Einstellungen → Automatisierung → Prozessabläufe; `AppSettingService.ScmBackmergeModeKey`): `0` = Deaktiviert, `1` = Global aktiviert, `2` = Projektbezogen aktivierbar; Standard bei fehlendem/ungültigem Eintrag: `0`. Der Backmerge-Prüfzyklus (`ScmBackmergeMonitoringService`) läuft unabhängig von `scm.monitoring.enabled` und `scm.monitoring.accountmode`, stets unter `UserAccount` |
| `scm.backmerge.draft_activation` | bool | Opt-in: Draft-Backmerge-PRs vor dem Merge automatisch als bereit markieren (`AppSettingService.ScmBackmergeDraftActivationKey`, Checkbox „Draft-Pull-Requests vor dem Merge automatisch aktivieren"); Standard bei fehlendem Eintrag: `false` |
| `scm.backmerge.admin_merge` | bool | Opt-in: nach einem blockierten regulären Merge einen Admin-Merge-Fallback versuchen (`AppSettingService.ScmBackmergeAdminMergeKey`, Checkbox „Bei blockiertem Merge Admin-Merge erlauben"); Standard bei fehlendem Eintrag: `false` |
| `plugins.backmerge.project.{projectId}` | bool | Projektbezogene Backmerge-Freigabe (`AppSettingService.BackmergeProjectKey`); wirkt nur bei `scm.backmerge.mode = 2`. Standard bei fehlendem Eintrag: `false` |
| `scm.prompt.append.general` | string? | Prompt-Anhang für alle SCM-Ereignisarten (`AppSettingService.ScmPromptAppendGeneralKey`); fehlender/leerer Eintrag: nichts anhängen. Wird bei der Zustellung vor dem spezifischen Anhang eingefügt (`ScmPromptAppendix`) |
| `scm.prompt.append.issue` | string? | Prompt-Anhang für SCM-Ereignisse aus Issue-Beschreibungen (`AppSettingService.ScmPromptAppendIssueKey`) |
| `scm.prompt.append.issuecomment` | string? | Prompt-Anhang für SCM-Ereignisse aus Kommentaren auf Issues (`AppSettingService.ScmPromptAppendIssueCommentKey`) |
| `scm.prompt.append.pr` | string? | Prompt-Anhang für SCM-Ereignisse aus Pull-Request-Beschreibungen (`AppSettingService.ScmPromptAppendPullRequestKey`) |
| `scm.prompt.append.prcomment` | string? | Prompt-Anhang für SCM-Ereignisse aus Kommentaren, Zeilenkommentaren und Reviews auf Pull Requests (`AppSettingService.ScmPromptAppendPullRequestCommentKey`) |
| `scm.taskcompletion.enabled` | bool | Laufzeit-Schalter "Aufgaben automatisch beenden" (Einstellungen → Automatisierung → Aufgaben beenden; `AppSettingService.ScmTaskCompletionEnabledKey`); Standardwert bei fehlendem Eintrag: `true`. Bei `false` prüft `ScmTaskCompletionMonitoringService` keine Referenzzustände und beendet keine Aufgaben |
| `scm.taskcompletion.scope` | int | Prüfmodus der Abschlussüberwachung als `ScmTaskCompletionCheckMode`-Wert (`AppSettingService.ScmTaskCompletionCheckModeKey`): `0` = `EventDriven` (nur Aufgaben mit SCM-Ereigniszuordnung), `1` = `AllWorkItems` (alle nicht beendeten/archivierten Aufgaben mit SCM-Referenz); Standard bei fehlendem/ungültigem Eintrag: `0` |
| `scm.taskcompletion.wait.minutes` | int | Wartezeit für Beendigung in Minuten zwischen erkanntem Schließen/Mergen der verknüpften SCM-Referenz und dem automatischen Beenden der Aufgabe (`AppSettingService.ScmTaskCompletionWaitMinutesKey`); Standard: `60` (`AppSettingService.ScmTaskCompletionDefaultWaitMinutes`), gültiger Bereich 0–43200 |
| `scm.execution.maxparallel` | int? | Globales Limit gleichzeitig laufender KI-CLI-Ausführungen aus SCM-Ereignissen (Einstellungen → Automatisierung → Aufgaben ausführen, Feld „Maximale parallele Ausführungen"; `ScmEventExecutionLimitService.GlobalLimitKey`); fehlender/leerer/ungültiger Eintrag: unbegrenzt, gültige Werte sind Ganzzahlen ≥ 1. Bei erreichtem Limit wartet das Ereignis mit Wartegrund (`ScmEventExecutionLimitService.GetStartBlockReasonAsync`) |
| `plugins.maxparallel.<AiPluginPrefix>` | int? | Zusätzliches Parallel-Limit je KI-Plugin (Einstellungen → Plugins → `<KI-Plugin>` → Gruppe „Ausführungsbegrenzung"; `ScmEventExecutionLimitService.PluginLimitKey`); fehlender/leerer/ungültiger Eintrag: unbegrenzt, gültige Werte sind Ganzzahlen ≥ 1. Gilt gleichzeitig mit dem globalen Limit — das restriktivere entscheidet. Host-Setting über `AppSettingService`, nicht im Plugin-Credential-Store |
| `updates.mode` | int | Update-Modus als `UpdateMode`-Wert (`AppSettingService.UpdateModeKey`): `0` = Aus, `1` = Nur prüfen, `2` = Bei Programmstart prüfen und ausführen; Standard bei fehlendem/ungültigem Eintrag: `1` |
| `updates.pfad` | string | Update-Pfad als exklusiver Kanalselektor (`AppSettingService.UpdatePathKey`): `Produktivversionen` = nur stabile Versionen, `Vorabversionen` = nur Release-Kandidaten; Standard bei fehlendem/leerem/unbekanntem Eintrag: `Produktivversionen` (`UpdatePathChannelResolver.NormalizePath`). Der frühere Schlüssel `updates.includePrereleases` verfällt still — er wird nicht mehr gelesen oder geschrieben |
| `plugins.sessionlimit.<AiPluginPrefix>` | string | Letzter vom KI-Plugin gemeldeter Session-Limit-Reset-Zeitpunkt (ISO-8601-Roundtrip, UTC). **Laufzeitstatus, keine Anwender-Konfiguration** — wird ausschließlich von `AiPluginLimitService` geschrieben (`SessionLimitKeyPrefix`); abgelaufene oder ungültige Werte werden beim Lesen ignoriert. Details: [Aufgaben — Datenmodell](../aufgaben/datenmodell.md) |
| `autoshutdown.toleranz.minuten` | int | Wartezeit des Auto-Shutdown-Countdowns in Minuten (Seitenleisten-Fußzeile, Feld „Wartezeit (Minuten):"; `AppSettingService.AutoShutdownToleranceMinutesKey`); Standard bei fehlendem Eintrag: `5` (`AutoShutdownDefaultToleranceMinutes`), gespeicherte Werte werden beim Lesen auf 0–1440 (`AutoShutdownMaxToleranceMinutes`) begrenzt |
| `autoshutdown.force` | bool | Schalter „Herunterfahren erzwingen" des Auto-Shutdowns (`AppSettingService.AutoShutdownForceKey`); Standard bei fehlendem Eintrag: `false`. Wirkt nur unter Windows (`/f`-Flag des Shutdown-Kommandos). Der Aktivierungszustand („Nach Leerlauf PC herunterfahren") wird bewusst **nicht** persistiert |

### `NotificationSetting`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `Mode` | `NotificationMode` | Immer / Nie / NurBeiFehler |
| `Channel` | `NotificationChannel` | Kanal (z.B. Audio, System) |

### `NotificationAudioFile`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `OriginalFileName` | `string` | Dateipfad zur Audiodatei |
| `Active` | `bool` | Ob die Datei verwendet wird |

### `NotificationDispatchLog`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `WorkItemId` | `Guid` | FK → Aufgabe |
| `CreatedAt` | `DateTimeOffset` | Zeitpunkt der Benachrichtigung |
| `Decision` | `NotificationDecision` | Gesendet / Unterdrückt |
| `Channel` | `NotificationChannel` | Genutzter Kanal |

### `PluginConfiguration`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `PluginPrefix` | `string` | FK → Plugin-Präfix |
| `FieldKey` | `string` | Einstellungsschlüssel des Felds |
| `Value` | `string?` | Verschlüsselter Wert (Windows Credential Store) |

### `PromptVorlage`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `Name` | `string` | Anzeigename der Vorlage |
| `Prompttext` | `string` | Prompt, der an die CLI gesendet wird |
| `Sortierung` | `int` | Stabile Anzeigenreihenfolge |
| `CreatedAt` | `DateTimeOffset` | Erstellungszeitpunkt |
| `AktualisiertAm` | `DateTimeOffset` | Letzte Änderung |
