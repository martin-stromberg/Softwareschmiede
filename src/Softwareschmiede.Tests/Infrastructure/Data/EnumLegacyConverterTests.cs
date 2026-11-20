using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;
using Softwareschmiede.Tests.Helpers;
using TestResult = Softwareschmiede.Domain.Entities.TestResult;

namespace Softwareschmiede.Tests.Infrastructure.Data;

/// <summary>
/// Stellt sicher, dass die als String persistierten Enums nach der Bezeichner-Umbenennung
/// weiterhin ihre bisherigen (deutschen) Datenbankwerte schreiben und lesen.
/// </summary>
public sealed class EnumLegacyConverterTests
{
    /// <summary>Gepinnte Enum-Properties mit ihren erwarteten Legacy-Persistenznamen.</summary>
    public static IEnumerable<object[]> PinnedEnumCases()
    {
        yield return [typeof(Project), "Status", typeof(ProjectStatus),
            new Dictionary<string, string> { ["Active"] = "Aktiv", ["Archived"] = "Archiviert" }];
        yield return [typeof(WorkItem), "Status", typeof(WorkItemStatus),
            new Dictionary<string, string>
            {
                ["New"] = "Neu", ["Started"] = "Gestartet", ["Waiting"] = "Wartend",
                ["Completed"] = "Beendet", ["Archived"] = "Archiviert"
            }];
        yield return [typeof(WorkItem), "ExecutionStatus", typeof(WorkItemExecutionStatus),
            new Dictionary<string, string>
            {
                ["NotStarted"] = "NichtGestartet", ["Active"] = "Aktiv", ["Completed"] = "Beendet"
            }];
        yield return [typeof(WorkItem), "RunStatus", typeof(WorkItemRunStatus),
            new Dictionary<string, string> { ["Running"] = "Laeuft", ["WaitingForInput"] = "WartetAufEingabe" }];
        yield return [typeof(LogEntry), "Type", typeof(LogEntryType),
            new Dictionary<string, string>
            {
                ["Prompt"] = "Prompt", ["AiResponse"] = "KiAntwort",
                ["StatusTransition"] = "StatusUebergang", ["TestResult"] = "TestErgebnis",
                ["GitAction"] = "GitAktion", ["CliOutput"] = "CliOutput",
                ["RateLimit"] = "RateLimit", ["SystemMessage"] = "SystemMeldung"
            }];
        yield return [typeof(TestResult), "Status", typeof(TestStatus),
            new Dictionary<string, string>
            {
                ["Passed"] = "Bestanden", ["Failed"] = "Fehlgeschlagen", ["Skipped"] = "Uebersprungen"
            }];
        yield return [typeof(PluginConfiguration), "PluginCategory", typeof(PluginCategory),
            new Dictionary<string, string> { ["Git"] = "Git", ["Ai"] = "Ki", ["Ide"] = "Ide" }];
        yield return [typeof(NotificationSetting), "BannerMode", typeof(NotificationMode),
            NotificationModeMap()];
        yield return [typeof(NotificationSetting), "SoundMode", typeof(NotificationMode),
            NotificationModeMap()];
        yield return [typeof(NotificationDispatchLog), "Channel", typeof(NotificationChannel),
            new Dictionary<string, string> { ["Banner"] = "Banner", ["Sound"] = "Ton" }];
        yield return [typeof(NotificationDispatchLog), "Mode", typeof(NotificationMode),
            NotificationModeMap()];
        yield return [typeof(NotificationDispatchLog), "Decision", typeof(NotificationDecision),
            new Dictionary<string, string>
            {
                ["Sent"] = "Gesendet", ["Suppressed"] = "Unterdrueckt",
                ["Deferred"] = "Zurueckgestellt", ["Failed"] = "Fehlgeschlagen"
            }];
    }

    /// <summary>Jeder Enum-Member wird auf den bisherigen persistierten String abgebildet und roundtrippt.</summary>
    [Theory]
    [MemberData(nameof(PinnedEnumCases))]
    public void PersistedEnumValues_ShouldRoundTripToLegacyStrings(Type entityType, string propertyName,
        Type enumType, Dictionary<string, string> expectedPersistedNames)
    {
        using var context = TestDbContextFactory.Create();
        var property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;
        var converter = property.GetTypeMapping().Converter!;

        foreach (var member in Enum.GetValues(enumType))
        {
            var name = Enum.GetName(enumType, member)!;
            var expected = expectedPersistedNames.TryGetValue(name, out var p) ? p : name;

            var persisted = converter.ConvertToProvider(member);
            persisted.Should().Be(expected,
                $"{enumType.Name}.{name} muss weiterhin als '{expected}' persistiert werden");

            converter.ConvertFromProvider(expected).Should().Be(member,
                $"Persistierter Wert '{expected}' muss nach {enumType.Name}.{name} lesen");
        }
    }

    /// <summary>Die bisher persistierten Legacy-Strings werden von existierenden Datenbanken gelesen.</summary>
    [Theory]
    [MemberData(nameof(PinnedEnumCases))]
    public void PersistedEnumValues_ShouldReadAllMappedLegacyStrings(Type entityType, string propertyName,
        Type enumType, Dictionary<string, string> expectedPersistedNames)
    {
        using var context = TestDbContextFactory.Create();
        var property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;
        var converter = property.GetTypeMapping().Converter!;

        foreach (var (name, persisted) in expectedPersistedNames)
        {
            var member = converter.ConvertFromProvider(persisted);
            Enum.GetName(enumType, member!).Should().Be(name);
        }
    }

    private static Dictionary<string, string> NotificationModeMap() => new()
    {
        ["Disabled"] = "Deaktiviert", ["Banner"] = "Banner", ["Sound"] = "Ton"
    };
}
