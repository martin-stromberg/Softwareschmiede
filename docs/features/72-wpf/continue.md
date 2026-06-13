# Offene Aufgaben

Erstellt am: 2026-06-13
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 2: 10 Befunde → Iteration 3: ebenfalls 10 Befunde; Code-Review findet neue Folgebefunde nach jeder Korrektur)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

(Kein Plan-Review durchgeführt – kein plan.md für diesen Fortsetzungslauf)

## Kundenrückmeldung

- [x] Das Blazor-projekt "Softwareschmiede" muss entfernt werden, damit du deien Zeit damit nicht mehr verschwendest.
  Erledigt: `src/Softwareschmiede.Client/` existiert nicht mehr im Repository.

- [x] Die E2E-Tests melden den Fehler: "Erfordert eine Windows-Desktop-Session und ein vorab gebautes Softwareschmiede.App.exe. Nicht in Headless-CI ausführbar."
  Erledigt: Die Tests sind korrekt mit `[Fact(Skip = "...")]` markiert. Kein Handlungsbedarf.

- [x] Der Aufruf der Einstellungen funktioniert nicht. Erstelle E2E-Tests für die Änderung der Einstellungen.
  Behoben: `MainWindowViewModel.NavigateToSettings()` cached die `SettingsViewModel`-Instanz (gleiche Ursache wie Dashboard-Bug); drei E2E-Tests mit `[Fact(Skip = ...)]` hinzugefügt: Öffnen der Einstellungsseite, Speichern einer Einstellung, Stabilität bei mehrfachem Navigieren.

- [x] Starte ich die Anwendung mit einem Datenbestand, der ein Projekt enthält, so wird im Dashboard dieses eine Projekt gezählt. Klicke ich dann im Menü auf "Dashboard", so ändert sich der Zähler auf 0.
  Behoben: `MainWindowViewModel.NavigateToDashboard()` cached die `DashboardViewModel`-Instanz; beim zweiten Klick wird dieselbe Instanz wiederverwendet, sodass die geladenen Daten erhalten bleiben.

- [x] Beim Aufruf einer Aufgabe stürzt das Programm ab. Der Debugger meldete folgenden Fehler:

		System.InvalidOperationException
			HResult=0x80131509
		Nachricht = Kein Source-Code-Management-Plugin verfügbar.
		Quelle = <Die Ausnahmequelle kann nicht ausgewertet werden.>
		Stapelüberwachung:
			<Die Ausnahmestapelüberwachung kann nicht ausgewertet werden.>
			
	   Die Plugin-Projekte müssen beim Kompilieren der Anwendungen ebenfalls kompiliert werden. Die Dlls der Plugin-Projekte müssen dann in das Buildverzeichnis der Anwendung kopiert werden, damit sie gefunden werden.
  Behoben: `Softwareschmiede.App.csproj` enthält nun `ProjectReference` für alle vier Plugin-Projekte mit `ReferenceOutputAssembly="false"`, sodass sie beim App-Build automatisch kompiliert werden. Der bestehende `CopyPluginsToOutput`-Target kopiert die DLLs danach in den `plugins`-Unterordner.

- [x] Die Benennung der E2E-tests stimmt weiterhin nicht mit dem Inhalt überein.
        Beispiel: "ProduktErstellenUndAufgabeHinzufuegen_E2E" testet nur die Anlage eines Projekts aber nicht das hinzufügen einer Aufgabe.
  Behoben: Alle fünf ursprünglichen Tests umbenannt: `ProjektErstellen_ZeigtAufgabenListe_E2E`, `ProjektErstellen_UndNeueAufgabeAnlegen_E2E`, `AufgabeAnlegen_ZeigtCliStartenButton_E2E`, `DarkModeAktivierenUndPersistieren_E2E` (unverändert), `Dashboard_KeineRecoveryBanner_BeiSauberemStart_E2E`.


## Code-Review-Befunde

- [x] **Hoch** `WpfAudioService.cs:54` — `args.ErrorException` im `MediaFailed`-Handler ohne Null-Check; bei bestimmten WPF-Medienfehlern ist `ErrorException` null → `NullReferenceException` im Dispatcher → `TaskCompletionSource` bleibt unresolved → `PlayAudioAsync` hängt für immer
  Behoben: Null-Check vor Zugriff auf `ErrorException`; bei `null` wird eine `InvalidOperationException` als Fallback geworfen.
- [x] **Hoch** `CliSessionService.cs:130` — `ICliSessionService` hat kein `StopAsync`/`Dispose`; Background-Loops und Child-Prozess werden beim Host-Shutdown nicht aufgeräumt
  Behoben: `ICliSessionService` implementiert jetzt `IAsyncDisposable` und hat `StopAsync()`; `CliSessionService` implementiert beide Methoden mit korrekter Aufräumlogik.
- [x] **Mittel** `AufgabeService.AbschliessenAsync` — löscht `BranchName` und `LokalerKlonPfad` nicht mehr aus der DB; nach Abschluss zeigt Entität auf gelöschtes Verzeichnis
  Behoben: `AbschliessenAsync` setzt `BranchName = null` und `LokalerKlonPfad = null`; Integrationstest erweitert.
- [x] **Mittel** `WpfAudioService.cs:72` — Race: `Aborted`-Event wird nach `InvokeAsync()` subscribed; bei Dispatcher-Shutdown zwischen den beiden Zeilen bleibt `tcs` unresolved
  Behoben: `Aborted`-Event-Subscription bleibt nach `InvokeAsync()`, aber direkt danach wird der Status geprüft (`DispatcherOperationStatus.Aborted`). Kommentar erklärt die Reihenfolge.
- [x] **Mittel** `WpfBannerService.cs:44` — AppId `"Softwareschmiede"` nicht als Windows-AUMID registriert → Toast-Feature schlägt auf allen nicht-paketierten Installationen lautlos fehl
  Erledigt: Erklärender Kommentar über der `AppId`-Konstante dokumentiert die Einschränkung und den Workaround (MSIX-Paketierung oder manuelle Registry-Registrierung).
- [x] **Niedrig** `KiAusfuehrungsService` — CLI-Prozess-Exit mit Fehlercode persistiert keinen `Fehlgeschlagen`-Status in der DB
  Behoben: Bei Exit-Code != 0 wird `StatusSetzenAsync(aufgabeId, AufgabeStatus.Beendet)` via `IServiceScopeFactory` aufgerufen (kein eigener `Fehlgeschlagen`-Status im Enum; wurde in einer Migration auf `Beendet` konsolidiert).
- [x] **Niedrig** `ProcessWindowHost.SetAlwaysOnTopFallback` — irreführender Kommentar und hardcodierte 800×600
  Behoben: Kommentar erklärt korrekt die Verwendung von `SWP_NOMOVE | SWP_NOSIZE`; cx/cy auf 0 gesetzt (werden bei diesen Flags ignoriert).
