using Xunit.Abstractions;
using Xunit.Sdk;

namespace Softwareschmiede.Tests.Infrastructure.Testing;

/// <summary>Liefert den xUnit-Trait fuer OS-Schnittstellen-Attribute.</summary>
public sealed class OsInterfaceTraitDiscoverer : ITraitDiscoverer
{
    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", TestCategories.OsInterface);
    }
}
