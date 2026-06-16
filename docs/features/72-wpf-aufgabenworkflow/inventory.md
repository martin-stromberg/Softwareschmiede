# Bestandsaufnahme: Aufgabenworkflow Optimierung

Diese Bestandsaufnahme analysiert den bestehenden Code bezüglich der Anforderung zur Optimierung des Aufgabenworkflows, insbesondere die Vereinfachung der Statuszustände, die neue Aktion „Starten" und die Dialog-gestützte Plugin-Auswahl mit Projekt-Speicherung.

## Zusammenfassung

| Aspekt | Status | Bemerkung |
|--------|--------|----------|
| **AufgabeStatus Enum** | Vorhanden, nicht optimiert | Enthält noch `ArbeitsverzeichnisEingerichtet` und `InArbeit` (lt. Anforderung zu entfernen) |
| **Aufgabe Entity** | Vorhanden | `KiPluginPrefix` Eigenschaft bereits vorhanden; keine Projekt-Level Plugin-Speicherung in Entity |
| **AufgabeService** | Teilweise vorhanden | `StartenAsync` setzt Status auf `ArbeitsverzeichnisEingerichtet` (muss zu `Gestartet` angepasst werden); keine kombinierte Klone+CLI-Start-Methode |
| **EntwicklungsprozessService** | Vorhanden | `ProzessStartenAsync` klont und richtet Repository ein, setzt Status auf `ArbeitsverzeichnisEingerichtet` (muss erweitert werden) |
| **PluginSelectionService** | Vorhanden | Kann Plugins auflösen, aber keine Dialog-Integration |
| **PluginDefaultSettingsService** | Vorhanden | Speichert/lädt globale Standardplugins; keine Projekt-Level-Unterstützung |
| **TaskDetailViewModel** | Vorhanden | Hat `CliStartenCommand`, `StatusGestartetSetzenCommand`, `AufgabeAbschliessenCommand`; keine `StartenCommand` oder `PluginAendernCommand` |
| **KiAusfuehrungsService** | Vorhanden | Startet/stoppt CLI, verwaltet Prozesse; keine Dialog-Integration für Plugin-Wechsel |
| **PluginSelectionDialog** | Nicht vorhanden | Muss neu erstellt werden |
| **Statusübergänge** | Dokumentiert | Transition-Tests vorhanden für aktuelles Modell |
| **E2E-Tests** | Teilweise vorhanden | Navigations-Tests existieren, aber keine Szenarien für neuen Workflow |
| **Migrationen** | Vorhanden | Migration `20260610000001_UpdateAufgabeStatusEnum` ordnet alte Statuszustände neuen zu |

## Details

- [Datenmodell](inventory/models.md)
- [Services und Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)
