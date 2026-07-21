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

### Visual-Studio-Code-Fallback aktivieren

1. Öffnen Sie die Registerkarte **Allgemein**
2. Aktivieren Sie **Visual Studio Code oeffnen, wenn keine Visual-Studio-Solution gefunden wurde**
3. Klicken Sie **Speichern**
4. Öffnen Sie danach eine Aufgabe ohne `*.sln`-Datei im Arbeitsverzeichnis
5. **IDE öffnen** startet nun Visual Studio Code mit dem Arbeitsverzeichnis, sofern VS Code gefunden wird

> **Hinweis:** Die Option ist standardmäßig ausgeschaltet. Bestehende Installationen ändern ihr Verhalten erst, wenn Sie diese Einstellung aktivieren.

## Quellcodeverwaltung (Registerkarte)

### Standard-SCM-Plugin auswählen

1. Öffnen Sie die Registerkarte **Quellcodeverwaltung**
2. Klicken Sie auf das Dropdown-Feld **Standard SCM-Plugin**
3. Wählen Sie das Plugin, das Sie verwenden möchten (z. B. GitHub)
4. Die verfügbaren Einstellungen für dieses Plugin werden automatisch unterhalb der Dropdown-Liste angezeigt

### Authentifizierung einrichten

1. Nachdem Sie das Plugin ausgewählt haben, werden die notwendigen Felder angezeigt
2. Typischerweise ist das erste Feld ein **Authentifizierungs-Token** oder **API-Schlüssel**
3. Kopieren Sie den Token von der Website des Plugins (z. B. GitHub Settings → Developer settings → Personal access tokens)
4. Fügen Sie den Token in das Feld ein — bei Geheim-Feldern wird die Eingabe maskiert
5. Füllen Sie weitere notwendige Felder (z. B. Benutzername, Zugangsparameter) aus
6. Klicken Sie **Speichern**

> **Hinweis:** Alle mit einem roten Sternchen (*) gekennzeichneten Felder sind Pflichtfelder und müssen ausgefüllt werden, bevor Sie speichern können.

### Einstellungen zwischen Plugins wechseln

1. Öffnen Sie die Registerkarte **Quellcodeverwaltung**
2. Wählen Sie ein anderes Plugin aus dem Dropdown-Feld
3. Die Einstellungsfelder werden sofort auf die Felder des neuen Plugins aktualisiert
4. Die Werte des vorherigen Plugins bleiben gespeichert und werden wiederhergestellt, wenn Sie zurückwechseln

## KI (Registerkarte)

### Standard-KI-Plugin auswählen

1. Öffnen Sie die Registerkarte **KI**
2. Klicken Sie auf das Dropdown-Feld **Standard KI-Plugin**
3. Wählen Sie das KI-Plugin, das Sie verwenden möchten (z. B. Claude)
4. Die Einstellungsfelder des gewählten Plugins werden angezeigt

### KI-API-Schlüssel konfigurieren

1. Nachdem Sie das Plugin ausgewählt haben, werden die Eingabefelder angezeigt
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

1. Öffnen Sie die Registerkarte **KI**
2. Wählen Sie **Codex CLI** als KI-Plugin
3. Tragen Sie im Feld **CommandLineParameters** nur die zusätzlichen Argumente ein, die Sie beim Start der Codex CLI verwenden möchten, z. B. `--model gpt-5`
4. Lassen Sie das Feld leer, wenn die Codex CLI ohne zusätzliche Argumente gestartet werden soll
5. Klicken Sie **Speichern**

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
