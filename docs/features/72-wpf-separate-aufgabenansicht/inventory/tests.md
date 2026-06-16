# Tests

## Unit-Testklassen

### `ProjectDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`

| Testmethode | Beschreibung |
|-------------|-------------|
| `ProjektSpeichernAsync_ErstelltNeuesProjekt_WennIdLeer` | Testet Projekt-Neuanlage und Zurück-Navigation |
| `ProjektSpeichernAsync_AktualisiertBestehendesProjekt_WennIdVorhanden` | Testet Projekt-Update |
| `ProjektLoeschenAsync_Success_RuftDeleteAsyncUndZurueckActionAuf` | Testet Projekt-Löschung und Callback |
| `ProjektLoeschenAsync_Aborted_RuftDeleteAsyncNichtAuf` | Testet Abbruch bei Löschungs-Dialog |
| `ProjektSpeichernAsync_ValidationError_CanExecuteFalse_WennNameLeer` | Testet Validierung des Projekt-Namens |
| `ProjektSpeichernAsync_ValidationError_CanExecuteFalse_WennNameNurLeerzeichen` | Testet Validierung (nur Leerzeichen) |

**Lücke für Feature 72:** Keine Tests für `AufgabeOeffnenCommand` und Aufgaben-Navigation

---

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

| Testmethode | Beschreibung |
|-------------|-------------|
| `ShowEditPanel_IsTrue_WhenStatusNeu` | Testet Edit-Panel-Sichtbarkeit bei Status Neu |
| `ShowCliPanel_IsTrue_WhenStatusGestartet` | Testet CLI-Panel-Sichtbarkeit bei Status Gestartet |
| `ShowCliPanel_IsTrue_WhenStatusInArbeit` | Testet CLI-Panel-Sichtbarkeit bei Status InArbeit |
| `ShowCliPanel_IsTrue_WhenStatusWartend` | Testet CLI-Panel-Sichtbarkeit bei Status Wartend |
| `ShowDiffPanel_IsTrue_WhenStatusBeendet` | Testet Diff-Panel-Sichtbarkeit bei Status Beendet |

**Lücke für Feature 72:** Keine Tests für Neuanlage, Speichern und Zurück-Navigation

---

## End-to-End Tests

### `ProjectDetailE2ETests`
Datei: `src/Softwareschmiede.Tests/E2E/ProjectDetailE2ETests.cs`

(Datei nicht vollständig gelesen – ggf. existieren bereits E2E-Tests für Aufgaben-Interaktion)

---

## Test-Hilfsmethoden

### In `ProjectDetailViewModelTests`

```csharp
private ProjectDetailViewModel CreateSut(
    Action? zurueckAction = null, 
    Func<Task>? projektHinzugefuegtCallback = null)
{
    var vm = new ProjectDetailViewModel(...);
    vm.ZurueckAction = zurueckAction;
    vm.ProjektListeAktualisierenCallback = projektHinzugefuegtCallback;
    return vm;
}
```

---

### In `TaskDetailViewModelTests`

```csharp
private TaskDetailViewModel CreateSut(Action? zurueckAction = null)
{
    var vm = new TaskDetailViewModel(...);
    vm.ZurueckAction = zurueckAction;
    return vm;
}

private async Task<Aufgabe> ErstelleAufgabe(AufgabeStatus status = AufgabeStatus.Neu)
{
    var aufgabe = await _aufgabeService.CreateAsync(_projektId, "Testaufgabe", "Beschreibung");
    if (status != AufgabeStatus.Neu)
        await _aufgabeService.StatusSetzenAsync(aufgabe.Id, status);
    return await _aufgabeService.GetByIdAsync(aufgabe.Id) ?? aufgabe;
}
```

---

## Test-Infrastruktur

### `TestDbContextFactory`
Verwendung in Tests zur Erstellung eines In-Memory-Test-DbContext

### Mocks
- `Mock<IServiceProvider>` – für Dependency Injection in ViewModels
- `Mock<IDialogService>` – für Dialog-Bestätigungen
- `Mock<IPluginManager>` – für Plugin-Verwaltung
- `Mock<IGitPlugin>` – für Git-Operationen

---

## Gaps und Anforderungen für Feature 72

**Neue Unit-Tests erforderlich:**
- Test für `ProjectDetailViewModel.AufgabeErstellenCommand` → Neuanlage mit Status Neu
- Test für `ProjectDetailViewModel.AufgabeOeffnenCommand` → Navigation zur Aufgabendetail
- Test für `TaskDetailViewModel.SpeichernCommand` bei Neuanlage → Zurück-Navigation
- Test für Datenpersistierung nach Speichern einer neuen Aufgabe
- Test für `TaskDetailViewModel.ZurueckAction` Callback-Aufruf

**Neue E2E-Tests erforderlich:**
- E2E: Klick auf Aufgabe in Projektdetail → Navigation zu Aufgabendetail View
- E2E: Speichern einer neuen Aufgabe → zurück zu Projektdetail mit Status Neu
- E2E: Abbrechen einer Neuanlage → zurück zu Projektdetail ohne Speicherung
- E2E: Zurück-Navigation aus Aufgabendetail zur Projektdetail

**Beschaffenheit der Tests:**
- Sollten Callback-Aufrufe via `ZurueckAction` oder `AufgabeListeAktualisierenCallback` testen
- Sollten Datenpersistierung in AufgabeService prüfen
- Sollten Navigation-Service/RootViewModel-Interaktionen verifizieren
