using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.Localization;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DevAcademy.Desktop.Markdown;

public sealed record MarkdownTheme(IBrush CodeBackground, IBrush Border, IBrush Muted, IBrush Link, FontFamily Mono)
{
    public static FontFamily DefaultMono { get; } = new("avares://DevAcademy.Desktop/Assets/Fonts#IBM Plex Mono, Menlo, Consolas, 'DejaVu Sans Mono', monospace");

    public static MarkdownTheme Fallback { get; } = new(
        new SolidColorBrush(Color.Parse("#F2F2F0")), new SolidColorBrush(Color.Parse("#D6D6D2")),
        new SolidColorBrush(Color.Parse("#6B6B66")), new SolidColorBrush(Color.Parse("#2563A8")), DefaultMono);
}

public sealed class MarkdownRenderOptions
{
    public MarkdownTheme Theme { get; init; } = MarkdownTheme.Fallback;
    public Action<string>? OpenLink { get; init; }
    /// <summary>When set, ```lang run blocks get an "open in editor" button.</summary>
    public Action<CodeSampleRequest>? OpenInEditor { get; init; }
    public double MaxWidth { get; init; } = 680;
}

/// <summary>Markdig AST to native Avalonia controls (no WebView). Class names (md-*) are stable and used by tests and styles.</summary>
public static class MarkdownRenderer
{
    private const double BodySize = 15;
    private const double BodyLine = 25;
    private static readonly Regex SectionComment = new(@"<!--\s*section:\s*(\w+)\s*-->", RegexOptions.Compiled);
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().Build();

    public static Control Render(string markdown, MarkdownRenderOptions? options = null)
    {
        options ??= new MarkdownRenderOptions();
        var doc = Markdig.Markdown.Parse(markdown ?? "", Pipeline);
        var root = new StackPanel { Spacing = 14, MaxWidth = options.MaxWidth, HorizontalAlignment = HorizontalAlignment.Left };
        root.Classes.Add("md-root");
        foreach (var block in doc) AddBlock(root.Children, block, options);
        return root;
    }

    private static void AddBlock(Avalonia.Controls.Controls target, Block block, MarkdownRenderOptions o)
    {
        switch (block)
        {
            case HeadingBlock h:
                target.Add(Heading(h, o));
                break;
            case ParagraphBlock p:
                target.Add(Text(p.Inline, o, "md-p", BodySize));
                break;
            case FencedCodeBlock { Info: "visualize" } v:
                target.Add(Visualizer(v.Arguments ?? "", v.Lines.ToString()));
                break;
            case FencedCodeBlock f:
                target.Add(CodeBlock(((f.Info ?? "") + " " + (f.Arguments ?? "")).Trim(), f.Lines.ToString(), o));
                break;
            case CodeBlock c:
                target.Add(CodeBlock("", c.Lines.ToString(), o));
                break;
            case ListBlock l:
                target.Add(List(l, o));
                break;
            case QuoteBlock q:
                target.Add(Quote(q, o));
                break;
            case ThematicBreakBlock:
                var rule = new Border { Height = 1, Background = o.Theme.Border, Margin = new Thickness(0, 4) };
                rule.Classes.Add("md-hr");
                target.Add(rule);
                break;
            case Table t:
                target.Add(TableControl(t, o));
                break;
            case HtmlBlock html:
                var text = html.Lines.ToString();
                if (SectionComment.Match(text) is { Success: true } m)
                {
                    var title = new TextBlock { Text = Loc.Has("Section_" + m.Groups[1].Value) ? Loc.T("Section_" + m.Groups[1].Value) : m.Groups[1].Value };
                    title.Classes.Add("md-section");
                    target.Add(title);
                }
                else if (!text.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
                    target.Add(Text(null, o, "md-p", BodySize, text.Trim()));
                break;
            case ContainerBlock container:
                foreach (var child in container) AddBlock(target, child, o);
                break;
        }
    }

    private static Control Heading(HeadingBlock h, MarkdownRenderOptions o)
    {
        // Type scale: h1 22/28, h2 17/24, h3 15/22 (SemiBold); the extra top margin is the heading rhythm.
        var (size, line) = h.Level switch { 1 => (22d, 28d), 2 => (17d, 24d), _ => (15d, 22d) };
        var tb = Text(h.Inline, o, "md-h" + Math.Min(h.Level, 4), size, line: line);
        tb.FontWeight = FontWeight.SemiBold;
        tb.Margin = new Thickness(0, h.Level <= 2 ? 18 : 8, 0, 0);
        return tb;
    }

    private static SelectableTextBlock Text(ContainerInline? inline, MarkdownRenderOptions o, string cls, double size, string? plain = null, double? line = null)
    {
        var tb = new SelectableTextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap, LineHeight = line ?? BodyLine };
        tb.Classes.Add(cls);
        if (plain is not null) tb.Inlines!.Add(new Run(plain));
        else if (inline is not null) AppendInlines(tb.Inlines!, inline, o, false, false);
        return tb;
    }

