using FluentAssertions;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="TerminalExecutableResolver"/>: PATHEXT-/where-Auflösung des
/// <see cref="TerminalSessionStartSpec.FileName"/> zu einer <c>CreateProcess</c>-fähigen Spec.</summary>
public sealed class TerminalExecutableResolverTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "SoftwareschmiedeTests", "TerminalExecutableResolver", Guid.NewGuid().ToString("N"));

    /// <summary>Dispose.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    /// <summary>Ein existierender absoluter .exe-Pfad bleibt unverändert und wird als Direct gemeldet.</summary>
    [Fact]
    public void Resolve_AbsoluterExePfad_BleibtDirect()
    {
        var exe = CreateFile("cli.exe");
        var spec = CreateSpec(fileName: exe, pathEnv: _tempRoot);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.Direct);
        result.ResolvedPath.Should().BeEquivalentTo(exe);
        result.NormalizedSpec.FileName.Should().BeEquivalentTo(exe);
    }

    /// <summary>Ein nackter Name findet eine .exe über den PATH aus den Spec-Umgebungsvariablen und liefert den absoluten Pfad.</summary>
    [Fact]
    public void Resolve_NackterName_FindetExeAufSpecPath_AbsoluterPfad()
    {
        var pathDir = CreateSubdir("path");
        var exe = CreateFile(Path.Combine(pathDir, "meincli.exe"));
        var spec = CreateSpec(fileName: "meincli", pathEnv: pathDir);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.Direct);
        result.ResolvedPath.Should().BeEquivalentTo(exe);
        result.NormalizedSpec.FileName.Should().BeEquivalentTo(exe);
    }

    /// <summary>Ein nackter Name, zu dem nur ein .cmd-Shim existiert, wird zu cmd.exe /d /s /c "&lt;pfad&gt;" normalisiert.</summary>
    [Fact]
    public void Resolve_NackterName_NurCmdShimVorhanden_WrapptCmdExeC()
    {
        var pathDir = CreateSubdir("path");
        var shim = CreateFile(Path.Combine(pathDir, "meincli.cmd"));
        var spec = CreateSpec(fileName: "meincli", pathEnv: pathDir);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.CmdWrapped);
        result.ResolvedPath.Should().BeEquivalentTo(shim);
        result.NormalizedSpec.FileName.Should().Be("cmd.exe");
        result.NormalizedSpec.Arguments.Should().BeEquivalentTo($"/d /s /c \"{shim}\"");
    }

    /// <summary>Ein expliziter .cmd-Pfad wird zu cmd.exe /d /s /c normalisiert, Original-Arguments werden angehängt.</summary>
    [Fact]
    public void Resolve_CmdBatPfad_WirdZuCmdExeCNormalisiert()
    {
        var shim = CreateFile("tool.cmd");
        var spec = CreateSpec(fileName: shim, arguments: "--flag wert", pathEnv: _tempRoot);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.CmdWrapped);
        result.NormalizedSpec.FileName.Should().Be("cmd.exe");
        result.NormalizedSpec.Arguments.Should().BeEquivalentTo($"/d /s /c \"{shim}\" --flag wert");
    }

    /// <summary>Ein Name mit Erweiterung wird nur literal gesucht — PATHEXT-Kandidaten greifen nicht.</summary>
    [Fact]
    public void Resolve_NameMitErweiterung_WirdNurLiteralGesucht()
    {
        var pathDir = CreateSubdir("path");
        CreateFile(Path.Combine(pathDir, "meincli.exe"));
        var spec = CreateSpec(fileName: "meincli.xyz", pathEnv: pathDir);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.NotFound);
    }

    /// <summary>Die Suche beginnt im WorkingDirectory — eine dort liegende Datei gewinnt gegen den PATH.</summary>
    [Fact]
    public void Resolve_SuchtZuerstImWorkingDirectory()
    {
        var workDir = CreateSubdir("work");
        var pathDir = CreateSubdir("path");
        var workExe = CreateFile(Path.Combine(workDir, "tool.exe"));
        CreateFile(Path.Combine(pathDir, "tool.exe"));
        var spec = new TerminalSessionStartSpec
        {
            FileName = "tool",
            WorkingDirectory = workDir,
            EnvironmentVariables = new Dictionary<string, string?> { ["PATH"] = pathDir },
        };

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.Direct);
        result.ResolvedPath.Should().BeEquivalentTo(workExe);
    }

    /// <summary>Ein nicht vorhandener Name ergibt NotFound.</summary>
    [Fact]
    public void Resolve_NichtGefunden_StatusNotFound()
    {
        var spec = CreateSpec(fileName: "gibtes-garantiert-nicht-4711.xyz", pathEnv: _tempRoot);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.NotFound);
        result.NormalizedSpec.FileName.Should().Be("gibtes-garantiert-nicht-4711.xyz");
    }

    /// <summary>Ein Treffer mit nicht ausführbarer Erweiterung (z. B. .ps1) ergibt NotExecutable.</summary>
    [Fact]
    public void Resolve_GefundenAberNichtAusfuehrbar_StatusNotExecutable()
    {
        var script = CreateFile("tool.ps1");
        var spec = CreateSpec(fileName: script, pathEnv: _tempRoot);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.NotExecutable);
        result.ResolvedPath.Should().BeEquivalentTo(script);
    }

    /// <summary>Liegt neben einem erweiterungslosen Nicht-PE-Shim (npm/git-bash-Layout) ein
    /// &lt;name&gt;.cmd, muss der Resolver das Shell-Skript überspringen und den .cmd-Shim wählen —
    /// CreateProcess würde sonst mit Win32Exception 193 scheitern.</summary>
    [Fact]
    public void Resolve_ErweiterungslosesSkriptNebenCmdShim_WrapptCmdShim()
    {
        var pathDir = CreateSubdir("path");
        var shim = Path.Combine(pathDir, "meincli");
        File.WriteAllText(shim, "#!/bin/sh\nexec node cli.js \"$@\"\n");
        var cmdShim = CreateFile(Path.Combine(pathDir, "meincli.cmd"));
        var spec = CreateSpec(fileName: "meincli", pathEnv: pathDir);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.CmdWrapped);
        result.ResolvedPath.Should().BeEquivalentTo(cmdShim);
        result.NormalizedSpec.FileName.Should().Be("cmd.exe");
    }

    /// <summary>Ein erweiterungsloser Treffer, der kein PE-Image ist und keinen PATHEXT-Ersatz hat,
    /// wird als NotExecutable gemeldet (statt als Direct in den Win32Exception-193-Fehler zu laufen).</summary>
    [Fact]
    public void Resolve_ErweiterungslosesSkriptOhneErsatz_StatusNotExecutable()
    {
        var pathDir = CreateSubdir("path");
        var shim = Path.Combine(pathDir, "meincli");
        File.WriteAllText(shim, "#!/bin/sh\n");
        var spec = CreateSpec(fileName: "meincli", pathEnv: pathDir);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.NotExecutable);
        result.ResolvedPath.Should().BeEquivalentTo(shim);
    }

    /// <summary>Ein erweiterungsloser Treffer mit PE-Magic (MZ) bleibt Direct — reale
    /// erweiterungslose Native-Images sind gültige CreateProcess-Ziele.</summary>
    [Fact]
    public void Resolve_ErweiterungslosePeDatei_BleibtDirect()
    {
        var pathDir = CreateSubdir("path");
        var exe = Path.Combine(pathDir, "meincli");
        File.WriteAllBytes(exe, [(byte)'M', (byte)'Z', 0x90, 0x00]);
        var spec = CreateSpec(fileName: "meincli", pathEnv: pathDir);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.Direct);
        result.ResolvedPath.Should().BeEquivalentTo(exe);
    }

    /// <summary>Ein leerer FileName ergibt sofort NotFound.</summary>
    [Fact]
    public void Resolve_LeereFileName_StatusNotFound()
    {
        var spec = CreateSpec(fileName: " ", pathEnv: _tempRoot);

        var result = TerminalExecutableResolver.Resolve(spec);

        result.Status.Should().Be(TerminalExecutableStatus.NotFound);
    }

    private string CreateSubdir(string name)
    {
        var dir = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string CreateFile(string relativeOrAbsolutePath)
    {
        var fullPath = Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.Combine(_tempRoot, relativeOrAbsolutePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "x");
        return fullPath;
    }

    private TerminalSessionStartSpec CreateSpec(string fileName, string? pathEnv, string arguments = "")
        => new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = _tempRoot + Path.DirectorySeparatorChar + "kein-matching",
            EnvironmentVariables = new Dictionary<string, string?> { ["PATH"] = pathEnv ?? _tempRoot },
        };
}
