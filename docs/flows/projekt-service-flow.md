# Ablauf – ProjektService (Projektverwaltung & Repository-Zuordnung)

## Titel & Kontext

Dieser Ablauf beschreibt die Projektverwaltung über `ProjektService`:
Übersicht laden, Projekt anlegen, Detaildaten laden, sowie ein einzelnes Projekt auf der Detailseite bearbeiten, archivieren, löschen und mit Repositorys verknüpfen.

Die UI trennt dabei Übersichts- und Detailaktionen:  
- **Übersicht (`ProjektListe`)**: Neu anlegen  
- **Detail (`ProjektDetail`)**: Bearbeiten, Archivieren, Löschen, Repository hinzufügen

---

## Diagramm A – Sequenz: Übersicht und Detailaktionen

```mermaid
sequenceDiagram
    actor U as Nutzer
    participant L as ProjektListe
    participant D as ProjektDetail
    participant Sel as PluginSelectionService
    participant PM as PluginManager
    participant PS as ProjektService
    participant AS as AufgabeService
    participant DB as SoftwareschmiededDbContext

    U->>L: Seite /projekte öffnen
    L->>PS: GetAllAsync()
    PS->>DB: Projekte AsNoTracking, OrderBy(Name)
    DB-->>PS: Projektliste
    PS-->>L: Liste rendern

    U->>L: Neues Projekt speichern
    L->>PS: CreateAsync(name, beschreibung)
    PS->>DB: INSERT Projekt (Status=Aktiv)
    DB-->>PS: gespeichert
    PS-->>L: neues Projekt
    L->>PS: GetAllAsync() (Reload)

    U->>L: Projekt öffnen
    L->>D: Navigate /projekte/{id}
    D->>PS: GetDetailAsync(id)
    PS->>DB: Projekt inkl. Repositories + Aufgaben laden
    D->>AS: GetByProjektAsync(id), GetArchiviertByProjektAsync(id)
    D-->>U: Detailseite mit Aktionsleiste

    alt Detailaktion: Projekt aktualisieren
        D->>PS: UpdateAsync(id, name, beschreibung)
        PS->>DB: UPDATE Projekt
        DB-->>PS: gespeichert
    else Detailaktion: Projekt archivieren
        D->>PS: ArchivierenAsync(id)
        PS->>DB: UPDATE Status = Archiviert
    else Detailaktion: Repository hinzufügen
        D->>PM: GetSourceCodeManagementPlugins()
        D->>Sel: ResolveSourceCodeManagementPluginAsync(selectedPrefix)
        Sel-->>D: aufgelöstes SCM-Plugin
        D->>D: Render dynamische Felder aus GetRepositoryLinkFields()
        Note over D: Beispiele: GitHub -> RepositoryUrl + RepositoryName\nLocalDirectory -> SourceDirectory
        D->>PS: AddRepositoryAsync(id, pluginPrefix, fieldValues)
        PS->>DB: INSERT GitRepository (plugin-spezifische Feldwerte normalisiert)
    else Detailaktion: Projekt löschen
        D->>PS: DeleteAsync(id)
        PS->>DB: DELETE Projekt (inkl. verknüpfter Daten via DB-Regeln)
        D-->>L: Navigate /projekte
    end
```

---

## Diagramm B – CRUD-/Guard-Logik im Service

```mermaid
flowchart TD
    A([Service-Methode aufgerufen]) --> B{Lesezugriff?}
    B -- Ja --> C[AsNoTracking Query]
    C --> Z([Ergebnis zurückgeben])

    B -- Nein --> D{Entität vorhanden?}
    D -- Nein --> E[InvalidOperationException]
    D -- Ja --> F{Aktion}
    F -- Create --> G[Projekt erzeugen + Status Aktiv]
    F -- Update --> H[Name/Beschreibung setzen]
    F -- Archivieren --> I[Status Archiviert setzen]
    F -- Delete --> J[Projekt entfernen]
    F -- AddRepository --> K[GitRepository erzeugen]
    G --> L[SaveChangesAsync]
    H --> L
    I --> L
    J --> L
    K --> L
    L --> Z
```

---

## Schrittbeschreibung

1. **Projektübersicht laden**  
   - **Code:** `ProjektListe.razor.cs` (`OnInitializedAsync`) + `ProjektService.GetAllAsync`  
   - **Verhalten:** Sortierte Liste aktiver/archivierter Projekte via `AsNoTracking`.

2. **Projekt anlegen (Übersichtsseite)**  
   - **Code:** `ProjektListe.razor.cs` (`SpeichernAsync`) + `ProjektService.CreateAsync`  
   - **Verhalten:** Validierung auf UI-Seite (`Name` Pflicht), dann Persistierung mit `ProjektStatus.Aktiv`.

3. **Projektdetails laden**  
   - **Code:** `ProjektDetail.razor.cs` (`LadeAsync`) + `ProjektService.GetDetailAsync`  
   - **Verhalten:** Projekt inkl. `Repositories`/`Aufgaben`, plus aktive und archivierte Aufgabenlisten.

4. **Einzelaktionen auf Detailseite**  
   - **Code:** `ProjektDetail.razor.cs` (`UpdateAsync`, `ArchivierenAsync`, `DeleteAsync`, `AddRepositoryAsync`)  
   - **Verhalten:** Alle Einzelaktionen werden über `ProjektService` ausgeführt und danach neu geladen oder umgeleitet.

5. **Repository zuordnen (plugin-gesteuerte Felder)**  
   - **Code:** `ProjektDetail.razor(.cs)` + `PluginSelectionService.ResolveSourceCodeManagementPluginAsync` + `ProjektService.AddRepositoryAsync`  
   - **Verhalten:** Das Feldschema wird pro SCM-Plugin über `GetRepositoryLinkFields()` geladen.  
     Beim Öffnen der Maske wird das gespeicherte SCM-Standardplugin (falls gültig) automatisch vorausgewählt.  
     Für GitHub sind typischerweise `RepositoryUrl` und `RepositoryName` Pflichtfelder; für LocalDirectory `SourceDirectory`.

---

## Fehlerbehandlung

- **Projekt/Repository nicht gefunden**  
  - `UpdateAsync`, `ArchivierenAsync`, `DeleteAsync`, `RemoveRepositoryAsync`, `AddRepositoryAsync` werfen `InvalidOperationException`.

- **Ungültige Eingaben in UI**  
  - `ProjektListe` und `ProjektDetail` prüfen Pflichtfelder und setzen lokale Fehlermeldungen.

- **Persistenzfehler (DB/Constraints)**  
  - Exception propagiert zur UI; Seite zeigt Fehlermeldung.

---

## Abhängigkeiten

- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Components/Pages/Projekte/ProjektListe.razor.cs`
- `src/Softwareschmiede/Components/Pages/Projekte/ProjektDetail.razor.cs`
- `src/Softwareschmiede/Application/Services/AufgabeService.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
