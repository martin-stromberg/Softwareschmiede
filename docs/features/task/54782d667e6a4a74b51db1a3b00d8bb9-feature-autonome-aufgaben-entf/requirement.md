# Übersetzte Anforderung: Entfernen des Features „Autonome Aufgaben“

## Fachliche Zusammenfassung

Die Anwendung bietet zwei Wege zur automatisierten Bearbeitung von Aufgaben: die ereignisgetriebene Bearbeitung (Reaktion auf SCM-Ereignisse wie Kommentare in Issues oder Pull Requests) und das ältere Feature „Autonome Aufgaben“ (manuell vom Anwender gestartet, Projektleiter-Agent mit Unteragenten, Initialisierungsdialog, Token-/Laufzeitbudget, Persistenz-Modus, Pausen-Mechanismus). Das Feature „Autonome Aufgaben“ wird vollständig aus der Anwendung entfernt, einschließlich Start-Pfad (`AutonomAufgabeStartService`, Initialisierungsdialog `AutonomAufgabeInitialisierungsDialog`, Projektleiter-Agent `ProjektleiterAgentService`, Unteragenten-Governance) und der zugehörigen Detailansicht. Der Feature-Flag „Autonome Aufgaben aktivieren“ (UI-Checkbox in der Registerkarte „Allgemein“, `AppEinstellungService.AutonomAufgabenEnabledKey`, `SettingsViewModel.IsAutonomAufgabenEnabled`, sowie die Konfiguration `AutonomAufgaben:Enabled` in `appsettings.json` / `AutonomAufgabenOptions`) entfällt. Die ereignisgetriebene Bearbeitung und der einfache Aufgabenstart („Starten“-Button mit direkter CLI-Ausführung) bleiben unverändert erhalten. Die Dokumentation wird so bereinigt, als hätte das Feature nie existiert; nur historische Protokolle (z. B. `changes.log`, Aufgabenverzeichnisse unter `docs/features/task/`) dürfen auf das frühere Feature verweisen.

## Betroffene Klassen und Komponenten

Hinweis: Die folgende Liste stammt aus einer Volltextsuche nach „autonom“ im Repository und ist vorläufig. Die vollständige Abgrenzung erfolgt in der Bestandsaufnahme.

**Datenmodell (Domain / Infrastructure)**
- `Aufgabe` (`Domain/Entities/Aufgabe.cs`, `AufgabeExtensions.cs`): Eigenschaften und Methoden rund um `IstAutonom`, ggf. Pausen-/Gestoppt-Logik, die nur für autonome Aufgaben gilt
- `AutonomAufgabeKonfiguration` (Domain/Entities), `AutonomAufgabenOptions`, `RessourcenLimits`, `UnteragentSpezifikation`, `SkillDefinition`
- Value Objects: `AutonomAufgabeInitialisierungsAnfrage`
- Enums: `PersistenzModus`, `PermissionsJsonOption` (sofern ausschließlich autonom genutzt)
- `SoftwareschmiededDbContext` (DbSets und Konfiguration der autonomen Modelle)
- Migrationen: `AddAutonomAufgabeModels`, `AddAutonomAufgabeMaxLengthConstraints`, `RenameSkillDefinitionProperties`, `RenameUnteragentSpezifikationProperties`, `VerschiebeProjektleiterFelderZuAutonomKonfiguration`, `NormalisiereAutonomAufgabeAusfuehrungsStatus`, `AddRepositoryInitialisierungKonfiguration`, `AddExplizitGestopptToAutonomAufgabeKonfiguration`, `AddAufgabeBasisBranchName` (zu prüfen, ob ausschließlich autonom), `AddAufgabePausiertBisUtc` (zu prüfen, ob von ereignisgetriebener Bearbeitung mitgenutzt) und Model-Snapshot

**Logik / Services (Application)**
- Entfallen vollständig: `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService`, `UnteragentGovernanceService`, `UnteragentGovernanceMonitoringService`, `AutonomAufgabenOptions`
- Prüfen / bereinigen: `AufgabeService` (Pause, Autonom-Zweige), `AufgabeRecoveryService`, `SessionManagementService`, `KiPluginLimitService`, `AppEinstellungService` (Schlüssel `autonomeaufgaben.enabled`), `StateJsonHelper`, `GitBranchHelper`
- App-Ebene: `Softwareschmiede.App/Services/AutonomAufgabeStartService`, `AutonomAufgabeStartResult`, `IDialogService` / `WpfDialogService` (Dialoge für autonome Aufgaben), `App.xaml.cs` (DI-Registrierung)

