using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Infrastructure.Terminal;

/// <summary>Auflösungsergebnis von <see cref="TerminalExecutableResolver.Resolve"/>.</summary>
/// <param name="NormalizedSpec">Die für <c>CreateProcess</c>/Pipe-Start normalisierte Spec
/// (bei <see cref="TerminalExecutableStatus.CmdWrapped"/> auf <c>cmd.exe /d /s /c</c> umgebaut).</param>
/// <param name="Status">Status der Auflösung.</param>
/// <param name="ResolvedPath">Der gefundene absolute Pfad der Executable, oder <c>null</c>.</param>
/// <param name="Detail">Diagnosehinweis (insbesondere bei Fehlschlag), oder <c>null</c>.</param>
public sealed record TerminalExecutableResolution(
    TerminalSessionStartSpec NormalizedSpec,
    TerminalExecutableStatus Status,
    string? ResolvedPath,
    string? Detail);

/// <summary>Status der Executable-Auflösung.</summary>
public enum TerminalExecutableStatus
{
    /// <summary>Executable direkt startbar (aufgelöster <c>.exe</c>- oder endungsloser Pfad).</summary>
    Direct,

    /// <summary>Das Ziel ist ein <c>.cmd</c>/<c>.bat</c>-Shim und wurde zu <c>cmd.exe /d /s /c</c> normalisiert.</summary>
    CmdWrapped,

    /// <summary>Kein Treffer in <c>WorkingDirectory</c> oder <c>PATH</c> (inkl. PATHEXT-Erweiterungen).</summary>
    NotFound,

    /// <summary>Treffer gefunden, aber nicht per <c>CreateProcess</c> ausführbar (z. B. <c>.ps1</c>).</summary>
    NotExecutable
}

/// <summary>Löst <see cref="TerminalSessionStartSpec.FileName"/> mit <c>where</c>-/PATHEXT-Semantik zu einer
/// <c>CreateProcess</c>-fähigen, normalisierten Spec auf. <c>CreateProcess</c> kann nur <c>.exe</c>-Images
/// laden und sucht bei erweiterungslosen Namen ohne PATHEXT — <c>.cmd</c>/<c>.bat</c>-Ziele werden daher zu
/// <c>cmd.exe /d /s /c</c> normalisiert, andere Treffer (z. B. <c>.ps1</c>) als <see cref="TerminalExecutableStatus.NotExecutable"/>
/// gemeldet.</summary>
internal static class TerminalExecutableResolver
{
    private static readonly HashSet<string> DirectExtensions = new(StringComparer.OrdinalIgnoreCase) { ".exe", string.Empty };
    private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cmd", ".bat" };

