using Xunit.Sdk;

namespace Softwareschmiede.Tests.Infrastructure.Testing;

/// <summary>Markiert einen Test oder eine Testklasse als OS-Schnittstellen-Test.</summary>
[TraitDiscoverer("Softwareschmiede.Tests.Infrastructure.Testing.OsInterfaceTraitDiscoverer", "Softwareschmiede.Tests")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class OsInterfaceAttribute : Attribute, ITraitAttribute
{
}
