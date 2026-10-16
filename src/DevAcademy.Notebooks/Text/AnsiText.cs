using System.Text;
using System.Text.RegularExpressions;

namespace DevAcademy.Notebooks.Text;

/// <summary>A run of text with one style. <see cref="Color"/> is an ANSI colour index 0-15 (8-15 = bright), or null for the default colour.</summary>
public readonly record struct AnsiRun(string Text, int? Color, bool Bold);

/// <summary>SGR (colour/bold) parsing of terminal output, as found in tracebacks and stream outputs. Other escape sequences are dropped.</summary>
public static partial class AnsiText
{
    [GeneratedRegex("\u001b\\[([0-9;]*)([A-Za-z])|\u001b\\][^\u0007\u001b]*(?:\u0007|\u001b\\\\)|\u001b[()][A-Za-z0-9]|\u001b[=>]")]
    private static partial Regex EscapePattern();

    public static string Strip(string text) => EscapePattern().Replace(text, "");

    public static IReadOnlyList<AnsiRun> Parse(string text)
    {
        var runs = new List<AnsiRun>();
        int? color = null;
        var bold = false;
        var pos = 0;
        void Flush(int end)
        {
            if (end > pos) runs.Add(new AnsiRun(text[pos..end], color, bold));
        }
        foreach (Match m in EscapePattern().Matches(text))
        {
            Flush(m.Index);
            pos = m.Index + m.Length;
            if (!m.Groups[2].Success || m.Groups[2].Value != "m") continue;
            var codes = m.Groups[1].Value.Length == 0 ? [0] : m.Groups[1].Value.Split(';').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            for (var i = 0; i < codes.Length; i++)
            {
                var c = codes[i];
                if (c == 0) { color = null; bold = false; }
                else if (c == 1) bold = true;
                else if (c == 22) bold = false;
                else if (c is >= 30 and <= 37) color = c - 30;
                else if (c is >= 90 and <= 97) color = c - 90 + 8;
                else if (c == 39) color = null;
                else if (c is 38 or 48)
                {
                    // 38;5;n or 38;2;r;g;b: skip the arguments (extended colours are shown in the default colour).
                    if (i + 1 < codes.Length) i += codes[i + 1] == 5 ? 2 : codes[i + 1] == 2 ? 4 : 1;
                }
            }
        }
        Flush(text.Length);
        return runs;
    }

    /// <summary>Applies carriage returns the way a terminal does for progress bars: text after \r replaces the current line.</summary>
    public static string ApplyCarriageReturns(string text)
    {
        if (!text.Contains('\r')) return text;
        var sb = new StringBuilder();
        var lineStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') continue; // CRLF: the newline handles it
                sb.Length = lineStart;
            }
            else
            {
                sb.Append(ch);
                if (ch == '\n') lineStart = sb.Length;
            }
        }
        return sb.ToString();
    }
}
