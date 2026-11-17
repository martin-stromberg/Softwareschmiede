# Übersetzte Anforderung: Screenshot-Nachweis — Bilder aus CLI-Läufen hostseitig hochladen und an Abschlussbericht anhängen

**Quelle:** Issue #405 / Aufgaben-ID `6b4ae49c-6c96-49e8-a3ed-632392a8de94`
**Branch:** `task/issue-405-6b4ae49c6c9649e8a3ed632392a8de94-screenshot-nachweis-bilder-aus`

## Fachliche Zusammenfassung

Die SCM-Prompt-Anhänge (`ScmPromptAppendix`, Einstellungsschlüssel `scm.prompt.append.*`) fordern bei UI-Änderungen Screenshots als Nachweis. Die Befolgung ist über mehrere Läufe instabil — anfangs geliefert, zuletzt nicht mehr. Zwei dokumentierte Ursachen:

- Die Anweisung steht nur im Prompt; nichts verifiziert die Lieferung. Die Befolgung hängt allein von der Selbst-Compliance des Agenten ab.
- Der Lieferweg ist unspezifiziert: „Screenshots liefern" sagt nicht, wohin. Selbst bereitwillige Läufe können daran scheitern, weil „anhängen" kein einfacher Schritt ist.

Die Anforderung verlagert den Upload in die Host-Anwendung — Vollzug statt Befolgung: Der Agent legt Screenshots lediglich unter einem festen Konventionspfad im Workspace ab (bzw. meldet sie optional über ein neues `attachments`-Feld in `complete.json`); die Anwendung lädt die Dateien zum GitHub-Attachment-Speicher hoch und hängt die einbettbaren URLs an den Abschlussbericht bzw. optional nachträglich an PR-Texte an.

## Fachlicher Scope

- **Verarbeitungseinheit:** Der Nachweis-Upload für abgelegte Bilddateien eines SCM-Ereignis-CLI-Laufs — Auffinden der Dateien, Upload pro Datei, Rückgabe der einbettbaren URLs und deren Einbettung in den an GitHub gesendeten Berichtstext.
- **Ablage-Konvention (Agentenseite):**
  - Der Agent legt Screenshots unter einem festen Pfad im Workspace ab, z. B. `.softwareschmiede-scm-exchange/screenshots/` (das bestehende Austauschverzeichnis `ScmEventExchangeProtocol.DirectoryName` ist bereits git-exkludiert und ACL-gesichert).
  - Ergänzend/alternativ: optionales `attachments`-Feld in `complete.json` mit Dateipfaden und optionalen Bildunterschriften (Protokoll-Erweiterung von `ScmExchangeComplete`).
- **Upload pro Datei (hostseitig):**
  ```
  POST https://uploads.github.com/user-attachments/assets?name={datei}&content_type={mime}
       &repository_id={id}
  Authorization: Bearer {konto-token}
  ```
  - `repository_id` wird über `GET /repos/{owner}/{repo}` (Feld `id`) ermittelt.
  - Die JSON-Antwort enthält die einbettbare `url` (`https://github.com/user-attachments/assets/...`).
  - Der Endpoint ist undokumentiert, aber verifiziert: Ein Test-Upload mit dem gh-Token hat funktioniert. Push-Zugriff des Kontos genügt.