**ViewModels / Views (UI)**
- Entfallen: `AutonomAufgabeDetailViewModel` / `AutonomAufgabeDetailView`, `AutonomAufgabeInitialisierungsDialogViewModel` / `AutonomAufgabeInitialisierungsDialog`
- Anpassen: `TaskDetailViewModel` / `TaskDetailView` (Aktion „Autonome Aufgabe starten“, Pausen-Button nur für nicht-autonome Aufgaben, Autonom-Verzweigungen), `SettingsViewModel` / `SettingsView` (Checkbox „Autonome Aufgaben aktivieren“ und Abschnitt „Automatisierung“, soweit nur dafür genutzt)

**Tests (Softwareschmiede.Tests / IntegrationTests)**
- Entfallen: `AutonomAufgabeStartServiceTests`, `AutonomAufgabeDetailViewModelTests`, `AutonomAufgabeInitialisierungsDialogViewModelTests*`, `AutonomAufgabenInitialisierungsServiceTests`, `ProjektleiterAgentServiceTests*`, `UnteragentGovernance*Tests`, `AufgabeTests_IstAutonom`, `AutonomAufgabeKonfigurationTests_RessourcenLimits`, `AufgabeRecoveryServiceTests`/`AufgabeServiceTests_Pause` (nur soweit autonom), `AutonomAufgabenInitialisierungsServiceTestFactory`, `ProjektleiterAgentServiceTestDatenFactory`
- Anpassen: `TaskDetailViewModelTests_Automatisierung`, `TaskDetailViewModelTests_Pause`, `TaskDetailViewModelTests_PluginAktivierung`, `TaskDetailViewModelTestFactory`, `MainWindowViewModelUpdateTestBase`, `SettingsViewModelTests`, `SettingsViewModelTests_IdePlugin`, `EntwicklungsprozessServiceTests` (IntegrationTests), `StartPs1IntegrationTests` (IntegrationTests, Verweise prüfen)
- E2E (FlaUI): entfallen `E2E_AutonomAufgabenAgentExecution`, `E2E_AutonomAufgabenFeatureFlagDisabled`, `E2E_AutonomAufgabenInitialisierung`, `Views/AutonomAufgabeDetailView`, `Views/Dialogs/AutonomAufgabeInitialisierungsDialogView`; anpassen `E2E_SettingsFeatureFlags`, `E2E_ViewPattern`, `MainTest`, `WpfTestBase`, `Views/SettingsView`, `Views/DialogView`, `Views/WindowExtensions`. Die verbleibenden E2E-Methoden sind so zu konsolidieren, dass die Laufzeit der FlaUI-Suite nicht steigt und keine Abdeckung verloren geht.

**Dokumentation (`docs/help/`, `README.md`)**
- Entfallen: `docs/help/aufgaben/autonome-aufgaben/` (gesamter Ordner)
- Anpassen: `docs/help/aufgaben/index.md` (Abschnitt „Autonome Aufgaben“), `docs/help/aufgaben/beschreibung.md` (Button-Beschreibung, Pausen-Regel), `docs/help/aufgaben/business-rules.md`, `docs/help/aufgaben/ablauf-*.md`, `docs/help/aufgaben/api.md`, `docs/help/aufgaben/troubleshooting.md`, `docs/help/index.md` (Absatz „Neu: Autonome Aufgaben“), `docs/help/einstellungen/beschreibung.md`, `ablauf-anwender.md`, `datenmodell.md`, `installation.md` (Abschnitt „Feature-Flag Autonome Aufgaben“), `docs/help/e2e-test-infrastruktur/*`, `README.md`
- Unverändert: `changes.log` und `docs/features/task/...` (historische Protokolle)

## Implementierungsansatz

