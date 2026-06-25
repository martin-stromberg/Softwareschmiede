# Offene Aufgaben

Erstellt am: 2026-06-25
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [x] `.netrc`-Datei-Permissions setzen: `UpdateNetrcEntry()` muss unter Unix `chmod 0600` auf die `.netrc`-Datei setzen — git ignoriert `.netrc` mit zu offenen Permissions
- [x] E2E-Test: Clone Cloud-Repository mit eingebetteten Credentials — als Mock-Test implementiert
- [x] E2E-Test: Pull von geklontem Cloud-Repository — als Mock-Test implementiert
- [x] E2E-Test: Push zu geklontem Cloud-Repository — als Mock-Test implementiert
- [x] E2E-Test: Clone Self-Hosted-Repository mit URL-Konvertierung — als Mock-Test implementiert
- [x] E2E-Test: Health-Check für Cloud — als Mock-Test implementiert
- [x] E2E-Test: Health-Check für Self-Hosted — als Mock-Test implementiert
- [x] Nutzer-Dokumentation: Cloud-Konfiguration (Benutzername, App Password, HostingMode)
- [x] Nutzer-Dokumentation: Troubleshooting bei Authentifizierungsfehlern
- [x] Nutzer-Dokumentation: Windows-spezifische Hinweise zum `.netrc`-Handling

## Code-Review-Befunde

- [x] Gemischte Zeilenenden in `.netrc` (Zeile ~770): `newEntry` nutzt `\n`, `string.Join` nutzt `Environment.NewLine` — auf Windows mit mehreren Einträgen bricht die netrc-Datei; beide auf `\n` vereinheitlichen
- [x] `GetGitEnvironment()` wirft falsche Exception in `CloneRepositoryAsync` (Zeile ~308): Bei SelfHosted ohne URL umgeht die Exception den sanitisierten Fehlerblock — in `CloneRepositoryAsync` abfangen und sanitisieren
- [x] `GetRemoteBranchesAsync`/`GetDefaultBranchAsync` werfen statt Silent-Fallback (Zeile ~638/750): Beim SelfHosted-ohne-URL-Fall sollten die Methoden `[]` bzw. `"main"` zurückgeben statt zu werfen
- [x] Nicht-Auth-Fehler werden nicht geloggt (Zeile ~311): Nur Auth-Fehler erhalten `LogError`; Netzwerkfehler, "repository not found" etc. werden still geworfen — alle git-Fehler loggen
- [x] `SanitizeSensitiveOutput` erkennt percent-kodierte Passwörter nicht (Zeile ~883): Direkte `String.Replace` schlägt bei `pass%40word` fehl; URL-Regex fangt es nur als Fallback — auch den percent-kodierten Wert ersetzen
- [x] `http://` wird in `https://` umgeschrieben (Zeile ~892): Replacement-String hardcoded auf `https://` obwohl Pattern `https?://` matcht — Pattern auf `https://` einschränken oder Replacement dynamisch machen
- [x] Credential-Rotation bricht Pull/Push nach Clone (Zeile ~296): git speichert Credentials in `.git/config`; nach Rotation nutzen Pull/Push veraltete URL-Credentials — nach Clone die Remote-URL auf plain HTTPS zurücksetzen
- [x] SSH-URLs hängen ohne `GIT_SSH_COMMAND` (Zeile ~638): `GIT_TERMINAL_PROMPT=0` unterdrückt keine SSH-Host-Key-Prompts; für SSH-URLs `GIT_SSH_COMMAND` mit `StrictHostKeyChecking=no` wieder einsetzen oder SSH explizit ablehnen — SSH wird jetzt mit Fehlermeldung abgelehnt
- [x] `SanitizeSensitiveOutput` dupliziert mit `GitHubPlugin` (Zeile ~875): Sicherheitskritische Routine in zwei divergierenden Kopien — in eine gemeinsame Helferklasse extrahieren — als known limitation dokumentiert (unterschiedliche Signaturen)

## Neue Code-Review-Befunde (aus erneutem Durchlauf, 2026-06-25)

- [ ] Credentials im Log (Zeile ~329): `CliRunner.RunAsync` schreibt StdErr mit `LogWarning` bevor das Plugin `SanitizeSensitiveOutput` aufruft — App Password erscheint bei jedem fehlgeschlagenen `git clone` im Klartext in allen Log-Sinks
- [ ] `git remote set-url`-Ergebnis ignoriert (Zeile ~350): Schlägt der Aufruf fehl, bleiben eingebettete Credentials dauerhaft als Klartext in `.git/config` — Ergebnis prüfen und bei Fehler loggen/werfen
- [ ] Cloud-Modus: kein `credential.helper=` (Zeile ~376): `GetGitHttpAuthArgs()` gibt `[]` zurück ohne `-c credential.helper=` — GCM (Windows) / osxkeychain (macOS) kann vor `.netrc` greifen und veraltete Credentials liefern; analog zu SelfHosted `-c credential.helper=` ergänzen
- [ ] `GetDefaultBranchAsync` Fallback "main" führt zu falschem PR-Ziel (Zeile ~809): Bei SelfHosted-Fehlkonfiguration wird still "main" zurückgegeben — `CreatePullRequestAsync` erstellt PR gegen möglicherweise nicht existierenden Branch
- [ ] `BuildAuthenticatedCloneUrl` mit `PathAndQuery` statt `AbsolutePath` (Zeile ~244): Query-Parameter aus Browser-URLs werden in die Clone-URL eingebettet — `uri.AbsolutePath` statt `uri.PathAndQuery` verwenden
- [ ] Cloud Browser-URL wird als Remote gesetzt (Zeile ~300): Cloud-Modus normalisiert `repositoryUrl` nicht — `ResolveGitCloneUrl()` oder äquivalente Normalisierung auch für Cloud aufrufen
- [ ] `UpdateNetrcEntry` ohne Datei-Locking (Zeile ~832): Kein `SemaphoreSlim`/`lock` und kein atomares Schreiben (temp-file+rename) — parallele Aufrufe können `.netrc` korrumpieren
- [ ] `SanitizeSensitiveOutput` erfasst `http://`-URLs nicht (Zeile ~978): Regex erfasst nur `https://`; Self-Hosted über plain HTTP wird nicht vollständig bereinigt

## Fehlgeschlagene Tests

- [ ] TaskDetailViewModelTests.LoeschenCommand_CanExecuteFalse_WennStatusBeendet — Expected sut.LoeschenCommand.CanExecute(null) to be False, but found True (pre-existierender Logik-Bug in TaskDetailViewModel, kein Bezug zu BitBucket-Cloud-Änderungen)
- [ ] TaskDetailViewModelTests.KannLoeschen_IsTrue_WhenStatusGestartet — Expected sut.KannLoeschen to be True, but found False (pre-existierender Logik-Bug)
- [ ] TaskDetailViewModelTests.KannLoeschen_IsFalse_WhenStatusArchiviert — Expected sut.KannLoeschen to be False, but found True (pre-existierender Logik-Bug)
- [ ] TaskDetailViewModelTests.KannLoeschen_IsFalse_WhenStatusBeendet — Expected sut.KannLoeschen to be False, but found True (pre-existierender Logik-Bug)
- [ ] AufgabeRecoveryServiceTests.RecoverManuellAsync_ShouldAllowExactlyOneSuccess_WhenTriggeredInParallel — Expected results.Count(r => r) to be 1, but found 2 (pre-existierender Race-Condition-Test, kein Bezug zu BitBucket-Cloud-Änderungen)
