# Umsetzungsplan: Arbeitsstatus in Aufgabenliste

## Übersicht

Die aktiven Aufgaben in der Navigationsseitenleiste und im Dashboard sollen ihren KI-Ausführungsstatus (▶ Läuft, ⏸ Wartet, ✓ Bereit) automatisch aktualisieren, ohne dass der Benutzer die Ansicht manuell neu laden muss. Zusätzlich soll der Wechsel des Status-Textes/-Symbols durch eine dezente visuelle Übergangsanimation (Opacity-Fade/Highlight) begleitet werden. Die Status-Berechnung (`KiAusfuehrungsStatusConverter`) und die gemeinsame Datenquelle (`AktiveAufgabenListe`) existieren bereits; es fehlt der Auslöser für die automatische Aktualisierung sowie die Übergangsanimation. Betroffen ist im Kern das `MainWindowViewModel` (Owner der gemeinsam genutzten `ObservableCollection`) und die Statuskachel in `ActiveTasksListControl.xaml`; das `DashboardViewModel` teilt dieselbe Instanz und profitiert ohne eigene Änderung.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Aktualisierungsstrategie | **Hybrid**: Abonnement von `IRunningAutomationStatusSource.RunningCountChanged` (Sofortreaktion bei Prozess-Start/-Stopp/-Fehler) **plus** `DispatcherTimer` mit festem Intervall (5 s) als Fallback | Das Event deckt Start/Stopp sofort ab. Der Übergang `Gestartet → Wartend` (Rate-Limit) und der Heartbeat-Ablauf (▶ Läuft → ✓ Bereit) lösen **kein** Event aus – dafür ist der Timer nötig. `RunningCountChanged` ist bereits ein injizierbares, in den Tests gemocktes Interface (`IRunningAutomationStatusSource`) und benötigt keine neue Abstraktion. |
| Collection-Update | Vollständiges Neuladen über die vorhandene `ObservableCollectionExtensions.ReplaceAll()` (Transaction-Script-Stil), Wiederverwendung von `AufgabeService.GetAktiveAufgabenAsync()` | Maximal 20 Elemente, geringe Last. `ReplaceAll` erzeugt neue `Aufgabe`-Instanzen in der Collection und erzwingt so die Neubewertung des `KiAusfuehrungsStatusConverter`-Bindings (`{Binding .}`). Kein Einzel-Item-Diffing nötig. |
| Gemeinsame Datenquelle | Nur `MainWindowViewModel` treibt die Aktualisierung; `DashboardViewModel` nutzt über `Initialize()` **dieselbe** `ObservableCollection<Aufgabe>`-Instanz | Vermeidet doppelte Abfragen und garantiert Synchronität zwischen Seitenleiste und Dashboard. Folgt dem bereits etablierten Muster (`MainWindowViewModel.NavigateToDashboard` → `DashboardViewModel.Initialize`). |
| UI-Thread-Sicherheit | Injizierbarer `Action<Action> dispatcherInvoke` im Konstruktor (Muster aus `TaskDetailViewModel`); `RunningCountChanged`-Handler marshallt darüber auf den UI-Thread | `RunningCountChanged` wird aus dem `Process.Exited`-Callback (Hintergrund-Thread) ausgelöst. Der `DispatcherTimer.Tick` läuft ohnehin auf dem UI-Thread. Der injizierbare Dispatcher erlaubt synchrones Testen ohne WPF-Dispatcher. |
| Re-Entrancy-Schutz | `SemaphoreSlim(1,1)` mit `Wait(0)`/Skip-if-busy in `AktiveAufgabenAktualisierenAsync()` | Timer-Tick und Event-Handler können zeitlich überlappen; der `AufgabeService`/`DbContext` ist nicht thread-/re-entrancy-sicher. Laufende Aktualisierung wird übersprungen statt eingereiht. |
| Übergangsanimation bei Statuswechsel | **Attached Behavior** (`StatusUebergangsAnimation`) am Status-`TextBlock`, das eine dezente Opacity-Fade-`Storyboard` **nur bei echtem Statuswechsel** startet. Die Wechsel-Erkennung liegt in einer eigenständigen, per `Aufgabe.Id` gekeyten Klasse (`StatusAenderungsErkennung`). | Da `ReplaceAll()` bei **jedem** Refresh (alle 5 s bzw. bei jedem Event) die Item-Container – und damit den Status-`TextBlock` – vollständig neu erzeugt, würden element-basierte Trigger (z. B. `EventTrigger` auf `Loaded` oder `Binding.TargetUpdated`) bei **jedem** Refresh feuern, nicht nur beim echten Wechsel. Das Merken des letzten Status je `Aufgabe.Id` erlaubt es, die Animation ausschließlich bei tatsächlicher Statusänderung auszulösen, **ohne** die bestehende `ReplaceAll`-Designentscheidung aufzugeben. Die reine Erkennungslogik (`StatusAenderungsErkennung`) ist als POCO ohne WPF-Abhängigkeit unit-testbar; die `Storyboard`-Ausführung wird visuell/E2E abgedeckt. |
| Animationsform | Dezenter Opacity-Fade (z. B. `0.3 → 1.0`, ~250 ms, `QuadraticEase`/`EaseOut`) auf dem Status-`TextBlock`, in Code konstruierte `DoubleAnimation` auf `UIElement.OpacityProperty` | Ein Opacity-Fade ist theme-agnostisch (keine Animation von `DynamicResource`-Brushes nötig) und wirkt als „Highlight" des neuen Status. Klein und unaufdringlich („dezent"), keine Layout-Verschiebung. Sound bleibt bewusst außen vor (nicht Teil dieser Anforderung). |
| E2E-Beobachtbarkeit des Status | Status-`TextBlock` in `ActiveTasksListControl.xaml` erhält `AutomationProperties.Name` (stabiler Bezeichner aus `Titel`) und `AutomationProperties.HelpText` (Converter-Ergebnis) | Spiegelt das bestehende Muster (`TerminalConsole`-PID über `HelpText`). Der Status-Text enthält Emoji/wechselt; ein stabiler Name plus HelpText macht ihn für FlaUI zuverlässig auslesbar. Die Opacity-Animation beeinflusst das Auslesen der `AutomationProperties` nicht. |

