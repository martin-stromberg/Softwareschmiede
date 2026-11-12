← [Zurück zur Übersicht](index.md)

# Einstellungen — Ablauf für Anwender

## Einstellungen öffnen

1. Klicken Sie im Hauptmenü auf **Einstellungen** oder nutzen Sie den Zugang über die Menüleiste
2. Das Einstellungsfenster öffnet sich mit mehreren Registerkarten

## Allgemein (Registerkarte)

### Design ändern

1. Öffnen Sie die Registerkarte **Allgemein**
2. Wählen Sie unter **Design** eine Option:
   - **Hell** — heller Modus mit hellen Hintergründen
   - **Dunkel** — dunkler Modus mit dunklen Hintergründen
3. Die Änderung wird sofort sichtbar
4. Klicken Sie **Speichern**, um die Einstellung zu persisieren

### Arbeitsverzeichnis einstellen

1. Öffnen Sie die Registerkarte **Allgemein**
2. Geben Sie unter **Arbeitsverzeichnis** einen lokalen Pfad ein, z. B. `C:\Projekte`
3. Dieses Verzeichnis wird verwendet, um geklonte Repositories zu speichern
4. Klicken Sie **Speichern**

> **Hinweis:** Änderungen am Arbeitsverzeichnis wirken sich nur auf neue Aufgaben aus. Bereits gestartete Aufgaben behalten ihren ursprünglichen Klonpfad.

### Update-Verhalten einstellen

1. Öffnen Sie die Registerkarte **Allgemein**
2. Wählen Sie im Abschnitt **Updates** in der Auswahlbox einen Modus:
   - **Aus** — keine Update-Prüfung
   - **Nur prüfen** — prüfen und Update anbieten (Standard)
   - **Bei Programmstart prüfen und ausführen** — beim Programmstart prüfen und ein gefundenes Update automatisch installieren
3. Wählen Sie in der Auswahlbox **„Update-Pfad"** den Update-Kanal:
   - **Produktivversionen** — nur stabile Versionen (Standard)
   - **Vorabversionen** — nur Release-Kandidaten/Vorabversionen; die Anzeige **„Kanal:"** darunter zeigt den resultierenden Kanal
4. Klicken Sie **Speichern**

> **Hinweis:** Werden die Update-Einstellungen geändert, während ein Update angeboten wird oder ein Update-Vorgang läuft, wird das Angebot zurückgenommen bzw. der Vorgang abgebrochen. Details zum Update-Ablauf finden Sie in der [Programmupdate-Dokumentation](../programmupdate/index.md).

### Konsolentestfenster öffnen (Diagnose)

1. Öffnen Sie die Registerkarte **Allgemein**
2. Klicken Sie im Abschnitt **Diagnose** auf **Konsolentestfenster öffnen**
3. Das Fenster „Konsolentest" öffnet sich als eigenes Fenster — die Anwendung bleibt parallel bedienbar
4. Laden Sie darin eine zuvor exportierte `.clireplay`-Aufzeichnung und spielen Sie sie zur Analyse der Terminal-Ausgabe ab

