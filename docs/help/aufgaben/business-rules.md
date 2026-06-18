# Aufgaben & KI-Entwicklungsprozess — Business Rules

## Exklusiver KI-Lauf

**Beschreibung:** Für eine Aufgabe kann immer nur ein KI-Lauf gleichzeitig aktiv sein.

**Bedingungen:**
- `KiAusfuehrungsService.IsRunning(aufgabeId)` gibt `true` zurück.

**Verhalten:**
- Wenn aktiv: Zweiter `StartKiLauf`-Aufruf wird mit `LogWarning` abgewiesen, kein zweiter Task gestartet.
- Wenn nicht aktiv: Neuer Lauf wird gestartet.

**Umsetzung:** `KiAusfuehrungsService.StartKiLauf` — Guards gegen doppelte Ausführung im In-Memory-Dictionary.

---

## Branch-Strategie beim Prozessstart

**Beschreibung:** Die Branch-Wahl hängt davon ab, ob ein vorhandener Remote-Branch angegeben wurde und ob dieser der Hauptbranch ist.

**Bedingungen:**
- `basisBranchName` leer oder null → immer neuer `task/`-Branch.
- `basisBranchName` == Hauptbranch → neuer `task/`-Branch (kein direkter Commit auf Main).
- `basisBranchName` != Hauptbranch → vorhandener Branch wird ausgecheckt.

**Umsetzung:** `EntwicklungsprozessService.ProzessStartenAsync` — `nutzeExistierendenBranch`-Flag.

---

## Kontextkomprimierung

**Beschreibung:** Wird die Kontextdatei zu groß, komprimiert die KI sie selbst auf ein strukturiertes Markdown-Dokument.

**Bedingungen:**
- `context.Length > SoftLimit` (Standard: 12 000 Zeichen): Komprimierung wird ausgelöst.
- `context.Length > HardLimit` (Standard: 20 000 Zeichen): Warneintrag im Protokoll, KI-Lauf läuft trotzdem.

**Verhalten:**
- Soft-Limit überschritten: `CompressContextAsync` startet einen separaten KI-Lauf mit Komprimierungsprompt. Das Ergebnis muss die Pflichtabschnitte „Ziel", „Offene Punkte" und „Letzte Entscheidungen" enthalten.
- Pflichtabschnitte fehlen oder Ergebnis leer: Exception, KI-Lauf wird nicht gestartet.

**Umsetzung:** `EntwicklungsprozessService.EnsureContextWithinLimitsAsync`, `CompressContextAsync`, `ContainsMandatoryCompressionSections`.

---

## Rate-Limit-Erkennung und Vorschlag

**Beschreibung:** Erkennt die KI ein Rate-Limit in der Ausgabe, wird automatisch ein Prompt-Vorschlag mit Ausführungszeitpunkt gespeichert.

**Bedingungen:**
- Ausgabezeile beginnt mit `[[SOFTWARESCHMIEDE_RATE_LIMIT]]`.
- Enthält `resetUtc=<ISO-Zeit>` und optional `prompt=<Text>`.

**Verhalten:**
- Vorschlag und Zeitpunkt werden in `Aufgabe.VorschlagPrompt` / `VorschlagAusfuehrenAbUtc` gespeichert.
- In der UI erscheint der Vorschlag vorausgefüllt mit dem gespeicherten Ausführungszeitpunkt.
- Kein automatischer Neustart — der Anwender muss manuell senden oder den Zeitpunkt anpassen.

**Umsetzung:** `EntwicklungsprozessService.TryParseRateLimitSuggestion`, `AufgabeService.SavePromptVorschlagAsync`.

---

## Aufgaben-Recovery

**Beschreibung:** Eine Aufgabe im Status `InBearbeitung` oder `KiAktiv` kann manuell wiederhergestellt werden, wenn der Prozess nicht mehr läuft.

**Bedingungen:**
- Status ist `InBearbeitung` oder `KiAktiv`.
- Kein aktiver KI-Lauf im `KiAusfuehrungsService`.
- `LastHeartbeatUtc` ist älter als 5 Minuten oder nicht gesetzt.

**Verhalten:**
- Wenn alle Bedingungen erfüllt: Button „🩹 Aufgabe wiederherstellen" ist klickbar, Status wird auf `InBearbeitung` zurückgesetzt.
- Wenn Bedingungen nicht erfüllt: Button deaktiviert mit erklärendem Hinweistext.

**Umsetzung:** `AufgabeRecoveryService`, `AufgabeDetail.razor.cs._recoveryAllowed`.

---

## Klonpfad-Bereinigung

**Beschreibung:** Das lokale Klonverzeichnis enthält unter Windows schreibgeschützte Git-Pack-Dateien, die normales `Directory.Delete` verhindern.

**Verhalten:**
- Vor dem Löschen werden alle Dateien im Verzeichnis auf `FileAttributes.Normal` gesetzt.
- Danach erfolgt `Directory.Delete(path, recursive: true)`.

**Umsetzung:** `EntwicklungsprozessService.DeleteDirectoryForce` — notwendig für `.git/objects/pack/*.idx`-Dateien unter Windows.
