using Microsoft.Extensions.Logging;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Service für Diff-Algorithmus-Operationen mit vereinfachter Implementierung.
/// </summary>
public sealed class DiffAlgorithmService
{
    private readonly ILogger<DiffAlgorithmService> _logger;

    /// <inheritdoc cref="DiffAlgorithmService"/>
    public DiffAlgorithmService(ILogger<DiffAlgorithmService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generiert einen Diff zwischen zwei Text-Strings mit einem einfachen Zeilendiff-Algorithmus.
    /// </summary>
    public async Task<(List<DiffBlock> blocks, int addedLines, int removedLines, int modifiedLines)> GenerateDiffAsync(
        string sourceContent,
        string targetContent,
        Guid diffResultId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceContent))
            throw new ArgumentNullException(nameof(sourceContent));
        if (string.IsNullOrWhiteSpace(targetContent))
            throw new ArgumentNullException(nameof(targetContent));

        return await Task.Run(() =>
        {
            _logger.LogInformation("Diff-Generierung gestartet. SourceLength: {SourceLength}, TargetLength: {TargetLength}",
                sourceContent.Length, targetContent.Length);

            // Split in Zeilen
            var sourceLines = sourceContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var targetLines = targetContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Verwende einfachen Diff-Algorithmus
            var (diffs, _) = ComputeDiff(sourceLines, targetLines);

            var blocks = GroupDiffsIntoBlocks(diffs, diffResultId);
            
            var addedLines = diffs.Count(d => d.type == DiffTypeLocal.Added);
            var removedLines = diffs.Count(d => d.type == DiffTypeLocal.Removed);
            var modifiedLines = 0;

            _logger.LogInformation("Diff generiert: {BlockCount} Blöcke, +{Added}, -{Removed}, ~{Modified}",
                blocks.Count, addedLines, removedLines, modifiedLines);

            return (blocks, addedLines, removedLines, modifiedLines);
        }, ct);
    }

    /// <summary>
    /// Einfacher Diff-Algorithmus basierend auf Zeilenvergleich.
    /// </summary>
    private static (List<(DiffTypeLocal type, string line)> diffs, List<string> allLines) ComputeDiff(
        string[] sourceLines,
        string[] targetLines)
    {
        var diffs = new List<(DiffTypeLocal type, string line)>();
        var allLines = new List<string>();

        var sourceSet = new HashSet<string>(sourceLines);
        var targetSet = new HashSet<string>(targetLines);

        var sourceIdx = 0;
        var targetIdx = 0;

        while (sourceIdx < sourceLines.Length || targetIdx < targetLines.Length)
        {
            if (sourceIdx < sourceLines.Length && targetIdx < targetLines.Length && 
                sourceLines[sourceIdx] == targetLines[targetIdx])
            {
                diffs.Add((DiffTypeLocal.Context, sourceLines[sourceIdx]));
                allLines.Add(sourceLines[sourceIdx]);
                sourceIdx++;
                targetIdx++;
            }
            else if (sourceIdx < sourceLines.Length && !targetSet.Contains(sourceLines[sourceIdx]))
            {
                diffs.Add((DiffTypeLocal.Removed, sourceLines[sourceIdx]));
                allLines.Add(sourceLines[sourceIdx]);
                sourceIdx++;
            }
            else if (targetIdx < targetLines.Length && !sourceSet.Contains(targetLines[targetIdx]))
            {
                diffs.Add((DiffTypeLocal.Added, targetLines[targetIdx]));
                allLines.Add(targetLines[targetIdx]);
                targetIdx++;
            }
            else if (sourceIdx < sourceLines.Length)
            {
                sourceIdx++;
            }
            else
            {
                targetIdx++;
            }
        }

        return (diffs, allLines);
    }

    /// <summary>
    /// Gruppiert die Diff-Zeilen in Blocks.
    /// </summary>
    private static List<DiffBlock> GroupDiffsIntoBlocks(
        List<(DiffTypeLocal type, string line)> diffs,
        Guid diffResultId)
    {
        var blocks = new List<DiffBlock>();
        var currentBlockLines = new List<(DiffTypeLocal type, string line)>();
        int sourceLineNum = 1;
        int targetLineNum = 1;
        int blockSequence = 0;

        foreach (var (type, line) in diffs)
        {
            var shouldStartNewBlock = currentBlockLines.Count > 0 &&
                currentBlockLines[0].type != type &&
                currentBlockLines[0].type != DiffTypeLocal.Context &&
                type != DiffTypeLocal.Context;

            if (shouldStartNewBlock)
            {
                var block = BuildBlockFromLines(
                    currentBlockLines,
                    diffResultId,
                    ref sourceLineNum,
                    ref targetLineNum,
                    blockSequence++);

                if (block != null)
                    blocks.Add(block);

                currentBlockLines.Clear();
            }

            currentBlockLines.Add((type, line));
        }

        if (currentBlockLines.Count > 0)
        {
            var block = BuildBlockFromLines(
                currentBlockLines,
                diffResultId,
                ref sourceLineNum,
                ref targetLineNum,
                blockSequence);

            if (block != null)
                blocks.Add(block);
        }

        return blocks;
    }

    /// <summary>
    /// Erstellt einen DiffBlock aus einer Gruppe von Diff-Zeilen.
    /// </summary>
    private static DiffBlock? BuildBlockFromLines(
        List<(DiffTypeLocal type, string line)> lines,
        Guid diffResultId,
        ref int sourceLineNum,
        ref int targetLineNum,
        int blockSequence)
    {
        if (lines.Count == 0)
            return null;

        var blockType = lines[0].type switch
        {
            DiffTypeLocal.Added => DiffBlockType.Added,
            DiffTypeLocal.Removed => DiffBlockType.Removed,
            DiffTypeLocal.Modified => DiffBlockType.Modified,
            _ => DiffBlockType.Context
        };

        var diffLines = new List<DiffLine>();
        int sourceStartLine = sourceLineNum;
        int targetStartLine = targetLineNum;
        int lineSequence = 0;

        foreach (var (type, lineContent) in lines)
        {
            var lineStatus = type switch
            {
                DiffTypeLocal.Added => DiffLineStatus.Added,
                DiffTypeLocal.Removed => DiffLineStatus.Removed,
                DiffTypeLocal.Modified => DiffLineStatus.Modified,
                _ => DiffLineStatus.Context
            };

            var diffLine = new DiffLine
            {
                Id = Guid.NewGuid(),
                LineStatus = lineStatus,
                Content = lineContent,
                SourceLineNumber = type != DiffTypeLocal.Added ? (int?)sourceLineNum : null,
                TargetLineNumber = type != DiffTypeLocal.Removed ? (int?)targetLineNum : null,
                LineSequence = lineSequence++
            };

            diffLines.Add(diffLine);

            if (type != DiffTypeLocal.Added)
                sourceLineNum++;
            if (type != DiffTypeLocal.Removed)
                targetLineNum++;
        }

        var block = new DiffBlock
        {
            Id = Guid.NewGuid(),
            DiffResultId = diffResultId,
            BlockType = blockType,
            SourceStartLine = blockType != DiffBlockType.Added ? sourceStartLine : 0,
            SourceEndLine = sourceLineNum - 1,
            TargetStartLine = blockType != DiffBlockType.Removed ? targetStartLine : 0,
            TargetEndLine = targetLineNum - 1,
            BlockSequence = blockSequence,
            DiffLines = diffLines
        };

        return block;
    }
}

/// <summary>
/// Lokale Enum für Diff-Typen während der Berechnung.
/// </summary>
internal enum DiffTypeLocal
{
    Added,
    Removed,
    Modified,
    Context
}
