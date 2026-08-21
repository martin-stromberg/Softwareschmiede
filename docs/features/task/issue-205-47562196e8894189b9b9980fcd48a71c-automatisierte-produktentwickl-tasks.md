# Tasks: Automatisierte Produktentwicklung mit autonomen Aufgaben

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Enum-Wert `AutonomAufgabe` zu `AufgabeAusfuehrungsStatus` hinzufügen | Offen | — |
| 2 | Datenmodell | Entity `AutonomAufgabeKonfiguration` erstellen (`src/Softwareschmiede/Domain/Entities/`) mit allen erforderlichen Properties | Offen | — |
| 3 | Datenmodell | Entity `UnteragentSpezifikation` erstellen (`src/Softwareschmiede/Domain/Entities/`) mit allen erforderlichen Properties | Offen | — |
| 4 | Datenmodell | Entity `SkillDefinition` erstellen (`src/Softwareschmiede/Domain/Entities/`) mit allen erforderlichen Properties | Offen | — |
| 5 | Datenmodell | Entity `Aufgabe` mit vier neuen Properties erweitern: `AutonomKonfiguration`, `ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten` | Offen | — |
| 6 | Datenmodell | DbContext-Registrierung: drei neue `DbSet`s und Beziehungen konfigurieren | Offen | — |
| 7 | Datenmodell | Migration `AddAutonomAufgabeModels` erstellen (neue Tabellen) | Offen | — |
| 8 | Datenmodell | Migration `AddAutonomAufgabeColumnsToAufgaben` erstellen (neue Spalten) | Offen | — |
| 9 | Datenmodell | Migration `UpdateAusfuehrungsStatusEnum` erstellen (Enum-Erweiterung) | Offen | — |
| 10 | Services — Core | `AutonomAufgabenInitialisierungsService` implementieren mit `InitialisiereAsync` und `ErstelleArbeitsverzeichnisStrukturAsync` | Offen | — |
| 11 | Services — Core | `UnteragentGovernanceService` implementieren mit `VerifiziereBerechtigung` und `ValidiereFehlerBedingungAsync` | Offen | — |
| 12 | Services — Core | `SessionManagementService` implementieren mit `PauseAufgabeBeiBudgetLimitAsync`, `SetzeFortAsync`, `PruefeAusfuehrungAsync` | Offen | — |
| 13 | Services — Agent | `ProjektleiterAgentService` implementieren mit `StarteAgenAsync`, `SteuereUnteragentAsync`, `IntegriereErgebnisseAsync` | Offen | — |
| 14 | Services — Integration | Methode `ErzeugeAutonomAufgabeAsync` zu `AufgabeService` hinzufügen | Offen | — |
| 15 | ViewModels | `AutonomAufgabeInitialisierungsDialogViewModel` implementieren mit Formularfeld-Properties und Bestätigung/Abbruch-Methoden | Offen | — |
| 16 | ViewModels | `AutonomAufgabeDetailViewModel` implementieren mit Konfiguration-, Plan-, Fortschritts- und Steuerungs-Properties/Methoden | Offen | — |
| 17 | XAML-Views | `AutonomAufgabeInitialisierungsDialog.xaml` erstellen mit Formularfeldern und Buttons | Offen | — |
| 18 | XAML-Views | `AutonomAufgabeDetailView.xaml` erstellen mit Tab-Interface für Konfiguration, Plan, Fortschritt, Governance, Skills, Unteragenten | Offen | — |
| 19 | Unit-Tests | `AutonomAufgabenInitialisierungsServiceTests` erstellen (mindestens 4 Tests) | Offen | — |
| 20 | Unit-Tests | `UnteragentGovernanceServiceTests` erstellen (mindestens 4 Tests) | Offen | — |
| 21 | Unit-Tests | `SessionManagementServiceTests` erstellen (mindestens 4 Tests) | Offen | — |
| 22 | Unit-Tests | `ProjektleiterAgentServiceTests` erstellen (mindestens 3 Tests) | Offen | — |
| 23 | Unit-Tests | `AutonomAufgabeInitialisierungsDialogViewModelTests` erstellen (mindestens 3 Tests) | Offen | — |
| 24 | Unit-Tests | `AutonomAufgabeDetailViewModelTests` erstellen (mindestens 3 Tests) | Offen | — |
| 25 | E2E-Tests | `E2E_AutonomAufgabenInitialisierung` erstellen (mindestens 3 Szenarien: Dialog, Verzeichnis, Detail-View) | Offen | — |
| 26 | E2E-Tests | `E2E_AutonomAufgabenAgentExecution` erstellen (mindestens 3 Szenarien: Agent-Start, Unteragenten, Session-Pause) | Offen | — |
| 27 | Integration | Bestehende `AufgabeService`-Tests überprüfen auf Regressions | Offen | — |
| 28 | Integration | Bestehende E2E-Tests überprüfen auf Seiteneffekte | Offen | — |
| 29 | Konfiguration | Konfigurationseinträge in `appsettings.json` oder Konfigurationsklasse hinzufügen (8 Einträge) | Offen | — |
| 30 | Validierung | Validierungslogik für `AutonomAufgabeKonfiguration`-Properties implementieren | Offen | — |
| 31 | Validierung | Validierungslogik für `permissions.json` und `state.json` implementieren | Offen | — |
| 32 | Dokumentation | Neue Services dokumentieren (Zweck, öffentliche API, Nutzungsbeispiele) | Offen | — |
| 33 | Dokumentation | Neue Entities dokumentieren (Beziehungen, Constraints, Nullable-Semantik) | Offen | — |
| 34 | Dokumentation | Arbeitsverzeichnis-Struktur und state.json-Schema dokumentieren | Offen | — |
| 35 | Dokumentation | Benutzer-Dokumentation für UI erstellen (Initialisierungsdialog, Detail-View, Kontroll-Buttons) | Offen | — |
| 36 | Dokumentation | Governance-Regeln und Permissions-Modell dokumentieren | Offen | — |
| 37 | Migrations | Datenbank-Migrationen ausführen und Schema verifizieren | Offen | — |
| 38 | Build & Test | Vollständiger Build durchführen (dotnet build) | Offen | — |
| 39 | Build & Test | Alle Unit-Tests ausführen und grün bestätigen | Offen | — |
| 40 | Build & Test | Alle E2E-Tests ausführen und grün bestätigen | Offen | — |
