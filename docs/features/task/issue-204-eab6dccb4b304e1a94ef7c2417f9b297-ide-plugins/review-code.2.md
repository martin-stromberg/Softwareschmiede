# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TaskDetailViewModel.cs (TaskDetailViewModel) / IdeOeffnenService.cs (IdeOeffnenService)

- **Doppelter Code (klassenübergreifend)** — Die neue private Methode `OeffneIdeInternAsync` (`TaskDetailViewModel.cs`, Zeile 1819–1861) implementiert exakt denselben Algorithmus wie `IdeOeffnenService.OpenRepositoryInIdeAsync` (`src/Softwareschmiede/Application/Services/IdeOeffnenService.cs`, Zeile 50–84) noch einmal eigenständig: Plugin via `ResolveIdePluginAsync` auflösen → `FindEntryPointsAsync` aufrufen → bei 0 Treffern `FileNotFoundException` mit **wortgleicher** Meldung `"Keine Einstiegspunkte im Repository gefunden: {pfad}"` werfen → bei genau 1 Treffer direkt öffnen → bei ≥2 Treffern optionalen Auswahl-Callback aufrufen (Abbruch bei `null`) → sonst ersten Treffer öffnen. Der iteration-1-Befund "Plugin-Auflösung wird pro Klick doppelt zur Laufzeit ausgeführt" wurde damit korrekt behoben (nur noch ein Aufruf pro Klick), aber um den Preis, dass dieselbe Geschäftslogik jetzt statisch an zwei unabhängigen Stellen im Code existiert. Ändert sich künftig z. B. die Fehlerbehandlung, die Reihenfolge der Verzweigung oder der Exception-Typ an einer Stelle, kann die andere unbemerkt abweichen (Logik-Drift). `IdeOeffnenService` hat dadurch zusätzlich keinen Produktions-Aufrufer mehr (siehe nächster Befund).

  Empfehlung: Die gemeinsame Algorithmus-Logik an einer einzigen Stelle bündeln, z. B. als (internal/public) Methode auf `IIdePlugin`-Ebene oder als eigene Hilfsmethode in `PluginSelectionService`/`IdeOeffnenService`, die ein bereits aufgelöstes Plugin entgegennimmt oder selbst auflöst und optional einen Auswahl-Callback annimmt; `TaskDetailViewModel.OeffneIdeInternAsync` ruft diese dann nur noch auf und ergänzt die UI-spezifische `KannIdeAuswaehlen`-Aktualisierung. Dadurch bleibt die Auflösung weiterhin nur einmal pro Klick ausgeführt, aber ohne zwei gepflegte Kopien derselben Verzweigungslogik.

### IdeOeffnenService.cs (IdeOeffnenService)

- **Toter Code (Produktionscode) / undokumentierte Architekturabweichung** — Mit dem Entfernen des `IdeOeffnenService`-Konstruktor-Parameters aus `TaskDetailViewModel` hat `IdeOeffnenService` keinen einzigen Aufrufer mehr in produktivem App-/Domain-Code (Grep über `src/Softwareschmiede.App` und `src/Softwareschmiede` liefert außer der DI-Registrierung `services.AddScoped<IdeOeffnenService>();` in `App.xaml.cs` Zeile 207 keine Konstruktor-Injektion oder Methodenverwendung mehr). Die Klasse wird ausschließlich noch von ihren eigenen Unit-Tests (`IdeOeffnenServiceTests.cs`) und von `E2E_IdePluginSelection.cs` (bewusst als reiner Objektgraph-Test der Plugin-Auswahl-Logik, unabhängig von der UI) direkt aufgerufen. `plan.md` (Abschnitt „Änderungen an bestehenden Klassen" / „Business-Logik (unverändert)") und `requirement.md` gehen beide noch davon aus, dass `IdeOeffnenService.OpenRepositoryInIdeAsync` von der UI aus dem Haupt- und Dropdown-Button aufgerufen wird — das ist nach dieser Iteration nicht mehr der Fall, ohne dass diese Abweichung irgendwo (Klassenkommentar, Plan-Update, Commit-Beschreibung) dokumentiert wäre. Die weiterhin aktive DI-Registrierung ist damit ebenfalls verwaist (erzeugt eine Instanz, die im Produktivbetrieb nie aus dem Container angefordert wird).

  Empfehlung: Entscheidung bewusst treffen und dokumentieren: entweder (a) `IdeOeffnenService`, seine DI-Registrierung und die dedizierten Tests entfernen und stattdessen nur noch die (dann gebündelte, siehe vorheriger Befund) Kernlogik testen, oder (b) einen Klassenkommentar auf `IdeOeffnenService` ergänzen, der erklärt, dass die Klasse aktuell keinen Produktions-Aufrufer mehr hat und bewusst als eigenständig getestete, wiederverwendbare API bzw. Referenzimplementierung erhalten bleibt (z. B. für zukünftige Nicht-UI-Aufrufer). `plan.md`/`requirement.md` entsprechend nachziehen, damit sie den tatsächlichen Datenfluss wiedergeben.

### TaskDetailViewModelTestFactory.cs (TaskDetailViewModelTestFactory)

