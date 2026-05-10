# Entity-Relationship-Modell – Plugin-Klassenbibliotheken (GitHub & GitHub Copilot)

> **Dokument-Typ:** Feature-spezifisches ERM  
> **Projekt:** Softwareschmiede  
> **Verbindliche Quellen:**  
> 1) [`../../.copilot-task.md`](../../.copilot-task.md)  
> 2) [`../requirements/plugin-klassenbibliotheken-github-und-copilot.md`](../requirements/plugin-klassenbibliotheken-github-und-copilot.md)  
> 3) [`./plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md`](./plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md)  
> **Status:** ✅ Umgesetzt  
> **Version:** 1.0.0

---

## 1. Kontext und Ziel

Dieses ERM beschreibt das **Datenmodell/Metadatenmodell für Plugin-Discovery und Plugin-Typisierung** gemäß Requirements und Architektur-Blueprint:

- Discovery aus `<Programmverzeichnis>/plugins/*.dll`
- Typisierung über `PluginType` mit
  - `SourceCodeManagement`
  - `DevelopmentAutomation`
- Registrierung über `PluginManager`/`PluginRegistry`

---

## 2. ERM-Diagramm (Mermaid)

```mermaid
erDiagram
    PluginType {
        int Id PK
        string Code UK "SourceCodeManagement | DevelopmentAutomation"
        string DisplayName
        bool IsActive
    }

    PluginAssembly {
        guid Id PK
        string FileName
        string RelativePath UK "plugins/Softwareschmiede.Plugin.X.dll"
        string AssemblyName
        string AssemblyVersion
        string FileHashSha256
        datetime LastWriteUtc
        bool IsLoadable
        string LoadError "optional"
    }

    PluginDescriptor {
        guid Id PK
        guid PluginAssemblyId FK
        int PluginTypeId FK
        string PluginName
        string PluginPrefix
        string ImplementationType "Namespace.ClassName"
        string ContractInterface "IGitPlugin | IKiPlugin"
        bool IsEnabled
        bool IsHealthy "optional, last check"
        datetime DiscoveredAtUtc
    }

    PluginCapability {
        guid Id PK
        string Code UK
        string Description
    }

    PluginDescriptorCapability {
        guid PluginDescriptorId FK
        guid PluginCapabilityId FK
        PK "PluginDescriptorId, PluginCapabilityId"
    }

    PluginRegistryEntry {
        guid Id PK
        int PluginTypeId FK
        guid PluginDescriptorId FK
        bool IsDefaultForType
        int Priority
        datetime RegisteredAtUtc
    }

    PluginConfigurationField {
        guid Id PK
        guid PluginDescriptorId FK
        string FieldKey
        string Label
        string DataType
        bool IsSecret
        bool IsRequired
    }

    PluginConfigurationBinding {
        guid Id PK
        guid PluginDescriptorId FK
        string CredentialStoreKey UK "<PluginPrefix>.<FieldKey>"
        string StorageProvider "WindowsCredentialStore"
        datetime LastValidatedUtc "optional"
    }

    PluginType ||--o{ PluginDescriptor : klassifiziert
    PluginAssembly ||--o{ PluginDescriptor : enthaelt
    PluginDescriptor ||--o{ PluginConfigurationField : definiert
    PluginDescriptor ||--o{ PluginConfigurationBinding : bindet
    PluginType ||--o{ PluginRegistryEntry : gruppiert
    PluginDescriptor ||--o{ PluginRegistryEntry : registriert
    PluginDescriptor ||--o{ PluginDescriptorCapability : hat
    PluginCapability ||--o{ PluginDescriptorCapability : wird_zugeordnet
```

---

## 3. Tabellarische Übersicht (Entitäten, Schlüssel, Beziehungen)

| Entität | Primärschlüssel | Wichtige Attribute | Beziehungen | Kardinalität |
|---|---|---|---|---|
| `PluginType` | `Id` | `Code`, `DisplayName`, `IsActive` | zu `PluginDescriptor`, `PluginRegistryEntry` | 1:n |
| `PluginAssembly` | `Id` | `RelativePath`, `AssemblyVersion`, `FileHashSha256`, `IsLoadable` | zu `PluginDescriptor` | 1:n |
| `PluginDescriptor` | `Id` | `PluginName`, `PluginPrefix`, `ImplementationType`, `ContractInterface`, `IsEnabled` | zu `PluginType`, `PluginAssembly`, `PluginRegistryEntry`, `PluginConfigurationField`, `PluginConfigurationBinding` | n:1 bzw. 1:n |
| `PluginCapability` | `Id` | `Code`, `Description` | zu `PluginDescriptor` über Join | n:m |
| `PluginDescriptorCapability` | `(PluginDescriptorId, PluginCapabilityId)` | — | Join zwischen Descriptor und Capability | n:m |
| `PluginRegistryEntry` | `Id` | `IsDefaultForType`, `Priority`, `RegisteredAtUtc` | zu `PluginType`, `PluginDescriptor` | n:1 + n:1 |
| `PluginConfigurationField` | `Id` | `FieldKey`, `DataType`, `IsSecret`, `IsRequired` | zu `PluginDescriptor` | n:1 |
| `PluginConfigurationBinding` | `Id` | `CredentialStoreKey`, `StorageProvider`, `LastValidatedUtc` | zu `PluginDescriptor` | n:1 |

