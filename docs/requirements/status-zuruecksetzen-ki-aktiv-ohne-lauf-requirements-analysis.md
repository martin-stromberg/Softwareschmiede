# Anforderungsanalyse – Status zurücksetzen bei `KI Aktiv` ohne Lauf

> **Dokument-Typ:** Requirements Analysis  
> **Status:** Entwurf  
> **Version:** 1.0.0  
> **Thema:** Der Aktionsbutton **„Status zurücksetzen“** darf bei `KI Aktiv` nicht allein wegen des Status gesperrt sein.

---

## 1. Überblick und Projektkontext

In der Aufgaben-Detailansicht existiert eine Aktion zum Zurücksetzen des Aufgabenstatus.  
Aktuell wird sie bei Aufgaben im Status `KI Aktiv` zu grob blockiert.  
Das ist nur dann korrekt, wenn tatsächlich noch eine KI-Ausführung läuft.  
Ist keine Ausführung aktiv, muss der Anwender den Status zurücksetzen können, damit eine neue Anfrage möglich wird.

**Abgrenzung:**  
Die bestehende Recovery-Logik für festhängende Aufgaben bleibt fachlich getrennt.  
Dieser Fix korrigiert primär die **Erreichbarkeit und Bestätigung** der Statusrücksetzung in der UI.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Priorität |
|---|---|---|
| **FR-1** | Der Button **„Status zurücksetzen“** ist in `AufgabeDetail` bei `KI Aktiv` aufrufbar, wenn keine KI-Ausführung läuft. | MUST |
| **FR-2** | Die Freigabe des Buttons richtet sich nach dem **tatsächlichen Laufzustand**, nicht nur nach dem Aufgabenstatus. | MUST |
| **FR-3** | Vor der Ausführung erscheint eine **Bestätigungsabfrage**. | MUST |
| **FR-4** | Nach Bestätigung wird der Aufgabenstatus so zurückgesetzt, dass **eine neue Anfrage möglich** ist. | MUST |
| **FR-5** | Läuft die KI doch noch, bleibt die Aktion gesperrt oder wird mit einem klaren Hinweis abgelehnt. | MUST |
| **FR-6** | Bestehende Recovery-/Abbruchpfade bleiben getrennt und unverändert. | MUST |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Priorität |
|---|---|---|
| **NFR-1** | Die UI muss klar zeigen, wann die Aktion verfügbar oder gesperrt ist. | HIGH |
| **NFR-2** | Vor der Statusmutation ist der Laufzustand erneut zu prüfen. | MUST |
| **NFR-3** | Für diesen Fix sind **keine neuen Tabellen, Spalten oder Beziehungen** erforderlich. Ein ERM ist nicht sinnvoll. | MUST |
| **NFR-4** | Der bestehende KI-Start-/Abschlussfluss darf nicht beeinträchtigt werden. | MUST |
| **NFR-5** | Button-Verhalten, Bestätigung und Statuswechsel müssen testbar abgesichert sein. | HIGH |

---

## 4. Akzeptanzkriterien

- **AC-1:** Bei einer Aufgabe mit Status `KI Aktiv` ist **„Status zurücksetzen“** verfügbar, wenn keine KI-Ausführung läuft.
- **AC-2:** Ein Klick auf den Button öffnet eine Bestätigungsabfrage.
- **AC-3:** Nach Bestätigung wird der Status auf einen Zustand gesetzt, der neue Anfragen erlaubt.
- **AC-4:** Wenn eine KI-Ausführung noch läuft, wird das Zurücksetzen blockiert.
- **AC-5:** Recovery-/Abbruchfunktionen bleiben fachlich und technisch getrennt.
- **AC-6:** Es sind keine Datenmodell- oder ERM-Anpassungen erforderlich.

---

## 5. Annahmen und Abhängigkeiten

| Typ | Eintrag | Auswirkung |
|---|---|---|
| Annahme | Der Laufzustand der KI ist zur Laufzeit zuverlässig prüfbar. | Die Freigabe kann am Realzustand ausgerichtet werden. |
| Abhängigkeit | `AufgabeDetail` steuert die Aktion und die Bestätigungslogik. | Der Fix liegt in der UI-/Komponentenlogik. |
| Abhängigkeit | Vorhandene Status-Transitions in `AufgabeService` / Recovery-Logik werden wiederverwendet. | Kein neuer Persistenzpfad nötig. |
| Hinweis | ERM nicht sinnvoll. | Für diesen Fix entstehen keine neuen Domänenobjekte. |

---

## 6. Scope und Out-of-Scope

**In Scope**
- Freigabe des Buttons **„Status zurücksetzen“** bei `KI Aktiv` ohne laufende KI
- Bestätigungsdialog vor der Ausführung
- Statusrücksetzung für neue Anfragen
- Saubere Trennung zur bestehenden Recovery-Logik

**Out of Scope**
- Änderungen an der eigentlichen KI-Ausführung
- Beenden laufender KI-Prozesse
- Neue Statuswerte
- Datenbankmigrationen
- Größere UI-Umgestaltung

---

## 7. Referenzen

- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor.cs`
- `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `docs/flows/development-process-flow.md`
- `docs/requirements/aufgabe-recovery-wiederherstellung-requirements-analysis.md`

---

## 8. Einordnung zur Dokumentation

Für diesen Bugfix ist eine **eigene** Requirements-Analyse sinnvoll.  
Sie grenzt die Fehlersituation klar von der bestehenden Recovery-Dokumentation ab und verhindert Doppelungen.
