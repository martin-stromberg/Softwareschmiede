using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.PluginImpl;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Lädt Plugins dynamisch aus dem Unterordner <c>plugins</c>.</summary>
public sealed class PluginManager : IPluginManager
{
    /// <summary>
    /// Whitelist der mitgelieferten First-Party-Plugin-Assemblies. Nur DLLs, deren Dateiname
    /// <em>und</em> echter Assembly-Name (Metadaten) hierauf passen, werden geladen — eine in
    /// <c>plugins</c> abgelegte fremde DLL läuft sonst mit vollen App-Rechten. Die Liste ist
    /// bewusst hartkodiert: Eine Konfigurationsdatei im nutzerschreibbaren Installationsverzeichnis
    /// ließe sich genauso fälschen wie die DLL selbst.
    /// </summary>
    private static readonly HashSet<string> AllowedPluginAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Softwareschmiede.Plugin.BitBucket",
        "Softwareschmiede.Plugin.ClaudeCli",
        "Softwareschmiede.Plugin.Codex",
        "Softwareschmiede.Plugin.Devin",
        "Softwareschmiede.Plugin.GitHub",
        "Softwareschmiede.Plugin.GitHubCopilot",
        "Softwareschmiede.Plugin.KiSimulator",
        "Softwareschmiede.Plugin.LocalDirectory",
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;
    private readonly string _pluginDirectory;
    private readonly bool _applyTestModeFilter;
    private readonly object _sync = new();
    private readonly List<IGitPlugin> _gitPlugins = [];
    private readonly List<IAiPlugin> _aiPlugins = [];
    private readonly List<IIdePlugin> _idePlugins = [];
    private bool _initialized;

    /// <summary>Erstellt eine neue Instanz von <see cref="PluginManager"/>.</summary>
    public PluginManager(
        IServiceProvider serviceProvider,
        ILogger<PluginManager> logger,
        string? pluginDirectory = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        // Test-Modus-Filter nur anwenden, wenn das Standard-Plugin-Verzeichnis genutzt wird.
        // Bei explizit übergebenem Verzeichnis (z. B. in Unit-Tests) wird der Filter nicht angewandt.
        _applyTestModeFilter = pluginDirectory is null;
        _pluginDirectory = pluginDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
    }