## Programmabläufe

### Sofort-Aktualisierung bei Prozess-Start/-Stopp (Event-Pfad)

1. `KiAusfuehrungsService` startet/stoppt einen CLI-Prozess und ruft intern `RaiseRunningCountChanged()` auf, das `IRunningAutomationStatusSource.RunningCountChanged` auslöst (ggf. aus dem `Process.Exited`-Hintergrund-Thread).
2. Der im `MainWindowViewModel`-Konstruktor registrierte Handler `OnRunningCountChanged(int, int)` wird aufgerufen.
3. Der Handler marshallt über `dispatcherInvoke` auf den UI-Thread und startet `AktiveAufgabenAktualisierenAsync()` per `SafeFireAndForget`.
4. `AktiveAufgabenAktualisierenAsync()` betritt den `SemaphoreSlim` (überspringt, falls bereits eine Aktualisierung läuft), ruft `AufgabeService.GetAktiveAufgabenAsync()` auf und ersetzt die `AktiveAufgabenListe` per `ReplaceAll()`.
5. WPF bewertet für die neuen `Aufgabe`-Instanzen das `KiAusfuehrungsStatusConverter`-Binding neu; Seitenleiste und Dashboard (gemeinsame Collection) zeigen den aktualisierten Status.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `IRunningAutomationStatusSource`, `KiAusfuehrungsService`, `AufgabeService`, `ObservableCollectionExtensions`, `KiAusfuehrungsStatusConverter`.

### Periodische Aktualisierung (Timer-Fallback)

1. Ein im `MainWindowViewModel`-Konstruktor gestarteter `DispatcherTimer` (Intervall = 5 s) löst zyklisch `Tick` auf dem UI-Thread aus.
2. Der Tick-Handler startet `AktiveAufgabenAktualisierenAsync()` per `SafeFireAndForget`.
3. Ablauf wie oben ab Schritt 4. Dieser Pfad fängt Statusänderungen ohne eigenes Event ab: `Gestartet → Wartend` (Rate-Limit) sowie den Heartbeat-Ablauf (▶ Läuft → ✓ Bereit nach 5 Minuten ohne Heartbeat) und das laufende Vorrücken von `LastHeartbeatUtc`.

