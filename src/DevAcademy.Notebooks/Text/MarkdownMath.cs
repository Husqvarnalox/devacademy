using System.Text;
using System.Text.RegularExpressions;

namespace DevAcademy.Notebooks.Text;

/// <summary>
/// Math is not rendered yet. Display math (<c>$$ ... $$</c>) is turned into a fenced <c>latex</c> block so that the Markdown renderer shows
/// it as source text in a monospace box; inline <c>$...$</c> stays as ordinary text. Fenced code blocks are left alone.
/// </summary>
public static partial class MarkdownMath
{
    [GeneratedRegex(@"\$\$(.+?)\$\$", RegexOptions.Singleline)]
    private static partial Regex Display();

    public static string Prepare(string markdown)
    {
        if (!markdown.Contains("$$", StringComparison.Ordinal)) return markdown;
        var result = new StringBuilder();
        var chunk = new StringBuilder();
        var inFence = false;
        string? fenceMarker = null;

        void FlushChunk()
        {
            if (chunk.Length == 0) return;
            result.Append(Display().Replace(chunk.ToString(), m => "\n```latex\n" + m.Groups[1].Value.Trim('\n', '\r', ' ') + "\n```\n"));
            chunk.Clear();
        }

        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimStart();
            var marker = trimmed.StartsWith("```", StringComparison.Ordinal) ? "```" : trimmed.StartsWith("~~~", StringComparison.Ordinal) ? "~~~" : null;
            if (marker is not null && (!inFence || marker == fenceMarker))
            {
                if (!inFence) FlushChunk();
                inFence = !inFence;
                fenceMarker = inFence ? marker : null;
                result.Append(line).Append('\n');
            }
            else if (inFence) result.Append(line).Append('\n');
            else chunk.Append(line).Append('\n');
        }
        FlushChunk();
        if (result.Length > 0 && !markdown.EndsWith('\n')) result.Length--; // undo the newline added after the last line
        return result.ToString();
    }
}
