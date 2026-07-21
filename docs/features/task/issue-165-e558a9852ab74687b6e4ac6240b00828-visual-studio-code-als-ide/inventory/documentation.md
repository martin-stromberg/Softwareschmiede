# Dokumentation

## Vorhandene Dateisystem-Dokumentation

Die Dokumentation fuer `IDE oeffnen` liegt unter `docs/help/dateisystem-integration/`.

Aktueller dokumentierter Stand:

- Die Dateisystem-Integration oeffnet Arbeitsverzeichnis oder Visual-Studio-Solution aus der Aufgabendetailansicht.
- `IDE oeffnen` oeffnet eine `*.sln` mit dem OS-Standardhandler.
- Ohne Solution ist der Button deaktiviert.
- Solutions werden nur auf oberster Ebene gesucht.
- Die Anwendung prueft nicht, ob Visual Studio installiert ist.

Fundstellen:

- `docs/help/index.md:21`
- `docs/help/dateisystem-integration/index.md:5`
- `docs/help/dateisystem-integration/beschreibung.md:22`
- `docs/help/dateisystem-integration/beschreibung.md:29`
- `docs/help/dateisystem-integration/beschreibung.md:31`
- `docs/help/dateisystem-integration/beschreibung.md:51`

## Vorhandener technischer Ablauf

`docs/help/dateisystem-integration/ablauf-technisch.md` beschreibt bereits:

- Prozessstart ueber `IProzessStarter`.
- Solution-Caching beim Laden der Aufgabe.
- `OeffneIdeCommand` bei `0` Solutions als nicht erreichbar, weil Button deaktiviert.
- Prozessstart der Solution ueber `IdeOeffnenService.OeffneSolution()`.
- Testmodus mit `AufzeichnenderProzessStarter`.

Fundstellen:

- `docs/help/dateisystem-integration/ablauf-technisch.md:7`
- `docs/help/dateisystem-integration/ablauf-technisch.md:32`
- `docs/help/dateisystem-integration/ablauf-technisch.md:53`
- `docs/help/dateisystem-integration/ablauf-technisch.md:57`
- `docs/help/dateisystem-integration/ablauf-technisch.md:78`
- `docs/help/dateisystem-integration/ablauf-technisch.md:151`

## Vorhandene Settings-Dokumentation

Die Settings-Dokumentation liegt unter `docs/help/einstellungen/`.

`ablauf-technisch.md` beschreibt:

- Laden der Settings via `SettingsViewModel.LadenAsync()`.
- Lesen globaler App-Einstellungen ueber `AppEinstellungService`.
- Rendering dynamischer Plugin-Settings.
- Speichern ueber `SettingsViewModel.SpeichernAsync()`.
- Weitere globale Einstellungen wie Arbeitsverzeichnis und Design.

Fundstellen:

- `docs/help/einstellungen/ablauf-technisch.md:7`
- `docs/help/einstellungen/ablauf-technisch.md:15`
- `docs/help/einstellungen/ablauf-technisch.md:18`
- `docs/help/einstellungen/ablauf-technisch.md:68`
- `docs/help/einstellungen/ablauf-technisch.md:99`
- `docs/help/einstellungen/ablauf-technisch.md:122`

## Dokumentationsbedarf nach Umsetzung

Folgende Hilfe-Dokumente sollten nach der Implementierung aktualisiert werden:

- `docs/help/dateisystem-integration/beschreibung.md`: Verhalten ohne Solution von "Button deaktiviert" auf "optional VS-Code-Fallback" erweitern.
- `docs/help/dateisystem-integration/ablauf-anwender.md`: Anwenderablauf fuer aktivierten Fallback und Hinweis bei nicht gefundenem VS Code ergaenzen.
- `docs/help/dateisystem-integration/ablauf-technisch.md`: neue Verzweigung nach `0` Solutions, Settings-Pruefung, VS-Code-Erkennung und Prozessstart dokumentieren.
- `docs/help/dateisystem-integration/architektur.md`: neue Methode/Klasse fuer VS-Code-Resolver bzw. Fallback ergaenzen.
- `docs/help/einstellungen/beschreibung.md` und `docs/help/einstellungen/ablauf-anwender.md`: neue Checkbox im Allgemein-Tab dokumentieren.
- `docs/help/einstellungen/datenmodell.md` oder `installation.md`: neuen `AppEinstellung`-Key und Default `false` aufnehmen, falls diese Dokumente Key-Tabellen fuehren.
- `docs/help/index.md`: Kurzbeschreibung der Dateisystem-Integration um VS-Code-Fallback ergaenzen.
