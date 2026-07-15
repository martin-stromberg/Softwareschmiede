using System.Globalization;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using FluentAssertions;
using Softwareschmiede.App.Converters;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.App.Converters;

/// <summary>Tests für DiffLineStatusToBrushConverter.</summary>
public sealed class DiffLineStatusToBrushConverterTests
{
    private static readonly Dispatcher StaDispatcher = StartStaDispatcher();

    /// <summary>Für Added/Removed/Modified wird der zum aktuell aktiven Theme gehörende Brush aus den Anwendungsressourcen aufgelöst statt eines fest codierten Hex-Werts.</summary>
    [Theory]
    [InlineData(DiffLineStatus.Added, "DiffAddedBrush")]
    [InlineData(DiffLineStatus.Removed, "DiffRemovedBrush")]
    [InlineData(DiffLineStatus.Modified, "DiffModifiedBrush")]
    public void Convert_StatusMitThemeBrush_LiefertBrushAusAnwendungsressourcen(DiffLineStatus status, string resourceKey)
    {
        StaDispatcher.Invoke(() =>
        {
            var expectedBrush = new SolidColorBrush(Colors.Magenta);
            System.Windows.Application.Current!.Resources[resourceKey] = expectedBrush;

            var converter = new DiffLineStatusToBrushConverter();
            var result = converter.Convert(status, typeof(Brush), null!, CultureInfo.InvariantCulture);

            result.Should().BeSameAs(expectedBrush);
        });
    }

    /// <summary>Für Context-Zeilen liefert der Converter unabhängig vom Theme einen transparenten Brush.</summary>
    [Fact]
    public void Convert_Context_LiefertTransparentenBrush()
    {
        StaDispatcher.Invoke(() =>
        {
            var converter = new DiffLineStatusToBrushConverter();

            var result = converter.Convert(DiffLineStatus.Context, typeof(Brush), null!, CultureInfo.InvariantCulture);

            result.Should().Be(Brushes.Transparent);
        });
    }

    private static Dispatcher StartStaDispatcher()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            if (System.Windows.Application.Current is null)
            {
                _ = new System.Windows.Application();
            }

            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();

        return dispatcher!;
    }
}
