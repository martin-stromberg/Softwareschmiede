# Übersetzte Anforderung: Versionsbereiche und ehrliche Fehlermeldung in der SCM-CLI-Erkennung

## Fachliche Zusammenfassung

Die CLI-Erkennung des SCM-Antwortkanals in `ScmPromptSessionAdapter.RecognizeStartup` akzeptiert derzeit nur exakt gepinnte Versionszeichenfolgen (`OpenAI Codex (v0.114.0)`, `Claude Code v2.1.0`, `Devin CLI` + `v3000.11.92`). Nach einem automatischen Versionsupdate einer unterstützten CLI bleibt `_recognizedCli` dauerhaft `false`, `ReadyStatus` bleibt `Unknown`, die Kanalprobe (`SendAccessProbeAsync`/`BuildAccessProbePrompt`) wird nie gesendet und eventgetriebene SCM-Ausführungen hängen im Status `Wartend` mit irreführendem Grund. Gefordert ist eine Erkennung über unterstützte Versionsbereiche pro CLI-Promptformat sowie — bei unbekannter CLI oder erkannter Version außerhalb aller Bereiche — eine verständliche, ehrliche Fehlermeldung, die die tatsächliche Ursache benennt, statt der bisherigen Pauschaltexte „Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen." bzw. „CLI beschäftigt; wartet auf Eingabebereitschaft."

## Betroffene Klassen und Komponenten

- `src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`
  - `RecognizeStartup` (Zeilen 272–281): Umstellung von exakten `Contains`-Prüfungen auf Erkennung mit Versionsbereichen; die erkannte Version muss dabei extrahiert und gemerkt werden.
  - `_recognizedCli` (Zeile 81): Der binäre Zustand reicht nicht mehr aus — es muss mindestens zwischen „CLI/Promptformat erkannt und unterstützt", „CLI erkannt, aber Version außerhalb aller unterstützten Bereiche" und „CLI nicht erkannt/unbekannt" unterschieden werden (inkl. der tatsächlich erkannten Versionszeichenfolge für die Fehlermeldung).
  - `RefreshScreen` (Zeilen 283–319): `if (!_recognizedCli) return;` (Zeile 291) muss auf die differenzierten Erkennungszustände erweitert werden.
  - `CheckExchangeAccessCoreAsync` (Zeilen 162–163): Der Reason-Text „CLI-Version oder Initialisierung ist nicht durch ein unterstütztes Promptformat bestätigt." soll die erkannte Version/den Erkennungszustand konkret benennen.
  - `ConfirmReady` (Zeilen 224–237): Die harte Ablehnung bei `!_recognizedCli` (Zeile 232) ist im Kontext der neuen Fehlersemantik zu überdenken (siehe Offene Fragen).
- `src/Softwareschmiede/Application/Services/ScmCliAccessEvaluator.cs`
  - Enum `ScmExchangeAccessStatus` und Record `ScmExchangeAccess` (mit `Reason`): prüfen, ob ein neuer Status bzw. erweiterte Reason-Ausprägung nötig ist oder `Unknown` mit konkretem Reason genügt.
- `IScmPromptSessionAdapter` (in `ScmPromptSessionAdapter.cs`, Zeilen 24–48): voraussichtlich neue Eigenschaft(en) für erkannte CLI-Version bzw. Erkennungszustand, damit der Orchestrator eine ehrliche Meldung erzeugen kann (Annahme — Alternative: Erweiterung von `ScmPromptReadyStatus` um einen eigenen Zustand wie `UnsupportedCli`).
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
  - `DispatchCoreAsync`, Zeilen 173–185: Neue Verzweigung für den Fall „CLI erkannt, aber nicht unterstützte Version" / „CLI unbekannt" mit Grundtext, der CLI-Name und erkannte Version nennt; die bestehenden Texte bleiben für die Fälle `TrustPrompt`/`Busy`/`Unknown`-Bereitschaft *bei unterstützter* CLI erhalten.
  - `ConfirmReadyAsync` (Zeilen 269–285): Fehlertext „CLI ist beschäftigt oder kann noch nicht bestätigt werden." ggf. an die neue Semantik anpassen.
