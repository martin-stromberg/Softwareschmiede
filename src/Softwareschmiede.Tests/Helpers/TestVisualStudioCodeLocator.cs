using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Test-Fake für <see cref="IVisualStudioCodeLocator"/> mit fest konfiguriertem Verfügbarkeits-Ergebnis.</summary>
/// <param name="availability">Das von <see cref="Locate"/> zurückgegebene Verfügbarkeits-Ergebnis.</param>
public sealed class TestVisualStudioCodeLocator(VisualStudioCodeAvailability availability) : IVisualStudioCodeLocator
{
    /// <inheritdoc/>
    public VisualStudioCodeAvailability Locate() => availability;
}