---

## 4. Integritätsregeln

1. **Discovery-Pfad-Regel:** `PluginAssembly.RelativePath` muss unter `plugins/` liegen (FR-2.1).  
2. **Typisierungsregel:** Jeder `PluginDescriptor` muss genau **einen** `PluginType` referenzieren (FR-2.2).  
3. **Registry-Regel:** Pro `PluginType` darf es höchstens **einen** Eintrag mit `IsDefaultForType = true` geben.  
4. **Eindeutigkeit Descriptor:** `(PluginAssemblyId, ImplementationType)` muss eindeutig sein.  
5. **Interface-Konsistenz:**  
   - `PluginType=SourceCodeManagement` ⇒ `ContractInterface` kompatibel zu `IGitPlugin`  
   - `PluginType=DevelopmentAutomation` ⇒ `ContractInterface` kompatibel zu `IKiPlugin`  
6. **Credential-Key-Regel:** `PluginConfigurationBinding.CredentialStoreKey = <PluginPrefix>.<FieldKey>` muss exakt dem Schlüsselkonzept entsprechen (Blueprint 6.2).  
7. **Fehlertoleranzregel:** `PluginAssembly.IsLoadable = false` darf Host-Startup nicht verhindern; es entsteht nur kein `PluginRegistryEntry` (FR-2.3, NFR-3).

---

## 5. Modellierungsentscheidungen (Kurzbegründung)

- **Trennung `PluginAssembly` vs. `PluginDescriptor`:** Eine DLL kann mehrere Pluginklassen enthalten; daher getrennte Entitäten.  
- **`PluginType` als eigene Entität:** stabilisiert Typisierung und verhindert String-Streuung im System.  
- **`PluginRegistryEntry` separat:** ermöglicht Priorisierung/Default-Logik je Typ ohne Descriptor-Duplikation.  
- **`PluginConfigurationBinding` statt Secret-Wert:** Secret-Daten bleiben außerhalb der DB im Credential Store (Sicherheitskonsistenz).

---

## 6. Konsistenzabgleich mit Architektur-Blueprint

| Blueprint-Aussage | ERM-Abbildung | Ergebnis |
|---|---|---|
| Discovery aus `<Programmverzeichnis>/plugins` | `PluginAssembly.RelativePath` + Integritätsregel 1 | ✅ Konsistent |
| Zwei Pluginarten SCM / Development Automation | `PluginType.Code` + Regel 5 | ✅ Konsistent |
| `PluginManager` registriert nach Typ in Registry | `PluginRegistryEntry` mit FK auf `PluginType` + `PluginDescriptor` | ✅ Konsistent |
| Fehlerhafte DLL wird übersprungen, Start bleibt robust | `PluginAssembly.IsLoadable/LoadError`, keine Registry-Erzeugung | ✅ Konsistent |
| Settings über `PluginPrefix` + Credential Store | `PluginConfigurationField` + `PluginConfigurationBinding` | ✅ Konsistent |

---

## 7. Migrationseinfluss

### 7.1 Einfluss auf bestehende Datenbank

- **Kurzfristig (MVP Discovery):** Kein zwingender EF-Core-Migrationsbedarf, wenn Discovery/Registry rein zur Laufzeit in-memory bleibt.  
- **Mittelfristig (Persistenz von Discovery-Metadaten):** Einführung neuer Tabellen gemäß diesem ERM sinnvoll für Diagnose, Monitoring und reproduzierbare Plugin-Auswahl.

### 7.2 Empfohlene Migrationsschritte (bei Persistenz)

1. `PluginType` als Seed-Tabelle anlegen (2 feste Datensätze).  
2. Tabellen `PluginAssembly`, `PluginDescriptor`, `PluginRegistryEntry` hinzufügen.  
3. Danach optionale Erweiterungen: `PluginCapability`, `PluginConfigurationField`, `PluginConfigurationBinding`.  
4. Unique-/Check-Constraints für Regeln 2–6 ergänzen.  
5. Backfill: Bestehende feste Implementierungen (GitHub, GitHub Copilot) als initiale Descriptor-/Registry-Datensätze übernehmen.

---

## 8. Querverweise

- Anforderungen: [`../requirements/plugin-klassenbibliotheken-github-und-copilot.md`](../requirements/plugin-klassenbibliotheken-github-und-copilot.md)  
- Architektur: [`./plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md`](./plugin-klassenbibliotheken-github-und-copilot-architecture-blueprint.md)  
- Ursprungsauftrag: [`../../.copilot-task.md`](../../.copilot-task.md)

---

## 9. Versionshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0.0 | 2026-05-10 | planning-entity-relationship-modeler | Initiales feature-spezifisches ERM für Plugin-Discovery und Plugin-Typisierung (GitHub & GitHub Copilot) |

