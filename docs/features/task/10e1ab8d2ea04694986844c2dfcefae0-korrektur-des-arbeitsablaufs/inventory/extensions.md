# Bestandsaufnahme: Erweiterungsmethoden

## `AufgabeAusfuehrungsStatusExtensions`

Datei: `src\Softwareschmiede\Domain\Enums\AufgabeAusfuehrungsStatusExtensions.cs`

### `DarfAusfuehrungStarten`

```csharp
public static bool DarfAusfuehrungStarten(this AufgabeAusfuehrungsStatus ausfuehrungsStatus, AufgabeStatus aufgabeStatus)
```

| Aspekt | Beschreibung |
|--------|-----------|
| **Sichtbarkeit** | public static |
| **Parameter** | `ausfuehrungsStatus`: Zu prüfender Ausführungsstatus; `aufgabeStatus`: Zu prüfender Aufgabenstatus |
| **Rückgabewert** | `bool` – `true` wenn eine Ausführung explizit gestartet werden darf |
| **Logik** | Gibt an, ob eine KI-Ausführung für die Aufgabe explizit gestartet werden darf. |
| **Bedingungen** | <ul><li>`aufgabeStatus` nicht `Beendet` und nicht `Archiviert`</li><li>`ausfuehrungsStatus` ist `NichtGestartet` oder `Beendet`</li></ul> |

### `SollCliAnzeigen`

```csharp
public static bool SollCliAnzeigen(this AufgabeAusfuehrungsStatus ausfuehrungsStatus, AufgabeStatus aufgabeStatus)
```

| Aspekt | Beschreibung |
|--------|-----------|
| **Sichtbarkeit** | public static |
| **Parameter** | `ausfuehrungsStatus`: Zu prüfender Ausführungsstatus; `aufgabeStatus`: Zu prüfender Aufgabenstatus |
| **Rückgabewert** | `bool` – `true` wenn die CLI-Ansicht angezeigt werden soll |
| **Logik** | Gibt an, ob die CLI-Ansicht für die Aufgabe angezeigt werden soll. |
| **Bedingungen (AKTUELL)** | <ul><li>`aufgabeStatus.IstAktivOderWartend()` muss `true` sein</li><li>`ausfuehrungsStatus == AufgabeAusfuehrungsStatus.Aktiv` (PROBLEM: nur Aktiv, nicht Beendet)</li></ul> |
| **Bedingungen (GEPLANT)** | <ul><li>`aufgabeStatus.IstAktivOderWartend()` muss `true` sein</li><li>`ausfuehrungsStatus` ist `Aktiv` oder `Beendet` (erweiterte Bedingung)</li></ul> |

### Abhängigkeiten

- Verwendet `AufgabeStatus.IstAktivOderWartend()` Extension
- Wird von `TaskDetailViewModel.ShowCliPanel` aufgerufen
- Wird von `TaskDetailViewModel.KannCliNeuStarten` aufgerufen
- Wird von `TaskDetailViewModel.LadenAsync` aufgerufen
