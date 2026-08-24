# Continue: Rückmeldungen zu "Autonome Aufgabe / Projektleiter-Modus"

Dieses Feature (`issue-205-47562196e8894189b9b9980fcd48a71c-automatisierte-produktentwickl`) war
zum Zeitpunkt dieser Notiz bereits über den lifecycle-Abschluss hinaus (Verzeichnis wurde nach
Abschluss des vorherigen Zyklus gelöscht, siehe Commit
`2e6173e feat: Anlageworkflow für autonome Aufgaben korrigieren (Klon-Quelle + Branch-Erstellung)`).
Dieses Verzeichnis wird hier ausschließlich wieder angelegt, um eine neue, davon unabhängige
Rückmeldung/Diagnose festzuhalten. **Es ist noch keine Umsetzung erfolgt** — nur Erfassung des
Befunds, wie vom Anwender angefordert.

## Offene Punkte

- [ ] **`AutonomAufgabenInitialisierungsService` klont immer über das global konfigurierte
  Default-SCM-Plugin statt über das am Repository der Aufgabe konfigurierte Plugin.**

  **Beobachtetes Symptom:** Beim Anlegen einer autonomen Aufgabe schlägt der Repository-Klon fehl.
  Vom Anwender bereitgestellter Stacktrace-Ausschnitt:

  ```
  Softwareschmiede.Infrastructure.Plugins.BitbucketPlugin.CloneRepositoryAsync(string, string, System.Threading.CancellationToken) in BitBucketPlugin.cs
  Softwareschmiede.Application.Services.AutonomAufgabenInitialisierungsService.KloneHauptRepositoryAsync(Softwareschmiede.Domain.Entities.Aufgabe, string, System.Threading.CancellationToken) in AutonomAufgabenInitialisierungsService.cs
  ```

  Der reguläre (nicht-autonome) Aufgaben-Start funktioniert hingegen einwandfrei.

  **Root Cause (durch Codeanalyse bestätigt):**
  - `AutonomAufgabenInitialisierungsService` bekommt `IGitPlugin` per Konstruktor injiziert.
    Die DI-Registrierung dafür (`src/Softwareschmiede.App/App.xaml.cs:265`) lautet:
    ```csharp
    services.AddScoped<IGitPlugin>(sp => sp.GetRequiredService<IPluginManager>().GetDefaultSourceCodeManagementPlugin());
    ```
    Das ist ein **global konfiguriertes „Default"-Plugin**, unabhängig davon, welches Plugin am
    `aufgabe.GitRepository.PluginTyp` der konkreten Aufgabe tatsächlich hinterlegt ist.
    `KloneHauptRepositoryAsync` (und `ErstelleProjektbranchAsync`) verwenden dieses injizierte
    `_gitPlugin` direkt, ohne es anhand der Aufgabe neu aufzulösen.
  - Im Gegensatz dazu löst der reguläre Start-Pfad
    (`EntwicklungsprozessService.ProzessStartenAsync` → `ResolvePluginAsync`,
    `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs:452-468`) das Plugin
    explizit anhand von `repository.PluginTyp` über
    `PluginSelectionService.ResolveSourceCodeManagementPluginAsync(resolvedPluginPrefix, ct)` auf —
    verwendet also stets das für dieses Repository tatsächlich konfigurierte Plugin.
  - Passt zum Stacktrace: Das global konfigurierte Default-Plugin ist offenbar Bitbucket
    (`BitbucketPlugin.CloneRepositoryAsync`), unabhängig davon, ob die konkrete Aufgabe wirklich
    ein Bitbucket-Repository referenziert. Ist für dieses Repository ein anderer Plugin-Typ
    konfiguriert, oder sind die Bitbucket-Zugangsdaten nicht hinterlegt (`BitBucketPlugin.cs:285f`
    wirft dann `InvalidOperationException("Bitbucket-Authentifizierung fehlt.")`), schlägt der Klon
    bei der Anlage der autonomen Aufgabe fehl.

  **Vermutete Korrektur:** `AutonomAufgabenInitialisierungsService` muss das zu verwendende
  `IGitPlugin` analog zu `EntwicklungsprozessService.ResolvePluginAsync` anhand von
  `aufgabe.GitRepository.PluginTyp` (über `PluginSelectionService` bzw. `IPluginManager`) auflösen,
  statt das per Default-DI injizierte `IGitPlugin` zu verwenden. Details der Umsetzung (z. B.
  Umbau der Konstruktor-Abhängigkeit von `IGitPlugin` auf `IPluginManager`/`PluginSelectionService`
  und Auflösung pro Aufruf) sind Teil der nachfolgenden Planung.

  **Status:** Nur als Befund erfasst. Codeanalyse (Root Cause) wurde bereits durchgeführt und ist
  oben dokumentiert; Umsetzung steht noch aus.

## Fehlgeschlagene Tests

_(keine — dieser Befund wurde durch Codeanalyse ermittelt, nicht durch einen fehlschlagenden Test)_