- **Einbettung:** Die URLs werden als `![{name}]({url})` bzw. `<img>` an den Bericht-Kommentar angehängt — die Stelle, an der `ResponseBody` in `GitHubPlugin.EventResponses` den an GitHub gesendeten Text zusammensetzt (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`, Zeile 140). Optional: PR-Texte nachträglich per `PATCH /pulls/{n}` um die Bilder ergänzen.
- **Anweisung entschärfen:** Der Prompt-Anhang muss anschließend nur noch „Screenshots unter {Konventionspfad} ablegen und im Report referenzieren" fordern — ein Schritt, den Agenten zuverlässig können. Wissen um den Upload-Mechanismus ist nicht mehr nötig.
- **Auslösende Ereignisse:** Verarbeitung des `complete.json`-Abschlusses eines SCM-Ereignis-Laufs, bei dem im Konventionspfad abgelegte Dateien bzw. gemeldete `attachments` vorliegen und ein Bericht-/Kommentartext an GitHub gesendet wird.
- **Erwartete Wirkung:**
  - Bei UI-Änderungen erscheinen die vom Agenten abgelegten Screenshots verlässlich als eingebettete Bilder im Abschlusskommentar (und optional im PR-Text), ohne dass der Agent einen Upload-Schritt kennen oder ausführen muss.
  - Der Upload erfolgt mit dem vorhandenen Konto-Token der SCM-Anbindung; kein zusätzlicher Authentifizierungsweg.
- **Umgang mit fehlenden/fehlerhaften Objekten:**
  - *Keine Screenshots abgelegt:* kein Upload, keine Änderung am Berichtstext. Optional (im Issue als Zusatz genannt): Plausibilitätsprüfung beim Abschluss — beschreibt der Bericht UI-Änderungen, aber es liegen keine Anhänge vor, erfolgt ein Hinweis ins Protokoll oder ein automatischer Kommentar „Screenshot-Nachweis fehlt".
  - *Upload schlägt fehl (einzelne Datei oder Endpoint):* nicht im Issue festgelegt — siehe Offene Fragen.
  - *Attachments an die Repo-Sichtbarkeit gebunden:* In privaten Repos erfordert das Ansehen der Bilder Authentifizierung (Hinweis aus dem Issue, kein Funktionsbaustein).
- **Scope-Ausschlüsse (verworfene Alternativen aus dem Issue):**
  - **Agent lädt selbst hoch** (curl-Endpoint oder `gh ... --attach`): funktioniert technisch, bleibt aber compliance-abhängig — genau der Ist-Schwachpunkt. Wird nicht umgesetzt.
  - **Bilder ins Repo committen und verlinken:** nicht gewünscht — keine Nachweis-Artefakte im Projektordner.
  - **Browser-Session-Flow** (3-Schritt-Upload der Web-UI): unnötig komplex, der Token-Weg reicht.

## Betroffene Klassen und Komponenten

### Zu ändernde Artefakte (aus dem Issue benannt bzw. fachlich zwingend)

- `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs`
  - `ScmExchangeComplete` (Zeilen 52–54): optionale Protokoll-Erweiterung um ein `attachments`-Feld (Dateipfad + optionale Bildunterschrift) — sofern die Feld-Variante neben/statt der reinen Verzeichniskonvention gewählt wird.
  - `ReadAsync`/`ValidateAttemptAsync` (Zeilen 229–271, 343–354): das Auffinden/Validieren abgelegter Bilddateien muss in die bestehende Pfad- und Integritätsprüfung des Antwortkanals passen (`EnsureSafePath`, `VerifyPrivateDirectory`, Größenlimit `MaximumMessageBytes` gilt bislang für JSON-Nachrichten — Umgang mit Binärdateien ist zu klären).
  - `BuildPrompt`/`completeSchema` (Zeilen 296–312): bei Protokoll-Erweiterung muss das dem Agenten beschriebene Schema das neue Feld nennen.
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs`
  - `ResponseBody` (Zeile 140) bzw. die Aufrufstellen in `ExecuteResponseAsync` (Zeilen 62–71): Anhängen der hochgeladenen `![{name}]({url})`- bzw. `<img>`-Referenzen an den an GitHub gesendeten Kommentar-/PR-Text.
  - Neuer Upload-Schritt: `POST https://uploads.github.com/user-attachments/assets` mit `Authorization: Bearer {konto-token}` und `repository_id` aus `GET /repos/{owner}/{repo}` (Feld `id`). Achtung: Der Upload-Host `uploads.github.com` weicht vom bisher verwendeten API-Host (`repository.Host` / `api.github.com`) ab.
  - Optional: `PATCH /pulls/{n}` zur nachträglichen Ergänzung von PR-Texten.
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`
  - Abschlussverarbeitung (`messages.Complete`, ab Zeile 426): fachliche Einschaltstelle, um abgelegte Dateien bzw. gemeldete `attachments` des Versuchs einzusammeln und dem Report-/Kommentarweg zuzuordnen.
- Prompt-Anhang-Texte (`scm.prompt.append.*`, aufgelöst über `ScmPromptAppendix`/`AppEinstellungService.GetScmPromptAppendicesAsync` und als `promptSuffix` im Prompt übertragen): inhaltliche Entschärfung auf „unter {Konventionspfad} ablegen und im Report referenzieren". Die Anhänge sind anwenderkonfigurierte Einstellungen — ob eine ausgelieferte Vorgabe/Doku-Vorlage angepasst wird, ist zu klären (siehe Offene Fragen).
- Tests: `src/Softwareschmiede.Tests/Application/Services/ScmPromptAppendixTests.cs`, `ScmEventExchangeServiceTests.cs` sowie `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests*` betreffen Austauschprotokoll und Antwortwege und sind bei Protokoll-/Verhaltensänderung zu berücksichtigen.

### Nicht betroffene Artefakte

- `ScmPromptAppendix.cs` (Ereignisart-Mapping/Kombination der Anhänge) — die Auflösungslogik selbst bleibt unverändert; nur der geforderte Textinhalt ändert sich fachlich.
- `ScmExchangeRequest`/`request.json` — der unveränderliche SCM-Auftrag bleibt bitgleich; die Ablage-Konvention ist außerhalb des Request-Schemas.
- Kein neuer Authentifizierungs-/Tokenpfad: Es wird das bestehende Konto-Token verwendet (Push-Zugriff genügt laut Issue-Verifikation).

## Implementierungsansatz

- Der Upload ist eine Host-Pflichtfunktion beim Abschluss eines SCM-Ereignis-Laufs, kein Agentenschritt und keine neue Konfigurationsfläche.
- Reihenfolge gemäß Issue: (1) Dateien am Konventionspfad bzw. aus `attachments` einsammeln, (2) `repository_id` auflösen, (3) pro Datei `POST` gegen `uploads.github.com/user-attachments/assets` mit `name`-, `content_type`- und `repository_id`-Queryparametern, (4) die zurückgelieferten `url`-Werte als Markdown-/`<img>`-Referenzen an den Berichtstext hängen, (5) optional `PATCH /pulls/{n}` für PR-Texte.
- Sicherheitskontext des Antwortkanals bleibt maßgeblich: Der Screenshot-Pfad liegt innerhalb des gesicherten Austauschverzeichnisses (git-exkludiert, `info/exclude`, ACL/Unix-Mode-Prüfung); das Einsammeln darf den vertrauenswürdigen Pfad nicht über Symlink-/Pfadtricks erweitern (`EnsureSafePath`-Konvention beachten).
- Endpoint-Referenz (Reverse-Engineering-Doku aus dem Issue): https://github.com/drogers0/gh-image/blob/main/documentation/github-image-upload-flow.md

## Konfiguration

- Im Issue ist **keine neue Konfigurierbarkeit** genannt: Der Konventionspfad ist fest, der Upload-Endpoint ist fest, das Konto-Token der SCM-Anbindung wird wiederverwendet.
- Bestehende Konfigurationsfläche `scm.prompt.append.*` bleibt die einzige Textsteuerung des Prompt-Anhangs; die „entschärfte" Formulierung ist Inhalt dieser Vorgaben/Anleitung, kein neuer Mechanismus.

## Offene Fragen

1. **Ablage-Weg:** Verzeichniskonvention (`.softwareschmiede-scm-exchange/screenshots/`), `attachments`-Feld in `complete.json`, oder beides komplementär (Feld verweist auf Pfade innerhalb/außerhalb des Austauschverzeichnisses)? Das Issue nennt „ergänzend/alternativ" — die konkrete Wahl ist offen.
2. **Fehlerfall Upload:** Was passiert, wenn der Upload einzelner oder aller Dateien fehlschlägt (undokumentierter Endpoint, Netzfehler, fehlende Berechtigung)? Bericht trotzdem posten, Fehler markieren, Wiederholung beim nächsten Poll?
3. **Einschaltpunkt und Datenfluss:** Sammelt der Orchestrator die Dateien und übergibt sie dem Plugin, oder lädt das Plugin direkt aus dem Workspace-Verzeichnis? Die Trennung Austauschprotokoll (Application) vs. Remote-Ausführung (Plugin) ist zu klären.
4. **Ziel der Einbettung:** Nur der Bericht-Kommentar (`ResponseBody`-Pfad) oder auch/optional PR-Texte per `PATCH /pulls/{n}` — das Issue markiert Letzteres als Optional; Entscheidung nötig.
5. **Feld- vs. Datei-Validierung:** Welche Dateitypen/Größen sind zulässig (MIME-Erkennung, `MaximumMessageBytes` gilt bislang nur für JSON-Nachrichten)? Sind Bildunterschriften Pflichtbestand oder freiwillig?
6. **Prompt-Anhang-Umstellung:** Da die Anhänge anwenderkonfiguriert sind — wird eine ausgelieferte Vorgabe/Doku-Vorlage angepasst, und/oder wird der Konventionspfad zusätzlich automatisch im `promptSuffix`/Prompt mitgeteilt, damit Bestandskonfigurationen den Pfad kennen?
7. **Plausibilitätsprüfung:** Soll die optionale Prüfung „Bericht beschreibt UI-Änderungen, aber keine Anhänge vorhanden → Protokollhinweis bzw. Kommentar ‚Screenshot-Nachweis fehlt'" umgesetzt werden oder entfällt sie im ersten Schritt?
8. **GitHub Enterprise:** Der verifizierte Weg nennt `uploads.github.com`; ist GHE-Abdeckung (`uploads.<host>` o. ä.) erforderlich oder außerhalb des Scopes?
9. **Idempotenz/Wiederanlauf:** Bei erneutem Zustellversand desselben Ereignisses dürfen Attachments nicht doppelt hochgeladen/eingebettet werden — wie wird der bereits hochgeladene Zustand erkannt (Markierung im Versuch, Reconcile über Kommentarinhalt)?
