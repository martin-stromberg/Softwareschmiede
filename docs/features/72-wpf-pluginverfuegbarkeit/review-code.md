```json
[
  {
    "file": "src/Softwareschmiede.Tests/ServiceIntegration/DarkModeServiceIntegrationTests.cs",
    "line": 25,
    "summary": "Integration tests call SetBoolSettingAsync/GetBoolSettingAsync with DesignModeKey, but the service now stores and reads design mode as a string (\"Dark\"/\"Light\"), not a boolean.",
    "failure_scenario": "Test stores \"True\" or \"False\" via SetBoolSettingAsync(DesignModeKey, true). DarkModeService.InitializeAsync then reads this value via GetSettingAsync and passes it to ApplyTheme. ApplyTheme does _themeUris[\"True\"] which throws KeyNotFoundException — test fails or ApplyTheme crashes at runtime when the persisted bool value is loaded."
  },
  {
    "file": "src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml",
    "line": 112,
    "summary": "IsEnabled=\"{Binding HasScmPlugins}\" on the 'Zuweisen' button overrides BestaetigenCommand.CanExecute, allowing the button to be clicked when no repository is selected.",
    "failure_scenario": "Plugins are loaded (HasScmPlugins = true), user sees the repository list but has not yet selected a repository (SelectedRepository = null). The button is enabled because HasScmPlugins is true. User clicks 'Zuweisen' → BestaetigenCommand.Execute fires → CloseRequested(this, true) is raised with SelectedRepository = null → caller receives null repository and crashes or silently assigns null."
  },
  {
    "file": "src/Softwareschmiede.App/Services/DarkModeService.cs",
    "line": 24,
    "summary": "DarkModeService._currentMode is never initialized (null in C#), so DarkModeService.Current returns null before InitializeAsync completes; SettingsViewModel reads it in its constructor and later passes null to SetModeAsync, crashing ApplyTheme.",
    "failure_scenario": "SettingsViewModel is resolved from DI (Transient) before MainWindow.OnSourceInitialized calls InitializeAsync (e.g. via eager resolution or test setup). Constructor line 113 sets _designMode = null. User opens Settings and clicks 'Speichern' without touching the design dropdown → SpeichernAsync calls SetModeAsync(null) → ApplyTheme(null) → _themeUris[null] throws ArgumentNullException, crashing the UI thread."
  },
  {
    "file": "plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs",
    "line": 74,
    "summary": "CheckHealthAsync does not kill the child process when WaitForExitAsync is cancelled by the 10-second timeout; Process.Dispose() releases only the managed handle, leaving the OS process running.",
    "failure_scenario": "The 'claude --version' subprocess hangs (e.g. on a network-mapped PATH) for longer than 10 seconds. The CTS fires, WaitForExitAsync throws OperationCanceledException, the catch block returns false, and 'using var process' calls Dispose(). The OS process continues running. On repeated health checks (plugin availability polling on app startup or settings refresh), orphaned 'claude --version' and 'copilot --version' processes accumulate."
  },
  {
    "file": "plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs",
    "line": 90,
    "summary": "Same zombie-process leak as ClaudeCliPlugin: process not killed before disposal when WaitForExitAsync is cancelled by the 10-second timeout.",
    "failure_scenario": "Same as above for 'copilot --version'. The Win32Exception catch handles missing executables correctly but does not cover the hung-process case."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs",
    "line": 121,
    "summary": "LadenAsync does not reload DesignMode from the database, so VerwerfenAsync (which calls LadenAsync) cannot revert an unsaved design mode change.",
    "failure_scenario": "User opens Settings, changes the Design dropdown from 'Dark' to 'Light' (without saving), then clicks 'Verwerfen'. VerwerfenAsync → LadenAsync runs and reloads Arbeitsverzeichnis, DefaultKiPlugin, ScmPlugins, KiPlugins — but never queries AppEinstellungService.GetSettingAsync(DesignModeKey). _designMode stays as the user's unsaved choice 'Light'. The dropdown still shows 'Light' after discard, inconsistently with all other settings which are correctly reverted."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/PluginSettingsViewModel.cs",
    "line": 163,
    "summary": "OperationCanceledException thrown by ct.ThrowIfCancellationRequested() is caught by the generic catch(Exception ex) block inside LadenAsync (which is synchronous), showing a spurious error message to the user on normal cancellation.",
    "failure_scenario": "User navigates away from the Plugin Settings tab while the synchronous loading loop is running. The outer AsyncRelayCommand would catch OperationCanceledException, but because LadenAsync is not async (returns Task.CompletedTask), the ct.ThrowIfCancellationRequested() throw is caught by the internal catch(Exception ex) at line 183. FehlerMeldung is set to \"Fehler: The operation was canceled.\" and the plugin list is cleared — the user sees a red error banner for a normal navigation event."
  },
  {
    "file": "src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs",
    "line": 83,
    "summary": "LadenAsync populates AvailableScmPlugins but never sets SelectedScmPlugin, so the repository list stays empty until the user manually selects a plugin in the dropdown.",
    "failure_scenario": "Dialog opens with exactly one SCM plugin installed. HasScmPlugins becomes true, the plugin ComboBox shows one item, but SelectedScmPlugin remains null. ReloadRepositoriesForSelectedPlugin is never triggered. VerfuegbareRepositories is empty. Combined with the IsEnabled bug (#2), the 'Zuweisen' button is immediately enabled (HasScmPlugins = true) but the repository list is empty — user can click 'Zuweisen' and close the dialog with SelectedRepository = null."
  }
]
```
