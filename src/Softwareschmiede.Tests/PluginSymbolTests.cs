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
    private static IReadOnlyList<IPlugin> AlleRepoPlugins()
    {
        var pluginTypen = typeof(PluginSymbolTests).Assembly.GetReferencedAssemblies()
            .Where(n => n.Name?.StartsWith("Softwareschmiede.Plugin.", StringComparison.Ordinal) == true)
            .Select(Assembly.Load)
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        pluginTypen.Should().NotBeEmpty("die Plugin-Assemblies müssen mindestens ein IPlugin liefern");
        return pluginTypen.Select(ErstellePlugin).ToList();
    }

    private static IPlugin ErstellePlugin(Type pluginTyp)
    {
        // Bei mehreren Konstruktoren (z. B. Convenience-Überladungen) genügt der einfachste.
        var ctor = pluginTyp.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var argumente = ctor.GetParameters().Select(p => ErstelleArgument(p.ParameterType)).ToArray();
        return (IPlugin)ctor.Invoke(argumente);
    }

    private static object ErstelleArgument(Type parameterTyp)
    {
        if (parameterTyp == typeof(ICliRunner))
            return new Mock<ICliRunner>().Object;
        if (parameterTyp == typeof(ICredentialStore))
            return new Mock<ICredentialStore>().Object;
        if (parameterTyp.IsGenericType && parameterTyp.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            var nullLoggerTyp = typeof(NullLogger<>).MakeGenericType(parameterTyp.GetGenericArguments());
            var instanceMember = (MemberInfo?)nullLoggerTyp.GetProperty("Instance")
                                 ?? nullLoggerTyp.GetField("Instance");
            return instanceMember is PropertyInfo prop
                ? prop.GetValue(null)!
                : ((FieldInfo)instanceMember!).GetValue(null)!;
        }

        // Fallback für weitere Abhängigkeitstypen: Mock<T> per Reflection erzeugen.
        var mockTyp = typeof(Mock<>).MakeGenericType(parameterTyp);
        return mockTyp.GetProperty("Object")!.GetValue(Activator.CreateInstance(mockTyp))!;
    }

    /// <summary>Alle Repo-Plugins liefern ein nicht-leeres Symbol, das vom Interface-Default abweicht.</summary>
    [Fact]
    public void PluginSymbol_ShouldBeNonEmptyAndDistinctFromDefault_ForAllRepoPlugins()
    {
        foreach (var plugin in AlleRepoPlugins())
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
        IPlugin plugin = new MinimalPlugin();

        plugin.PluginSymbol.Should().Be(InterfaceDefault);
    }

    private sealed class MinimalPlugin : IPlugin
    {
        public string PluginName => "Minimal";
        public string PluginPrefix => "Test.Minimal";
        public PluginType PluginType => PluginType.Ide;
        public IReadOnlyList<PluginSettingGroup> GetSettingGroups() => [];
    }
}