    private static void AppendInlines(InlineCollection target, ContainerInline container, MarkdownRenderOptions o, bool bold, bool italic)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    target.Add(Styled(new Run(lit.Content.ToString()), bold, italic));
                    break;
                case EmphasisInline em:
                    AppendInlines(target, em, o, bold || em.DelimiterCount >= 2, italic || em.DelimiterCount == 1);
                    break;
                case CodeInline code:
                    var run = new Run(code.Content)
                    {
                        FontFamily = o.Theme.Mono,
                        FontSize = 13,
                        Background = o.Theme.CodeBackground,
                    };
                    target.Add(Styled(run, bold, italic));
                    break;
                case LineBreakInline lb:
                    target.Add(lb.IsHard ? new LineBreak() : new Run(" "));
                    break;
                case LinkInline link when link.IsImage:
                    target.Add(new Run(Loc.F("Markdown_Image", PlainText(link))) { Foreground = o.Theme.Muted });
                    break;
                case LinkInline link:
                    target.Add(LinkInlineUi(PlainText(link), link.Url ?? "", o));
                    break;
                case AutolinkInline auto:
                    target.Add(LinkInlineUi(auto.Url, auto.Url, o));
                    break;
                case HtmlInline html:
                    if (!html.Tag.StartsWith("<!--", StringComparison.Ordinal)) target.Add(new Run(html.Tag));
                    break;
                case ContainerInline nested:
                    AppendInlines(target, nested, o, bold, italic);
                    break;
            }
        }
    }

    private static Run Styled(Run r, bool bold, bool italic)
    {
        if (bold) r.FontWeight = FontWeight.SemiBold;
        if (italic) r.FontStyle = FontStyle.Italic;
        return r;
    }

    private static string PlainText(ContainerInline c)
    {
        var sb = new StringBuilder();
        foreach (var i in c)
        {
            switch (i)
            {
                case LiteralInline l: sb.Append(l.Content.ToString()); break;
                case CodeInline code: sb.Append(code.Content); break;
                case ContainerInline nested: sb.Append(PlainText(nested)); break;
            }
        }
        return sb.ToString();
    }

    private static InlineUIContainer LinkInlineUi(string text, string url, MarkdownRenderOptions o)
    {
        var label = new TextBlock { Text = text.Length == 0 ? url : text, Foreground = o.Theme.Link, TextDecorations = TextDecorations.Underline, FontSize = BodySize, FontWeight = FontWeight.Normal };
        // The theme's Button MinHeight (32) is taller than BodyLine and breaks the line the link wraps onto.
        var button = new Button
        {
            Content = label,
            MinHeight = 0,
            Padding = default,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Tag = url,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        button.Classes.Add("md-link");
        // An inline control sits on the baseline by its bottom edge; offset it by the label's own baseline so the link text lines up with the run text.
        label.SizeChanged += (_, _) =>
        {
            var baseline = label.TextLayout.TextLines.Count > 0 ? label.TextLayout.TextLines[0].Baseline : 0;
            if (baseline <= 0 || Math.Abs(TextBlock.GetBaselineOffset(button) - baseline) < 0.5) return;
            TextBlock.SetBaselineOffset(button, baseline);
            (button.GetVisualParent() as Layoutable)?.InvalidateMeasure();
        };
        ToolTip.SetTip(button, url);
        button.Click += (_, _) => o.OpenLink?.Invoke(url);
        return new InlineUIContainer(button);
    }

    /// <summary>
    /// ```visualize sorting [algorithm=bubble|insertion|selection|merge|quick]; an optional body lists the initial numbers.
    /// ```visualize gradient-descent [function=quadratic|rosenbrock|double-well] [optimizer=gd|momentum|adam] [lr=0.1].
    /// ```visualize kmeans [k=2..6] [init=random|kmeans++] [seed=N] and ```visualize pca; both take optional "x y" point lines as the body.
    /// An unknown kind renders a muted note (the content validator reports it as an error).
    /// </summary>
    private static Control Visualizer(string args, string body)
    {
        var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0 && tokens[0] == "gradient-descent")
        {
            var function = GdFunctions.QuadraticId;
            var optimizer = GdOptimizerKind.Gd;
            double? lr = null;
            foreach (var t in tokens.Skip(1))
            {
                var kv = t.Split('=', 2);
                if (kv.Length != 2) continue;
                if (kv[0] == "function" && GdFunctions.Ids.Contains(kv[1])) function = kv[1];
                else if (kv[0] == "optimizer" && GradientDescentLab.TryParseOptimizer(kv[1], out var o)) optimizer = o;
                else if (kv[0] == "lr" && double.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) && v > 0) lr = v;
            }
            var gd = new Views.GradientDescentView(new ViewModels.GradientDescentViewModel(function, optimizer, lr));
            gd.Classes.Add("md-visualizer");
            gd.Classes.Add("md-visualizer-gd");
            return gd;
        }
        if (tokens.Length > 0 && tokens[0] == "kmeans")
        {
            int k = 3, seed = KMeansLab.DefaultSeed;
            var init = KMeansInit.KMeansPlusPlus;
            foreach (var t in tokens.Skip(1))
            {
                var kv = t.Split('=', 2);
                if (kv.Length != 2) continue;
                if (kv[0] == "k" && int.TryParse(kv[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var kk) && kk is >= KMeansLab.MinK and <= KMeansLab.MaxK) k = kk;
                else if (kv[0] == "init" && KMeansLab.TryParseInit(kv[1], out var i)) init = i;
                else if (kv[0] == "seed" && int.TryParse(kv[1], System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var sd)) seed = sd;
            }
            var km = new Views.KMeansView(new ViewModels.KMeansViewModel(k, init, seed, PointData.TryParse(body, out var kpoints, out _) ? kpoints : null));
            km.Classes.Add("md-visualizer");
            km.Classes.Add("md-visualizer-kmeans");
            return km;
        }
        if (tokens.Length > 0 && tokens[0] == "pca")
        {
            var pca = new Views.PcaView(new ViewModels.PcaViewModel(PointData.TryParse(body, out var ppoints, out _) ? ppoints : null));
            pca.Classes.Add("md-visualizer");
            pca.Classes.Add("md-visualizer-pca");
            return pca;
        }
        if (tokens.Length == 0 || tokens[0] != "sorting")
        {
            var note = new TextBlock { Text = Loc.F("Viz_Unknown", args.Trim()), FontSize = 13, TextWrapping = TextWrapping.Wrap };
            note.Classes.Add("muted");
            note.Classes.Add("md-visualizer-unknown");
            return note;
        }
        var algorithm = SortAlgorithm.Bubble;
        foreach (var t in tokens.Skip(1))
        {
            var kv = t.Split('=', 2);
            if (kv.Length == 2 && kv[0] == "algorithm" && Enum.TryParse<SortAlgorithm>(kv[1], true, out var a)) algorithm = a;
        }
        IReadOnlyList<int>? input = SortInput.TryParse(body, out var parsed, out _) ? parsed : null;
        var view = new Views.SortingVisualizerView(new ViewModels.SortingVisualizerViewModel(algorithm, input));
        view.Classes.Add("md-visualizer");
        return view;
    }

    private static Control CodeBlock(string info, string code, MarkdownRenderOptions o)
    {
        var tokens = info.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lang = tokens.FirstOrDefault(t => t != "run") ?? "";
        var isRun = tokens.Contains("run");
        var isOutput = lang == "output";
        code = code.TrimEnd('\n', '\r');

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12, 4, 6, 0), MinHeight = 28 };
        var label = new TextBlock
        {
            Text = isOutput ? Loc.T("Markdown_Output") : lang,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = o.Theme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(label);
        if (isRun && tokens.Contains("posix") && OperatingSystem.IsWindows())
        {
            var note = new TextBlock { Text = Loc.T("Markdown_PosixOnly"), FontSize = 12, Foreground = o.Theme.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(note, 1);
            header.Children.Add(note);
        }
        else if (isRun && o.OpenInEditor is { } open)
        {
            var btn = new Button { Content = Loc.T("Markdown_OpenInEditor") };
            btn.Classes.Add("md-open-in-editor");
            btn.Classes.Add("ghost");
            btn.Classes.Add("compact");
            btn.Click += (_, _) => open(new CodeSampleRequest(lang, code));
            Grid.SetColumn(btn, 1);
            header.Children.Add(btn);
        }

        var text = new SelectableTextBlock { Text = code, FontFamily = o.Theme.Mono, FontSize = 13, LineHeight = 20 };
        text.Classes.Add("md-code-text");
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = text,
            Padding = new Thickness(12, 2, 12, 12),
        };
        var stack = new StackPanel { Children = { header, scroll } };
        var border = new Border
        {
            Background = o.Theme.CodeBackground,
            BorderBrush = o.Theme.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = stack,
        };
        border.Classes.Add("md-codeblock");
        if (isOutput) border.Classes.Add("md-output");
        return border;
    }

    private static Control List(ListBlock list, MarkdownRenderOptions o)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Classes.Add("md-list");
        var n = int.TryParse(list.OrderedStart, out var s) ? s : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = new TextBlock
            {
                Text = list.IsOrdered ? $"{n++}." : "•",
                FontSize = BodySize,
                LineHeight = BodyLine,
                Foreground = o.Theme.Muted,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
            };
            var content = new StackPanel { Spacing = 6 };
            foreach (var b in item) AddBlock(content.Children, b, o);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*") };
            row.Children.Add(marker);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            panel.Children.Add(row);
        }
        return panel;
    }

    private static Control Quote(QuoteBlock q, MarkdownRenderOptions o)
    {
        var inner = new StackPanel { Spacing = 8 };
        foreach (var b in q) AddBlock(inner.Children, b, o);
        // A callout: tinted surface, no side stripe (see docs/design/DESIGN_SYSTEM.md).
        var border = new Border { Child = inner };
        border.Classes.Add("md-quote");
        border.Classes.Add("callout");
        return border;
    }

    private static Control TableControl(Table table, MarkdownRenderOptions o)
    {
        var rows = table.OfType<TableRow>().ToList();
        var cols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var grid = new Grid();
        grid.Classes.Add("md-table");
        for (var c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var cells = rows[r].OfType<TableCell>().ToList();
            for (var c = 0; c < cells.Count; c++)
            {
                var inner = new StackPanel { Spacing = 4 };
                foreach (var b in cells[c]) AddBlock(inner.Children, b, o);
                foreach (var tb in inner.Children.OfType<TextBlock>())
                {
                    tb.FontSize = 14;
                    tb.LineHeight = 20;
                    if (rows[r].IsHeader) tb.FontWeight = FontWeight.SemiBold;
                }
                var cell = new Border
                {
                    BorderBrush = o.Theme.Border,
                    BorderThickness = new Thickness(c == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1),
                    Padding = new Thickness(12, 6),
                    Background = rows[r].IsHeader ? o.Theme.CodeBackground : null,
                    Child = inner,
                };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        return grid;
    }
}
