using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests;

/// <summary>Tests für das PluginSymbol-Member des Plugin-Vertrags.</summary>
public sealed class PluginSymbolTests
{
    /// <summary>Bewusster Contract-Pin: der Interface-Default muss genau dieser Glyph bleiben
    /// (binärkompatible Alt-Plugins) — wird daher bewusst als Literal gehalten statt
    /// <see cref="IPlugin.FallbackSymbol"/> zu referenzieren.</summary>
    private const string InterfaceDefault = "◻";

    /// <summary>
    /// Ermittelt alle konkreten <see cref="IPlugin"/>-Implementierungen der referenzierten
    /// Plugin-Assemblies per Reflection — neu hinzugefügte Plugins werden so automatisch
    /// mit abgedeckt, ohne die Liste hier pflegen zu müssen.
    /// </summary>
    private static IReadOnlyList<IPlugin> AllRepoPlugins()
    {
        var pluginTypes = typeof(PluginSymbolTests).Assembly.GetReferencedAssemblies()
            .Where(n => n.Name?.StartsWith("Softwareschmiede.Plugin.", StringComparison.Ordinal) == true)
            .Select(Assembly.Load)
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        pluginTypes.Should().NotBeEmpty("die Plugin-Assemblies müssen mindestens ein IPlugin liefern");
        return pluginTypes.Select(CreatePlugin).ToList();
    }

    private static IPlugin CreatePlugin(Type pluginType)
    {
        // Bei mehreren Konstruktoren (z. B. Convenience-Überladungen) genügt der einfachste.
        var ctor = pluginType.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var arguments = ctor.GetParameters().Select(p => CreateArgument(p.ParameterType)).ToArray();
        return (IPlugin)ctor.Invoke(arguments);
    }

    private static object CreateArgument(Type parameterType)
    {
        if (parameterType == typeof(ICliRunner))
            return new Mock<ICliRunner>().Object;
        if (parameterType == typeof(ICredentialStore))
            return new Mock<ICredentialStore>().Object;
        if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            var nullLoggerType = typeof(NullLogger<>).MakeGenericType(parameterType.GetGenericArguments());
            var instanceMember = (MemberInfo?)nullLoggerType.GetProperty("Instance")
                                 ?? nullLoggerType.GetField("Instance");
            return instanceMember is PropertyInfo prop
                ? prop.GetValue(null)!
                : ((FieldInfo)instanceMember!).GetValue(null)!;
        }

        // Fallback für weitere Abhängigkeitstypen: Mock<T> per Reflection erzeugen.
        var mockType = typeof(Mock<>).MakeGenericType(parameterType);
        return mockType.GetProperty("Object")!.GetValue(Activator.CreateInstance(mockType))!;
    }

    /// <summary>Alle Repo-Plugins liefern ein nicht-leeres Symbol, das vom Interface-Default abweicht.</summary>
    [Fact]
    public void PluginSymbol_ShouldBeNonEmptyAndDistinctFromDefault_ForAllRepoPlugins()
    {
        foreach (var plugin in AllRepoPlugins())
        {
            plugin.PluginSymbol.Should().NotBeNullOrWhiteSpace(
                $"{plugin.PluginName} muss ein eigenes Symbol liefern");
            plugin.PluginSymbol.Should().NotBe(IPlugin.FallbackSymbol,
                $"{plugin.PluginName} darf nicht auf dem Interface-Default bleiben");
        }
    }

    /// <summary>Ein Minimal-IPlugin ohne Override erhält den Interface-Default (binärkompatible Alt-Plugins).</summary>
    [Fact]
    public void PluginSymbol_ShouldReturnInterfaceDefault_WhenNotOverridden()
    {
        IPlugin plugin = new MinimumPlugin();

        plugin.PluginSymbol.Should().Be(InterfaceDefault);
    }

    private sealed class MinimumPlugin : IPlugin
    {
        public string PluginName => "Minimal";
        public string PluginPrefix => "Test.Minimal";
        public PluginType PluginType => PluginType.Ide;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
    }
}
