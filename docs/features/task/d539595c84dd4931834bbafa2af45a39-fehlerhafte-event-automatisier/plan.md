# Umsetzungsplan: Versionsbereiche und ehrliche Fehlermeldung in der SCM-CLI-Erkennung

Anforderung: [`requirement.md`](requirement.md) · Bestandsaufnahme: [`inventory.md`](inventory.md)

## Übersicht

Die CLI-Erkennung in `ScmPromptSessionAdapter.RecognizeStartup` wird von exakt gepinnten `Contains`-Prüfungen auf eine profilbasierte Erkennung mit Versionsbereichen umgestellt. Ein neuer statischer Deskriptor `ScmCliPromptProfile` kapselt pro `PluginPrefix` Banner-Anker, Versionsextraktion und die zulässigen Versionsbereiche. Der binäre Zustand `_recognizedCli` wird durch einen dreiwertigen Erkennungszustand `ScmCliRecognition` (`Unknown` / `Unsupported` / `Supported`) samt erkannter Versionszeichenfolge ersetzt und über `IScmPromptSessionAdapter.CliRecognition` nach außen gegeben. `ScmEventOrchestrator.DispatchCoreAsync` bildet daraus einen ursachenehrlichen Warte-Grund, der CLI-Name und erkannte Version benennt; die Sichtbarkeit des Bestätigen-Buttons `ScmEventConfirmReady` wird über eine geteilte Textkonstante an die neue Grund-Semantik gekoppelt. Keine Datenbankmigration, keine Anwenderkonfiguration; der Simulator-Vertrag `SCM_SIMULATOR_PROTOCOL_V1` bleibt unverändert.

## Designentscheidungen

