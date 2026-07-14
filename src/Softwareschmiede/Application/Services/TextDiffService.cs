using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Baut aus altem und neuem Dateiinhalt einen zeilenweisen Präsentations-Diff.</summary>
public sealed class TextDiffService : ITextDiffService
{
    /// <inheritdoc/>
    public FileTextDiff BuildDiff(string? originalContent, string? currentContent)
    {
        var oldLines = SplitLines(originalContent);
        var newLines = SplitLines(currentContent);
        var operations = ComputeLineOperations(oldLines, newLines);

        var lines = new List<TextDiffLine>();
        var addedCount = 0;
        var removedCount = 0;
        var modifiedCount = 0;
        var oldLineNumber = 0;
        var newLineNumber = 0;

        var index = 0;
        while (index < operations.Count)
        {
            var operation = operations[index];
            if (operation.Kind == LineOperationKind.Equal)
            {
                oldLineNumber++;
                newLineNumber++;
                lines.Add(new TextDiffLine(operation.OldText!, DiffLineStatus.Context, oldLineNumber, newLineNumber, SingleSegment(operation.OldText!)));
                index++;
                continue;
            }

            var deletes = new List<string>();
            var inserts = new List<string>();
            while (index < operations.Count && operations[index].Kind != LineOperationKind.Equal)
            {
                if (operations[index].Kind == LineOperationKind.Delete)
                    deletes.Add(operations[index].OldText!);
                else
                    inserts.Add(operations[index].NewText!);
                index++;
            }

            var pairCount = Math.Min(deletes.Count, inserts.Count);
            for (var i = 0; i < pairCount; i++)
            {
                oldLineNumber++;
                newLineNumber++;
                modifiedCount++;
                lines.Add(BuildModifiedLine(deletes[i], inserts[i], oldLineNumber, newLineNumber));
            }

            for (var i = pairCount; i < deletes.Count; i++)
            {
                oldLineNumber++;
                removedCount++;
                lines.Add(new TextDiffLine(deletes[i], DiffLineStatus.Removed, oldLineNumber, null, SingleSegment(deletes[i])));
            }

            for (var i = pairCount; i < inserts.Count; i++)
            {
                newLineNumber++;
                addedCount++;
                lines.Add(new TextDiffLine(inserts[i], DiffLineStatus.Added, null, newLineNumber, SingleSegment(inserts[i])));
            }
        }

        return new FileTextDiff(lines, addedCount, removedCount, modifiedCount);
    }

    private static string[] SplitLines(string? content)
        => string.IsNullOrEmpty(content) ? [] : content.Replace("\r\n", "\n").Split('\n');

    private static List<LineOperation> ComputeLineOperations(string[] oldLines, string[] newLines)
    {
        var n = oldLines.Length;
        var m = newLines.Length;
        var lcsLengths = new int[n + 1, m + 1];

        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lcsLengths[i, j] = oldLines[i] == newLines[j]
                    ? lcsLengths[i + 1, j + 1] + 1
                    : Math.Max(lcsLengths[i + 1, j], lcsLengths[i, j + 1]);
            }
        }

        var operations = new List<LineOperation>();
        var x = 0;
        var y = 0;
        while (x < n && y < m)
        {
            if (oldLines[x] == newLines[y])
            {
                operations.Add(new LineOperation(LineOperationKind.Equal, oldLines[x], newLines[y]));
                x++;
                y++;
            }
            else if (lcsLengths[x + 1, y] >= lcsLengths[x, y + 1])
            {
                operations.Add(new LineOperation(LineOperationKind.Delete, oldLines[x], null));
                x++;
            }
            else
            {
                operations.Add(new LineOperation(LineOperationKind.Insert, null, newLines[y]));
                y++;
            }
        }

        while (x < n)
        {
            operations.Add(new LineOperation(LineOperationKind.Delete, oldLines[x], null));
            x++;
        }

        while (y < m)
        {
            operations.Add(new LineOperation(LineOperationKind.Insert, null, newLines[y]));
            y++;
        }

        return operations;
    }

    private static TextDiffLine BuildModifiedLine(string oldText, string newText, int oldLineNumber, int newLineNumber)
        => new(newText, DiffLineStatus.Modified, oldLineNumber, newLineNumber, BuildInlineSegments(oldText, newText));

    private static IReadOnlyList<InlineDiffSegment> BuildInlineSegments(string oldText, string newText)
    {
        var maxCommon = Math.Min(oldText.Length, newText.Length);

        var prefixLength = 0;
        while (prefixLength < maxCommon && oldText[prefixLength] == newText[prefixLength])
            prefixLength++;

        var maxSuffix = maxCommon - prefixLength;
        var suffixLength = 0;
        while (suffixLength < maxSuffix
               && oldText[oldText.Length - 1 - suffixLength] == newText[newText.Length - 1 - suffixLength])
            suffixLength++;

        var segments = new List<InlineDiffSegment>();
        if (prefixLength > 0)
            segments.Add(new InlineDiffSegment(newText[..prefixLength], false));

        var middle = newText[prefixLength..(newText.Length - suffixLength)];
        if (middle.Length > 0)
            segments.Add(new InlineDiffSegment(middle, true));

        if (suffixLength > 0)
            segments.Add(new InlineDiffSegment(newText[(newText.Length - suffixLength)..], false));

        if (segments.Count == 0)
            segments.Add(new InlineDiffSegment(string.Empty, false));

        return segments;
    }

    private static IReadOnlyList<InlineDiffSegment> SingleSegment(string text) => [new InlineDiffSegment(text, false)];

    private enum LineOperationKind
    {
        Equal,
        Delete,
        Insert
    }

    private sealed record LineOperation(LineOperationKind Kind, string? OldText, string? NewText);
}
