using System.Globalization;
using FluentAssertions;
using Softwareschmiede.App.Converters;

namespace Softwareschmiede.Tests.App.Converters;

/// <summary>Tests für UnassignedRepositoriesConverter.</summary>
public sealed class UnassignedRepositoriesConverterTests
{
    private readonly UnassignedRepositoriesConverter _sut = new();

    /// <summary>Convert gibt "gerade eben" für Timestamps unter einer Minute zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_JustNow()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddSeconds(-30), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("gerade eben");
    }

    /// <summary>Convert gibt "vor X Minuten" für Timestamps unter einer Stunde zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_Minutes()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddMinutes(-5), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 5 Minuten");
    }

    /// <summary>Convert gibt "vor 1 Minute" für genau eine Minute zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_OneMinute()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddMinutes(-1), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 1 Minute");
    }

    /// <summary>Convert gibt "vor X Stunden" für Timestamps unter einem Tag zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_Hours()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddHours(-3), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 3 Stunden");
    }

    /// <summary>Convert gibt "vor 1 Stunde" für genau eine Stunde zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_OneHour()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddHours(-1), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 1 Stunde");
    }

    /// <summary>Convert gibt "vor X Tagen" für Timestamps unter einem Monat zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_Days()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddDays(-5), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 5 Tagen");
    }

    /// <summary>Convert gibt "vor 1 Tag" für genau einen Tag zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_OneDay()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddDays(-1), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 1 Tag");
    }

    /// <summary>Convert gibt "vor X Monaten" für Timestamps unter einem Jahr zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_Months()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddDays(-90), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 3 Monaten");
    }

    /// <summary>Convert gibt "vor X Jahren" für sehr alte Timestamps zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldFormatRelativeTime_Years()
    {
        var result = _sut.Convert(DateTime.UtcNow.AddDays(-730), typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("vor 2 Jahren");
    }

    /// <summary>Convert gibt "unbekannt" für DateTime.MinValue zurück.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldHandleNullAndMinValue_MinValue()
    {
        var result = _sut.Convert(DateTime.MinValue, typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("unbekannt");
    }

    /// <summary>Convert gibt "unbekannt" zurück, wenn ein nicht-DateTime-Wert übergeben wird.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ShouldHandleNullAndMinValue_NonDateTimeValue()
    {
        var result = _sut.Convert("invalid", typeof(string), null!, CultureInfo.InvariantCulture);
        result.Should().Be("unbekannt");
    }

    /// <summary>ConvertBack wirft NotSupportedException.</summary>
    [Fact]
    public void UnassignedRepositoriesConverter_ConvertBack_ShouldThrowNotSupportedException()
    {
        var act = () => _sut.ConvertBack("vor 2 Stunden", typeof(DateTime), null!, CultureInfo.InvariantCulture);
        act.Should().Throw<NotSupportedException>();
    }
}
