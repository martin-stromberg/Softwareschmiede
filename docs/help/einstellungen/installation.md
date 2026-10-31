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
| `Deaktiviert` | Keine Benachrichtigungen |
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

## SCM-Ereignisüberwachung konfigurieren

Die SCM-Ereignisüberwachung wird auf der Registerkarte „Allgemein" im Abschnitt „SCM-Ereignisse" gesteuert:

| Einstellung | Schlüssel (`AppEinstellung`) | Standardwert | Wirkung |
|-------------|------------------------------|--------------|---------|
| Checkbox „SCM-Ereignisüberwachung aktivieren" (`SettingsViewModel.IsScmMonitoringEnabled`) | `scm.monitoring.enabled` (`AppEinstellungService.ScmMonitoringEnabledKey`) | `true` (Fallback, falls kein Eintrag vorhanden) | Wirkt ohne Neustart; bei `false` beobachten `ScmEventMonitoringService` und `ScmEventExchangeService` keine Quellen und stellen keine Ereignisse automatisch zu |
| Textfeld „Trigger-Keyword" (`SettingsViewModel.ScmTriggerKeyword`) | `scm.trigger.keyword` (`AppEinstellungService.ScmTriggerKeywordKey`) | `$Softwareschmiede` (`ScmEventValidationService.DefaultTriggerKeyword`) | Wirkt ohne Neustart; leere/Leerraum-Eingaben werden in der GUI abgelehnt, ein ungültiger gespeicherter Wert fällt auf den Standard zurück |
| Auswahlbox „Konto-Modus" (`SettingsViewModel.SelectedKontoModus`) | `scm.monitoring.accountmode` (`AppEinstellungService.ScmKontoModusKey`) | `Benutzerkonto` | Wirkt ohne Neustart; `Benutzerkonto` = repository-weite Enumeration mit Trigger-Keyword, `Separates Konto` = Notifications-Inbox eines separaten Kontos mit `@`-Erwähnung als Auslöser. Bei `Separates Konto` muss das GitHub-Plugin-Feld „Personal Access Token des separaten Kontos" gefüllt sein, sonst bricht das Speichern mit einer Fehlermeldung ab; im separaten Modus entfallen Enumeration und Backmerge-Entdeckungen |
| Textfeld „Prompt-Anhänge → Allgemein" (`SettingsViewModel.ScmPromptAppendixGeneral`) | `scm.prompt.append.general` (`AppEinstellungService.ScmPromptAppendGeneralKey`) | leer | Wirkt ohne Neustart; wird bei jeder SCM-Ereigniszustellung dem Prompt angehängt; leer = nichts anhängen |
| Textfeld „Prompt-Anhänge → Issue" (`SettingsViewModel.ScmPromptAppendixIssue`) | `scm.prompt.append.issue` (`AppEinstellungService.ScmPromptAppendIssueKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Issue-Beschreibungen (neuer/geänderter Issue-Text) |
| Textfeld „Prompt-Anhänge → Issue-Kommentar" (`SettingsViewModel.ScmPromptAppendixIssueComment`) | `scm.prompt.append.issuecomment` (`AppEinstellungService.ScmPromptAppendIssueCommentKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Kommentaren auf Issues |
| Textfeld „Prompt-Anhänge → Pull Request" (`SettingsViewModel.ScmPromptAppendixPullRequest`) | `scm.prompt.append.pr` (`AppEinstellungService.ScmPromptAppendPullRequestKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Pull-Request-Beschreibungen (neuer/geänderter PR-Text) |
| Textfeld „Prompt-Anhänge → Pull-Request-Kommentar" (`SettingsViewModel.ScmPromptAppendixPullRequestComment`) | `scm.prompt.append.prcomment` (`AppEinstellungService.ScmPromptAppendPullRequestCommentKey`) | leer | Wirkt ohne Neustart; Anhang für Ereignisse aus Kommentaren, Zeilenkommentaren und veröffentlichten Reviews auf Pull Requests |

Bei der Zustellung wird zuerst der allgemeine, dann der zur Ereignisart passende Anhang mit einer Leerzeile getrennt angehängt (als eigenes `promptSuffix`-Feld im JSON-Prompt). Die Anhänge verändern weder den gespeicherten Ereignistext noch die Trigger-Prüfung; ein leerer spezifischer Anhang ersetzt den allgemeinen nicht.

## Überprüfung

Nach dem Speichern des Arbeitsverzeichnisses zeigt die Einstellungsseite den aufgelösten Pfad an. Ist ein Fallback aktiv, erscheint ein Hinweis mit dem Reason-Code.