> **Hinweis:** Die Aufzeichnung exportieren Sie zuvor in der Aufgabendetailansicht über den Button **Aufzeichnung exportieren** (Ribbon-Gruppe „CLI"). Details zur Bedienung des Fensters finden Sie im [Ablauf für Anwender der Terminal-Integration](../terminal/ablauf-anwender.md).

## Automatisierung (Registerkarte)

Die Registerkarte **Automatisierung** (direkt nach **Allgemein**) bündelt die zyklischen Automatisierungsfunktionen in drei auf-/zuklappbaren Panels. Jede Panel-Titelleiste zeigt an, ob die Funktion gerade **aktiviert** oder **deaktiviert** ist — auch im zugeklappten Zustand. Alle Schalter wirken nach dem **Speichern** ohne Neustart.

### Ereignisüberwachung einstellen (Panel „Aufgaben ausführen")

1. Öffnen Sie die Registerkarte **Automatisierung** und klappen Sie das Panel **Aufgaben ausführen** auf
2. Schalten Sie **Aufgaben automatisch starten** ein, damit die Anwendung GitHub-Issues und Pull Requests auf Anfragen überwacht
3. Wählen Sie im **Überwachungsmodus** den Eingangskanal:
   - **Benutzerkonto** — (Standard) die Projekt-Repositories werden repository-weit überwacht; Auslöser ist das **Trigger-Keyword** (Standard `$Softwareschmiede`), das nur in diesem Modus sichtbar ist
   - **Separates Konto** — es werden ausschließlich die GitHub-Benachrichtigungen eines separaten Kontos überwacht; Auslöser ist eine frei stehende `@`-Erwähnung dieses Kontos. Dafür muss unter **Plugins → GitHub → Authentifizierung** ein **Personal Access Token des separaten Kontos** hinterlegt sein, sonst bricht das Speichern mit einer Fehlermeldung ab
4. Pflegen Sie bei Bedarf die mehrzeiligen **Prompt-Anhänge** (Allgemein, Issue, Issue-Kommentar, Pull Request, Pull-Request-Kommentar) — Texte, die bei der Zustellung an den Prompt angehängt werden; sie sind nur bei aktivierter Funktion sichtbar
5. Klicken Sie **Speichern**

> **Hinweis:** Der Überwachungsmodus ist nur bei aktivierter Funktion bedienbar. Details zum Verhalten finden Sie in der [GitHub-Ereignisüberwachung](../plugins/github-ereignisse/index.md).

### Abschlussüberwachung einstellen (Panel „Aufgaben beenden")

1. Öffnen Sie das Panel **Aufgaben beenden**
2. Schalten Sie **Aufgaben automatisch beenden** ein
3. Wählen Sie im **Prüfmodus**, welche Aufgaben beobachtet werden:
   - **Nur ereignisgesteuerte Aufgaben** — (Standard) nur Aufgaben mit mindestens einem SCM-Ereignis
   - **Alle Aufgaben** — zusätzlich auch manuell angelegte Aufgaben
4. Stellen Sie die **Wartezeit für Beendigung** in Minuten ein — so lange wartet die Anwendung nach dem erkannten Schließen/Mergen der Referenz, bevor die Aufgabe samt laufender KI-Ausführung beendet wird
5. Klicken Sie **Speichern**

> **Hinweis:** Beobachtet werden nur Aufgaben mit SCM-Referenz — eine Aufgabe ohne verknüpftes Issue und ohne verknüpften Pull Request wird nie automatisch beendet. Prüfmodus und Wartezeit sind nur bei aktivierter Funktion bedienbar.

### Backmerge einstellen (Panel „Prozessabläufe")

1. Öffnen Sie das Panel **Prozessabläufe**
2. Wählen Sie unter **Backmerge-Pull-Requests** den Modus:
   - **Deaktiviert** — (Standard) keine automatischen Merges
   - **Global aktiviert** — Backmerge-PRs werden in allen Projekten automatisch gemergt
   - **Projektbezogen aktivierbar** — zusätzlich muss die Option **„Backmerge-Pull-Requests automatisch mergen"** in der Basis-Branch-Kachel der Projektdetailansicht eingeschaltet sein
3. Aktivieren Sie bei Bedarf **„Draft-Pull-Requests vor dem Merge automatisch aktivieren"** (als Entwurf markierte Backmerge-PRs werden automatisch als bereit für Review markiert) und/oder **„Bei blockiertem Merge Admin-Merge erlauben"** (ein am Branch-Schutz gescheiterter regulärer Merge wird einmalig als Admin-Merge wiederholt)
4. Klicken Sie **Speichern**

> **Hinweis:** Die Backmerge-Prüfung läuft in einem eigenen Zyklus — unabhängig vom Schalter „Aufgaben automatisch starten" und immer unter dem Benutzerkonto, also auch im Überwachungsmodus „Separates Konto". Die beiden Merge-Optionen sind nur bedienbar, wenn der Backmerge-Modus nicht auf „Deaktiviert" steht.

## Plugins (Registerkarte) — IDE-Plugins

### IDE-Plugin aktivieren, deaktivieren oder priorisieren

1. Öffnen Sie die Registerkarte **Plugins**
2. Wählen Sie in der Liste **Integrierte Entwicklungsumgebungen (IDE)** ein Plugin aus (z. B. Visual Studio Code)
3. Aktivieren oder deaktivieren Sie es im Inhaltsbereich rechts über die Checkbox **Plugin aktiviert**
4. Passen Sie bei Bedarf die Auswahl-Priorität über die Pfeile **↑**/**↓** neben dem Plugin-Eintrag an
5. Klicken Sie **Speichern**

> **Hinweis:** Mindestens ein IDE-Plugin muss aktiv bleiben. Beim Klick auf **IDE öffnen** in der Aufgabendetailansicht wird automatisch das erste explizit kompatible Plugin verwendet (z. B. Visual Studio bei vorhandener `*.sln`-Datei); ist keines explizit kompatibel, öffnet automatisch das erste aktive Fallback-Plugin (standardmäßig Visual Studio Code) das Arbeitsverzeichnis — eine separate Opt-in-Einstellung dafür ist nicht mehr nötig.

## Plugins (Registerkarte) — SCM- und KI-Plugins

### Standard-Plugin auswählen und Plugin-Einstellungen pflegen

1. Öffnen Sie die Registerkarte **Plugins**
2. Wählen Sie links oben in den Dropdown-Feldern **Standard SCM-Plugin** bzw. **Standard KI-Plugin** das gewünschte Plugin (z. B. GitHub bzw. Claude)
3. Wählen Sie in den gruppierten Listen darunter das Plugin aus, dessen Einstellungen Sie bearbeiten möchten — die Felder des gewählten Plugins erscheinen im Inhaltsbereich rechts
4. Füllen Sie die angezeigten Felder aus — typischerweise ein **Authentifizierungs-Token** oder **API-Schlüssel** (bei Geheim-Feldern wird die Eingabe maskiert)
5. Klicken Sie **Speichern**

> **Hinweis:** Alle mit einem roten Sternchen (*) gekennzeichneten Felder sind Pflichtfelder und müssen ausgefüllt werden, bevor Sie speichern können. Über die Checkboxen in den Plugin-Listen aktivieren oder deaktivieren Sie einzelne Plugins — mindestens ein Plugin pro Kategorie muss aktiv bleiben. Die Einstellungswerte eines Plugins bleiben gespeichert, wenn Sie zu einem anderen Plugin wechseln.

### SCM-Authentifizierung einrichten (Beispiel GitHub)

1. Wählen Sie in der Liste **Quellcodeverwaltung** das Plugin **GitHub**
2. Kopieren Sie den Token von der Website des Anbieters (GitHub → Settings → Developer settings → Personal access tokens)
3. Fügen Sie den Token in das Feld **Personal Access Token** ein — die Eingabe wird maskiert
4. Füllen Sie weitere notwendige Felder aus (z. B. den **Personal Access Token des separaten Kontos**, wenn Sie den Überwachungsmodus „Separates Konto" verwenden möchten)
5. Klicken Sie **Speichern**

### KI-API-Schlüssel konfigurieren

1. Wählen Sie in der Liste **KI** das gewünschte KI-Plugin
2. Das erste Feld ist üblicherweise der **API-Schlüssel** oder **Authentifizierungs-Token**
3. Beschaffen Sie sich den Token:
   - Besuchen Sie die Website des KI-Anbieters (z. B. Anthropic, OpenAI)
   - Navigieren Sie zu Einstellungen → API-Schlüssel
   - Erstellen Sie einen neuen Schlüssel, falls noch nicht vorhanden
4. Kopieren Sie den Schlüssel (er wird oft nur einmal angezeigt!)
5. Fügen Sie ihn in das Geheim-Feld ein
6. Füllen Sie ggf. weitere Felder aus (z. B. Modell-Auswahl, Temperatur, Max. Tokens)
7. Klicken Sie **Speichern**

> **Hinweis:** Speichern Sie den API-Schlüssel an einem sicheren Ort. Die Anwendung speichert ihn verschlüsselt im Windows Credential Store, Sie können ihn aber nicht später abrufen.

### Codex-CLI-Parameter konfigurieren

1. Wählen Sie in der Liste **KI** das Plugin **Codex CLI**
2. Tragen Sie im Feld **CommandLineParameters** nur die zusätzlichen Argumente ein, die Sie beim Start der Codex CLI verwenden möchten, z. B. `--model gpt-5`
3. Lassen Sie das Feld leer, wenn die Codex CLI ohne zusätzliche Argumente gestartet werden soll
4. Klicken Sie **Speichern**

Der gespeicherte Wert wird beim nächsten Öffnen unverändert angezeigt. Ein leer gespeichertes Feld bleibt leer; die Anwendung ergänzt für Codex keine automatischen Standardparameter.

## Promptvorlagen (Registerkarte)

### Promptvorlage anlegen

1. Öffnen Sie die Registerkarte **Promptvorlagen**
2. Klicken Sie auf **Hinzufügen**
3. Geben Sie einen **Namen** und den **Prompttext** ein
4. Klicken Sie **Speichern**

### Promptvorlage bearbeiten oder löschen

1. Öffnen Sie die Registerkarte **Promptvorlagen**
2. Ändern Sie Name oder Prompttext direkt in der Liste
3. Klicken Sie **Löschen**, wenn eine Vorlage entfernt werden soll
4. Klicken Sie **Speichern**, um die Änderungen dauerhaft zu übernehmen

### Platzhalter verwenden

Prompttexte können Platzhalter enthalten:

| Platzhalter | Bedeutung |
|-------------|-----------|
| `%ProjectName%` | Name des aktuellen Projekts |
| `%TaskName%` | Name der aktuellen Aufgabe |
| `%RepositoryUrl%` | URL des zugewiesenen Repositories |

Die Platzhalter werden erst beim Versand aus der Aufgabendetailansicht ersetzt.

## Speichern und Verwerfen

### Alle Einstellungen speichern

1. Nachdem Sie alle notwendigen Änderungen vorgenommen haben, klicken Sie in der Symbolleiste auf **Speichern**
2. Eine grüne Erfolgsmeldung bestätigt, dass die Einstellungen gespeichert wurden
3. Die Einstellungen werden persistent gespeichert und beim nächsten Start automatisch geladen

### Nicht gespeicherte Änderungen verwerfen

1. Klicken Sie auf die Schaltfläche **Verwerfen** in der Symbolleiste
2. Alle nicht gespeicherten Änderungen werden zurückgesetzt und die zuletzt gespeicherten Werte werden wiederhergestellt

## Fehlerbehebung

### Validierungsfehler beim Speichern

**Symptom:** Beim Klick auf „Speichern" erscheint eine Fehlermeldung wie „Pflichtfeld darf nicht leer sein" oder „Feld muss eine gültige Ganzzahl sein".

**Lösung:**
1. Lesen Sie die Fehlermeldung sorgfältig — sie teilt mit, welches Feld problematisch ist
2. Überprüfen Sie:
   - Alle mit roten Sternchen (*) gekennzeichneten Felder sind gefüllt
   - Zahlenfelder enthalten tatsächlich nur Ziffern
   - URLs vollständige Adressen sind (z. B. https://...)
3. Füllen Sie das Feld korrekt aus und versuchen Sie erneut zu speichern

### Einstellungen werden nach dem Neustart nicht beibehalten

**Symptom:** Sie speichern die Einstellungen, aber beim Öffnen des Fensters später sind sie wieder auf alte Werte zurückgesetzt.

**Lösung:**
1. Stellen Sie sicher, dass Sie tatsächlich **Speichern** geklickt haben — achten Sie auf die grüne Erfolgsmeldung
2. Falls Sie auf **Verwerfen** geklickt haben, werden die Änderungen nicht gespeichert
3. Öffnen Sie die Einstellungen erneut und wiederholen Sie die Änderung mit explizitem Speichern

### Plugin-Einstellungen werden nicht angezeigt

**Symptom:** Sie wählen ein Plugin aus, aber es erscheinen keine Einstellungsfelder.

**Lösung:**
1. Das Plugin hat möglicherweise keine zusätzlichen Einstellungen — das ist zulässig
2. Überprüfen Sie in der Dokumentation des Plugins, ob es konfigurierbar ist
3. Stellen Sie sicher, dass das Plugin korrekt installiert ist
4. Laden Sie die Anwendung neu und versuchen Sie es erneut
