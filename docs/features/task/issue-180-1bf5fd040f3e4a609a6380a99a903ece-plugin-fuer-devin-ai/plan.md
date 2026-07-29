# Umsetzungsplan: Devin-CLI-Plugin

## Ziel

Ein neues Plugin-Projekt `Softwareschmiede.Plugin.Devin` integriert die lokale Devin-CLI in die vorhandene `CliKiPluginBase`-Infrastruktur und stellt sie als `PluginType.DevelopmentAutomation` bereit. Die bestehende ConPTY-Ausfuehrung und grafische Terminalausgabe bleiben die Benutzerschnittstelle. Authentifizierung erfolgt ausschliesslich ueber die Devin-CLI.

## Verifizierter CLI-Vertrag

- Executable: `devin`.
- Die CLI ist ein lokaler, interaktiver Coding-Agent und wird im Projektverzeichnis gestartet.
- `devin` ohne Argumente startet die interaktive Sitzung; ein optionaler Prompt kann als Initialnachricht uebergeben werden.
- Windows wird unter anderem ueber Installer oder PowerShell-Installation unterstuetzt; danach ist `devin` aus PowerShell, Windows Terminal und Git Bash nutzbar.
- Relevante CLI-Optionen sind `--continue`/`-c`, `--resume <SESSION_ID>`/`-r`, `--print`, `--prompt-file`, `--model` und `--permission-mode`.
- Die Authentifizierungsbefehle `devin auth login`, `devin auth logout` und `devin auth status` verbleiben in der CLI. Das Plugin bietet weder Token- noch API-Key-Konfiguration und setzt keine Authentifizierungsumgebungsvariablen.

## Umsetzungsschritte

1. **Plugin-Projekt anlegen**
   - `plugins/Softwareschmiede.Plugin.Devin/Softwareschmiede.Plugin.Devin.csproj` nach dem bestehenden Claude-/CLI-Plugin-Muster erstellen.
   - Projekt in `Softwareschmiede.slnx` aufnehmen und `Softwareschmiede.Plugin.Contracts` referenzieren.

2. **`DevinPlugin` implementieren**
   - Von `CliKiPluginBase` ableiten und `PluginType.DevelopmentAutomation` setzen.
   - Anzeigename, stabilen Plugin-Prefix und den Standard-Executable-Namen `devin` definieren.
   - Einen optionalen `ExecutablePath` unterstuetzen; ohne Konfiguration wird `devin` ueber PATH aufgeloest.
   - Den interaktiven Standardstart ohne zusaetzliche Argumente ermoeglichen. Optionale Parameter werden ueber die bestehende sichere Parameterweitergabe an die CLI uebergeben, damit unter anderem Prompt, `--continue`, `--resume`, `--model`, `--permission-mode`, `--prompt-file` und der explizite Print-Modus genutzt werden koennen.
   - `BuildProcessStartInfo` mit dem Repository-Arbeitsverzeichnis sowie der vorhandenen ConPTY-/Standardausgabe-Anbindung umsetzen.
   - Keine Token-, API-Key- oder sonstigen Credential-Felder, Argumente oder Umgebungsvariablen hinzufuegen.

3. **Discovery und Testmodus integrieren**
   - `PluginManager.IsAllowedInTestMode` um das Devin-Plugin ergaenzen, falls der Filter providerbezogen ist.
   - Sicherstellen, dass die Plugin-DLL im Build-/Testoutput landet und dynamisch geladen wird.

4. **Tests ergaenzen**
   - `DevinPluginTests` analog zu den bestehenden Claude-/CLI-Plugin-Tests anlegen und in das Testprojekt aufnehmen.
   - Metadaten, Plugin-Typ, Prefix und Standard-Executable pruefen.
   - `ExecutablePath`, PATH-Fallback, Repository-Arbeitsverzeichnis und optionale Parameter pruefen.
   - Interaktiven Start ohne Argumente sowie die Weitergabe der verifizierten Devin-Optionen pruefen.
   - Sicherstellen, dass keine Authentifizierungsvariablen oder Credential-Argumente erzeugt werden.
   - Plugin-Discovery und Testmodus-Freigabe pruefen.
   - Die Unit-Tests duerfen keine installierte oder eingeloggte Devin-CLI voraussetzen; ein echter CLI-Aufruf bleibt ein optionaler Integrationstest.

5. **Verifikation und Dokumentation**
   - Betroffene Tests und anschliessend die relevante Solution bzw. das Testprojekt ausfuehren.
   - Benutzer- oder Entwicklerdokumentation zur Installation der Devin-CLI, zur Anmeldung ueber `devin auth login`, zur `ExecutablePath`-Einstellung und zur fehlenden Token-Konfiguration ergaenzen.
   - Lifecycle-Artefakte mit Testergebnis, Reviews und verbleibenden Einschraenkungen aktualisieren.

## Abnahmekriterien

- Das Plugin wird als `DevelopmentAutomation`-Plugin dynamisch geladen und in der Auswahl angeboten.
- Ohne manuelle Konfiguration wird der lokale Prozess `devin` gestartet; ein optionaler `ExecutablePath` funktioniert.
- Der Prozess erhaelt das Repository als Arbeitsverzeichnis und seine Ausgabe wird ueber die bestehende ConPTY-/grafische Terminaloberflaeche angezeigt.
- Interaktive Eingaben koennen ueber die bestehende Terminalausfuehrung verarbeitet werden.
- Zulaessige optionale CLI-Parameter werden sicher weitergereicht.
- Es werden keine Devin-Zugangsdaten gespeichert oder an den Prozess uebergeben; Login bleibt in der CLI.
- Tests decken Metadaten, Konfiguration, Prozessstart, Sicherheitsanforderungen und Discovery ab.

## Offene Punkte
