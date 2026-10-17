# Anforderung – GitHub-Projekte vollständig überwachen und Ereignisse ohne bestehende Aufgabe ausführen

**Aufgaben-ID:** 2b92467c-e14e-49ca-92e3-72b84bb1e97f
**Verknüpftes Issue:** #283
**Branch:** task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u
**Quelle:** `issue.md` (Repository-Root, Stand 2026-10-17)

---

## 1. Ziel

Für jedes als Projekt erfasste und aktivierte GitHub-Repository sollen GitHub-Ereignisse automatisch überwacht werden. Ein neues, qualifiziertes Ereignis soll auch dann verarbeitet werden, wenn noch keine lokale Aufgabe existiert – inklusive automatischer Aufgabenanlage bzw. -wiederverwendung, automatischem Start und Zustellung an die konfigurierte Standard-CLI. Zusätzlich soll der beobachtete Fehlerfall behoben werden, in dem die CLI-Zustellung nach einer erstmaligen Vertrauenswürdigkeitsabfrage des Arbeitsverzeichnisses dauerhaft im Status „Wartend“ verbleibt.

## 2. Fachliche Anforderungen

Abgeleitet aus „Ausgangslage“ und „Anforderung“ der issue.md:

- **FR-1 (Ist-Zustand):** Die aktuelle SCM-Ereignisüberwachung liest ausschließlich Issue- und Pull-Request-Referenzen, die bereits an einer lokalen Aufgabe hinterlegt sind. Neue Issues, neue PRs und Kommentare zu noch nicht erfassten Referenzen bleiben unbeachtet.
- **FR-2 (Überwachungsumfang):** Für jedes als Projekt erfasste **und aktivierte** GitHub-Repository sollen GitHub-Ereignisse automatisch überwacht werden – nicht mehr nur Referenzen mit bereits bestehender Aufgabe.
- **FR-3 (Ereignisverarbeitung ohne Aufgabe):** Ein neues, qualifiziertes Ereignis soll auch dann verarbeitet werden, wenn noch keine lokale Aufgabe existiert. Bei qualifiziertem Auslöser wird eine passende lokale Aufgabe angelegt.
- **FR-4 (Wiederverwendung):** Existiert bereits eine passende Aufgabe, wird sie wiederverwendet; es entstehen keine Duplikate.
- **FR-5 (Automatischer Start und Zustellung):** Eine neu angelegte oder wiederverwendete Aufgabe wird – nach den vorhandenen Sicherheits- und Owner-Prüfungen – automatisch gestartet, und der Auftrag wird an die konfigurierte Standard-CLI zugestellt.
- **FR-6 (Fehlende Standard-CLI):** Fehlt eine Standard-CLI, bleibt das Ereignis mit einer eindeutigen, bedienbaren Aufforderung wartend.
- **FR-7 (Vertrauenswürdigkeitsabfrage – Fortsetzung):** Erkennt die CLI beim ersten Start das Arbeitsverzeichnis als nicht vertrauenswürdig, muss die Anwendung nach der manuellen Bestätigung die Zustellung zuverlässig fortsetzen können – ohne dass der Benutzer einen nicht wirksamen Wiederholungsweg suchen muss.
- **FR-8 (Vertrauenswürdigkeitsabfrage – Vermeidung, Prüfauftrag):** Es ist zu prüfen, ob die CLI mit sicheren, passenden Startparametern für das Arbeitsverzeichnis gestartet werden kann, um die Vertrauenswürdigkeitsabfrage kontrolliert zu vermeiden oder vor dem Prompt deterministisch abzuschließen.
- **FR-9 (Bestandsschutz):** Bestehende Anforderungen an Trigger, Repository-Owner, Token-Berechtigungen, deduplizierte Zustellung und die SCM-Ereignisübersicht bleiben erhalten.

*Annahmen (gekennzeichnet, nicht aus dem Text belegt):*

- *Annahme A1:* „Qualifizierter Auslöser“ bezieht sich auf die bereits vorhandenen Trigger-Regeln der SCM-Ereignisverarbeitung; der Text definiert keine neuen Trigger-Bedingungen.
- *Annahme A2:* Die Überwachung nutzt den bestehenden Erkennungsmechanismus (Polling/API-Abfrage); ein neuer Mechanismus (z. B. Webhooks) wird nicht gefordert.

## 3. Betroffene Bereiche

Soweit aus der Anforderung ableitbar:

