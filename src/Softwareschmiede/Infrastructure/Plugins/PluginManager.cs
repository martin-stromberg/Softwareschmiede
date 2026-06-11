using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Abstractions;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;

namespace Softwareschmiede.Infrastructure.Plugins;

/// <summary>Lädt Plugins dynamisch aus dem Unterordner <c>plugins</c>.</summary>
public sealed class PluginManager : IPluginManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;
    private readonly string _pluginDirectory;
    private readonly object _sync = new();
    private readonly List<IGitPlugin> _gitPlugins = [];
    private readonly List<IKiPlugin> _kiPlugins = [];
    private bool _initialized;

    public PluginManager(
        IServiceProvider serviceProvider,
        ILogger<PluginManager> logger,
        string? pluginDirectory = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pluginDirectory = pluginDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
    }

    public IReadOnlyList<IGitPlugin> GetSourceCodeManagementPlugins()
    {
        EnsureInitialized();
        return _gitPlugins;
    }

    public IReadOnlyList<IKiPlugin> GetDevelopmentAutomationPlugins()
    {
        EnsureInitialized();
        return _kiPlugins;
    }

    public IGitPlugin GetDefaultSourceCodeManagementPlugin()
    {
        EnsureInitialized();
        return _gitPlugins.FirstOrDefault()
               ?? throw new InvalidOperationException("Kein Source-Code-Management-Plugin verfügbar.");
    }

    public IKiPlugin GetDefaultDevelopmentAutomationPlugin()
    {
        EnsureInitialized();
        return _kiPlugins
                   .OrderBy(GetKiPluginPriority)
                   .ThenBy(p => p.PluginName, StringComparer.OrdinalIgnoreCase)
                   .FirstOrDefault()
               ?? throw new InvalidOperationException("Kein Development-Automation-Plugin verfügbar.");
    }

    private static int GetKiPluginPriority(IKiPlugin plugin)
        => plugin is CliKiPluginBase cliPlugin
           && string.Equals(cliPlugin.ProviderDateiPraefix, "copilot", StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;

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

    private void DiscoverPlugins()
    {
        _gitPlugins.Clear();
        _kiPlugins.Clear();

        if (!Directory.Exists(_pluginDirectory))
        {
            _logger.LogWarning("Plugin-Ordner nicht gefunden: {PluginDirectory}", _pluginDirectory);
            return;
        }

        foreach (var dllPath in Directory.GetFiles(_pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            LoadPluginsFromDll(dllPath);
        }

        _logger.LogInformation(
            "Plugin-Discovery abgeschlossen. SCM={ScmCount}, DevelopmentAutomation={DevCount}",
            _gitPlugins.Count,
            _kiPlugins.Count);
    }

    private void LoadPluginsFromDll(string dllPath)
    {
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
            var pluginTypes = assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface &&
                            (typeof(IGitPlugin).IsAssignableFrom(t) || typeof(IKiPlugin).IsAssignableFrom(t)))
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

                case PluginType.DevelopmentAutomation when instance is IKiPlugin kiPlugin:
                    _kiPlugins.Add(kiPlugin);
                    _logger.LogInformation("Development-Automation-Plugin geladen: {PluginName} ({Type})", kiPlugin.PluginName, pluginType.FullName);
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
