using System.Text.RegularExpressions;
using DevAcademy.Domain;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DevAcademy.Content;

public static partial class TheoryParser
{
    public static readonly IReadOnlyList<string> RequiredSections = ["simple", "precise", "examples", "mistakes", "summary"];
    public static readonly IReadOnlyList<string> OptionalSections = ["facts", "deep-dive"];

    [GeneratedRegex(@"^[ \t]*<!--\s*section:\s*([A-Za-z0-9_-]+)\s*-->[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex SectionMarker();

    public static Theory Parse(string markdown)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var matches = SectionMarker().Matches(markdown);
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
            sections.TryAdd(matches[i].Groups[1].Value, markdown[start..end].Trim());
        }
        return new Theory(markdown, sections, ExtractSamples(markdown));
    }

    private static List<CodeSample> ExtractSamples(string markdown)
    {
        var doc = Markdown.Parse(markdown);
        var samples = new List<CodeSample>();
        for (var i = 0; i < doc.Count; i++)
        {
            if (doc[i] is not FencedCodeBlock block) continue;
            // Markdig splits the info string into Info (first word) and Arguments (the rest).
            if (string.IsNullOrEmpty(block.Info) || block.Arguments?.Trim() != "run") continue;
            string? expected = null;
            if (i + 1 < doc.Count && doc[i + 1] is FencedCodeBlock { Info: "output" } output)
                expected = output.Lines.ToString();
            samples.Add(new CodeSample(block.Info, block.Lines.ToString(), expected));
        }
        return samples;
    }

    public static readonly IReadOnlyList<string> SortingAlgorithms = ["bubble", "insertion", "selection", "merge", "quick"];
    public static readonly IReadOnlyList<string> GradientDescentFunctions = ["quadratic", "rosenbrock", "double-well"];
    public static readonly IReadOnlyList<string> GradientDescentOptimizers = ["gd", "momentum", "adam"];

    /// <summary>
    /// Fenced <c>```visualize sorting [algorithm=merge]</c> and <c>```visualize gradient-descent [function=..] [optimizer=..] [lr=..]</c>
    /// blocks (interactive visualisers in the app). Error is non-null when the directive is malformed; the validator reports it.
    /// </summary>
    public static IReadOnlyList<(string Info, string? Error)> Visualizations(string markdown)
    {
        var result = new List<(string, string?)>();
        foreach (var block in Markdown.Parse(markdown).Descendants<FencedCodeBlock>())
        {
            if (block.Info != "visualize") continue;
            var tokens = (block.Arguments ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var info = "visualize " + string.Join(' ', tokens);
            string? error = null;
            if (tokens.Length == 0 || tokens[0] is not ("sorting" or "gradient-descent"))
            {
                result.Add((info, "unknown visualization (supported: 'sorting', 'gradient-descent')"));
                continue;
            }
            foreach (var t in tokens.Skip(1))
            {
                var kv = t.Split('=', 2);
                if (tokens[0] == "sorting")
                {
                    if (kv[0] != "algorithm" || kv.Length != 2) error = $"unknown argument '{t}' (expected algorithm=<{string.Join('|', SortingAlgorithms)}>)";
                    else if (!SortingAlgorithms.Contains(kv[1])) error = $"unknown sorting algorithm '{kv[1]}' (expected one of: {string.Join(", ", SortingAlgorithms)})";
                }
                else if (kv.Length != 2 || kv[0] is not ("function" or "optimizer" or "lr"))
                    error = $"unknown argument '{t}' (expected function=<{string.Join('|', GradientDescentFunctions)}> optimizer=<{string.Join('|', GradientDescentOptimizers)}> lr=<number>)";
                else if (kv[0] == "function" && !GradientDescentFunctions.Contains(kv[1]))
                    error = $"unknown function '{kv[1]}' (expected one of: {string.Join(", ", GradientDescentFunctions)})";
                else if (kv[0] == "optimizer" && !GradientDescentOptimizers.Contains(kv[1]))
                    error = $"unknown optimizer '{kv[1]}' (expected one of: {string.Join(", ", GradientDescentOptimizers)})";
                else if (kv[0] == "lr" && !(double.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lr)
                                           && double.IsFinite(lr) && lr > 0))
                    error = $"lr must be a positive number, got '{kv[1]}'";
            }
            result.Add((info, error));
        }
        return result;
    }

    /// <summary>Targets of all `lesson:&lt;id&gt;` links in the Markdown.</summary>
    public static IEnumerable<string> LessonLinks(string markdown) =>
        Markdown.Parse(markdown).Descendants<LinkInline>()
            .Select(l => l.Url ?? "")
            .Where(u => u.StartsWith("lesson:", StringComparison.Ordinal))
            .Select(u => u["lesson:".Length..].Split('#')[0]);
}
