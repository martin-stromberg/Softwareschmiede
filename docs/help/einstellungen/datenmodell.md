# Einstellungen — Datenmodell

## Entitäten

### `AppEinstellung`

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
| `DefaultKiPlugin` (`ki.plugin.default`) | string | `PluginName` des Standard-KI-Plugins; letzte Fallback-Stufe der CLI-Auflösung für Aufgaben-Starts und SCM-Ereigniszustellung |
| `DarkModeEnabled` | bool | Dark Mode aktiviert (`"true"` / `"false"`) |
| `WindowPosition.X` | int | Fenster-X-Position (Pixel) |
| `WindowPosition.Y` | int | Fenster-Y-Position (Pixel) |
| `WindowPosition.Width` | int | Fensterbreite (Pixel) |
| `WindowPosition.Height` | int | Fensterhöhe (Pixel) |
| `NotificationMode` | Enum | Benachrichtigungsmodus (`Deaktiviert` / `Banner` / `Ton`) |
| `NotificationAudioPath` | string | Pfad zur Benachrichtigungs-Audiodatei (MP3/WAV) |
| `LogLevel` | Enum | Logging-Granularität (`Debug` / `Information`) |
| `autonomeaufgaben.enabled` | bool | Feature-Flag "Autonome Aufgaben aktivieren" (`AppEinstellungService.AutonomAufgabenEnabledKey`); Standardwert bei fehlendem Eintrag: `true` |
| `scm.monitoring.enabled` | bool | Laufzeit-Schalter "SCM-Ereignisüberwachung aktivieren" (`AppEinstellungService.ScmMonitoringEnabledKey`); Standardwert bei fehlendem Eintrag: `true`. Bei `false` beobachten/enumerieren die Hintergrunddienste keine SCM-Quellen und stellen keine Ereignisse automatisch zu; manuelle Queue-Aktionen bleiben funktionsfähig |
| `scm.trigger.keyword` | string | Trigger-Keyword für SCM-Ereignisse (`AppEinstellungService.ScmTriggerKeywordKey`); Standard bei fehlendem oder leerem Eintrag: `$Softwareschmiede` (`ScmEventValidationService.DefaultTriggerKeyword`). Leere/Leerraum-Werte sind ungültig und fallen auf den Standard zurück |
| `scm.backmerge.mode` | int | Backmerge-Aktivierungsmodus als `ScmBackmergeModus`-Wert (`AppEinstellungService.ScmBackmergeModeKey`): `0` = Deaktiviert, `1` = Global aktiviert, `2` = Projektbezogen aktivierbar; Standard bei fehlendem/ungültigem Eintrag: `0` |
| `plugins.backmerge.project.{projektId}` | bool | Projektbezogene Backmerge-Freigabe (`AppEinstellungService.BackmergeProjektKey`); wirkt nur bei `scm.backmerge.mode = 2`. Standard bei fehlendem Eintrag: `false` |
| `scm.prompt.append.general` | string? | Prompt-Anhang für alle SCM-Ereignisarten (`AppEinstellungService.ScmPromptAppendGeneralKey`); fehlender/leerer Eintrag: nichts anhängen. Wird bei der Zustellung vor dem spezifischen Anhang eingefügt (`ScmPromptAppendix`) |
| `scm.prompt.append.issue` | string? | Prompt-Anhang für SCM-Ereignisse aus Issue-Beschreibungen (`AppEinstellungService.ScmPromptAppendIssueKey`) |
| `scm.prompt.append.issuecomment` | string? | Prompt-Anhang für SCM-Ereignisse aus Kommentaren auf Issues (`AppEinstellungService.ScmPromptAppendIssueCommentKey`) |
| `scm.prompt.append.pr` | string? | Prompt-Anhang für SCM-Ereignisse aus Pull-Request-Beschreibungen (`AppEinstellungService.ScmPromptAppendPullRequestKey`) |
| `scm.prompt.append.prcomment` | string? | Prompt-Anhang für SCM-Ereignisse aus Kommentaren, Zeilenkommentaren und Reviews auf Pull Requests (`AppEinstellungService.ScmPromptAppendPullRequestCommentKey`) |
| `updates.mode` | int | Update-Modus als `UpdateMode`-Wert (`AppEinstellungService.UpdateModeKey`): `0` = Aus, `1` = Nur prüfen, `2` = Bei Programmstart prüfen und ausführen; Standard bei fehlendem/ungültigem Eintrag: `1` |
| `updates.includePrereleases` | bool | Prerelease-Versionen bei der Update-Prüfung berücksichtigen (`AppEinstellungService.IncludePrereleasesKey`); Standard: `false` |
| `plugins.sessionlimit.<KiPluginPrefix>` | string | Letzter vom KI-Plugin gemeldeter Session-Limit-Reset-Zeitpunkt (ISO-8601-Roundtrip, UTC). **Laufzeitstatus, keine Anwender-Konfiguration** — wird ausschließlich von `KiPluginLimitService` geschrieben (`SessionLimitKeyPrefix`); abgelaufene oder ungültige Werte werden beim Lesen ignoriert. Details: [Aufgaben — Datenmodell](../aufgaben/datenmodell.md) |

### `BenachrichtigungsEinstellung`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `Modus` | `BenachrichtigungsModus` | Immer / Nie / NurBeiFehler |
| `Kanal` | `BenachrichtigungsKanal` | Kanal (z.B. Audio, System) |

### `BenachrichtigungsAudioDatei`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `Pfad` | `string` | Dateipfad zur Audiodatei |
| `Aktiv` | `bool` | Ob die Datei verwendet wird |

### `BenachrichtigungsDispatchLog`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Primärschlüssel |
| `AufgabeId` | `Guid` | FK → Aufgabe |
| `Zeitstempel` | `DateTimeOffset` | Zeitpunkt der Benachrichtigung |
| `Entscheidung` | `BenachrichtigungsEntscheidung` | Gesendet / Unterdrückt |
| `Kanal` | `BenachrichtigungsKanal` | Genutzter Kanal |

### `PluginKonfiguration`

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
| `ErstelltAm` | `DateTimeOffset` | Erstellungszeitpunkt |
| `AktualisiertAm` | `DateTimeOffset` | Letzte Änderung |