    /// <inheritdoc/>
    public IReadOnlyList<IGitPlugin> GetSourceCodeManagementPlugins()
    {
        EnsureInitialized();
        return _gitPlugins;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAiPlugin> GetDevelopmentAutomationPlugins()
    {
        EnsureInitialized();
        return _aiPlugins;
    }

    /// <inheritdoc/>
    public IGitPlugin GetDefaultSourceCodeManagementPlugin()
    {
        EnsureInitialized();
        return _gitPlugins.FirstOrDefault()
               ?? throw new InvalidOperationException("Kein Source-Code-Management-Plugin verfügbar.");
    }

    /// <inheritdoc/>
    public IAiPlugin GetDefaultDevelopmentAutomationPlugin()
    {
        EnsureInitialized();
        return _aiPlugins
                   .OrderBy(GetAiPluginPriority)
                   .ThenBy(p => p.PluginName, StringComparer.OrdinalIgnoreCase)
                   .FirstOrDefault()
               ?? throw new InvalidOperationException("Kein Development-Automation-Plugin verfügbar.");
    }

    private static int GetAiPluginPriority(IAiPlugin plugin)
        => plugin is CliAiPluginBase cliPlugin
           && string.Equals(cliPlugin.ProviderFilePrefix, "copilot", StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;

    /// <inheritdoc/>
    public IReadOnlyList<IIdePlugin> GetIdePlugins()
    {
        EnsureInitialized();
        return _idePlugins;
    }

    /// <inheritdoc/>
    public IIdePlugin GetDefaultIdePlugin()
    {
        EnsureInitialized();
        return _idePlugins.FirstOrDefault()
               ?? throw new InvalidOperationException("Kein IDE-Plugin verfügbar.");
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_sync)
        {
            if (_initialized)
            {
                return;
            }

            DiscoverPlugins();
            _initialized = true;
        }
    }

    private static bool IsTestMode()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Terminal.TerminalSessionService.TestDatabasePathVariable));

    private static bool IsAllowedInTestMode(string dllFileName)
    {
        var name = Path.GetFileNameWithoutExtension(dllFileName);
        return name.Equals("Softwareschmiede.Plugin.LocalDirectory", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Softwareschmiede.Plugin.KiSimulator", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Softwareschmiede.Plugin.ClaudeCli", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Softwareschmiede.Plugin.Codex", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Softwareschmiede.Plugin.Devin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Softwareschmiede.Plugin.GitHubCopilot", StringComparison.OrdinalIgnoreCase);
    }

    private void DiscoverPlugins()
    {
        _gitPlugins.Clear();
        _aiPlugins.Clear();
        _idePlugins.Clear();

        RegisterBuiltInIdePlugins();

        if (!Directory.Exists(_pluginDirectory))
        {
            _logger.LogWarning("Plugin-Ordner nicht gefunden: {PluginDirectory}", _pluginDirectory);
            return;
        }

        var testMode = _applyTestModeFilter && IsTestMode();
        foreach (var dllPath in EnumeratePluginCandidates())
        {
            if (testMode && !IsAllowedInTestMode(Path.GetFileName(dllPath)))
            {
                _logger.LogInformation("Test-Modus: Plugin übersprungen: {DllPath}", dllPath);
                continue;
            }

            if (!IsAllowedPlugin(dllPath))
            {
                continue;
            }

            LoadPluginsFromDll(dllPath);
        }

        _logger.LogInformation(
            "Plugin-Discovery abgeschlossen. SCM={ScmCount}, DevelopmentAutomation={DevCount}, Ide={IdeCount}",
            _gitPlugins.Count,
            _aiPlugins.Count,
            _idePlugins.Count);
    }

    /// <summary>
    /// Liefert die Ladekandidaten: direkt in <c>plugins</c> liegende DLLs (Legacy-Layout) sowie je
    /// Plugin-Unterordner nur die zum Ordnernamen passende DLL (<c>&lt;Ordner&gt;\&lt;Ordner&gt;.dll</c>).
    /// Übrige DLLs in den Unterordnern sind Dependencies des jeweiligen Plugins und keine Kandidaten.
    /// </summary>
    private IEnumerable<string> EnumeratePluginCandidates()
    {
        foreach (var dllPath in Directory.GetFiles(_pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            yield return dllPath;
        }

        foreach (var folder in Directory.GetDirectories(_pluginDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var entryDll = Path.Combine(folder, Path.GetFileName(folder) + ".dll");
            if (File.Exists(entryDll))
            {
                yield return entryDll;
            }
        }
    }

    /// <summary>
    /// Prüft eine Ladekandidaten-DLL gegen <see cref="AllowedPluginAssemblyNames"/> — zuerst den
    /// Dateinamen, dann den echten Assembly-Namen aus den Metadaten (ohne die Assembly zu laden),
    /// damit eine fremde DLL sich nicht per Umbenennung als bekanntes Plugin maskieren kann.
    /// Abgelehnte DLLs erzeugen einen Warn-Log-Eintrag.
    /// </summary>
    private bool IsAllowedPlugin(string dllPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(dllPath);
        if (!AllowedPluginAssemblyNames.Contains(fileName))
        {
            _logger.LogWarning(
                "Plugin-DLL steht nicht auf der Whitelist bekannter Plugin-Assemblies und wird nicht geladen: {DllPath}",
                dllPath);
            return false;
        }

        string? assemblyName;
        try
        {
            assemblyName = AssemblyLoadContext.GetAssemblyName(Path.GetFullPath(dllPath)).Name;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metadaten der Plugin-DLL konnten nicht gelesen werden, DLL wird nicht geladen: {DllPath}", dllPath);
            return false;
        }

        if (!string.Equals(assemblyName, fileName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Assembly-Name {AssemblyName} stimmt nicht mit dem whitelisted Dateinamen überein, DLL wird nicht geladen: {DllPath}",
                assemblyName,
                dllPath);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Registriert die eingebauten IDE-Plugins (Visual Studio, Visual Studio Code) direkt, ohne sie aus
    /// einer Plugin-DLL zu laden. Sie sind fester Bestandteil dieser Assembly.
    /// </summary>
    private void RegisterBuiltInIdePlugins()
    {
        TryCreateAndRegister(typeof(VisualStudioIdePlugin), "<built-in>");
        TryCreateAndRegister(typeof(VisualStudioCodeIdePlugin), "<built-in>");
    }

    private void LoadPluginsFromDll(string dllPath)
    {
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
            var pluginTypes = assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface &&
                            (typeof(IGitPlugin).IsAssignableFrom(t) || typeof(IAiPlugin).IsAssignableFrom(t) || typeof(IIdePlugin).IsAssignableFrom(t)))
                .ToList();

            foreach (var pluginType in pluginTypes)
            {
                TryCreateAndRegister(pluginType, dllPath);
            }
        }
        catch (BadImageFormatException ex)
        {
            _logger.LogWarning(ex, "Plugin-DLL ist kein gültiges .NET-Assembly und wird übersprungen: {DllPath}", dllPath);
        }
        catch (FileLoadException ex)
        {
            _logger.LogWarning(ex, "Plugin-DLL konnte nicht geladen werden und wird übersprungen: {DllPath}", dllPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Laden der Plugin-DLL, DLL wird übersprungen: {DllPath}", dllPath);
        }
    }

    private void TryCreateAndRegister(Type pluginType, string dllPath)
    {
        try
        {
            var instance = ActivatorUtilities.CreateInstance(_serviceProvider, pluginType);
            if (instance is not IPlugin plugin)
            {
                _logger.LogDebug("Keine unterstützte Plugin-Art in Typ {Type} aus {DllPath}.", pluginType.FullName, dllPath);
                return;
            }

            switch (plugin.PluginType)
            {
                case PluginType.SourceCodeManagement when instance is IGitPlugin gitPlugin:
                    _gitPlugins.Add(gitPlugin);
                    _logger.LogInformation("SCM-Plugin geladen: {PluginName} ({Type})", gitPlugin.PluginName, pluginType.FullName);
                    break;

                case PluginType.DevelopmentAutomation when instance is IAiPlugin aiPlugin:
                    _aiPlugins.Add(aiPlugin);
                    _logger.LogInformation("Development-Automation-Plugin geladen: {PluginName} ({Type})", aiPlugin.PluginName, pluginType.FullName);
                    break;

                case PluginType.Ide when instance is IIdePlugin idePlugin:
                    _idePlugins.Add(idePlugin);
                    _logger.LogInformation("IDE-Plugin geladen: {PluginName} ({Type})", idePlugin.PluginName, pluginType.FullName);
                    break;

                default:
                    _logger.LogWarning(
                        "Plugin-Typ/Interface-Kombination ungültig, Plugin wird übersprungen: {Type} ({PluginType}) aus {DllPath}",
                        pluginType.FullName,
                        plugin.PluginType,
                        dllPath);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin-Typ konnte nicht instanziiert werden und wird übersprungen: {Type} ({DllPath})", pluginType.FullName, dllPath);
        }
    }
}