Beteiligte Klassen/Komponenten: `MainWindowViewModel`, `AufgabeService`, `KiAusfuehrungsStatusConverter`.

### Übergangsanimation bei Statuswechsel (UI-Pfad)

1. Nach einem Refresh (Event- oder Timer-Pfad) regeneriert das `ItemsControl` in `ActiveTasksListControl.xaml` die Item-Container; der Status-`TextBlock` wird für jede `Aufgabe` neu erzeugt.
2. Das am Status-`TextBlock` gesetzte Attached Property `StatusUebergangsAnimation.Status` (gebunden an `{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}`) erhält beim Initialisieren des Elements seinen Wert; die `PropertyChangedCallback` wird aufgerufen.
3. Der Callback ermittelt die `Aufgabe.Id` aus dem `DataContext` des `TextBlock` und fragt `StatusAenderungsErkennung.HatSichGeaendert(id, neuerStatus)` ab.
   - Erste Beobachtung einer `Id` (Baseline nach App-Start/erstem Rendern) → `false`, keine Animation, Status wird gemerkt.
   - Gleicher Status wie zuletzt gemerkt (Routine-Refresh alle 5 s ohne echten Wechsel) → `false`, keine Animation.
   - Abweichender Status (echter Wechsel, z. B. ▶ Läuft → ⏸ Wartet) → `true`, gemerkter Status wird aktualisiert.
4. Bei `true` startet der Callback eine in Code konstruierte, dezente Opacity-Fade-`DoubleAnimation` (`0.3 → 1.0`, ~250 ms, EaseOut) auf dem Status-`TextBlock` (`UIElement.OpacityProperty`).
5. Die Animation läuft rein visuell ab; `AutomationProperties.Name`/`HelpText` bleiben durchgängig auslesbar.

Beteiligte Klassen/Komponenten: `ActiveTasksListControl` (XAML), `StatusUebergangsAnimation` (Attached Behavior), `StatusAenderungsErkennung`, `KiAusfuehrungsStatusConverter`, `Aufgabe`.

### Freigabe der Ressourcen beim Schließen

1. `MainWindow.OnClosed` ruft `(DataContext as IDisposable)?.Dispose()` auf.
2. `MainWindowViewModel.Dispose()` stoppt den `DispatcherTimer`, hebt das Abonnement von `IRunningAutomationStatusSource.RunningCountChanged` auf und gibt den `SemaphoreSlim` frei.

Beteiligte Klassen/Komponenten: `MainWindow`, `MainWindowViewModel`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `StatusAenderungsErkennung` | Klasse (POCO, ohne WPF-Abhängigkeit) | Merkt je `Aufgabe.Id` (`Guid`) den zuletzt beobachteten Status-String in einem internen `Dictionary<Guid,string?>`; Methode `HatSichGeaendert(Guid, string?)` liefert `false` bei Erstbeobachtung oder unverändertem Status, `true` bei echtem Wechsel und aktualisiert den gemerkten Wert. Unit-testbar. |
| `StatusUebergangsAnimation` | Static class (Attached Behavior) | Stellt das Attached Property `Status` (string) bereit; die `PropertyChangedCallback` ermittelt die `Aufgabe.Id` aus dem `DataContext`, fragt eine statische `StatusAenderungsErkennung`-Instanz ab und startet bei echtem Wechsel eine dezente Opacity-Fade-`DoubleAnimation` auf dem Ziel-`TextBlock`. |

## Änderungen an bestehenden Klassen

### `MainWindowViewModel` (ViewModel)

- **Neue Konstruktorparameter:** `IRunningAutomationStatusSource runningStatusSource` (bereits als Singleton in DI registriert) und optional `Action<Action>? dispatcherInvoke = null` (für Testbarkeit; Fallback auf `Application.Current?.Dispatcher`, analog `TaskDetailViewModel`).
- **Neue Felder:** `DispatcherTimer` (Intervall 5 s), `SemaphoreSlim _refreshGate` (1,1), `Action<Action> _dispatcherInvoke`, `bool _disposed`. Konstante `private const int AktualisierungsIntervallSekunden = 5`.
- **Neue Methoden:**
  - `OnRunningCountChanged(int previous, int current)` — Event-Handler; marshallt per `_dispatcherInvoke` und stößt `AktiveAufgabenAktualisierenAsync()` per `SafeFireAndForget` an.
  - `OnAktualisierungsTimerTick(object?, EventArgs)` — Timer-Tick-Handler; stößt `AktiveAufgabenAktualisierenAsync()` per `SafeFireAndForget` an.
