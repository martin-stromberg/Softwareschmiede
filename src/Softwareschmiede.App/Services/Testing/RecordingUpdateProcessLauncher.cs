using System.Collections.Generic;
using System.Linq;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.Services.Testing;

/// <summary>
/// E2E-Testadapter für <see cref="IUpdateProcessLauncher"/>: protokolliert Skriptpfad,
/// Arbeitsverzeichnis, Argumente und Elevation im JSONL-Protokoll und liefert das per Szenario
/// gesteuerte Ergebnis, ohne einen echten Prozess zu starten.
/// </summary>
public sealed class RecordingUpdateProcessLauncher : IUpdateProcessLauncher
{
    private readonly UpdateE2ETestKontext _kontext;

    /// <inheritdoc cref="RecordingUpdateProcessLauncher"/>
    /// <param name="kontext">Der Laufzeitkontext der Update-E2E-Umgebung.</param>
    public RecordingUpdateProcessLauncher(UpdateE2ETestKontext kontext)
    {
        _kontext = kontext;
    }

    /// <inheritdoc/>
    public bool Start(string fileName, IEnumerable<string> arguments, string workingDirectory, bool runElevated)
    {
        var argumente = arguments.ToArray();
        var ergebnis = _kontext.LeseSzenario()?.ProzessStart?.Ergebnis ?? "Erfolg";

        _kontext.Protokoll.Schreibe(
            UpdateE2EEreignisse.UpdateProcessStartRecorded,
            new
            {
                fileName,
                arguments = argumente,
                workingDirectory,
                runElevated,
                result = ergebnis
            });

        return ergebnis switch
        {
            "Fehler" => false,
            "Ausnahme" => throw new InvalidOperationException("Kontrollierter Update-E2E-Launcherfehler."),
            _ => true
        };
    }
}
