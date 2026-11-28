using msTools.Updater;

namespace Softwareschmiede.StatusServer.Update;

/// <summary>Überträgt die persistierten Admin-Einstellungen
/// (<see cref="StatusServerUpdateSettings"/>) auf den laufzeitveränderbaren
/// <see cref="AutoUpdateOptions"/>-Singleton der <c>msTools.Updater</c>-Bibliothek,
/// damit in der Admin-Oberfläche geänderte Werte sofort wirken.</summary>
public static class StatusServerAutoUpdateOptionsMapper
{
    /// <summary>Die tägliche Quellprüfung läuft einmal pro 24 Stunden; das
    /// konfigurierte Zeitfenster entscheidet, wann die Prüfung erlaubt ist.</summary>
    public const int DailySourceCheckIntervalMinutes = 24 * 60;

    /// <summary>Standard-Beginn des täglichen Prüfzeitfensters.</summary>
    public static TimeOnly DefaultSourceCheckStartTime { get; } = new(20, 0);

    /// <summary>Standard-Ende des täglichen Prüfzeitfensters.</summary>
    public static TimeOnly DefaultSourceCheckEndTime { get; } = new(6, 0);

    /// <summary>Wendet <paramref name="settings"/> auf <paramref name="options"/> an:
    /// Kanal (<see cref="AutoUpdateOptions.AllowPrereleaseUpdates"/>),
    /// Automatisierungsmodus (<see cref="AutoUpdateOptions.Enabled"/>,
    /// <see cref="AutoUpdateOptions.EnableAutomaticDownload"/>,
    /// <see cref="AutoUpdateOptions.EnableAutomaticInstallation"/>) und die
    /// Installationsuhrzeit (<see cref="AutoUpdateOptions.ScheduledInstallTime"/>,
    /// ohne die der Scheduler nie feuert). Das Prüfintervall wird auf den
    /// Tageswert gesetzt; das Zeitfenster ist Deployment-Konfiguration und wird
    /// beim Start aus <see cref="StatusServerUpdateOptions"/> übernommen.</summary>
    /// <param name="options">Der laufzeitveränderbare Options-Singleton.</param>
    /// <param name="settings">Die anzuwendenden Admin-Einstellungen.</param>
    public static void ApplySettings(AutoUpdateOptions options, StatusServerUpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        options.Enabled = settings.Mode != StatusServerAutomationMode.Off;
        options.AllowPrereleaseUpdates = settings.Channel == StatusServerUpdateChannel.ReleaseCandidate;
        options.EnableAutomaticDownload = settings.Mode == StatusServerAutomationMode.CheckAndInstall;
        options.EnableAutomaticInstallation = settings.Mode == StatusServerAutomationMode.CheckAndInstall;
        options.ScheduledInstallTime = settings.Mode == StatusServerAutomationMode.CheckAndInstall
            ? settings.ScheduledInstallTime
            : null;
        options.SourceCheck.Interval = DailySourceCheckIntervalMinutes;
    }

    /// <summary>Baut ein Prüfzeitfenster pro Wochentag. Fenster über Mitternacht
    /// werden in zwei tageseigene Ranges gespalten, weil der Evaluator der
    /// Bibliothek je Prüfung nur den aktuellen lokalen Wochentag und die Uhrzeit
    /// sieht (der <c>AutoUpdateOptionsValidator</c> verwirft Über-Mitternacht-Fenster).</summary>
    /// <param name="startTime">Inklusiver Beginn des täglichen Prüffensters.</param>
    /// <param name="endTime">Exklusives Ende des täglichen Prüffensters.</param>
    /// <returns>Die Bibliotheks-Zeitranges, die das konfigurierte Fenster abdecken.</returns>
    public static IReadOnlyList<SourceCheckTimeRange> BuildSourceCheckTimeRanges(TimeOnly startTime, TimeOnly endTime)
    {
        var ranges = new List<SourceCheckTimeRange>();
        foreach (var day in Enum.GetValues<DayOfWeek>())
            AddDailyRanges(ranges, day, startTime, endTime);

        return ranges;
    }

    private static void AddDailyRanges(List<SourceCheckTimeRange> ranges, DayOfWeek day, TimeOnly startTime, TimeOnly endTime)
    {
        if (startTime == endTime)
        {
            ranges.Add(new SourceCheckTimeRange { DayOfWeek = day, StartTime = TimeOnly.MinValue, EndTime = TimeOnly.MaxValue });
            return;
        }

        if (startTime < endTime)
        {
            ranges.Add(new SourceCheckTimeRange { DayOfWeek = day, StartTime = startTime, EndTime = endTime });
            return;
        }

        ranges.Add(new SourceCheckTimeRange { DayOfWeek = day, StartTime = startTime, EndTime = TimeOnly.MaxValue });
        ranges.Add(new SourceCheckTimeRange { DayOfWeek = day, StartTime = TimeOnly.MinValue, EndTime = endTime });
    }
}