- **Geänderte Methoden:** `AktiveAufgabenAktualisierenAsync(CancellationToken)` — erhält Re-Entrancy-Schutz über `_refreshGate` (`Wait(0)`/Skip-if-busy), Freigabe in `finally`. Vorhandene `try/catch`-Fehlerbehandlung (Beibehaltung der letzten Liste, Warn-Log) bleibt bestehen.
- **Geänderter Konstruktor:** abonniert `runningStatusSource.RunningCountChanged += OnRunningCountChanged`, erzeugt und startet den `DispatcherTimer`.
- **Neue Schnittstelle:** implementiert `IDisposable` — `Dispose()` stoppt den Timer, meldet den Event-Handler ab, gibt den `SemaphoreSlim` frei (idempotent über `_disposed`).

### `MainWindow` (View / Code-behind)

- **Geänderte Methode:** `OnClosed(EventArgs)` — ruft zusätzlich `(DataContext as IDisposable)?.Dispose()` auf, um das ViewModel sauber freizugeben.

### `ActiveTasksListControl.xaml` (View / UserControl)

- **Geändertes Element:** Der Status-`TextBlock` im `AufgabenKachelInhaltTemplate` (Binding `{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}`) erhält:
  - `AutomationProperties.Name` (stabiler Bezeichner aus `Titel`, z. B. `StringFormat=AufgabeStatus:{0}`) und `AutomationProperties.HelpText="{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}"`, damit E2E-Tests den Status zuverlässig auslesen können.
  - Das Attached Property `behaviors:StatusUebergangsAnimation.Status="{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}"` (neuer XML-Namespace auf das Behavior-Namespace), das die Übergangsanimation bei echtem Statuswechsel auslöst.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Das Aktualisierungsintervall ist als private Konstante (5 s) im `MainWindowViewModel` festgelegt und nicht konfigurierbar (siehe Designentscheidungen). Die Animationsparameter (Fade-Dauer, Start-Opacity) sind als Konstanten in `StatusUebergangsAnimation` festgelegt und nicht konfigurierbar.

## Seiteneffekte und Risiken

- **DbContext-Nebenläufigkeit:** `MainWindowViewModel` hält einen (scoped) `AufgabeService` als Captive Dependency – bereits bestehendes Muster. Der neue Timer-/Event-getriebene Zugriff erhöht die Abruffrequenz; der `SemaphoreSlim`-Re-Entrancy-Schutz verhindert überlappende Zugriffe auf denselben `DbContext`.
- **Captive-Dependency-Frequenz:** Alle 5 s ein Abruf von `GetAktiveAufgabenAsync()` (≤ 20 Zeilen). Für eine Desktop-App vernachlässigbar; kein Caching nötig.
- **UI-Flicker durch `ReplaceAll`:** `ReplaceAll` leert die Collection und baut sie neu auf, was theoretisch Auswahl/Scroll-Position der Seitenleistenliste zurücksetzt. Bei der kleinen, nicht-selektierbaren Statusliste unkritisch. Die Übergangsanimation feuert dank `StatusAenderungsErkennung` **nicht** bei diesem Routine-Neuaufbau, sondern nur bei echtem Statuswechsel – dadurch kein störendes Flackern alle 5 s.
- **Statisches Änderungsgedächtnis (`StatusAenderungsErkennung`):** Die per `Aufgabe.Id` gekeyten Einträge einer statischen Instanz wachsen theoretisch über die App-Laufzeit. Da nur je einmal pro tatsächlich aktiver Aufgabe ein Eintrag entsteht (≤ 20 gleichzeitig, insgesamt begrenzt durch die Zahl der über die Sitzung aktiven Aufgaben), ist der Speicherbedarf vernachlässigbar; keine Bereinigung nötig.
- **Lebenszyklus/Leak:** Ohne `Dispose`-Verdrahtung im `MainWindow` bliebe das `RunningCountChanged`-Abonnement bestehen. Da `MainWindowViewModel` transient ist und für die Fensterlebensdauer existiert, ist der Effekt gering, wird aber durch die `Dispose`-Verdrahtung sauber behandelt.
- **`DashboardViewModel`:** Die Liste aktualisiert sich automatisch (gemeinsame Collection) inklusive Übergangsanimation (dasselbe `ActiveTasksListControl`). Die Zähler `AktiveAufgaben`/`WartendAufgaben` werden weiterhin nur bei `LadenAsync()` aktualisiert – außerhalb des Anforderungsumfangs (betrifft nur Kacheln der Aufgabenliste, nicht die Zählerkacheln).

