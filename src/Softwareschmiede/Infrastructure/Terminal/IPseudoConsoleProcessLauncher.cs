using System.Diagnostics;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Austauschpunkt für den eigentlichen ConPTY-Prozessstart in <see cref="Application.Services.KiAusfuehrungsService"/>.</summary>
public interface IPseudoConsoleProcessLauncher
{
    /// <summary>Startet den CLI-Prozess und liefert Prozess, Sitzung und natives Prozess-Handle.</summary>
    /// <param name="aufgabeId">ID der Aufgabe (für Logging).</param>
    /// <param name="effectiveWorkingDirectory">Effektives Arbeitsverzeichnis des Prozesses.</param>
    /// <param name="pluginCommand">Der später an die Sitzung zu sendende Plugin-Befehl (nur für Logging verwendet).</param>
    /// <returns>Den gestarteten <see cref="Process"/>, die zugehörige <see cref="PseudoConsoleSession"/> und das native Prozess-Handle für eine zuverlässige Exit-Code-Ermittlung (<see cref="IntPtr.Zero"/>, wenn nicht zutreffend).</returns>
    (Process Process, PseudoConsoleSession Session, IntPtr NativeProcessHandle) Start(Guid aufgabeId, string effectiveWorkingDirectory, string pluginCommand);
}
