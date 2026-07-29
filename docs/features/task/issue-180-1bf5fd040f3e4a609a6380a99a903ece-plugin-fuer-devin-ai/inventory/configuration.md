# Konfiguration und Executable-Auflösung

## Bestehendes Muster

Plugin-Einstellungen werden über `GetSettingGroups()` beschrieben und unter `<PluginPrefix>.<Key>` im Credential Store gespeichert. `CliKiPluginBase.ResolveExecutablePath` liest den optionalen Wert `<PluginPrefix>.ExecutablePath`; bei leerem Wert wird der Plugin-spezifische Standardname verwendet.

Der tatsächliche Prozessstarter `CliRunner` löst einen nicht absoluten Befehlsnamen über `PATH` und Windows-Fallbackpfade auf. Ein absolut konfigurierter Pfad wird direkt verwendet.

## Devin-Konfiguration

Vorgesehen ist mindestens ein optionales Textfeld:

- Schlüssel: `ExecutablePath`
- Zweck: manueller absoluter Pfad zur Devin-CLI-Executable
- kein Feld `Token`, `ApiKey` oder vergleichbares Authentifizierungsgeheimnis

Ein optionales `CommandLineParameters`-Feld ist mit dem bestehenden CLI-Plugin-Muster vereinbar, muss aber in der Planung gegen die gewünschte Sicherheits- und Bedienoberfläche abgewogen werden.

## Offene Punkte

- Offizieller Executable-Name auf Windows, macOS und Linux.
- Ob Devin über `PATH` genügt oder zusätzliche Installationspfade benötigt.
- Ob eine Devin-spezifische Erweiterung von `CliRunner.ResolveExecutablePath` erforderlich ist. Diese Methode ist derzeit privat und enthält bereits überwiegend Windows-/PATH-Logik.
- Verhalten bei fehlender CLI: aktuell liefern Health-/Help-Methoden Fehlerstatus bzw. `null`; eine Installationsanleitung ist im Bestand nicht automatisch gekoppelt.