## Umsetzungsreihenfolge

1. **`MainWindowViewModel` um Aktualisierungsmechanik erweitern**
   - Voraussetzungen: `IRunningAutomationStatusSource` (vorhanden, Singleton in DI), `KiAusfuehrungsService.RunningCountChanged` (vorhanden), `ObservableCollectionExtensions.ReplaceAll()` (vorhanden), `SafeFireAndForget`-Helper (vorhanden), Dispatcher-Muster aus `TaskDetailViewModel` (vorhanden).
   - Beschreibung: Neue Konstruktorparameter (`IRunningAutomationStatusSource`, optionaler `dispatcherInvoke`), Felder (`DispatcherTimer`, `SemaphoreSlim`, Konstante), Event-Abonnement, Timer-Start, Handler `OnRunningCountChanged`/`OnAktualisierungsTimerTick`, Re-Entrancy-Schutz in `AktiveAufgabenAktualisierenAsync()`, `IDisposable`-Implementierung.

2. **`MainWindow`-Code-behind: ViewModel-Freigabe verdrahten**
   - Voraussetzungen: Schritt 1 (`MainWindowViewModel` implementiert `IDisposable`).
   - Beschreibung: In `OnClosed` `(DataContext as IDisposable)?.Dispose()` aufrufen.

3. **`StatusAenderungsErkennung` anlegen (Wechsel-Erkennung)**
   - Voraussetzungen: `Aufgabe.Id` (`Guid`, vorhanden).
   - Beschreibung: POCO mit `Dictionary<Guid,string?>` und Methode `HatSichGeaendert(Guid aufgabeId, string? neuerStatus)` (Baseline/unverändert → `false`, echter Wechsel → `true`, Wert wird gemerkt). Keine WPF-Abhängigkeit.

4. **`StatusUebergangsAnimation` anlegen (Attached Behavior)**
   - Voraussetzungen: Schritt 3 (`StatusAenderungsErkennung`), `KiAusfuehrungsStatusConverter` (vorhanden).
   - Beschreibung: Static class mit Attached Property `Status` (string) und `PropertyChangedCallback`. Callback liest `Aufgabe.Id` aus dem `DataContext` des Ziel-`TextBlock`, fragt eine statische `StatusAenderungsErkennung`-Instanz ab und startet bei `true` eine in Code konstruierte dezente Opacity-Fade-`DoubleAnimation` (Konstanten für Dauer/Start-Opacity).

5. **`ActiveTasksListControl.xaml`: Status beobachtbar machen und Animation anbinden**
   - Voraussetzungen: Schritt 4 (`StatusUebergangsAnimation`).
   - Beschreibung: Am Status-`TextBlock` `AutomationProperties.Name` + `AutomationProperties.HelpText` (Converter-Ergebnis) ergänzen; XML-Namespace für das Behavior-Namespace deklarieren und `StatusUebergangsAnimation.Status="{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}"` setzen.

6. **DI prüfen (keine Änderung erwartet)**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Sicherstellen, dass `MainWindowViewModel` (transient) den neuen `IRunningAutomationStatusSource`-Parameter über den bestehenden Container auflöst; Registrierung existiert bereits (`App.xaml.cs`). Keine Codeänderung, nur Verifikation.

7. **Unit-Tests für `MainWindowViewModel` anpassen/ergänzen**
   - Voraussetzungen: Schritt 1; vorhandener `Mock<IRunningAutomationStatusSource>` in `MainWindowViewModelTests`.
   - Beschreibung: `CreateSut()` um neue Konstruktorargumente erweitern (synchroner `dispatcherInvoke`), neue Tests für Event-getriebenes Neuladen, Re-Entrancy-Skip und `Dispose`-Abmeldung.

