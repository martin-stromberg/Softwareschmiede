# Bestandsaufnahme: Konto `github-actions` als akzeptierte Autor-Identität der SCM-Ereignisvalidierung

Bestandsaufnahme der SCM-Ereignisvalidierung und ihrer Aufrufer bezogen auf die Anforderung, neben dem Repository-Owner auch das GitHub-Konto `github-actions` als vertrauenswürdigen Akteur einer beobachteten Textversion zu akzeptieren (Issue #394 / `requirement.md`).

## Zusammenfassung

- **Zentrale Prüfstelle:** `ScmEventValidationService.GetRejection` (statisch, `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`, Z. 33–61) enthält die gesamte Akteurs-Identitätsprüfung. Die relevante Regel „Autor/Bearbeiter = Repository-Owner" ist exakt ein `SameIdentity`-Aufruf (Z. 56–57); davor liegt die Regel „Bearbeiter nicht bestimmbar" (Z. 54–55). Es existiert **kein** Konzept vertrauenswürdiger Zusatzidentitäten, keine Whitelist und keinerlei `github-actions`-Bezug im gesamten Code- und Testbestand (Verifikation per Volltextsuche).
- **Beobachtungsmodell liefert bereits alles Nötige:** `ScmEventObservation` trägt `AuthorId`/`AuthorLogin`/`EditorId`/`EditorLogin`; `ScmEventRepository` trägt `ProviderPrefix` (`Softwareschmiede.GitHub`), `Host`, `OwnerId`, `Owner` und den `Konto`-Modus — eine Provider-Bindung der Akzeptanz ist ohne Modelländerung möglich.
- **Drei Aufrufstellen, eine Entscheidung:** `ScmEventStore.AcceptBatchAsync` (Z. 62, Aufnahme-Diagnose), `ScmEventMonitoringService` (Z. 187, Trigger-Suche für `ScmEventTaskResolver` bei unzugeordneten Referenzen) und `ScmEventOrchestrator.DispatchCoreAsync` (Z. 137–146, Revalidierung beim Dispatch mit Wartegrund „Angemeldeter Benutzer ist nicht mehr der bestätigte Repository-Owner."). Eine Erweiterung in `GetRejection` wirkt end-to-end ohne Aufruferänderung.
- **Provider-Lage:** Nur `GitHubPlugin` implementiert `IScmEventSource` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, Z. 19); `BitbucketPlugin` (`plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`, Z. 12) und `LocalDirectoryPlugin` implementieren die Ereignisquelle nicht — `github-actions`-Beobachtungen können faktisch nur aus dem GitHub-Pfad entstehen.
- **GitHub-Lesepfad:** Das GraphQL-Feldfragment `TextFields` (`GitHubPlugin.Events.cs`, Z. 13) fragt `author { login ... on Node { id } }` und `editor { login ... on Node { id } }` ab — kein `__typename`. `AddObservation` (Z. 232–264) übernimmt `id`/`login` unverändert in `AuthorId`/`AuthorLogin` bzw. `EditorId`/`EditorLogin`; für `github-actions[bot]`-Autoren landet also der Bot-Login als `AuthorLogin` in der Beobachtung.
- **Dokumentation:** `docs/help/plugins/github-ereignisse/business-rules.md` (Abschnitte „Trigger und Repository-Owner" und „Kontomodus und Notifications-Inbox") sowie `troubleshooting.md` (Z. 63–65, empfiehlt aktuell bei `github-actions[bot]`-Autorenschaft das Secret `REVIEW_ISSUE_TOKEN`) beschreiben den heutigen Owner-Vertrag.
- **Tests:** `ScmEventValidationServiceTests` deckt Owner-/Editor-Identität, Trigger, Reaktionen und beide Kontomodi ab — aber keine Bot-/Fremdidentität jenseits von „bot-id". `ScmDispatchFixture` und alle Integrations-Helfer erzeugen ausschließlich Owner-Autoren (`"U_1"`/`"owner"`); es gibt keinen bestehenden Testfall mit `github-actions`-Autorschaft.

**Test-Ausgangszustand:** Voll-Build `dotnet build Softwareschmiede.slnx` fehlerfrei (0 Warnungen, 0 Fehler). Reguläre Unit-Test-Lane: **2446 bestanden, 0 fehlgeschlagen, 3 übersprungen** (plattformbedingte Skips). Reguläre Integrations-Test-Lane: **280 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Keine nachgewiesenen Testfehler. Nicht ausgeführt: die `Category=OsInterface`-Lane (WPF-E2E/ConPTY/echte Prozessstarts — separate umgebungssensitive Lane, siehe Begründung und Umfang in [Tests](inventory/tests.md)).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
