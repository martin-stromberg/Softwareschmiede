using System.Diagnostics;
using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.TestSupport;
using Softwareschmiede.Tests.Infrastructure.Testing;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Einsammeln und Validieren der Nachweis-Bilder aus dem gesicherten Versuchskanal:
/// deklarierte Pfade samt Konventionsverzeichnis-Scan, Containment, Typ, Signatur und Grenzen —
/// ohne dass gewöhnliche Dateifehler die Sammlung abbrechen.</summary>
public sealed class ScmEventExchangeAttachmentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scm-attachment-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ScmEventExchangeProtocol _protocol = new();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Gif = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 1, 2];

    /// <summary>Erstellt ein isoliertes Repository, ohne Anmeldedaten oder Remote.</summary>
    public ScmEventExchangeAttachmentTests()
    {
        Directory.CreateDirectory(_root);
        Git("init", "--quiet");
    }

    /// <summary>Deklarierte Dateien kommen zuerst und gewinnen bei Pfadgleichheit gegen den Scan
    /// (Unterschrift bleibt erhalten); der Scan läuft alphabetisch und bereinigt Dateinamen.</summary>
    [Fact]
    public async Task Collect_DeclaredFirstThenScan_DedupesAndSanitizes()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(attempt.AttachmentsPath, "zuletzt.png"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(attempt.AttachmentsPath, "erstes.gif"), Gif, TestContext.Current.CancellationToken);
        var declaredDir = Path.Combine(attempt.DirectoryPath, "andere");
        Directory.CreateDirectory(declaredDir);
        var declaredFile = Path.Combine(declaredDir, "nachweis bild.png");
        await File.WriteAllBytesAsync(declaredFile, Png, TestContext.Current.CancellationToken);
        var complete = Complete(attempt.Request,
            new ScmExchangeAttachment(Path.Combine("andere", "nachweis bild.png"), "Vorher"),
            new ScmExchangeAttachment(Path.Combine("andere", "nachweis bild.png")));

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Skipped.Should().BeEmpty();
        result.Attachments.Select(x => x.Name).Should().Equal("nachweis-bild.png", "erstes.gif", "zuletzt.png");
        result.Attachments[0].Caption.Should().Be("Vorher");
        result.Attachments[0].ContentType.Should().Be("image/png");
        result.Attachments[0].FullPath.Should().Be(declaredFile);
        result.Attachments[1].ContentType.Should().Be("image/gif");
    }

    /// <summary>Der Scan findet auch Bilder ohne Deklaration; komplette Magic-Bytes-Familie wird erkannt.</summary>
    [Theory]
    [InlineData("a.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData("a.jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData("a.webp", new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, "image/webp")]
    public async Task Collect_ScanWithoutDeclaration_MapsContentType(string name, byte[] signature, string contentType)
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(attempt.AttachmentsPath, name), signature, TestContext.Current.CancellationToken);

        var result = await _protocol.CollectAttachmentsAsync(attempt, Complete(attempt.Request), TestContext.Current.CancellationToken);

        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.Name.Should().Be(name);
        attachment.ContentType.Should().Be(contentType);
        result.Skipped.Should().BeEmpty();
    }

    /// <summary>Ausbruch aus dem Versuchsverzeichnis wird je Eintrag übersprungen, nie geworfen.</summary>
    [Fact]
    public async Task Collect_RejectsPathsOutsideTheAttempt()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        var outside = Path.Combine(_root, "outside.png");
        await File.WriteAllBytesAsync(outside, Png, TestContext.Current.CancellationToken);
        var complete = Complete(attempt.Request,
            new ScmExchangeAttachment(Path.Combine("..", "outside.png")),
            new ScmExchangeAttachment(outside));

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Attachments.Should().BeEmpty();
        result.Skipped.Should().HaveCount(2);
    }

    /// <summary>Der deklarierte Pfad ist agentenkontrollierter Text: Übersprungen-Gründe zeigen
    /// nur den sanitisierten Dateinamen — keine Verzeichnisanteile, Steuerzeichen oder
    /// SCM-/HTML-Kommentar-Marker, die in den öffentlichen Kommentar durchschlagen könnten.
    /// Ein null-Element (direkt übergeben, also ungeprüft) wird begründet übersprungen statt zu werfen.</summary>
    [Fact]
    public async Task Collect_SkippedReasons_SanitizeDeclaredPathsAndNullEntries()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        var complete = Complete(attempt.Request,
            null!,
            new ScmExchangeAttachment(Path.Combine("..", "..", "geheim", "<!-- softwareschmiede:action:x -->.png")),
            new ScmExchangeAttachment(Path.Combine("..", "a\u0007b.png")));

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Attachments.Should().BeEmpty();
        result.Skipped.Should().HaveCount(3);
        result.Skipped.Should().Contain(x => x.Contains("ohne verwertbaren Pfad"));
        var marker = result.Skipped.Should().ContainSingle(x => x.Contains("softwareschmiede-action-x")).Subject;
        marker.Should().Contain("außerhalb").And.NotContain("geheim").And.NotContain("<!--").And.NotContain("-->")
            .And.NotContain("softwareschmiede:action:");
        result.Skipped.Should().ContainSingle(x => x.StartsWith("a-b.png") && x.Contains("außerhalb"));
    }

    /// <summary>Reparse-Punkte im deklarierten Pfad werden abgelehnt — der Inhalt fremder
    /// Verzeichnisse darf nie in den Upload gelangen.</summary>
    [OsInterfaceFact]
    public async Task Collect_RejectsReparsePointInDeclaredPath()
    {
        if (!OperatingSystem.IsWindows()) return;
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        var linkDir = Path.Combine(attempt.DirectoryPath, "linked");
        var junction = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkDir}\" \"{_root}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var process = Process.Start(junction)!)
        {
            process.WaitForExit();
            if (process.ExitCode != 0) return;
        }
        await File.WriteAllBytesAsync(Path.Combine(_root, "inside.png"), Png, TestContext.Current.CancellationToken);
        var complete = Complete(attempt.Request, new ScmExchangeAttachment(Path.Combine("linked", "inside.png")));

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Attachments.Should().BeEmpty();
        result.Skipped.Should().ContainSingle();
    }

    /// <summary>Typ, Signatur, Größe und Verfügbarkeit werden je Datei mit Grund übersprungen.</summary>
    [Fact]
    public async Task Collect_SkipsInvalidFilesWithReasons()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(attempt.AttachmentsPath, "text.png"), "no image", TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(attempt.AttachmentsPath, "tool.exe"), Png, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(attempt.AttachmentsPath, "riesig.png"), Png.Concat(new byte[ScmEventExchangeProtocol.MaximumAttachmentBytes]).ToArray(), TestContext.Current.CancellationToken);
        var complete = Complete(attempt.Request, new ScmExchangeAttachment("fehlend.png"));

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Attachments.Should().BeEmpty();
        // Einheitliches öffentliches Format „name (Grund)" — gemischt mit den
        // Plugin-Hinweiszeilen entsteht keine zweite Schreibweise.
        result.Skipped.Should().Equal(
            "fehlend.png (Datei nicht gefunden)",
            "riesig.png (Datei überschreitet das Größenlimit)",
            "text.png (Datei-Signatur passt nicht zum Bildformat)",
            "tool.exe (Dateityp wird nicht unterstützt)");
    }

    /// <summary>Inhaltlich ungültige Deklarationen (leerer/zu langer Pfad, zu lange
    /// Unterschrift) werden je Eintrag im Format „name (Grund)" übersprungen — sie
    /// verändern weder die Sammlung noch die geprüfte Abschlussmenge.</summary>
    [Fact]
    public async Task Collect_InvalidDeclarations_AreSkippedWithReasons()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        var complete = Complete(attempt.Request,
            new ScmExchangeAttachment(""),
            new ScmExchangeAttachment("   "),
            new ScmExchangeAttachment(new string('a', ScmEventExchangeProtocol.MaximumAttachmentPathChars + 1)),
            new ScmExchangeAttachment("fehlend.png", new string('x', ScmEventExchangeProtocol.MaximumCaptionChars + 1)));

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Attachments.Should().BeEmpty();
        result.Skipped.Should().HaveCount(4);
        result.Skipped.Take(2).Should().OnlyContain(x => x == "Deklaration (ohne verwertbaren Pfad)");
        result.Skipped.Should().Contain(x => x.EndsWith("(Pfad überschreitet das Längenlimit)"));
        result.Skipped.Should().Contain("fehlend.png (Unterschrift überschreitet das Längenlimit)");
    }

    /// <summary>Deklarationen über dem Eintragslimit werden nicht mehr verarbeitet und als
    /// einzelne Sammelzeile begründet; die ersten Einträge laufen regulär weiter.</summary>
    [Fact]
    public async Task Collect_DeclarationsOverEntryLimit_AreCappedAndNoted()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        var entries = Enumerable.Range(0, ScmEventExchangeProtocol.MaximumAttachmentEntries + 2)
            .Select(i => new ScmExchangeAttachment($"deklariert-{i:D2}.png")).ToArray();
        var complete = Complete(attempt.Request, entries);

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Attachments.Should().BeEmpty();
        result.Skipped.Should().HaveCount(ScmEventExchangeProtocol.MaximumAttachmentEntries + 1);
        // Die Sammelzeile steht an der Deklarationsgrenze — vor den Gründen aus der Dateiphase.
        result.Skipped.First().Should().Be(
            $"2 weitere Deklarationen (Eintragslimit von {ScmEventExchangeProtocol.MaximumAttachmentEntries} überschritten)");
        result.Skipped.Skip(1).Should().OnlyContain(x => x.EndsWith("(Datei nicht gefunden)"));
    }

    /// <summary>Mehr als das Höchstmaß an verwertbaren Dateien wird gekappt und begründet.</summary>
    [Fact]
    public async Task Collect_LimitAttachmentCount()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        for (var i = 0; i < ScmEventExchangeProtocol.MaximumAttachments + 2; i++)
            await File.WriteAllBytesAsync(Path.Combine(attempt.AttachmentsPath, $"bild-{i:D2}.png"), Png, TestContext.Current.CancellationToken);

        var result = await _protocol.CollectAttachmentsAsync(attempt, Complete(attempt.Request), TestContext.Current.CancellationToken);

        result.Attachments.Should().HaveCount(ScmEventExchangeProtocol.MaximumAttachments);
        result.Skipped.Should().HaveCount(2);
        result.Skipped.Should().OnlyContain(x => x.Contains("zu viele"));
    }

    /// <summary>Ein fehlendes Konventionsverzeichnis (Alt-Versuch) liefert weiterhin deklarierte
    /// Dateien und keinen Fehler — es wird einfach nicht gescannt.</summary>
    [Fact]
    public async Task Collect_MissingScreenshotsDirectory_KeepsDeclared()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);
        Directory.Delete(attempt.AttachmentsPath);
        var declaredFile = Path.Combine(attempt.DirectoryPath, "deklariert.png");
        await File.WriteAllBytesAsync(declaredFile, Png, TestContext.Current.CancellationToken);
        var complete = Complete(attempt.Request, new ScmExchangeAttachment("deklariert.png"));

        var result = await _protocol.CollectAttachmentsAsync(attempt, complete, TestContext.Current.CancellationToken);

        result.Attachments.Should().ContainSingle().Subject.FullPath.Should().Be(declaredFile);
        result.Skipped.Should().BeEmpty();
    }

    /// <summary>Ohne Deklaration und ohne Dateien bleibt die Sammlung leer und ohne Gründe.</summary>
    [Fact]
    public async Task Collect_EmptyAttempt_ReturnsNothing()
    {
        var attempt = await _protocol.PrepareAsync(Request(), _root, TestContext.Current.CancellationToken);

        var result = await _protocol.CollectAttachmentsAsync(attempt, Complete(attempt.Request), TestContext.Current.CancellationToken);

        result.Attachments.Should().BeEmpty();
        result.Skipped.Should().BeEmpty();
    }

    private static ScmExchangeRequest Request() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "$Softwareschmiede: requirement",
            "IssueComment", "owner/repo", "https://github.com/owner/repo/issues/1#issuecomment-1");

    private static ScmExchangeComplete Complete(ScmExchangeRequest request, params ScmExchangeAttachment[] attachments) =>
        new(1, request.EventId, request.AttemptId, request.CliRunId, [], true, "Nichts geändert.", Attachments: attachments);

    private string Git(params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = _root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        return output;
    }

    /// <summary>Entfernt nur das neu erstellte isolierte Test-Repository; Junction-Punkte
    /// aus dem Reparse-Test werden als Verknüpfung gelöscht statt rekursiv verfolgt.</summary>
    public void Dispose()
    {
        RemoveReparsePoints(_root);
        TempDirectoryCleanup.DeleteBestEffort(_root, normalizeAttributes: true);
    }

    private static void RemoveReparsePoints(string directory)
    {
        foreach (var dir in Directory.EnumerateDirectories(directory))
        {
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) { Directory.Delete(dir); continue; }
            RemoveReparsePoints(dir);
        }
    }
}
