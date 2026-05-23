# Ablauf – Manuelle Aufgaben-Recovery

## Titel & Kontext

Dieser Ablauf beschreibt die manuelle Wiederherstellung einer festhängenden Aufgabe in `AufgabeDetail`.  
Die Recovery ist ausschließlich für `KiAktiv` und `TestsLaufen` vorgesehen und setzt die Aufgabe auf `InBearbeitung`, sofern keine Verarbeitung mehr läuft.

> Verwandte Artefakte:  
> [Requirements](../requirements/aufgabe-recovery-wiederherstellung-requirements-analysis.md) ·
> [Blueprint](../architecture/aufgabe-recovery-wiederherstellung-architecture-blueprint.md) ·
> [Technischer Contract](../api/aufgabe-recovery.md)

---

## Diagramm A – Sequenzablauf

```mermaid
sequenceDiagram
    actor Nutzer
    participant Detail as AufgabeDetail
    participant Recovery as AufgabeRecoveryService
    participant Runtime as IRunningAutomationStatusSource
    participant DB as SoftwareschmiededDbContext

    Nutzer->>Detail: "Aufgabe wiederherstellen" klicken
    Detail->>Detail: Bestätigungsdialog
    Nutzer->>Detail: "Ja, wiederherstellen"
    Detail->>Recovery: RecoverManuellAsync(aufgabeId)
    Recovery->>DB: Aufgabe laden
    Recovery->>Runtime: IsRunning(aufgabeId)
    alt isRunning == true
        Recovery-->>Detail: Fehler StillRunning
        Detail->>Detail: Fehlermeldung anzeigen + Daten neu laden
    else isRunning == false
        Recovery->>DB: Conditional Statusupdate + RecoveryVersion++
        alt Konflikt (rowCount == 0)
            Recovery-->>Detail: Fehler ConcurrencyConflict
            Detail->>Detail: Fehlermeldung anzeigen + Daten neu laden
        else Erfolg
            Recovery->>DB: Audit-Protokoll speichern
            Recovery-->>Detail: Erfolg
            Detail->>Detail: Erfolgsmeldung anzeigen + Daten neu laden
        end
    end
```

---

## Diagramm B – Entscheidungslogik

```mermaid
flowchart TD
    A[Recovery in UI angefordert] --> B{Status recoverbar?}
    B -- Nein --> B1[Aktion nicht sichtbar]
    B -- Ja --> C{Laufstatus prüfbar?}
    C -- Nein --> C1[Aktion deaktiviert + Hinweis]
    C -- Ja --> D{IsRunning?}
    D -- Ja --> D1[Aktion deaktiviert + Hinweis]
    D -- Nein --> E[Bestätigung]
    E --> F[RecoverManuellAsync]
    F --> G{Conditional Update erfolgreich?}
    G -- Nein --> G1[Concurrency-Fehler]
    G -- Ja --> H[Audit schreiben + Erfolg]
```

---

## Schrittbeschreibung

1. **UI-Einblendung und Vorprüfung**
   - Nur bei `IstRecoveryStatus` (`KiAktiv`, `TestsLaufen`).
   - Disable-Reason bei laufender Verarbeitung oder nicht prüfbarem Laufstatus.

2. **Bestätigung durch Anwender**
   - Recovery startet erst nach expliziter Bestätigung.

3. **Service-Eligibility**
   - Aufgabe vorhanden?
   - Status recoverbar?
   - Laufstatus verfügbar und `!IsRunning`?

4. **Konsistenter Statuswechsel**
   - Conditionales Update mit Guard auf `Status` und `RecoveryVersion`.
   - Bei Erfolg: `Status = InBearbeitung`, `RecoveryVersion++`.

5. **Audit und Rückmeldung**
   - `Protokolleintrag` vom Typ `StatusUebergang`.
   - UI lädt Aufgabe neu und zeigt Erfolg/Fehler.

---

## Fehlerpfade

- Aufgabe nicht gefunden
- Nicht recoverbarer Status
- Verarbeitung läuft noch
- Laufstatusprüfung nicht möglich
- Concurrency-Konflikt

Alle Fehler werden als `InvalidOperationException` mit verständlichem Text an die UI propagiert.