- **Toter Code** — `CreateVerzeichnisAktionenServices` (Zeile 92–100) konstruiert weiterhin eine vollständige `IdeOeffnenService`-Instanz und gibt sie als Tupel-Element zurück. Nach der Entfernung des `IdeOeffnenService`-Parameters aus `TaskDetailViewModel` wird dieser Rückgabewert an **allen** Aufrufstellen im Repository verworfen: `TaskDetailViewModelTestFactory.cs` selbst (Zeile 55, `var (arbeitsverzeichnisOeffnenService, _) = ...`), `TaskDetailViewModelTestsBase.cs` (Zeile 132), `TaskDetailViewModelTests.cs` (Zeile 164) und `TaskDetailViewModelTests_PluginAktivierung.cs`/`TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` (jeweils per Diff sichtbar). Der dafür extra durchgereichte `pluginSelectionService`-Parameter (z. B. in `TaskDetailViewModelTests.cs` Zeile 164–166) dient ebenfalls nur noch dem Bau dieses ungenutzten Rückgabewerts.

  Empfehlung: `CreateVerzeichnisAktionenServices` auf die Rückgabe von nur noch `ArbeitsverzeichnisOeffnenService` reduzieren (Rückgabetyp ändern, kein Tupel mehr), sofern `IdeOeffnenService` nicht ohnehin wie im vorherigen Befund beschrieben ganz entfernt wird. Andernfalls, falls die Methode bewusst als generischer "baue beide zusammengehörigen Services" Helper für andere/zukünftige Aufrufer (z. B. `E2E_IdePluginSelection.cs`, die aber einen eigenen Stack bauen, siehe dort) erhalten bleiben soll, dies im Methodenkommentar begründen.

- **Veraltete Dokumentation** — Der XML-Doc-Kommentar auf dem `pluginSelectionService`-Parameter von `CreateVerzeichnisAktionenServices` (Zeile 86–90: „Muss derselbe sein wie der dem TaskDetailViewModel übergebene, damit OeffneIdeAsync() konsistent auflöst […] wenn OpenRepositoryInIdeAsync im Test nicht aufgerufen wird") beschreibt ein Verhalten, das es nicht mehr gibt: `TaskDetailViewModel` erhält gar keinen `IdeOeffnenService` mehr, und `OeffneIdeAsync` ruft nicht mehr `OpenRepositoryInIdeAsync` auf. Der Kommentar ist damit fachlich falsch und irreführend für zukünftige Leser.

  Empfehlung: Kommentar an die aktuelle Realität anpassen (analog zur bereits korrekt aktualisierten Kommentierung in `TaskDetailViewModelTests.cs` Zeile 166–167, die dasselbe Problem schon berücksichtigt) oder — falls der `IdeOeffnenService`-Rückgabewert wie oben empfohlen entfernt wird — ersatzlos streichen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyleHelper.cs`
- `src/Softwareschmiede.App/Controls/RibbonButtonStyles.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSplitButton.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTestsBase.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_PluginAktivierung.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_VerzeichnisAktionen.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailView_IdeAuswahl.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

## Ergänzende Prüfung (auf Anfrage): Design-Änderung TaskDetailViewModel → direkte Plugin-/Einstiegspunkt-Auflösung

Explizit geprüft: Ob durch die Umstellung von `TaskDetailViewModel` auf direkten Aufruf von `PluginSelectionService.ResolveIdePluginAsync` + `IIdePlugin.FindEntryPointsAsync`/`OpenEntryPointAsync` (statt `IdeOeffnenService.OpenRepositoryInIdeAsync`) Verhalten verloren geht.

- **Kein Funktionsverlust festgestellt.** `OeffneIdeInternAsync` bildet alle Verzweigungen von `OpenRepositoryInIdeAsync` 1:1 nach (0/1/mehrere Einstiegspunkte, optionaler Auswahl-Callback, identische `FileNotFoundException`-Meldung). Die Pfad-Validierung `ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath)`, die `IdeOeffnenService.OpenRepositoryInIdeAsync` selbst durchführt, bleibt erhalten, da sie identisch in `PluginSelectionService.ResolveIdePluginAsync` (Zeile 126) enthalten ist, die weiterhin aufgerufen wird.
- Die einzige zusätzliche Prüfung in `IdeOeffnenService.OpenRepositoryInIdeAsync` — `if (pluginSelectionService is null) throw new InvalidOperationException(...)` — betraf ausschließlich den Fall, dass der optionale Konstruktor-Parameter von `IdeOeffnenService` nicht gesetzt wurde. Da `TaskDetailViewModel._pluginSelectionService` ein verpflichtender, nicht-nullable Konstruktor-Parameter ist (immer per DI gesetzt), war dieser Zweig für `TaskDetailViewModel` faktisch nie erreichbar und geht durch den Wegfall nicht verloren.
- Das eigentliche Ergebnis dieser Umstellung ist demnach keine Verhaltensänderung, sondern die oben unter „Doppelter Code (klassenübergreifend)" beschriebene Verlagerung von einer *doppelten Laufzeit-Ausführung* (iteration-1-Befund) zu einer *doppelten Code-Kopie* derselben Logik — inklusive des daraus resultierenden toten `IdeOeffnenService` in der Produktion (siehe Befunde oben).
