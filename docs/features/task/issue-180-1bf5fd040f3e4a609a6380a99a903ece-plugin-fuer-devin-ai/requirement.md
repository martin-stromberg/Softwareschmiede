# Übersetzte Anforderung: Devin-CLI-Plugin

## Fachliche Zusammenfassung

Die Anwendung soll um ein neues KI-Plugin für die Devin-CLI erweitert werden. Das Plugin muss die Devin-CLI als externen CLI-Prozess starten und in die vorhandene Plugin-Auswahl sowie den bestehenden CLI-Ausführungsablauf integrieren. Der Pfad zur CLI-Executable soll möglichst automatisch ermittelt werden; zusätzlich soll eine manuelle Pfadangabe möglich sein, falls die automatische Erkennung nicht erfolgreich ist. Ein Authentifizierungstoken wird nicht benötigt, da die Anmeldung innerhalb der Devin-CLI erfolgt.

## Betroffene Klassen und Komponenten

- **Neue Plugin-Klasse:** `DevinPlugin`, abgeleitet von `CliKiPluginBase` und gemäß den bestehenden CLI-Plugin-Konventionen implementiert.
- **Plugin-Metadaten:** Registrierung von Plugin-Name, Dateipräfix, Pluginpräfix und passendem `PluginType` für die Devin-CLI.
- **CLI-Prozessstart:** Nutzung der bestehenden Prozessstart- und Gesundheitsprüfungsmechanismen aus `CliKiPluginBase`, einschließlich eines Devin-spezifischen CLI-Aufrufs.
- **Konfiguration:** Plugin-Einstellung für einen optionalen manuellen Pfad zur Devin-CLI-Executable; die Einstellung darf kein Authentifizierungstoken enthalten.
- **Executable-Erkennung:** Wiederverwendung des vorhandenen Mechanismus zur Auflösung von CLI-Executables oder Erweiterung dieses Mechanismus, falls die Devin-CLI abweichende Installationsorte erfordert.
- **Plugin-Registrierung:** Einbindung des neuen Plugins in die bestehende Plugin-Discovery bzw. Dependency-Injection-Registrierung.
- **Tests:** Unit-Tests für Metadaten, Konfiguration, automatische bzw. manuelle Executable-Auflösung und den Aufbau des Devin-CLI-Prozessstarts sowie gegebenenfalls Integrations- oder Health-Check-Tests.

## Implementierungsansatz

`DevinPlugin` wird nach dem Muster vorhandener CLI-Plugins wie `CodexPlugin` umgesetzt und in die bestehende Plugin-Infrastruktur integriert. Der Prozessstart soll den von `CliKiPluginBase` vorgesehenen Erweiterungspunkt verwenden; die Devin-CLI wird dabei ohne Token- oder Credential-Parameter gestartet. Für die Executable wird zuerst eine explizit konfigurierte Pfadangabe verwendet und andernfalls die vorhandene automatische Auflösung mit dem Devin-CLI-Befehlsnamen versucht. Die Authentifizierung bleibt vollständig dem interaktiven Devin-CLI-Aufruf überlassen.

Falls der bestehende Auflösungsmechanismus den Devin-Installationsweg nicht abdeckt, wird dieser gezielt um eine Devin-spezifische Erkennung ergänzt. Die konkrete Devin-CLI-Syntax, erforderliche Arbeitsverzeichnisse und die Unterstützung von Hilfe-, Versions-, Sitzungsfortsetzungs- oder One-Shot-Aufrufen müssen anhand der CLI-Spezifikation und des vorhandenen Plugin-Vertrags geprüft werden.

## Konfiguration

Die Konfiguration erfolgt auf Plugin-Ebene über eine optionale Einstellung für den Pfad zur Devin-CLI-Executable, analog zur bestehenden `ExecutablePath`-Einstellung anderer CLI-Plugins. Bei fehlender Pfadangabe wird die CLI möglichst automatisch über die verfügbare Executable-Auflösung gesucht. Es werden keine Authentifizierungsdaten und insbesondere kein Authentifizierungstoken als Anwendungseinstellung gespeichert oder an die CLI übergeben.

## Offene Fragen

- Wie lautet der offizielle Name der Devin-CLI-Executable auf den unterstützten Betriebssystemen und welche Installationsorte müssen automatisch erkannt werden?
- Welche konkreten Devin-CLI-Parameter werden für den normalen Plugin-Start, die Gesundheitsprüfung, die Hilfeausgabe und gegebenenfalls die Sitzungsfortsetzung benötigt?
- Unterstützt die Devin-CLI einen nicht-interaktiven Aufruf, der mit dem bestehenden `CliKiPluginBase`-Ausführungsmodell kompatibel ist, oder muss das Plugin einen speziellen Interaktionsmodus verwenden?
- Welcher bestehende `PluginType` ist für Devin fachlich korrekt, und unter welchem Anzeigenamen sowie Dateipräfix soll das Plugin registriert werden?
- Soll bei nicht erkannter CLI ausschließlich ein Health-Check-Fehler angezeigt werden, oder soll zusätzlich eine plattformspezifische Installationsanleitung angeboten werden?
- Auf welchen Betriebssystemen muss die automatische Pfaderkennung funktionieren?
