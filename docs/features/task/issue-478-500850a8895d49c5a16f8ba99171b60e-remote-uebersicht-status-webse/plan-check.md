# Plan-Prüfung: Remote-Übersicht über externe Status-Webseite

**Status: Nacharbeit nötig**

Geprüft am 2026-11-25 gegen `requirement.md`, `inventory.md` + `inventory/*`, `plan.md`,
`docs/features/task-issue-478-…-tasks.md`, `design-entwurf.md`, die Anwender-Entscheidungen
vom 2026-11-25 (v1 rein lesend; GitHub-Links überall; mobil kein Überragen über den rechten
Rand; alle Empfehlungen übernommen) sowie Stichproben im echten Code
(`AppSettingService.cs`, `App.xaml.cs`, `SettingsView.xaml`, `SettingsViewModel.cs`,
`Softwareschmiede.csproj`, `Softwareschmiede.slnx`, Test-csprojs, CI-Workflows,
`ScmEvents.cs`, `WindowsCredentialStore.cs`).

## Befunde

### Kritisch

1. **tasks.md enthält den ausgeschlossenen Kommentar-Aktions-Pfad als offene Aufgaben.**
   Die Anwender-Entscheidung 2026-11-25 schließt Aktionsbuttons, `CommentActionService`,
   `POST /actions/comment` und Write-Token ausdrücklich aus. `plan.md` bildet das korrekt
   ab (Scope-Ausschluss 5+9, Schritt 11 gestrichen, Szenario 6 gestrichen, Entscheidungen 3/5
   „entfällt in v1"), aber die operative Aufgabenliste wurde nicht nachgezogen:
   - Task 6: `Program.cs` soll weiterhin Endpunkt-Mapping für `/actions/comment` einrichten.
   - Task 7: `appsettings.json` soll eine Konfigurationssektion `Actions` enthalten.
   - Task 8: Options-Klassen sollen `Actions:Enabled/Templates` enthalten.
   - Task 16: `PageAccessMiddleware` soll `/actions/comment` schützen.
   - Task 17: `CommentActionService` komplett implementieren (Allowlist + GitHub-Comment-POST).
   - Task 18: `POST /actions/comment`-Endpunkt + Aktions-Buttons + Write-Token-Bedingung.
   - Task 22: `CommentAction_*`-Tests.
   Alle stehen auf „Offen" ohne „entfällt"-Markierung. Ein Implementierer, der tasks.md
   abarbeitet, baut exakt den vom Anwender ausgeschlossenen Scope (inkl. Schreib-Token,
   das die v1-Entscheidung ausdrücklich verneint).
   **Korrektur:** Tasks 17, 18 und der `CommentAction_*`-Teil von Task 22 streichen oder
   als „entfällt in v1 (rein lesend)" markieren; aus Tasks 6, 7, 8 und 16 die
   `/actions/comment`- bzw. `Actions:*`-Reste entfernen; ggf. Verweis auf den
   dokumentierten Erweiterungspfad in `plan.md` (Programmabläufe, Designentscheidung
   „Kommentar-Aktionen").

### Mittel

2. **`WebApplicationFactory`-Roundtrip: Verbindungsmechanismus nicht spezifiziert.**
   `WebApplicationFactory<Program>` nutzt `Microsoft.AspNetCore.TestHost.TestServer`
   (in-memory) — es wird **kein TCP-Port** geöffnet. Eine echte `HubConnection` klappt nur
   mit `HttpConnectionOptions.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()`;
   für den WebSocket-Transport ist zusätzlich `WebSocketFactory` mit
   `factory.Server.CreateWebSocketClient()` zu verdrahten (der Default-`ClientWebSocket`
   versucht sonst einen echten TCP-Connect auf `localhost` und scheitert) oder explizit
   `HttpTransportType.LongPolling` zu wählen. Plan (`RelayRoundtrip_*`, „hostet StatusServer
   auf Test-Port") und Tasks 21/42 nennen diesen Mechanismus nicht. In-memory TestServer
   unterstützt SignalR grundsätzlich — aber nur mit dieser Verdrahtung. Wichtig dabei:
   `RemoteStatusRelayClient` braucht einen injizierbaren `HttpConnectionOptions`-Hook
   (Handler-/WebSocket-Factory/Transportwahl), damit im `IntegrationTests`-Roundtrip der
   **produktive** Client-Code fährt und nicht ein Parallel-Client.
   **Korrektur:** In `plan.md` (Tests/Akzeptanz Szenario 1) und Tasks 21/31/42 den Mechanismus
   festlegen; Options-Hook in die Klassenbeschreibung von `RemoteStatusRelayClient` aufnehmen.

3. **`StatusServer.Tests` fehlen Paket- und csproj-Konventionen.**
   Weder plan.md Schritt 13 noch Task 19 nennen:
   - `Microsoft.AspNetCore.SignalR.Client` im **Testprojekt** — `Microsoft.AspNetCore.App`
     (Web-SDK-Framework-Referenz) enthält den Client nicht; die HubConnection im
     `RelayRoundtrip`-Test braucht das Paket explizit (die Übertragung über die
     `Softwareschmiede`-Projektreferenz hilft nur in `Softwareschmiede.IntegrationTests`,
     nicht in `StatusServer.Tests`).
   - `<IsTestingPlatformApplication>false</IsTestingPlatformApplication>` — ohne diese
     Property bricht `dotnet test` im VSTest-Modus unter .NET 10+/11 (expliziter Kommentar
     in `Softwareschmiede.IntegrationTests.csproj` und `Softwareschmiede.Tests.csproj`).
   - Versionsgleichstand `xunit.v3` 4.0.3 / `xunit.runner.visualstudio` 4.0.1 /
     `coverlet.collector` 10.1.1 / `FluentAssertions` 8.11.0 sowie
     `GenerateDocumentationFile` + `WarningsAsErrors CS1591` (CI baut mit
     `TreatWarningsAsErrors`).
   **Korrektur:** Task 19 um die vollständige PropertyGroup/PackageReference-Liste
   (Vorlage `Softwareschmiede.IntegrationTests.csproj`) und das SignalR-Client-Paket ergänzen.

4. **Verbindliche Anwender-Entscheidung „GitHub-Links auf allen referenzierten Objekten"
   hat keinen definierten Nachweis.** `Page_*`-Tests (Task 23, plan.md Tests-Tabelle) prüfen
   nur Sektionen/„keine Daten"/Basic-Auth/`status.json`; kein Akzeptanzszenario und kein
   Test assertiert, dass Issue-/PR-/Release-Zeilen als `<a href>` zur `html_url` gerendert
   werden. Dazu gehört auch: `StatusSnapshotScmEvent` hat nur „Referenzanzeige" — dabei
   liefert `ScmEventObservation.Url` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:40`)
   die fertige Remote-URL des Ereignisses; der DTO sollte die URL explizit führen, sonst
   ist die Links-Vorgabe in der SCM-Sektion nicht erfüllbar („soweit bekannt" ist hier
   unnötig zurückhaltend).
   **Korrektur:** Link-Assertion in `Page_*`/`RelayRoundtrip`-Tests und Akzeptanzszenario 1
   aufnehmen; `Url`-Feld in `StatusSnapshotScmEvent` (Task 4 / Klassenliste) ergänzen.

5. **Verbindliche Anwender-Entscheidung „mobil kein Überragen über den rechten Rand" hat
   keinen Nachweis.** Die Designentscheidung „Responsive Darstellung" (plan.md Z. 45)
   beschreibt den Ansatz, aber weder Akzeptanzszenarien noch Tests noch die
   Abnahme-Checkliste (Screenshots) verlangen einen mobilen Nachweis — obwohl genau das der
   korrigierte Entwurfs-Fehler war (Referenz `ui-entwuerfe/04-entwurf-mobil.html`).
   **Korrektur:** Mindestens (a) `Page_*`-Test, der `<meta name="viewport">` und das
   Fehlen fester Inhaltsbreiten/`nowrap`-Muster im gerenderten HTML assertiert, und
   (b) Screenshot-Beleg der Seite in mobiler Breite für `test-results.md` (Lifecycle-
   Schritt 10) in den Akzeptanzszenarien/Tasks ergänzen.

### Klein

6. **Verwaiste Voraussetzungs-Verweise auf den gestrichenen Schritt 11.** plan.md
   Umsetzungsreihenfolge Schritt 13 („Voraussetzungen: Schritt 3, 10, 11") und Schritt 16
   („Voraussetzungen: Schritt 10, 11") verweisen auf den als „entfällt" markierten Schritt 11.
   **Korrektur:** Verweise auf „Schritt 10" (bzw. entfernen) reduzieren — kosmetisch, aber
   verwirrend bei sequentieller Abarbeitung.

7. **CI-Aufgabe (Task 24) unpräzise: Build-Schritte fehlen.** In `pr-staging-ci.yml`
   (Z. 121–122, 217–218), `staging-ci.yml` (Z. 113–114, 206–207) und `release.yml` laufen
   die `dotnet test`-Schritte mit `--no-build` und werden durch explizite
   `dotnet build <testcsproj>`-Schritte gedeckt (die slnx-Gesamtbuilds laufen in
   separaten Jobs/Stufen). Task 24 verlangt nur den `dotnet test`-Schritt.
   **Korrektur:** Task so formulieren, dass der Server-Test an denselben Stellen wie die
   beiden bestehenden Testprojekte eingetragen wird (build + test in der regulären Lane;
   die OsInterface-Lane muss nur erweitert werden, wenn dort `OsInterface`-Tests entstehen).

8. **Nicht adressierte Randbedingungen (Doku/Einschränkung reicht):**
   - **Proxy/NAT:** Ausgehende `wss://`-Verbindung vom Windows-PC nutzt per
     Default-`HttpMessageHandler` automatisch den Systemproxy — ein
     authentifizierter Unternehmensproxy ist damit aber nicht abgedeckt. Als bekannte
     Einschränkung in `server/README.md`/Risiken dokumentieren (kein Implementierungsbedarf).
   - **`WindowsCredentialStore.SetCredential` wirft** `InvalidOperationException` bei
     `CredWrite`-Fehler (Z. 75–77). Der `SaveAsync`-Pfad des `SettingsViewModel` braucht
     explizite Fehlerbehandlung (Fehlermeldung statt ungeprüfter Exception) — in
     Task 36/Validierungsregeln ergänzen.
   - **`GetCredential` liefert `string.Empty`** für vorhandene, aber leere Credentials —
     die Token-Pflichtprüfung muss `string.IsNullOrEmpty` verwenden (steht implizit in der
     Validierungsregel „nicht leer"; präzisieren schadet nicht).

## Explizit bestätigt

- **plan.md selbst ist entscheidungskonform:** Kommentar-/Aktionspfad ist überall, wo er
  vorkommt, als „entfällt in v1 / Erweiterungspfad" markiert (Z. 24, 28, 43, 92–94,
  273–275, 343, 354, 356); `StatusServerOptions` enthält keine `Actions:*`-Keys mehr;
  `GitHub:Token` ist lesend/optional. Die Widersprüche liegen ausschließlich in tasks.md.
- **Akzeptanzszenarien-Abdeckung:** Szenarien 1–5 und 7 haben jeweils benannte Nachweise
  (Integrationstest, Server-Tests, Service-Tests, E2E, Sichtprüfung); Szenario 6 korrekt
  gestrichen. Multi-Instanz, „keine Daten", Protokoll-Mismatch und Offline-Markierung sind
  jeweils durch Tests abgesichert.
- **DI-/Startup-Muster stimmen mit dem Code überein:** `AddSingleton` +
  `AddHostedService`-Muster (`App.xaml.cs` Z. 243–250, 319–326), `ScmEventStartupGate.Ready()`
  in Z. 115, `IServiceScopeFactory`-Scoped-Zugriff aus Singletons, `TimeProvider` als
  Singleton (Z. 317), `IRunningAutomationStatusSource`/`IRunningAutomationsCount`-
  Forwarder-Registrierungen (Z. 330–333, 341) — alles wie geplant vorhanden.
- **`AppSettingService`-Konvention bestätigt:** `public const string XKey` +
  typisierte `Get/SetXxxAsync`-Paare, Default-/Klemmungsmuster (z. B.
  `ScmTaskCompletionWaitMinutes`) — die geplanten `status.remote.*`-Schlüssel und
  `GetOrCreateRemoteStatusInstanceIdAsync` passen; eine persistente Instanz-ID existiert
  tatsächlich noch nicht.
- **net11-/NuGet-Konsistenz:** `net11.0`-Ziel und Paketlinie 11.x passen zu
  `Microsoft.AspNetCore.SignalR.Client` 11.0.0 und `Microsoft.AspNetCore.Mvc.Testing`
  11.x (SDK 11.0.100, EF Core 11.0.0 bereits im Repo).
- **slnx-Format:** `Softwareschmiede.slnx` unterstützt `<Folder Name="/Server/">` mit
  `<Project Path="…"/>`-Kinder exakt wie `/Plugins/` — der geplante Eintrag ist
  formatkorrekt; slnx-Aufnahme deckt `dotnet build`, `dotnet format --verify-no-changes`
  und CI automatisch ab.
- **`ICredentialStore`-Weg ist sauber:** Singleton `WindowsCredentialStore` (Z. 379),
  in E2E per `ScmE2ECredentialStore` ersetzt (Z. 413–414) — kein Credential-Manager-
  Seiteneffekt in Tests; PasswordBox-Konvention existiert bereits in `SettingsView.xaml`
  (Z. 19–23, `Tag="{Binding}"` + `Loaded`-Handler).
- **SettingsView-Konventionen bestätigt:** `AutomationProperties.Name`-Namensschema,
  `IsTriggerKeywordVisible`-Sichtbarkeitsmuster (`SettingsViewModel.cs` Z. 333),
  `ScrollViewer` + `MaxWidth="640"` im Automatisierung-Tab — der geplante Abschnitt und
  die BoundingBox-Idee passen.
- **Ehrliche Umgebungs-Eingrenzung:** Ausschluss 8 benennt klar, dass produktive Domain/
  Linux-Server hier nicht nachweisbar sind; localhost:5055 + WebApplicationFactory +
  Screenshots + `server/README.md` (systemd/nginx/WebSocket-Upgrade) sind als lokaler
  Ersatznachweis plausibel und transparent dokumentiert.
- **Lokale Nachweisführung insgesamt realistisch:** App (Windows) ↔ localhost-Server über
  `ws://` ist in dieser Umgebung ausführbar; `TestDbContextFactory`, `TestTimeProvider`,
  `FakeGitHubEventApi`/`StaticHttpHandler`, `CredentialStoreSnapshot` existieren als
  Test-Infrastruktur.
- **Weitere Datenquellen verifiziert:** `IssueReference.IssueUrl`, `PullRequestReference.Url`,
  `ScmEventObservation.Url` liefern die Linkziele; `WorkItemRunActivity`/
  `WorkItemRecoveryService.HeartbeatTimeoutMinutes = 5` begründet die 300-s-Stale-Schwelle;
  `ScmIncomingEventStatus.NonBlocking` passt zur geplanten Warte-Ereignis-Filterung.
