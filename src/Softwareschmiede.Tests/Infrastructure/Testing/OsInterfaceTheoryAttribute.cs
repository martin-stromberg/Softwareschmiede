using Xunit.Sdk;

namespace Softwareschmiede.Tests.Infrastructure.Testing;

/// <summary>xUnit-Theory fuer Tests mit echter Betriebssystem-Schnittstellen-Beruehrung.</summary>
[TraitDiscoverer("Softwareschmiede.Tests.Infrastructure.Testing.OsInterfaceTraitDiscoverer", "Softwareschmiede.Tests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OsInterfaceTheoryAttribute : TheoryAttribute, ITraitAttribute
{
}