    /// <summary>Löst <paramref name="spec"/>.<see cref="TerminalSessionStartSpec.FileName"/> auf und liefert die
    /// normalisierte Spec samt Status.</summary>
    /// <param name="spec">Die vom Plugin gelieferte Startbeschreibung.</param>
    /// <returns>Das Auflösungsergebnis; bei <see cref="TerminalExecutableStatus.Direct"/>/
    /// <see cref="TerminalExecutableStatus.CmdWrapped"/> enthält <c>NormalizedSpec.FileName</c> den
    /// <c>CreateProcess</c>-fähigen Wert (absoluter Pfad bzw. <c>cmd.exe</c>-Wrap).</returns>
    internal static TerminalExecutableResolution Resolve(TerminalSessionStartSpec spec)
    {
        var fileName = spec.FileName?.Trim() ?? string.Empty;
        if (fileName.Length == 0)
            return new TerminalExecutableResolution(spec, TerminalExecutableStatus.NotFound, null, "FileName ist leer.");

        var pathValue = GetEnvironmentVariable(spec, "PATH");
        var pathExtValue = GetEnvironmentVariable(spec, "PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";

        var hasDirectoryPart = fileName.IndexOfAny(['\\', '/']) >= 0 || fileName.Contains(':');
        string baseName;
        List<string> searchDirectories;
        if (hasDirectoryPart)
        {
            var directoryPart = Path.GetDirectoryName(fileName) ?? string.Empty;
            var searchDirectory = Path.IsPathRooted(directoryPart)
                ? directoryPart
                : Path.GetFullPath(Path.Combine(spec.WorkingDirectory ?? string.Empty, directoryPart));
            searchDirectories = [searchDirectory];
            baseName = Path.GetFileName(fileName);
        }
        else
        {
            searchDirectories = [];
            if (!string.IsNullOrWhiteSpace(spec.WorkingDirectory))
                searchDirectories.Add(spec.WorkingDirectory);
            foreach (var entry in (pathValue ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                searchDirectories.Add(entry);
            baseName = fileName;
        }

        var candidates = Path.HasExtension(baseName)
            ? new[] { baseName }
            : BuildExtensionCandidates(baseName, pathExtValue);

        // Ein erweiterungsloser Treffer, der kein PE-Image ist (z. B. das POSIX-Shell-Shim, das npm
        // neben <name>.cmd ablegt), kann CreateProcess nicht laden (Win32Exception 193). Er wird daher
        // übersprungen, damit PATHEXT-Kandidaten (z. B. <name>.cmd) noch gefunden werden; bleibt er der
        // einzige Treffer, wird er als NotExecutable gemeldet statt als NotFound.
        string? nichtAusfuehrbarerTreffer = null;
        foreach (var directory in searchDirectories)
        {
            string? hit = null;
            foreach (var candidate in candidates)
            {
                var candidatePath = Path.Combine(directory, candidate);
                if (!File.Exists(candidatePath))
                    continue;

                if (!Path.HasExtension(candidatePath) && !IsPeImage(candidatePath))
                {
                    nichtAusfuehrbarerTreffer ??= candidatePath;
                    continue;
                }

                hit = candidatePath;
                break;
            }

            if (hit is null)
                continue;

            return CreateResolution(spec, hit);
        }

        if (nichtAusfuehrbarerTreffer is not null)
        {
            return new TerminalExecutableResolution(
                spec,
                TerminalExecutableStatus.NotExecutable,
                nichtAusfuehrbarerTreffer,
                $"'{nichtAusfuehrbarerTreffer}' ist ein Skript ohne Erweiterung (kein PE-Image) und für CreateProcess nicht ausführbar.");
        }

        return new TerminalExecutableResolution(
            spec,
            TerminalExecutableStatus.NotFound,
            null,
            $"'{fileName}' wurde weder im Arbeitsverzeichnis noch im PATH gefunden.");
    }

    /// <summary>Prüft anhand des PE-Magic (<c>MZ</c>), ob eine erweiterungslose Datei ein natives
    /// Windows-Image ist. Skript-Shims (npm/git-bash) beginnen typischerweise mit <c>#!/bin/sh</c>.</summary>
    /// <param name="path">Der zu prüfende Dateipfad.</param>
    /// <returns><c>true</c>, wenn die Datei mit <c>MZ</c> beginnt; andernfalls <c>false</c>.</returns>
    private static bool IsPeImage(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die Datei existiert (File.Exists war true), ist aber nicht lesbar — als
            // "kein PE-Image" werten; alle anderen Fehler sollen nicht verschluckt werden.
            return false;
        }
    }

    private static string[] BuildExtensionCandidates(string baseName, string pathExtValue)
    {
        var extensions = pathExtValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidates = new string[extensions.Length + 1];
        candidates[0] = baseName;
        for (var i = 0; i < extensions.Length; i++)
            candidates[i + 1] = baseName + extensions[i];
        return candidates;
    }

    private static TerminalExecutableResolution CreateResolution(TerminalSessionStartSpec spec, string resolvedPath)
    {
        var extension = Path.GetExtension(resolvedPath);
        if (DirectExtensions.Contains(extension))
        {
            return new TerminalExecutableResolution(
                spec with { FileName = resolvedPath },
                TerminalExecutableStatus.Direct,
                resolvedPath,
                null);
        }

        if (ScriptExtensions.Contains(extension))
        {
            var arguments = $"/d /s /c \"{resolvedPath}\"";
            if (!string.IsNullOrWhiteSpace(spec.Arguments))
                arguments += " " + spec.Arguments;

            return new TerminalExecutableResolution(
                spec with { FileName = "cmd.exe", Arguments = arguments },
                TerminalExecutableStatus.CmdWrapped,
                resolvedPath,
                null);
        }

        return new TerminalExecutableResolution(
            spec,
            TerminalExecutableStatus.NotExecutable,
            resolvedPath,
            $"'{resolvedPath}' ist nicht als CreateProcess-Image ausführbar (Erweiterung '{extension}').");
    }

    private static string? GetEnvironmentVariable(TerminalSessionStartSpec spec, string key)
    {
        if (spec.EnvironmentVariables.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;
        return Environment.GetEnvironmentVariable(key);
    }
}
