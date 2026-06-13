# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine Tests fehlgeschlagen.

## Zusammenfassung

- Gesamt: 416
- Bestanden: 416
- Fehlgeschlagen: 0
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 25.36 %

| Datei | Abdeckung |
|-------|-----------|
| src\Softwareschmiede.App\ViewModels\MainWindowViewModel.cs | 0% |
| src\Softwareschmiede\Migrations\20260507051631_202507_Fix_DateTimeOffset_SQLiteOrdering.cs | 0% |
| src\Softwareschmiede\Domain\ValueObjects\FileTreeNode.cs | 0% |
| src\Softwareschmiede.App\Controls\RibbonGroup.xaml | 0% |
| src\Softwareschmiede.App\Controls\RecoveryBannerControl.xaml | 0% |
| src\Softwareschmiede.App\ViewModels\DashboardViewModel.cs | 0% |
| src\Softwareschmiede.App\Controls\RibbonSmallButton.xaml.cs | 0% |
| src\Softwareschmiede\Infrastructure\Services\CliRunner.cs | 0% |
| src\Softwareschmiede\Migrations\SoftwareschmiededDbContextModelSnapshot.cs | 0% |
| src\Softwareschmiede.App\Services\WpfAudioService.cs | 0% |
| src\Softwareschmiede.Plugin.Contracts\Domain\ValueObjects\AgentInfo.cs | 0% |
| src\Softwareschmiede\Infrastructure\Data\SoftwareschmiededDbContextDesignTimeFactory.cs | 0% |
| src\Softwareschmiede\Application\Services\CliProcessManager.cs | 0% |
| src\Softwareschmiede\Migrations\20260506195327_InitialCreate.cs | 0% |
| src\Softwareschmiede\Migrations\20260612193852_202606120001_RenameBannerModus.cs | 0% |
| src\Softwareschmiede\Migrations\20260610000003_202606100003_AddWindowGeometrySettings.cs | 0% |
| src\Softwareschmiede.App\Views\SettingsView.xaml.cs | 0% |
| src\Softwareschmiede.App\Controls\RibbonGroup.xaml.cs | 0% |
| src\Softwareschmiede\Application\Services\KiAufgabenBenachrichtigungsHub.cs | 0% |
| src\Softwareschmiede.App\Controls\RibbonSmallButton.xaml | 0% |
| src\Softwareschmiede\Migrations\20260531192120_202606011955_AddTaskPromptSuggestionColumns.cs | 0% |
| src\Softwareschmiede.App\ViewModels\ProjectListViewModel.cs | 0% |
| src\Softwareschmiede.App\Views\TaskDetailView.xaml.cs | 0% |
| src\Softwareschmiede.App\Views\PluginSettingsView.xaml.cs | 0% |
| src\Softwareschmiede.App\Views\TaskDetailView.xaml | 0% |
| src\Softwareschmiede\Migrations\20260610000001_202606100001_UpdateAufgabeStatusEnum.cs | 0% |
| src\Softwareschmiede\Infrastructure\Services\SystemShutdownService.cs | 0% |
| src\Softwareschmiede\Domain\Entities\PluginKonfiguration.cs | 0% |
| src\Softwareschmiede.Plugin.Contracts\Domain\Interfaces\IGitPlugin.cs | 0% |
| src\Softwareschmiede.App\Controls\ProcessWindowHost.cs | 0% |
| src\Softwareschmiede.App\Controls\RecoveryBannerControl.xaml.cs | 0% |
| src\Softwareschmiede.App\Views\RepositoryAssignDialog.xaml | 0% |
| src\Softwareschmiede.App\Views\ProjectListView.xaml.cs | 0% |
| src\Softwareschmiede.App\Views\PluginSettingsView.xaml | 0% |
| src\Softwareschmiede\Migrations\20260523113807_AddKiTaskNotifications.cs | 0% |
| src\Softwareschmiede.App\Views\DashboardView.xaml.cs | 0% |
| src\Softwareschmiede.App\App.xaml.cs | 0% |
| src\Softwareschmiede\Migrations\20260523052722_202605230001_AddTaskRecoveryIndicators.cs | 0% |
| src\Softwareschmiede.App\Controls\RibbonLargeButton.xaml | 0% |
| src\Softwareschmiede\Infrastructure\Services\WindowsCredentialStore.cs | 0% |
| src\Softwareschmiede.App\Views\ProjectDetailView.xaml | 0% |
| src\Softwareschmiede\Infrastructure\Services\BenutzerkontextService.cs | 0% |
| src\Softwareschmiede.App\ViewModels\TaskListViewModel.cs | 0% |
| src\Softwareschmiede.App\ViewModels\SettingsViewModel.cs | 0% |
| src\Softwareschmiede.App\Controls\StatusIndicatorControl.xaml | 0% |
| src\Softwareschmiede.App\Views\TaskListView.xaml | 0% |
| src\Softwareschmiede.App\Views\RepositoryAssignDialog.xaml.cs | 0% |
| src\Softwareschmiede\Migrations\20260610000002_202606100002_UpdateBenachrichtigungsEnums.cs | 0% |
| src\Softwareschmiede\Migrations\20260522192947_202605171230_AddDiffComparison.cs | 0% |
| src\Softwareschmiede\Migrations\20260516085728_202605161100_RemoveStartScriptArguments.cs | 0% |

## Fehlende Tests

Quelle: Coverage-Daten

Dateien ohne Testabdeckung (0 % Abdeckung) umfassen primär:
- **Migrations** (Designer.cs und Snapshot-Dateien) — automatisch generiert
- **WPF-Views und ViewModels** (XAML, Code-Behind) — UI-Layer ohne Unit-Tests
- **App-Services und Plugin-Implementierungen** — werden durch E2E-Tests verdeckt, nicht direkt getestet
- **Infrastruktur-Services** (CliRunner, WindowsCredentialStore, SystemShutdownService) — Hard-Dependencies auf System-Ressourcen