8. **Unit-Tests für `StatusAenderungsErkennung`**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: Tests für Erstbeobachtung (kein Wechsel), unveränderten Status (kein Wechsel), echten Wechsel (Wechsel erkannt) und Unabhängigkeit mehrerer `Id`s.

9. **E2E-Test für automatische Statusaktualisierung**
   - Voraussetzungen: Schritte 1–5; E2E-Infrastruktur (`WpfTestBase`, KiSimulator-Plugin) vorhanden.
   - Beschreibung: Neuen E2E-Test anlegen, der eine Aufgabe startet und den Statuswechsel in der Seitenleiste ohne manuelles Neuladen verifiziert (Auslesen über `AutomationProperties.HelpText`).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RunningCountChanged_ShouldReloadAktiveAufgabenListe_WhenRaised` | `MainWindowViewModelTests` | Auslösen von `RunningCountChanged` am Mock aktualisiert `AktiveAufgabenListe` (synchroner Dispatcher injiziert). |
| `AktiveAufgabenAktualisierenAsync_ShouldSkip_WhenAlreadyRunning` | `MainWindowViewModelTests` | Re-Entrancy-Schutz: paralleler/erneuter Aufruf während laufender Aktualisierung wird übersprungen (kein DbContext-Konflikt). |
| `Dispose_ShouldUnsubscribeFromRunningCountChanged` | `MainWindowViewModelTests` | Nach `Dispose()` löst ein erneutes `RunningCountChanged` kein Neuladen aus und wirft nicht. |
| `CreateSut()` (Anpassung/Hilfsmethode) | `MainWindowViewModelTests` | Erzeugt SUT mit `runningStatusSourceMock.Object` und synchronem `dispatcherInvoke` (`action => action()`). |
| `HatSichGeaendert_ShouldReturnFalse_OnErstbeobachtung` | `StatusAenderungsErkennungTests` | Erste Meldung einer `Id` gilt als Baseline → `false` (keine Animation beim ersten Rendern). |
| `HatSichGeaendert_ShouldReturnFalse_WhenStatusUnveraendert` | `StatusAenderungsErkennungTests` | Gleicher Status wie zuletzt (Routine-Refresh) → `false` (keine Animation alle 5 s). |
| `HatSichGeaendert_ShouldReturnTrue_WhenStatusWechselt` | `StatusAenderungsErkennungTests` | Abweichender Status gegenüber dem gemerkten Wert → `true` (Animation ausgelöst). |
| `HatSichGeaendert_ShouldTrackIdsUnabhaengig` | `StatusAenderungsErkennungTests` | Mehrere `Aufgabe.Id`s werden getrennt verfolgt; ein Wechsel bei einer Id beeinflusst andere nicht. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MainWindowViewModelTests.CreateSut()` und alle darüber erzeugten Tests | Neue Konstruktorparameter (`IRunningAutomationStatusSource`, optionaler `dispatcherInvoke`) müssen übergeben werden. |
| `MainWindowViewModelTests` (Klasse als Ganzes) | Sollte `IDisposable`-SUT ggf. am Testende freigeben; vorhandener `runningStatusSourceMock` wird nun an das SUT durchgereicht. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Aufgabe wird gestartet → Seitenleisten-Kachel zeigt „▶ Läuft" ohne manuelles Neuladen; nach CLI-Stopp Wechsel zu „✓ Bereit" | `E2E_ArbeitsstatusAktualisierung` (neu, `[Trait("Category","E2E")]`, `[Collection("E2E")]`, abgeleitet von `WpfTestBase`) | Automatische Statusaktualisierung der aktiven Aufgaben in der Seitenleiste ohne manuelles Neuladen. Status wird über `AutomationProperties.HelpText` des Status-`TextBlock` ausgelesen. Die Übergangsanimation ist visuell/dezent und wird nicht separat per FlaUI-Assertion geprüft (Opacity-Endzustand = 1.0, Statuswert bleibt auslesbar). |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine. (`E2E_TaskWechselUeberMenue` nutzt die Seitenleisten-Kacheln, prüft aber Navigation/PID, nicht den Statustext; die zusätzlichen `AutomationProperties` und das Animation-Attached-Property am Status-`TextBlock` ändern die dort verwendeten Selektoren nicht.)

## Offene Punkte

Keine.