- `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`
  - `ScmEventQueueItem.CanConfirmReady` (Zeile 333): Die Sichtbarkeit des Buttons „Eingabebereitschaft bestätigen" (`ScmEventConfirmReady` in `ScmEventQueueView.xaml`, Zeilen 117–118) ist aktuell per Textabgleich an den Grund „Eingabebereitschaft unbekannt" gekoppelt und muss auf die neue Grund-Semantik abgestimmt werden.
- Neu zu erstellendes Artefakt (Vorschlag/Annahme): eine Beschreibung der unterstützten Promptformate mit Versionsbereichen pro `PluginPrefix` — z. B. eine statische Profil-/Deskriptorklasse (Arbeitsname z. B. `ScmCliPromptProfile` o. ä.) im Bereich `Application/Services`, die Banner-/Versionsmuster und zulässige Versionsbereiche kapselt, statt der Inline-Strings in `RecognizeStartup`. Der Simulator-Vertrag (`SCM_SIMULATOR_PROTOCOL_V1`) bleibt davon unberührt.
- Tests:
  - `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`: `ScmKiProfile`/`Devin`-Profil (Banner „Devin CLI\nv3000.11.92", Zeile 31) um abweichende Versionen im Rahmen erweitern.
  - `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.cs` (+ `.PendingProbe.cs`, `.RestrictedProcess.cs`): InlineData-Banner mit gepinnten Versionen durch Fälle innerhalb/außerhalb des Bereichs ergänzen; neue Fälle für ehrliche Fehlermeldung und `ConfirmReady`-Verhalten.
  - `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` und `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Devin.cs`: Warte-Grund-Assertions anpassen/erweitern.
  - `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`: Assertions auf `WaitReason` „Eingabebereitschaft unbekannt" (u. a. Zeilen 191, 394, 406, 421) und auf den `ScmEventConfirmReady`-Button prüfen/anpassen.

## Implementierungsansatz

- Zentraler Eingriffspunkt ist `ScmPromptSessionAdapter.RecognizeStartup`: Statt exakter Zeichenfolgen wird pro `PluginPrefix` ein Versionsmuster (z. B. regulärer Ausdruck auf dem Banner-/Versions-Token der Startausgabe) gegen eine definierte Menge unterstützter Versionsbereiche geprüft. Die Extraktion der tatsächlichen Versionszeichenfolge ist Teil der Erkennung, damit sie in Fehlermeldungen benannt werden kann.
- Der Erkennungszustand wird von `_recognizedCli` (bool) auf einen dreiwertigen Zustand erweitert (Annahme): „nicht erkannt", „erkannt und unterstützt", „erkannt, aber nicht unterstützte Version" — inklusive der erkannten Version. `RefreshScreen` setzt `ReadyStatus` nur bei unterstütztem Format; die anderen Zustände erzeugen keinen `Ready`-Status.
- Die Fehlermeldung entsteht im Zusammenspiel von Adapter und Orchestrator: Der Adapter stellt Erkennungszustand und erkannte Version bereit (z. B. über `IScmPromptSessionAdapter` oder im `Reason` von `CheckExchangeAccessAsync`); `ScmEventOrchestrator.DispatchCoreAsync` bildet daraus einen neuen, ursachenehrlichen Warte-Grund (z. B. sinngemäß „Devin CLI v3000.11.205 wird nicht unterstützt" bzw. „CLI-Promptformat unbekannt"), der in der Ereignisqueue sichtbar wird und via `WaitWithCommentAsync` ggf. auch als Warte-Kommentar an die SCM-Referenz geht.
- Abhängigkeiten: `ScmCliAccessEvaluator.Evaluate` (Startvertrag) und `ScmEventExchangeProtocol` (Kanalprobe) bleiben unverändert; die Bereichsprüfung betrifft ausschließlich die Transkript-Erkennung. `ScmCliWorkspaceTrust` und `TrustDialogMarkers` sind unberührt.
- Konsistenzverankerung im UI: Ändert sich der Grundtext, muss `CanConfirmReady` (und damit der `ScmEventConfirmReady`-Button) sowie die E2E-Assertions darauf angepasst werden, damit die Bestätigung bei den Fällen angeboten bleibt, für die sie gedacht ist.
- Dokumentation: `docs/help/plugins/github-ereignisse/architektur.md` (Zeile 20) und `ablauf-technisch.md` (Zeile 35) listen die gepinnten Versionen und müssen auf die Bereichslogik umgestellt werden; `troubleshooting.md` und `beschreibung.md` sind um den neuen Fehlerfall zu ergänzen.

## Konfiguration

- Keine Anwenderkonfiguration geplant: Die unterstützten Versionsbereiche gehören wie bisher die gepinnten Strings zum festen, getesteten Promptformat-Vertrag je CLI (Bestandskommentar in `RecognizeStartup`: „New/changed formats require a fixture."). Die Bereiche erweitern lediglich den zulässigen Patch-/Minor-Spielraum innerhalb des unveränderten Transkriptformats.
- Annahme: Bereiche werden im Code gepflegt (pro `PluginPrefix`), nicht über `AppEinstellungService`/`scm.*`-Schlüssel. Eine Laufzeit-Konfigurierbarkeit wäre möglich, ist aber aus der Anforderung nicht ableitbar und würde die Sicherheitsgarantie des geprüften Startvertrags aufweichen — ggf. als offene Frage mit dem Kunden klären.

## Offene Fragen

1. **Bereichsgrenzen:** Welche Versionsbereiche gelten pro CLI als kompatibel — z. B. bei Devin `v3000.11.x` (beliebiger Patch) oder breiter? Die Anforderung nennt kein Schema; Vorschlag: gleiches Major.Minor mit beliebigem Patch bzw. explizit je CLI dokumentierte Untergrenze/Obergrenze.
2. **Manuelle Bestätigung (`ConfirmReady`):** Darf der Anwender eine *erkannte, aber außerhalb des Bereichs liegende* CLI manuell bestätigen (er verifiziert das sichtbare Promptformat selbst), oder bleibt die Bestätigung hart gesperrt und nur ein Versions-Update/Downgrade bzw. eine Anwendungsaktualisierung löst den Fall? Die Anforderung verlangt die Berücksichtigung der bisherigen harten Ablehnung, legt die Zielentscheidung aber nicht fest.
3. **Unbekannte CLI vs. bekannte CLI mit unbekannter Version:** Sollen beide Fälle denselben Grundtext erhalten oder getrennte Meldungen (bei bekannter CLI die erkannte Version benennen; bei unbekannter CLI ausdrücklich „Promptformat unbekannt")?
4. **Wiederholungsverhalten:** Soll ein Ereignis mit „nicht unterstützte Version" wartend mit automatischem Neuversuch bleiben (die CLI könnte zwischenzeitlich aktualisiert/zurückgesetzt worden sein) oder bis zur ausdrücklichen Prüfung gesperrt werden (`NextAttemptUtc = long.MaxValue`, wie bei anderen Konfigurationsfehlern)?
5. **Erkennungsfenster:** `_startupOutput` sammelt maximal 32 KiB (Zeile 267). Reicht diese Grenze, damit aktualisierte Banner sicher erkannt werden, oder muss die Erkennung zusätzlich zeitlich/auf Pufferbasis (`RefreshScreen`) verlässlich greifen, wenn das Banner erst spät oder gar nicht erscheint (dann: Meldung „CLI-Version konnte nicht ermittelt werden")?
6. **Version-Parsing:** Welche Versionsformate müssen zusätzlich toleriert werden (Suffixe wie `-beta`, Aufbauten `+build`, fehlendes `v`-Präfix)? Vorschlag: nur das dokumentierte Format des jeweiligen Banners plus einfache Patch-Variation; alles andere bleibt „unbekannt" mit ehrlicher Meldung.