- **SCM-Ereignisüberwachung:** Erweiterung von „nur bereits referenzierte Issues/PRs“ auf vollständige Überwachung erfasster, aktivierter GitHub-Repositories (neue Issues, neue PRs, Kommentare, Reviews zu noch nicht erfassten Referenzen).
- **Aufgabenverwaltung:** Automatische Anlage lokaler Aufgaben aus qualifizierten Ereignissen sowie Wiederverwendung/Deduplizierung gegenüber bestehenden Aufgaben.
- **CLI-Zustellung:** Auflösung der Standard-CLI, Umgang mit fehlender Standard-CLI (wartend + bedienbare Aufforderung), Fortsetzung der Zustellung nach manueller Vertrauenswürdigkeitsbestätigung des Arbeitsverzeichnisses, ggf. Startparameter der CLI.
- **Sicherheits- und Owner-Prüfungen:** Bleiben unverändert wirksam und werden vor dem automatischen Start angewendet.
- **SCM-Ereignisübersicht:** Bestehende Anforderungen bleiben erhalten. *Annahme A3:* Die „eindeutige, bedienbare Aufforderung“ bei fehlender Standard-CLI wird dort (oder im vorhandenen Ereignis-Detailkontext mit den Aktionen „Öffnen / CLI auswählen“ und „Erneut prüfen“) dargestellt – die konkrete Stelle ist im Text nicht benannt.

## 4. Abläufe

### 4.1 Beobachteter Ablauf (Ist, Fehlerfall)

1. Ein Issue-Kommentar wurde erkannt.
2. Zunächst fehlte eine Standard-CLI.
3. Nach „Öffnen / CLI auswählen“ erwartete die CLI die Bestätigung der Vertrauenswürdigkeit des Arbeitsverzeichnisses.
4. Nach manueller Bestätigung lösen weder ein weiterer Klick auf „Öffnen / CLI auswählen“ noch „Erneut prüfen“ die Prompt-Zustellung aus; der Status bleibt „Wartend“.

### 4.2 Erwarteter neuer Ablauf (Soll)

1. Ein GitHub-Ereignis (neues Issue, neuer PR, neuer Kommentar, neues Review) tritt in einem als Projekt erfassten und aktivierten Repository auf.
2. Das Ereignis wird erkannt – unabhängig davon, ob bereits eine lokale Aufgabe für die Referenz existiert.
3. Bei qualifiziertem Auslöser:
   - Existiert eine passende Aufgabe, wird sie wiederverwendet (kein Duplikat).
   - Existiert keine, wird eine passende lokale Aufgabe angelegt.
4. Nach den vorhandenen Sicherheits- und Owner-Prüfungen wird die Aufgabe automatisch gestartet und der Auftrag an die konfigurierte Standard-CLI zugestellt.
5. Fehlt eine Standard-CLI: Das Ereignis bleibt mit einer eindeutigen, bedienbaren Aufforderung wartend.
6. Verlangt die CLI beim ersten Start eine Vertrauenswürdigkeitsbestätigung des Arbeitsverzeichnisses:
   - Nach manueller Bestätigung setzt die Anwendung die Zustellung zuverlässig fort (kein toter „Wartend“-Zustand, kein unwirksamer Wiederholungsweg), **oder**
   - die CLI wird mit sicheren, passenden Startparametern gestartet, sodass die Abfrage kontrolliert vermieden bzw. vor dem Prompt deterministisch abgeschlossen wird (Prüfauftrag).

## 5. Konfiguration

Soweit aus der Anforderung ableitbar:

- **Projekterfassung/-aktivierung:** Ein GitHub-Repository muss als Projekt erfasst und aktiviert sein, damit seine Ereignisse überwacht werden.
- **Standard-CLI:** Muss konfiguriert sein, damit die Zustellung erfolgen kann; ohne Standard-CLI bleibt das Ereignis wartend.
- **Trigger (qualifizierter Auslöser):** Bestehende Trigger-Konfiguration bleibt erhalten; Ereignisse ohne qualifizierten Auslöser führen nicht zur Aufgabenanlage.
- **Repository-Owner:** Bestehende Owner-Prüfung bleibt erhalten.
- **Token-Berechtigungen:** Bestehende Anforderungen bleiben erhalten. *Annahme A4:* Für die repositoryweite Überwachung könnten Token-Berechtigungen fachlich relevant sein; konkrete Änderungen sind im Text nicht gefordert.

## 6. Akzeptanzkriterien

1:1 aus issue.md übernommen:

- [ ] Neue Issues und Pull Requests in einem erfassten GitHub-Repository werden erkannt und führen bei qualifiziertem Auslöser zur Anlage einer passenden lokalen Aufgabe.
- [ ] Neue Kommentare und Reviews zu Issues bzw. PRs werden erkannt, auch wenn die Referenz zuvor noch nicht als Aufgabe angelegt wurde.
- [ ] Falls bereits eine passende Aufgabe existiert, wird sie wiederverwendet; es entstehen keine Duplikate.
- [ ] Eine neu angelegte oder wiederverwendete Aufgabe wird nach den vorhandenen Sicherheits- und Owner-Prüfungen automatisch gestartet und der Auftrag an die konfigurierte Standard-CLI zugestellt.
- [ ] Fehlt eine Standard-CLI, bleibt das Ereignis mit einer eindeutigen, bedienbaren Aufforderung wartend.
- [ ] Erkennt die CLI beim ersten Start das Arbeitsverzeichnis als nicht vertrauenswürdig, muss die Anwendung nach der manuellen Bestätigung die Zustellung zuverlässig fortsetzen können – ohne dass der Benutzer einen nicht wirksamen Wiederholungsweg suchen muss.
- [ ] Prüfen, ob die CLI mit sicheren, passenden Startparametern für das Arbeitsverzeichnis gestartet werden kann, um die Vertrauenswürdigkeitsabfrage kontrolliert zu vermeiden oder vor dem Prompt deterministisch abzuschließen.
- [ ] Bestehende Anforderungen an Trigger, Repository-Owner, Token-Berechtigungen, deduplizierte Zustellung und die SCM-Ereignisübersicht bleiben erhalten.

## 7. Explizite Nicht-Ziele

Nur aus der Anforderung ableitbar:

- Keine Änderung oder Abschwächung bestehender Anforderungen an Trigger, Repository-Owner, Token-Berechtigungen, deduplizierte Zustellung und die SCM-Ereignisübersicht.
- Keine Duplikatanlage: Für Ereignisse mit bereits passender Aufgabe wird keine neue Aufgabe erzeugt.
- Keine Verarbeitung nicht-qualifizierter Ereignisse: Die Aufgabenanlage erfolgt nur bei qualifiziertem Auslöser.
- Keine Einführung neuer Trigger-Arten (ableitbar aus dem Bestandsschutz der Trigger-Anforderungen; *als Annahme gekennzeichnet*).
- Repositories, die nicht als Projekt erfasst oder nicht aktiviert sind, werden nicht überwacht.

## 8. Offene Fragen

- **Q1 – Qualifizierter Auslöser:** Welche bestehenden Trigger-Regeln qualifizieren ein Ereignis zur Aufgabenanlage, wenn noch keine Aufgabe existiert (z. B. Labels, Autor/Owner, Schlüsselwörter)? Der Text nennt nur „qualifizierter Auslöser“ ohne Definition.
- **Q2 – Inhalte der automatisch angelegten Aufgabe:** Aus welchen Ereignisdaten werden Titel und Auftrag/Prompt der neuen Aufgabe gebildet (Issue-/PR-Text, Kommentartext)? Nicht spezifiziert.
- **Q3 – Ereignistypen im Detail:** Sind mit „Kommentare und Reviews“ auch Review-Kommentare und Review-Threads an PRs gemeint oder nur Top-Level-Kommentare bzw. Review-Entscheide?
- **Q4 – „Bedienbare Aufforderung“:** Wo und wie wird die Aufforderung bei fehlender Standard-UI dargestellt (SCM-Ereignisübersicht, Benachrichtigung)? Bestehen die Aktionen „Öffnen / CLI auswählen“ und „Erneut prüfen“ weiter, und müssen sie nach der Korrektur wirksam sein?
- **Q5 – Fortsetzung nach Trust-Bestätigung:** Soll die Anwendung die Zustellung nach manueller Bestätigung automatisch fortsetzen, oder genügt ein zuverlässig wirksamer manueller Wiederholungsweg? Der Text fordert „zuverlässig fortsetzen können“, lässt die konkrete Bedienung offen.
- **Q6 – CLI-Startparameter:** Für welche CLI(s) und mit welchen konkreten Parametern soll geprüft werden, die Vertrauenswürdigkeitsabfrage zu vermeiden/vorwegzunehmen? Der Text formuliert nur einen Prüfauftrag.
- **Q7 – Überwachungsmechanismus:** Erfolgt die vollständige Repository-Überwachung über den bestehenden Polling-/API-Mechanismus oder ist ein anderer Ansatz vorgesehen? Nicht spezifiziert.
