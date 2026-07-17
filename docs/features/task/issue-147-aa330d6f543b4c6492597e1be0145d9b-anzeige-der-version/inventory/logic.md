# Logikklassen

## `ApplicationVersionProvider`
Datei: `src/Softwareschmiede/Application/Services/Updates/ApplicationVersionProvider.cs`

Implementiert `IApplicationVersionProvider` und liest die lokal installierte Programmversion aus `version.json`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApplicationVersionProvider(ILogger<ApplicationVersionProvider>)` | `public` | Konstruktor mit Logger; verwendet `AppContext.BaseDirectory` als Basispfad |
| `ApplicationVersionProvider(string, ILogger<ApplicationVersionProvider>)` | `public` | Konstruktor mit explizitem Basispfad für Tests |
| `GetInstalledVersionAsync(CancellationToken)` | `public async` | Liest `version.json` aus dem Basispfad, deserialisiert die Version, normalisiert sie via `UpdateVersionComparer.Normalize()` und gibt `InstalledVersionInfo` zurück. Gibt `null` zurück, wenn die Datei fehlt, die Version ungültig ist oder ein Fehler auftritt. Loggt Warnungen für Fehler. |

**Private innere Klasse:**
- `VersionJson` – POCO für JSON-Deserialisierung mit Properties: `Version`, `TagName`, `Commit`, `CreatedAtUtc`

**Fehlerbehandlung:**
- Fehlerfall: Datei nicht vorhanden → `null`, Warnung geloggt
- Fehlerfall: Ungültige Version (leer oder keine Normalisierung möglich) → `null`, Warnung geloggt
- Fehlerfall: Lese-/Parse-Fehler (IOException, UnauthorizedAccessException, JsonException, FormatException) → `null`, Warnung geloggt

**Abhängigkeiten:**
- `ILogger<ApplicationVersionProvider>` – für Logging von Fehlern und Warnungen
- `UpdateVersionComparer.TryParse()` – zur Validierung der Version
- `UpdateVersionComparer.Normalize()` – zur Normalisierung der Version (z. B. "v1.2.3" → "1.2.3")

## `MainWindowViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

ViewModel für das Hauptfenster. Verwaltet Navigation, Dark-Mode, Update-Status und aktive Aufgaben in der Seitenleiste.

**Relevante vorhandene Properties und Methoden:**

| Methode/Property | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `Title` | `public` | Der Fenstertitel |
| `CurrentView` | `public` | Das aktuell angezeigte ViewModel (Navigationsinhalt) |
| `IsNavigationExpanded` | `public` | Gibt an, ob die Navigation aufgeklappt ist |
| `UpdateVerfuegbar` | `public` | Gibt an, ob ein neueres Programmupdate verfügbar ist |
| `VerfuegbaresUpdate` | `public` | Informationen zum aktuell verfügbaren Update |
| `UpdateCheckLaeuft` | `public` | Gibt an, ob gerade eine Update-Prüfung läuft |
| `UpdateWirdVorbereitet` | `public` | Gibt an, ob gerade ein Update heruntergeladen oder vorbereitet wird |
| `UpdateHinweis` | `public` | Optionaler Hinweis zur letzten Update-Prüfung |
| `AktiveAufgabenListe` | `public` | Aktuell aktive Aufgaben (Status Gestartet oder Wartend) für die Seitenleisten-Anzeige |
| `AktiveAufgabenAktualisierenAsync(CancellationToken)` | `public async` | Lädt die aktuell aktiven Aufgaben und aktualisiert die Seitenleisten-Anzeige |
| `Dispose()` | `public` | Cleanup beim Beenden |

**Abonnierte Events:**
- `IRunningAutomationStatusSource.RunningCountChanged` – wird auf `OnRunningCountChanged()` abgelenkt, um Aufgabenliste zu aktualisieren

**Konstruktor-Abhängigkeiten:**
- `DarkModeService` – für Dark-Mode-Umschaltung
- `IServiceProvider` – für Lazy-Loading von ViewModels
- `AufgabeService` – für Datenbeschaffung aktiver Aufgaben
- `PromptZeitVersandService` – für zeitgesteuerte Prompts
- `ILogger<MainWindowViewModel>` – für Logging
- `IRunningAutomationStatusSource` – für Running-Status-Events
- `Action<Action>?` – für Dispatcher-Invocation (optional)
- `IUpdateService?` – für Update-Verwaltung (optional)
- `ICliUpdateSafetyService?` – für Update-Sicherheitsprüfung (optional)
- `IUpdateProgressDialogService?` – für Update-Dialog (optional)
- `IDialogService?` – für allgemeine Dialoge (optional)

**Besonderheit:** Die `CurrentVersion` Property ist **NICHT vorhanden** und muss für diese Anforderung hinzugefügt werden.