| Komponente | Ansatz | Begründung |
|---|---|---|
| Versionsbereiche / Bannermuster | Neue statische Katalogklasse `ScmCliPromptProfile` (`Application/Services`) mit einem Record je `PluginPrefix`: `BannerAnchor` (Literal), `VersionPattern` (Regex mit `version`-Gruppe), `SupportedRanges` (Liste von `ScmCliVersionRange`), `DisplayName`, `SupportedVersionsText` | Anforderung schlägt genau dieses Artefakt vor; zentralisiert den Promptformat-Vertrag, den der Bestandskommentar („New/changed formats require a fixture") beschreibt, und macht Bereiche je CLI explizit dokumentierbar statt verstreuter Inline-Strings. Alternative „Inline-Regex in `RecognizeStartup`" verworfen, weil Version und Bereich dann nicht für die Fehlermeldung wiederverwendbar wären. |
| Erkennungszustand | Neues Enum `ScmCliRecognitionState` (`Unknown`, `Unsupported`, `Supported`) + Record `ScmCliRecognition(State, CliName, Version, SupportedVersionsText)`; **kein** neuer `ScmPromptReadyStatus`-Wert | `ScmPromptReadyStatus` beschreibt die dynamische Prompt-Bereitschaft (Bildschirmzustand), die Erkennung die statische Sitzungsidentität — zwei verschiedene Achsen. Ein `UnsupportedCli`-Wert in `ReadyStatus` würde `WaitForInputReadyAsync`-Semantik und alle Busy/Ready-Verzweigungen verwässern. `CliName == null` kodiert „kein Profil / unbekannte CLI" und unterscheidet damit „CLI unbekannt" von „bekannte CLI, Banner/Version noch nicht erkannt". |
| Versionsbereichsform | `ScmCliVersionRange(Version Min, Version MaxExclusive)`; initial je CLI genau ein Bereich „gleiches Major.Minor, beliebiger Patch": Codex `[0.114.0, 0.115.0)`, Claude `[2.1.0, 2.2.0)`, Devin `[3000.11.0, 3000.12.0)` | Entspricht der Empfehlung zu Offener Frage 1: Patch-/Hotfix-Updates ändern das Transkriptformat erfahrungsgemäß nicht, Minor-/Major-Sprünge bleiben bewusst Fixture-pflichtig (Sicherheitsgarantie des geprüften Startvertrags). Explizite Bereiche statt Major.Minor-Regel erlauben spätere Abweichungen je CLI ohne Strukturwechsel. |
| Simulator-Erkennung | Simulator erhält einen Katalogeintrag (`BannerAnchor` `SCM_SIMULATOR_PROTOCOL_V1`, **ohne** `VersionPattern`/Bereiche); die Zusatzbedingung `_access.Status ∈ {Supported, Denied}` bleibt im Adapter | Der Protokollvertrag ist unversioniert und laut Anforderung unberührt; die Katalogmitgliedschaft liefert lediglich `CliName` („KI-Simulator") für ehrliche Meldungen, wenn das Banner fehlt. |
| Ehrliche Grundtexte | Neue statische Klasse `ScmCliWaitReason` mit `Describe(ScmCliRecognition)` (liefert den fallabhängigen Grundtext), Konstante `UnknownReady` (bisheriger Text unverändert), Konstante `ConfirmableMarker` (Suffix „Für die angezeigte CLI-Sitzung bestätigen.") und `IsConfirmable(string?)` | Grundtexte werden an zwei Stellen benötigt (Orchestrator-Wartegrund, Adapter-`access.Reason`) und für die Button-Kopplung ein drittes Mal ausgewertet — eine einzige getestete Textquelle verhindert Drift. Persistiertes Flag (`ScmIncomingEvent`-Spalte) als Alternative verworfen: vermeidet eine Migration; `Reason` bleibt alleinige Transportquelle wie bisher. |
| Unterscheidung der Wartegründe | `TrustPrompt`- und `Busy`-Texte bleiben; `Supported` + kein Prompt → unverändert `UnknownReady` (bestätigbar); `Unsupported` → Grund mit CLI-Name + Version + unterstütztem Bereich; `Unknown` mit `CliName` → „Banner/Version nicht erkannt"-Grund; `Unknown` ohne `CliName` → „Promptformat unbekannt"-Grund | Trennung der Fälle (Offene Frage 3, Empfehlung: getrennte Meldungen). Nur bestätigbare Gründe tragen den `ConfirmableMarker`; nicht bestätigbare Zustände zeigen ehrlich keinen Button mehr, statt in die heutige Ablehnungsschleife zu laufen. |
| `ConfirmReady` bei `Unsupported` | **Empfehlung (Offener Punkt 2):** Bestätigung zulassen; sie setzt neben `_manuallyConfirmed` ein lauffgebundenes `_formatUserConfirmed`, das die Erkennungs-Vorbedingung in `CheckExchangeAccessCoreAsync` ersetzt — andere Gates (Lebendigkeit, unveränderte Konfiguration, Kanalpfad, Dateitool-Probe) bleiben unverändert | Die tatsächliche Sicherheitsgrenze ist die Dateitool-Kanalprobe, nicht die Version; die manuelle Bestätigung ist der bestehende, ausdrückliche „Anwender verifiziert sichtbares Prompt"-Mechanismus und löst den geschilderten Stillstand (CLI-Auto-Update → hängende Ereignisse) ohne App-Update auf. `Unknown` bleibt hart gesperrt, weil eine Bestätigung gegen ein völlig unbekanntes Format keine verifizierbare Aussage trägt. |
| `ScmExchangeAccessStatus` | Kein neuer Wert; `Unknown` + konkreter `Reason` genügt | `Unknown` trägt bereits sämtliche „nicht sicher nachweisbar"-Fälle; ein eigener Status hätte keine weitere Verzweigung zur Folge (Orchestrator behandelt `!= Supported` einheitlich). |
| Konfigurierbarkeit der Bereiche | Bereiche im Code je `PluginPrefix`, kein `AppEinstellungService`-Schlüssel | Fortführung der Bestandsentscheidung (Promptformat-Vertrag ist Code + Fixture); Laufzeitkonfiguration würde die Sicherheitsgarantie aufweichen. |

## Programmabläufe

### Ablauf 1: Erkennung der CLI-Startausgabe (Adapter)

Beteiligte Klassen: `ScmPromptSessionAdapter`, `ScmCliPromptProfile`, `ScmCliVersionRange`, `ScmCliPromptMatch`, `ScmCliRecognition`.

1. `ScmPromptSessionAdapter`-Konstruktor initialisiert `_cliRecognition` anhand von `ScmCliPromptProfile.TryGet(PluginPrefix)`: Profil vorhanden → `Unknown` mit `CliName` aus dem Profil; kein Profil → `Unknown` mit `CliName = null` (terminaler Zustand „CLI unbekannt").
2. `OnOutput` (Rohbytes ≤ 32 KiB) und `RefreshScreen` (Bildschirmpuffer) rufen weiterhin `RecognizeStartup(text)` auf.
3. `RecognizeStartup` fragt `ScmCliPromptProfile.TryGet(PluginPrefix)`:
   - Kein Profil → Zustand bleibt `Unknown` (kein Banner kann je matchen).
   - Profil ohne Versionsmuster (Simulator) → `text.Contains(BannerAnchor)` **und** `_access.Status ∈ {Supported, Denied}` → `Supported` (mit `CliName`, `Version = null`).
   - Profil mit Versionsmuster → `BannerAnchor` im Text gefunden → `VersionPattern` extrahiert das Versions-Token: gefunden und in einem `SupportedRanges`-Eintrag → `Supported` + erkannte Version; gefunden und außerhalb aller Bereiche → `Unsupported` + erkannte Version; Anker gefunden, aber kein parsebares Versions-Token → `Unsupported` mit `Version = null` („Version nicht ermittelbar"). Anker nicht gefunden → bisheriger Zustand bleibt (Banner kann später erscheinen).
4. Der einmal ermittelte Zustand `Supported`/`Unsupported` ist klebrig: Scrollt das Banner aus dem sichtbaren Puffer, wird er nicht auf `Unknown` zurückgesetzt; ein erneut gefundener Anker mit anderem Token aktualisiert den Zustand.

### Ablauf 2: Dispatch mit ehrlichem Wartegrund (Orchestrator)

Beteiligte Klassen: `ScmEventOrchestrator`, `IScmPromptSessionAdapter`, `ScmCliWaitReason`, `ScmIncomingEvent`, `ScmEventQueueItem`.

1. `DispatchCoreAsync` erhält nach `adapter.WaitForInputReadyAsync(ct)` einen Status `!= Ready`.
2. Grundwahl in Reihenfolge: `TrustPrompt` → bisheriger Vertrauens-Text; `adapter.IsBusy` → „CLI beschäftigt; wartet auf Eingabebereitschaft."; `adapter.CliRecognition.State != Supported` → `ScmCliWaitReason.Describe(adapter.CliRecognition)`; sonst → `ScmCliWaitReason.UnknownReady` („Eingabebereitschaft unbekannt. …").
3. `Describe` liefert:
   - `Unsupported` + Version: „<CliName> <Version> wird nicht unterstützt; unterstützt ist <SupportedVersionsText>. …" — mit `ConfirmableMarker`-Suffix, sofern Offener Punkt 2 wie empfohlen umgesetzt wird.
   - `Unsupported` ohne Version: „<CliName>-Version konnte nicht ermittelt werden; …" (+ Marker wie oben).
   - `Unknown` + `CliName`: „<CliName>: in der Startausgabe wurde kein unterstütztes Versionsbanner erkannt; die CLI initialisiert möglicherweise noch oder nutzt ein unbekanntes Ausgabeformat. CLI regulär neu starten oder erneut prüfen." — **ohne** Marker.
   - `Unknown` ohne `CliName`: „Das Promptformat der gestarteten CLI ist unbekannt; Eingabebereitschaft und Antwortkanal können nicht geprüft werden." — **ohne** Marker.
4. `WaitWithCommentAsync` persistiert `Status = "Wartend"` + `Reason` (unverändert); der Remote-`waiting`-Kommentar bleibt der generische Text ohne Grund — keine Versionsdaten verlassen die Anwendung.
5. `ScmEventQueueItem.CanConfirmReady` wertet `Reason` über `ScmCliWaitReason.IsConfirmable` aus (Marker-Suffix statt Volltext-`Contains`): Button erscheint nur bei bestätigbaren Gründen.
6. `ConfirmReadyAsync` ruft `session.ConfirmReady` unverändert auf; der Adapter lehnt nur noch `Unknown`-Erkennung sowie `Busy`/`TrustPrompt` ab (bei Empfehlung zu Offenem Punkt 2). Der Fehlertext bei Ablehnung benennt den tatsächlichen Grund aus `CliRecognition` (z. B. „Das Promptformat der CLI-Sitzung ist unbekannt und kann nicht bestätigt werden.").
7. Zugestellte Ereignisse, Reconcile, Resend, Cancel: unverändert.

### Ablauf 3: Kanalzugriff nach manueller Bestätigung (nur bei Empfehlung Offener Punkt 2)

Beteiligte Klassen: `ScmPromptSessionAdapter`, `ScmEventExchangeProtocol`, `ScmCliAccessEvaluator`.

1. `ConfirmReady(runId)` bei `State == Unsupported`: setzt `_manuallyConfirmed = true` **und** `_formatUserConfirmed = true` (laufgebunden; wird von `MarkInputStarted` **nicht** zurückgesetzt — verworfen wird nur die aktuelle Prompt-Bereitschaft, nicht die Format-Verifikation des Laufs).
2. `CheckExchangeAccessCoreAsync` ersetzt die bisherige `!_recognizedCli`-Bedingung durch `State != Supported && !_formatUserConfirmed` → `Unknown` + `ScmCliWaitReason.Describe(...)`.
3. Nach Bestätigung erreicht der Pfad erstmals die Kanalprobe: `SendAccessProbeAsync` sendet `BuildAccessProbePrompt`, die Dateitool-Probe läuft wie bei unterstützten Versionen; der Nachweis schlägt weiter fehl, wenn die neue CLI-Version die Probe nicht ausführt — dann bleibt der (jetzt ehrliche) Probe-Wartegrund bestehen.
4. CLI-Neustart verwirft `_formatUserConfirmed` (neues Adapter-Objekt pro Lauf) — die Verifikation gilt nie über einen Lauf hinaus.

## Neue Klassen

| Klasse/Typ | Datei | Zweck |
|---|---|---|
| `ScmCliPromptProfile` | `src/Softwareschmiede/Application/Services/ScmCliPromptProfile.cs` | Statischer Katalog unterstützter Promptformate je `PluginPrefix` (`TryGet`, `TryMatch`); kapselt `BannerAnchor`, `VersionPattern`, `SupportedRanges`, `DisplayName`, `SupportedVersionsText` |
| `ScmCliVersionRange` | ebenda | Record `Min`/`MaxExclusive` mit `Contains(Version)` |
| `ScmCliPromptMatch` | ebenda | Ergebnis von `TryMatch`: `Profile`, `Version` (null bei nicht parsebarem Token), `IsSupported` |
| `ScmCliRecognition` + `ScmCliRecognitionState` | ebenda | Dreiwertiger Erkennungszustand (`Unknown`/`Unsupported`/`Supported`) mit `CliName` (null = kein Profil) und erkannter `Version` |
| `ScmCliWaitReason` | `src/Softwareschmiede/Application/Services/ScmCliWaitReason.cs` | Einzige Quelle der Warte-/Access-Grundtexte: `UnknownReady`, `ConfirmableMarker`, `Describe(ScmCliRecognition)`, `IsConfirmable(string?)` |

## Änderungen an bestehenden Klassen

### `IScmPromptSessionAdapter` (`src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`, Zeilen 24–48)

- Neue Eigenschaft `ScmCliRecognition CliRecognition { get; }` — „Erkannter CLI-Typ und Versionszustand des gebundenen Laufs." Einzige Implementierung `ScmPromptSessionAdapter`; keine Mocks des Interfaces im Repo.

### `ScmPromptSessionAdapter` (`src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`)

- `_recognizedCli` (Zeile 81) → `_cliRecognition` (`ScmCliRecognition`, Initialisierung im Konstruktor via `ScmCliPromptProfile.TryGet`); neues Feld `_formatUserConfirmed` (nur bei Empfehlung Offener Punkt 2).
- Neue öffentliche Eigenschaft `CliRecognition` (Interface-Implementierung).
- `RecognizeStartup` (Zeilen 272–281): Umsetzung von Ablauf 1 Schritt 3; Inline-Strings entfallen zugunsten des Profilkatalogs; der Kommentar zum Fixture-Vertrag bleibt sinngemäß erhalten.
- `RefreshScreen` (Zeile 291): `if (!_recognizedCli) return;` → `if (_cliRecognition.State != ScmCliRecognitionState.Supported) return;`. `ReadyStatus` bleibt damit bei nicht unterstütztem Format `Unknown` — keine neuen `ScmPromptReadyStatus`-Werte.
- `CheckExchangeAccessCoreAsync` (Zeilen 162–163): Bedingung `!_recognizedCli` → `_cliRecognition.State != Supported && !_formatUserConfirmed`; `Reason` aus `ScmCliWaitReason.Describe(_cliRecognition)` statt Pauschaltext.
- `ConfirmReady` (Zeile 232): Ablehnung nur bei `State == Unknown` bzw. `Busy`/`TrustPrompt`; bei `State == Unsupported` zusätzlich `_formatUserConfirmed = true` (nur Empfehlung Offener Punkt 2; andernfalls bleibt die Ablehnung für `State != Supported` bestehen). Kommentar zur Bestätigungssemantik aktualisieren.
- Klassen-Doc (Zeilen 50–53): unbekannte vs. nicht unterstützte Version unterscheiden.

### `ScmEventOrchestrator` (`src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`)

- `DispatchCoreAsync` (Zeilen 173–181): Grundwahl um den `CliRecognition`-Zweig nach Ablauf 2 Schritt 2–3 erweitern; bisherige Texte über `ScmCliWaitReason.UnknownReady`/`IsBusy` beziehen.
- `ConfirmReadyAsync` (Zeile 279): Fehlertext um den konkreten Erkennungsgrund ergänzen (z. B. Anhängen von `ScmCliWaitReason.Describe(session.CliRecognition)` bei Ablehnung wegen `Unknown`-Erkennung).

### `ScmEventQueueItem` (`src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs`, Zeile 333)

- `CanConfirmReady`: `Reason?.Contains("Eingabebereitschaft unbekannt", …)` → `ScmCliWaitReason.IsConfirmable(Reason)` (Marker-Suffix). Sichtbarkeit des `ScmEventConfirmReady`-Buttons (`ScmEventQueueView.xaml`, Zeilen 117–118) folgt damit der tatsächlichen Bestätigbarkeit.

### `ScmKiProfile` (`src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs`, Zeilen 29–31)

- `Devin(arguments, onWritePrompt)` → optionaler `banner`-Parameter (Default `Devin CLI\nv3000.11.92`), damit abweichende Versionen im Fixture darstellbar sind; `Screen` nutzt `KiProfile.Banner` bereits parametrisiert.

### `KiSimulatorPlugin` (`plugins/Softwareschmiede.Plugin.KiSimulator/KiSimulatorPlugin.cs`, Zeile 129)

- Neues Fixture-Gate (z. B. `unknown-banner`): Bei vorhandener Steuerdatei wird die Startzeile `SCM_SIMULATOR_PROTOCOL_V1` im SCM-Fixture-Modus nicht ausgegeben — Test-Enabler für den E2E-Nachweis „Banner nicht erkannt → ehrlicher Grund, kein Bestätigen-Button". Der Produktivvertrag bleibt unverändert (Gate greift nur bei gesetztem Fixture-Verzeichnis, gleiches Muster wie `unknown-ready`, `fail-start`, `read-only`).

## Datenbankmigrationen

Keine. Der ehrliche Grund reist im vorhandenen `ScmIncomingEvent.Reason`-Feld; die Button-Kopplung nutzt einen geteilten Textmarker statt einer neuen Spalte.

## Validierungsregeln

- Versionsextraktion strikt auf das dokumentierte Banner-Format je Profil (`v`-Präfix wie im Banner; nur `Major.Minor.Patch` numerisch); Suffixe (`-beta`, `+build`) oder fehlende Segmente gelten als „nicht parsebar" → `Unsupported` mit `Version = null` (ehrliche Meldung statt Raterei). Keine Toleranz über das Bannerformat hinaus (Offene Frage 6, Empfehlung).
- `ScmCliVersionRange.Contains` vergleicht `System.Version` numerisch (kein String-Vergleich).

## Konfigurationsänderungen

Keine. Versionsbereiche werden im Code je `PluginPrefix` gepflegt (kein `scm.*`-Schlüssel).

## Seiteneffekte und Risiken

- **Remote-Sichtbarkeit:** `WaitWithCommentAsync` postet weiterhin nur den generischen Wartehinweis; Versions-/CLI-Details bleiben lokal — kein Datenabfluss.
- **Polling-Verhalten:** Die neuen Wartegründe setzen `NextAttemptUtc` nicht (Empfehlung Offene Frage 4: normaler `Wartend` ohne `long.MaxValue`-Sperre), sodass ein regulärer CLI-Neustart oder ein Versions-Downgrade ohne manuellen Eingriff aufgegriffen wird; Kosten = ein erneuter Erkennungslauf pro Poll — wie bei allen übrigen Wartegründen.
- **`WaitForInputReadyAsync`:** Bei terminalem `Unsupported`/profil-losem `Unknown` wartet die Methode weiterhin die gebundene ~2-s-Pollfrist; ein Early-Return auf terminale Zustände ist eine optionale Mikro-Optimierung, nicht erforderlich.
- **`WritePromptAsync`:** `InvalidOperationException(access.Reason)` landet weiterhin im generischen Catch (`NextAttemptUtc = long.MaxValue`) — unverändertes Verhalten für Schreibzeitpunkt-Prüfungen.
- **Bestehender Bestätigungspfad bleibt:** Der `unknown-ready`-Simulatorpfad (`Supported`-Erkennung ohne `SCM_READY_V1`) erzeugt weiterhin exakt „Eingabebereitschaft unbekannt. Für die angezeigte CLI-Sitzung bestätigen." — E2E-Assertions (Zeilen 191, 394, 406, 421) und der `ConfirmReady`-Erfolg bleiben gültig.
- **Ehrlichkeits-Nebeneffekt:** Bei profil-losen CLIs (z. B. GitHub Copilot, Shells) erscheint der Bestätigen-Button nicht mehr — das beendet die bisherige „Button sichtbar, Bestätigung wird trotzdem abgelehnt"-Schleife zugunsten der ehrlichen Meldung. Gewollte Verhaltensänderung.
- **`_formatUserConfirmed` (nur Empfehlung Offener Punkt 2):** Erweitert die manuelle Bestätigung um eine Format-Verifikation; Restrisiko: Anwender bestätigt eine semantisch abweichende CLI-Version — die Dateitool-Probe bleibt die harte Grenze, `MarkInputStarted` setzt die Bereitschaft weiter zurück.
- **Formatierung:** `dotnet format Softwareschmiede.slnx --verify-no-changes` muss sauber bleiben (Pre-Push-Hook/CI).

## Umsetzungsreihenfolge

1. **Neue Typen anlegen** (`ScmCliPromptProfile.cs`, `ScmCliWaitReason.cs` mit allen neuen Records/Enums/Konstanten). Voraussetzung: keine — reine Neuanlage.
2. **Adapter umstellen** (`IScmPromptSessionAdapter.CliRecognition`, `_cliRecognition`, `RecognizeStartup`, `RefreshScreen`, `CheckExchangeAccessCoreAsync`, `ConfirmReady`, ggf. `_formatUserConfirmed`). Voraussetzung: Schritt 1.
3. **Orchestrator umstellen** (`DispatchCoreAsync`-Grundwahl, `ConfirmReadyAsync`-Fehlertext). Voraussetzung: Schritt 2 (Interface-Eigenschaft).
4. **ViewModel umstellen** (`CanConfirmReady` → `ScmCliWaitReason.IsConfirmable`). Voraussetzung: Schritt 1.
5. **Test-Fixture erweitern** (`ScmKiProfile.Devin` `banner`-Parameter). Voraussetzung: keine; parallel zu 2–4 möglich.
6. **Unit-/Integrationstests ergänzen und anpassen** (siehe Abschnitt Tests). Voraussetzung: Schritte 2–5.
7. **Simulator-Gate `unknown-banner` + E2E-Abschnitt** in `E2E_ScmEvents`. Voraussetzung: Schritte 2–4.
8. **Hilfe-Dokumente aktualisieren** (`architektur.md` Zeile 20, `ablauf-technisch.md` Zeile 35, `troubleshooting.md`, `beschreibung.md`; Passagen mit „unbekannte Versionen" in `installation.md`/`ablauf-anwender.md` an die Bereichslogik angleichen). Voraussetzung: Schritte 1–4.
9. **Vollbuild + Tests:** `dotnet build Softwareschmiede.slnx -c Debug`, dann `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`, analog `Softwareschmiede.IntegrationTests`, danach die OsInterface-Lanes (synchron, nicht im Hintergrund). Voraussetzung: Schritte 1–8.

## Tests

### Neue Tests

| Test | Datei | Zweck |
|---|---|---|
| `UnsupportedVersion_ReportsHonestReason` (Theory: Codex `v0.115.0`, Claude `v2.2.0`, Devin `v3000.12.0`/`v2999.9.9`; je außerhalb des Bereichs) | `src/Softwareschmiede.IntegrationTests/Services/ScmEventCliExchangeAccessTests.cs` | Adapter meldet `Unknown`-Zugriff mit Reason, der Version + „nicht unterstützt" benennt; `ReadyStatus` bleibt `Unknown`; kein `WritePromptAsync`-Aufruf |
| `SupportedPatchVersion_IsRecognized` (InlineData-Erweiterung: `v0.114.7`, `v2.1.15`, `v3000.11.205` in `ActualStartSpec_AllowsWorkspaceChannelWithoutAddingBypass` und einer Zeile in `.PendingProbe.cs`) | `…/ScmEventCliExchangeAccessTests.cs`, `….PendingProbe.cs` | Versionen innerhalb des Bereichs werden erkannt — Probe-/Ready-Pfad funktioniert wie bei der gepinnten Version |
| `UnparseableVersion_ReportsVersionNotDetermined` (z. B. `Devin CLI\nbeta` ohne numerisches Token) | `…/ScmEventCliExchangeAccessTests.cs` | `Unsupported` mit `Version = null` → Reason „Version konnte nicht ermittelt" |
| `UnknownCli_ReportsHonestReason` (profil-loser Prefix, z. B. `Softwareschmiede.GitHubCopilot` mit beliebigem Banner) | `…/ScmEventCliExchangeAccessTests.cs` | `CliRecognition.State == Unknown`, Reason „Promptformat unbekannt", `ConfirmReady` → `false` |
| `DevinTask_WithSupportedPatchVersion_DeliversEvent` | `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Devin.cs` | Voller Dispatch mit `v3000.11.205`-Banner → `Zugestellt` (Versionsbereich Ende-zu-Ende) |
| `DevinTask_WithUnsupportedVersion_WaitsWithHonestReason` | ebenda | `Wartend` mit Reason, der `v3000.12.x` + CLI-Namen nennt; `Prompts` leer |
| `UnknownCliFormat_WaitsWithoutConfirmOption` | `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` | Profil-loses `ScmKiProfile` → `Wartend` + „Promptformat unbekannt"-Grund; `ScmCliWaitReason.IsConfirmable(reason)` → `false`; `ConfirmReadyAsync` wirft mit ehrlichem Grund |
| `ConfirmReady_UnsupportedVersion_*` (Verhalten je Entscheidung Offener Punkt 2: bei Empfehlung → Bestätigung liefert Zustellung nach Probe; alternativ → wirft und Grund bleibt) | ebenda | Sichert die gewählte `ConfirmReady`-Semantik für `Unsupported` |
| `ScmCliPromptProfile`-Einheiten: Bereichsgrenzen (exakt Unter-/Obergrenze), fehlender Anker, nicht parsebares Token, Simulator-Match | `src/Softwareschmiede.Tests/Application/Services/` (neue Testdatei, z. B. `ScmCliPromptProfileTests.cs`) | Deskriptor-Logik isoliert prüfbar |

### Betroffene bestehende Tests

| Test | Änderung |
|---|---|
| `ScmEventCliExchangeAccessTests*` (alle `InlineData` mit gepinnten Bannern, `Banner(provider)` in `.RestrictedProcess.cs`) | Bestehende gepinnte Zeilen bleiben als In-Range-Fälle bestehen; Ergänzungen wie oben |
| `ScmEventOrchestratorTests` (Zeilen 144, 163–164) | „Eingabebereitschaft unbekannt"/„Vertrauenswürdigkeit"-Assertions bleiben gültig (Simulator-Pfad unverändert); keine Anpassung nötig, nur neue Tests |
| `ScmEventDispatchTests_Devin.DevinTask_WithAutoPermissionMode_WaitsWithoutPrompt` | unverändert (Zugriffs-Pfad, nicht Erkennungs-Pfad) |
| `E2E_ScmEvents` Zeilen 191, 394, 406, 421 | Assertions auf „Eingabebereitschaft unbekannt" bleiben gültig (`unknown-ready`-Pfad produziert denselben Text); neuer Abschnitt siehe unten |

### E2E-Tests als primärer Funktionsnachweis

Wiederverwendete UI-Muster: `fixture.Control`-Gate-Mechanismus (wie `unknown-ready`, `read-only`), `WaitReasonAsync`, `Click`/`Element`-Hilfen, `TaskDetailView.StopCli` + `CliNeustarten` (regulärer Neustart, Muster aus dem `read-only`-Abschnitt), `ScmEventRetry`/`ScmEventConfirmReady`-Button-Prüfung — alles im bestehenden, konsolidierten Test `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` (kein neuer FlaUI-Test pro Szenario, Laufzeitbindung).

| Priorität | Szenario | Testdatei | Akzeptanzkriterium | Begründung |
|---|---|---|---|---|
| 1 | CLI-Banner nicht erkannt (Gate `unknown-banner`): Ereignis wartet mit ehrlichem Grund statt „Eingabebereitschaft unbekannt"; `ScmEventConfirmReady` ist nicht sichtbar; nach regulärem CLI-Neustart ohne Gate → Erkennung → Zustellung | `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (neuer Abschnitt im bestehenden Test) | `WaitReasonAsync` trifft den neuen Grund (CLI-Name/„nicht erkannt"); `ScmEventConfirmReady` nicht per `ByAutomationId` auffindbar bzw. `IsOffscreen`; nach Neustart `Verarbeitet` mit neuer `CliRunId` | Primärer Funktionsnachweis für die ehrliche Fehlermeldung im realen UI-Fluss |
| 2 | Bestätigbarer Unbekannt-Prompt-Pfad bleibt (`unknown-ready`): Grund „Eingabebereitschaft unbekannt" + sichtbarer `ScmEventConfirmReady` + Bestätigung liefert Zustellung | ebenda (bestehende Abschnitte) | Unveränderte grüne Assertions an Zeilen 191/394/406/421 | Regressionsschutz für den bestehenden Bestätigungsfluss |
| 3 | Falls Offener Punkt 2 wie empfohlen umgesetzt: Bestätigungs-Angebot bei nicht unterstützter Version | ebenda — *nicht* über Simulator darstellbar (unversioniert); Nachweis stattdessen durch Integrationstest + `CanConfirmReady`-Marker-Test | Marker-Suffix im `Unsupported`-Grund macht `CanConfirmReady` wahr (Unit-/Integrationsebene) | Versionsbanner lassen sich über den Simulator nicht abbilden; ehrliche Benennung bleibt auf Integrationsebene bewiesen |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---|---|
| 1 | Bereichsgrenzen je CLI (welche Versionen gelten als kompatibel) | Gleiches Major.Minor, beliebiger Patch: Codex `[0.114.0, 0.115.0)`, Claude `[2.1.0, 2.2.0)`, Devin `[3000.11.0, 3000.12.0)`; Minor-/Major-Sprünge bleiben Fixture-pflichtig |
| 2 | Darf eine erkannte, aber außerhalb des Bereichs liegende CLI manuell bestätigt werden? | **Ja** (Empfehlung): `ConfirmReady` lässt `Unsupported` zu und setzt lauffgebundenes `_formatUserConfirmed`; die Dateitool-Kanalprobe bleibt als harte Sicherheitsgrenze bestehen; `Unknown` bleibt gesperrt. Kundenentscheid einholen — Alternative: harte Sperre, nur Versions-/App-Update löst den Fall |
| 3 | Unbekannte CLI vs. bekannte CLI mit unbekannter Version — gleicher oder getrennter Grundtext? | Getrennt: `Unsupported` nennt CLI-Name + erkannte Version + unterstützten Bereich; profil-loses `Unknown` meldet ausdrücklich „Promptformat unbekannt" |
| 4 | Wiederholungsverhalten bei „nicht unterstützte Version" | Normaler `Wartend` ohne `NextAttemptUtc`-Sperre — ein regulärer CLI-Neustart/-Downgrade wird automatisch aufgegriffen; keine manuelle Sperre nötig |
| 5 | Reicht das 32-KiB-`_startupOutput`-Fenster für aktualisierte Banner? | Ja: `RecognizeStartup` läuft zusätzlich auf dem aktuellen Bildschirmpuffer (`RefreshScreen`); Banner-Anker gefunden + Token nicht parsebar → „Version nicht ermittelbar"-Meldung; Banner dauerhaft unsichtbar → „nicht erkannt"-Meldung statt Pauschaltext |
| 6 | Welche Versionsformate werden toleriert (Suffixe, fehlendes `v`)? | Nur das dokumentierte Bannerformat je Profil (`Major.Minor.Patch` numerisch, `v` wie im Banner); alles andere → „nicht ermittelbar/unbekannt" mit ehrlicher Meldung |
