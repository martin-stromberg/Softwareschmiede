using FluentAssertions;
using msTools.Updater;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Update;

/// <summary>Tests für <see cref="StatusServerAutoUpdateOptionsMapper"/>:
/// Modus-/Kanal-Mapping auf die Bibliotheksoptionen und der
/// Über-Mitternacht-Split des Prüfzeitfensters.</summary>
public sealed class StatusServerAutoUpdateOptionsMapperTests
{
    /// <summary>Der Modus <c>Off</c> deaktiviert das Subsystem komplett;
    /// <c>CheckOnly</c> aktiviert es ohne Auto-Download/-Installation;
    /// <c>CheckAndInstall</c> aktiviert alles inkl. Installationsuhrzeit.</summary>
    [Fact]
    public void ApplySettings_MapsModes()
    {
        var options = new AutoUpdateOptions();

        StatusServerAutoUpdateOptionsMapper.ApplySettings(options,
            new StatusServerUpdateSettings(StatusServerUpdateChannel.Stable, StatusServerAutomationMode.Off, null));
        options.Enabled.Should().BeFalse();
        options.EnableAutomaticDownload.Should().BeFalse();
        options.EnableAutomaticInstallation.Should().BeFalse();
        options.ScheduledInstallTime.Should().BeNull();

        StatusServerAutoUpdateOptionsMapper.ApplySettings(options,
            new StatusServerUpdateSettings(StatusServerUpdateChannel.Stable, StatusServerAutomationMode.CheckOnly, null));
        options.Enabled.Should().BeTrue();
        options.EnableAutomaticDownload.Should().BeFalse();
        options.EnableAutomaticInstallation.Should().BeFalse();
        options.ScheduledInstallTime.Should().BeNull();

        var installTime = new TimeOnly(3, 30);
        StatusServerAutoUpdateOptionsMapper.ApplySettings(options,
            new StatusServerUpdateSettings(StatusServerUpdateChannel.Stable, StatusServerAutomationMode.CheckAndInstall, installTime));
        options.Enabled.Should().BeTrue();
        options.EnableAutomaticDownload.Should().BeTrue();
        options.EnableAutomaticInstallation.Should().BeTrue();
        options.ScheduledInstallTime.Should().Be(installTime);
    }

    /// <summary>Der Kanal <c>ReleaseCandidate</c> aktiviert
    /// <see cref="AutoUpdateOptions.AllowPrereleaseUpdates"/>, <c>Stable</c> nicht.</summary>
    [Fact]
    public void ApplySettings_MapsChannel()
    {
        var options = new AutoUpdateOptions();

        StatusServerAutoUpdateOptionsMapper.ApplySettings(options,
            new StatusServerUpdateSettings(StatusServerUpdateChannel.ReleaseCandidate, StatusServerAutomationMode.CheckOnly, null));
        options.AllowPrereleaseUpdates.Should().BeTrue();

        StatusServerAutoUpdateOptionsMapper.ApplySettings(options,
            new StatusServerUpdateSettings(StatusServerUpdateChannel.Stable, StatusServerAutomationMode.CheckOnly, null));
        options.AllowPrereleaseUpdates.Should().BeFalse();
    }

    /// <summary>Das Über-Mitternacht-Fenster 20:00–06:00 wird je Wochentag in zwei
    /// Ranges gespalten (20:00–Mitternacht, Mitternacht–06:00), weil der
    /// Options-Validator Fenster über Tagesgrenzen verwerfen würde.</summary>
    [Fact]
    public void BuildSourceCheckTimeRanges_SplitsOvernight()
    {
        var ranges = StatusServerAutoUpdateOptionsMapper
            .BuildSourceCheckTimeRanges(new TimeOnly(20, 0), new TimeOnly(6, 0));

        ranges.Should().HaveCount(14);
        var monday = ranges.Where(r => r.DayOfWeek == DayOfWeek.Monday).ToList();
        monday.Should().HaveCount(2);
        monday.Should().Contain(r => r.StartTime == new TimeOnly(20, 0) && r.EndTime == TimeOnly.MaxValue);
        monday.Should().Contain(r => r.StartTime == TimeOnly.MinValue && r.EndTime == new TimeOnly(6, 0));
    }

    /// <summary>Ein reguläres Fenster innerhalb eines Tages erzeugt je Wochentag
    /// genau eine Range.</summary>
    [Fact]
    public void BuildSourceCheckTimeRanges_SameDay_SingleRangePerDay()
    {
        var ranges = StatusServerAutoUpdateOptionsMapper
            .BuildSourceCheckTimeRanges(new TimeOnly(2, 0), new TimeOnly(5, 0));

        ranges.Should().HaveCount(7);
        ranges.Should().OnlyContain(r => r.StartTime == new TimeOnly(2, 0) && r.EndTime == new TimeOnly(5, 0));
    }
}
