# Templates und KI-Integration

## Vorhandene Promptvorlagen

`PromptVorlagenService` verwaltet persistierte lokale `PromptVorlage`-Entitäten und initiale Standardvorlagen. `PromptVorlagenPlatzhalterService` löst Aufgaben-Platzhalter für CLI-Prompts auf. Diese Vorlagen sind Anwendungs-Promptvorlagen und nicht Repository-Issue-Templates.

Die Aufgabendetailansicht bindet im CLI-Ribbon eine `PromptVorlagen`-Auswahl und kann eine Vorlage an eine laufende CLI senden. Dieser Mechanismus liefert kein synchrones, editierbares Textresultat für einen neuen Issue-Body.

## KI-Verträge

`IKiPlugin` und `IAiCliProvider` (`src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces`) starten externe KI-CLI-Prozesse und unterstützen optional Session-Fortsetzung. Die konkreten Plugins wie Codex, Claude CLI, GitHub Copilot und der Simulator implementieren damit Prozessstart/Health, aber kein allgemeines `GenerateTextAsync`- oder `FillTemplateAsync`-Verhalten.

`KiAusfuehrungsService` ist auf laufende Aufgabenprozesse, PseudoConsole, Status und Heartbeats ausgerichtet. Eine neue Dialogaktion darf diesen langlebigen Aufgabenlauf nicht implizit als Textgenerator missbrauchen, weil dadurch Antwortabholung, Abbruch, Parallelität und UI-Zustand ungeklärt wären.

## Geforderte Template-Komposition

Für einen ausgewählten Provider-Template-Inhalt muss der Dialog den editierbaren Body deterministisch erzeugen:

1. Template-Inhalt übernehmen.
2. Trennlinie ergänzen.
3. Abschnitt `Originalanforderung:` ergänzen.
4. Nicht vorhandene oder leere `AnforderungsBeschreibung` ohne Fehler behandeln.
5. Den resultierenden Text vollständig editierbar lassen.

Die KI-Aktion braucht mindestens Template-Inhalt und Originalanforderung als Eingaben und muss ihr Ergebnis in denselben editierbaren Body zurückschreiben. Das Ergebnis sollte als Vorschlag behandelt werden, nicht als unveränderliche Provider-Antwort.

## Offene technische Entscheidungen

- Direkter neuer Textgenerierungsvertrag versus einmaliger KI-CLI-Prozess mit erfassbarem stdout-Ergebnis.
- Auswahl des KI-Providers anhand von Aufgaben-/Projekt-/globalem Plugin-Kontext; `PluginSelectionService` enthält bereits Auflösungslogik für KI-Plugin-Prefixe.
- Verhalten bei nicht unterstützten Platzhaltern und bei KI-Fehlern: Body erhalten, Fehler anzeigen, erneuten Versuch erlauben.
- Abbruch und Cancellation müssen sowohl Template-/Issue-Laden als auch KI-Ausführung und Provider-Erstellung abdecken.
