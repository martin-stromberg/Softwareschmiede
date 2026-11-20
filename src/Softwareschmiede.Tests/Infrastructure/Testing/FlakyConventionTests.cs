using System.Reflection;
using FluentAssertions;
using Xunit.v3;

namespace Softwareschmiede.Tests.Infrastructure.Testing;

/// <summary>Sichert die Konvention ab, dass das <see cref="FlakyAttribute"/> nur in Kombination
/// mit <see cref="OsInterfaceAttribute"/> vergeben wird — ein Verstoss wuerde einen
/// retry-berechtigten Test in die blockierende Lane schieben.</summary>
public sealed class FlakyConventionTests
{
    /// <summary>Jede Methode oder Klasse, deren Trait-Attribute Category=Flaky liefern,
    /// muss zusaetzlich Category=OsInterface liefern.</summary>
    [Fact]
    public void FlakyMarkedTests_AlwaysCarryOsInterface()
    {
        var violations = new List<string>();
        var assembly = typeof(FlakyConventionTests).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            var typeTraits = Traits(type.GetCustomAttributes(inherit: true)).ToArray();
            if (HasCategory(typeTraits, TestCategories.Flaky) && !HasCategory(typeTraits, TestCategories.OsInterface))
                violations.Add(type.FullName ?? type.Name);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var methodTraits = Traits(method.GetCustomAttributes(inherit: true)).Concat(typeTraits).ToArray();
                if (HasCategory(methodTraits, TestCategories.Flaky) && !HasCategory(methodTraits, TestCategories.OsInterface))
                    violations.Add($"{type.FullName}.{method.Name}");
            }
        }
        violations.Should().BeEmpty("Flaky darf nur in Kombination mit OsInterface vergeben werden");
    }

    private static IEnumerable<KeyValuePair<string, string>> Traits(IEnumerable<object> attributes)
        => attributes.OfType<ITraitAttribute>().SelectMany(attribute => attribute.GetTraits());

    private static bool HasCategory(IEnumerable<KeyValuePair<string, string>> traits, string value)
        => traits.Any(trait => trait.Key == "Category" && trait.Value == value);
}
