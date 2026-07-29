← [Zurück zur Übersicht](index.md)

# Devin-Plugin - Technische Hinweise

## Plugin-Vertrag

`DevinPlugin` ist ein `DevelopmentAutomation`-Plugin und nutzt die gemeinsame `CliKiPluginBase`-Infrastruktur. Der Standardaufruf lautet `devin`; falls `Softwareschmiede.Devin.ExecutablePath` gesetzt ist, verwendet das Plugin diesen absoluten Pfad.

Der Prozess wird im lokalen Aufgaben-Repository gestartet. `UseShellExecute` ist deaktiviert und `CreateNoWindow` bleibt `false`, damit die vorhandene Terminal- und ConPTY-Ausfuehrung die interaktive Sitzung anzeigen und Eingaben annehmen kann.

## Authentifizierung

Das Plugin setzt keine Devin-spezifischen Authentifizierungsargumente und keine Umgebungsvariablen wie Tokens oder API Keys. Die Devin CLI liest ihre eigenen Anmeldedaten, die vorher mit `devin auth login` eingerichtet wurden.

Wichtige Konsequenzen:

- Es gibt kein Feld `Token`, `ApiKey` oder vergleichbares Secret im Devin-Plugin.
- Health-Check und Prozessstart setzen voraus, dass `devin` lokal erreichbar ist.
- Authentifizierungsprobleme werden ueber die Devin CLI beziehungsweise deren Terminalausgabe sichtbar.

## Parameterweitergabe

Die Softwareschmiede uebergibt die vom Anwender gesetzten Parameter unveraendert an `ProcessStartInfo.Arguments` und haengt gespeicherte `CommandLineParameters` an. Dadurch koennen Devin-Optionen wie `--continue`, `--resume`, `--print`, `--prompt-file`, `--model` und `--permission-mode` verwendet werden, ohne dass das Plugin eine eigene Devin-spezifische Parserlogik besitzt.

Die offizielle Referenz befindet sich unter https://docs.devin.ai/cli/reference/commands.