1. Bestandsaufnahme: Abgrenzung der Artefakte, die ausschließlich vom autonomen Feature genutzt werden, von jenen, die die ereignisgetriebene Bearbeitung oder den einfachen Aufgabenstart mitbenutzen (insbesondere `Aufgabe`, `AufgabeService`, `AufgabeRecoveryService`, `KiPluginLimitService`, Pausen-Mechanismus, `AufgabeScmAbschluss`).
2. Entfernen der Start-, Initialisierungs- und Agent-Pfade samt UI und DI-Registrierungen. Entfernen der Konfiguration (`AutonomAufgabenOptions`, `appsettings.json`-Abschnitt, `AppEinstellung`-Schlüssel).
3. Datenmodell: Entfernen der autonomen Entitäten und Eigenschaften aus `SoftwareschmiededDbContext`. Die Schemaänderung erfolgt über eine neue EF-Core-Migration (keine Änderung bestehender Migrationen), die bestehende Installationen sicher aktualisiert (Skill `entityframework-database`).
4. Anpassung der Aufgabendetailansicht, sodass nur der einfache Aufgabenstart und die ereignisgetriebene Bearbeitung verbleiben.
5. Tests: Entfernen der autonomen Tests, Anpassen der gemeinsamen Tests, Konsolidierung der E2E-Methoden ohne Verlust von Abdeckung.
6. Dokumentation: Entfernen des Feature-Ordners und aller Verweise in den aktiven Dokumentationsdateien, sodass kein Hinweis auf eine Entfernung entsteht. `changes.log` bleibt unverändert.

Abhängigkeiten: Die ereignisgetriebene Bearbeitung darf nicht über gemeinsam genutzte Komponenten an das autonome Feature gekoppelt sein. Diese Kopplungen müssen vor der Entfernung identifiziert und getrennt werden.

## Konfiguration

Das Feature ist nicht mehr konfigurierbar. Die bisherigen Konfigurationsebenen entfallen vollständig:
- Anwendungseinstellung `autonomeaufgaben.enabled` (AppEinstellungen, Registerkarte „Allgemein“, Abschnitt „Automatisierung“)
- Deployment-Konfiguration `AutonomAufgaben:Enabled` in `appsettings.json`

Die ereignisgetriebene Bearbeitung und der einfache Aufgabenstart bleiben ohne neue Konfiguration bestehen. Ist der Abschnitt „Automatisierung“ nach Entfernung der Checkbox leer, ist zu entscheiden, ob er entfällt oder andere Einstellungen der ereignisgetriebenen Bearbeitung aufnimmt.

## Offene Fragen

1. **Datenbestand bestehender Installationen:** Sollen die autonomen Tabellen (z. B. `AutonomAufgabeKonfiguration`, Projektleiter- und Unteragent-Daten) per Migration gelöscht werden, oder bleiben sie als ungenutzte Tabellen bestehen? Löschen ist die sauberere Variante, erfordert aber eine Datenbereinigungs-Migration mit Hinweis an den Anwender, dass Daten verloren gehen.
2. **Laufende oder pausierte autonome Aufgaben:** Wie werden bestehende Aufgaben mit `IstAutonom = true` behandelt (Status ändern, sperren, oder Migration zu einfacher Aufgabe)?
3. **Gemeinsam genutzte Felder:** Sind `AufgabeScmAbschluss`, `AddAufgabePausiertBisUtc`, `AddAufgabeBasisBranchName` und `KiPluginLimitService` auch für die ereignisgetriebene Bearbeitung nötig? Falls ja, bleiben sie erhalten.
4. **Umfang der Dokumentation:** Gilt die Bereinigung auch für `docs/features/` (Feature-Dokumentation) und `docs/help/` ausschließlich für aktive Dokumente? Die Datei `docs/features.md` aus dem Skill-Kontext existiert im Repository nicht; die Übersicht liegt unter `docs/features/`.
5. **Abschnitt „Automatisierung“:** Entfällt der Einstellungsabschnitt vollständig, wenn keine weiteren Einstellungen vorhanden sind?
6. **Annahme:** Der Begriff „ereignisgetriebene Bearbeitung“ bezeichnet die SCM-Ereignisverarbeitung (`ScmEvent`-Inbox, Plugin-Ereignisse, Kommentar-Reaktion). Der Begriff „Aufgabe starten“ im Sinne des einfachen Starts (`EntwicklungsprozessService`, Button „Starten“) bleibt erhalten.
