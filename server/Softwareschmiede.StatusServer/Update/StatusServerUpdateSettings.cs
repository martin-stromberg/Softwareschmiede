namespace Softwareschmiede.StatusServer.Update;

/// <summary>Die in der <c>ServerSettings</c>-Tabelle persistierten Admin-Einstellungen
/// der Update-Funktion (Kanal, Automatisierungsmodus, tägliche Installationsuhrzeit).</summary>
/// <param name="Channel">Der Release-Kanal.</param>
/// <param name="Mode">Der Automatisierungsmodus.</param>
/// <param name="ScheduledInstallTime">Die tägliche Installationsuhrzeit —
/// nur für <see cref="StatusServerAutomationMode.CheckAndInstall"/> relevant.</param>
public sealed record StatusServerUpdateSettings(
    StatusServerUpdateChannel Channel,
    StatusServerAutomationMode Mode,
    TimeOnly? ScheduledInstallTime);
