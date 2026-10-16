using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using DevAcademy.Desktop.Markdown;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.Protocol;

namespace DevAcademy.Desktop.Controls.Lsp;

/// <summary>One server completion item as AvaloniaEdit shows it. Applying it replaces the typed word (plus the server's extra edits) in one undo step.</summary>
public sealed partial class LspCompletionData : ICompletionData
{
    private readonly CompletionItem _item;
    private readonly LspDocument _doc;
    private readonly EditorLspBinding _binding;
    private ContentControl? _description;

    public LspCompletionData(CompletionItem item, LspDocument doc, EditorLspBinding binding, double priority)
    {
        _item = item;
        _doc = doc;
        _binding = binding;
        Priority = priority;
        Text = string.IsNullOrEmpty(item.FilterText) ? item.Label.Trim() : item.FilterText!;
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        label.Children.Add(new TextBlock { Text = item.Label.Trim(), FontFamily = new FontFamily("Menlo, Consolas, 'DejaVu Sans Mono', monospace"), FontSize = 12.5 });
        if (!string.IsNullOrEmpty(item.Detail))
            label.Children.Add(new TextBlock { Text = Truncate(item.Detail!, 48), Opacity = 0.6, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        Content = label;
    }

    public IImage? Image => null;
    public string Text { get; }
    public object Content { get; }
    public double Priority { get; }

    public object? Description
    {
        get
        {
            if (_description is not null) return _description;
            var (text, md) = LspText.Flatten(_item.Documentation);
            var canResolve = _doc.Capabilities.CompletionResolve;
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrEmpty(_item.Detail) && !canResolve) return null;
            _description = new ContentControl { MaxWidth = 460 };
            Fill(_item.Detail, text, md);
            if (string.IsNullOrWhiteSpace(text) && canResolve) _ = ResolveAsync();
            return _description;
        }
    }

    private async Task ResolveAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var resolved = await _doc.ResolveAsync(_item, cts.Token);
            if (resolved is null) return;
            var (text, md) = LspText.Flatten(resolved.Documentation);
            Fill(resolved.Detail ?? _item.Detail, text, md);
        }
        catch (Exception) { /* documentation is a nicety */ }
    }

    private void Fill(string? detail, string text, bool markdown)
    {
        var panel = new StackPanel { Spacing = 6 };
        if (!string.IsNullOrEmpty(detail)) panel.Children.Add(LspPopups.Plain(detail!, mono: true));
        if (!string.IsNullOrWhiteSpace(text))
            panel.Children.Add(markdown ? new MarkdownViewer { Markdown = text } : LspPopups.Plain(text));
        _description!.Content = panel;
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var doc = textArea.Document;
        var edits = new List<(int Start, int End, string Text, bool Main)>();
        var mainText = StripSnippet(_item);
        var start = completionSegment.Offset;
        var end = completionSegment.EndOffset;
        if (_item.PrimaryEdit() is { } primary)
        {
            // The server's range was relative to where the request was made; keep whatever was typed since then.
            var s = _binding.ToOffset(primary.Range.Start);
            if (s is { } so && so <= start && so >= doc.GetLineByOffset(start).Offset) start = so;
            mainText = _item.IsSnippet ? StripSnippetText(primary.NewText) : primary.NewText;
        }
        edits.Add((start, end, mainText, true));
        foreach (var e in _item.AdditionalTextEdits ?? [])
        {
            if (_binding.ToOffset(e.Range.Start) is not { } s || _binding.ToOffset(e.Range.End) is not { } t) continue;
            if (t > start && s < end) continue; // overlaps the word being completed
            edits.Add((s, t, e.NewText, false));
        }
        var caret = start + mainText.Length;
        using (doc.RunUpdate())
        {
            foreach (var e in edits.OrderByDescending(x => x.Start))
            {
                doc.Replace(e.Start, e.End - e.Start, e.Text);
                if (!e.Main && e.Start <= start) caret += e.Text.Length - (e.End - e.Start);
            }
        }
        textArea.Caret.Offset = Math.Clamp(caret, 0, doc.TextLength);
    }

    private static string StripSnippet(CompletionItem item) => item.IsSnippet ? StripSnippetText(item.TextToInsert) : item.TextToInsert;

    /// <summary>Servers are told we do not support snippets, but some answer with them anyway: keep the placeholder defaults, drop the tab stops.</summary>
    public static string StripSnippetText(string s)
    {
        s = PlaceholderRegex().Replace(s, "$1");
        s = TabStopRegex().Replace(s, "");
        return s.Replace("\\$", "$").Replace("\\}", "}");
    }

    [GeneratedRegex(@"\$\{\d+:([^}]*)\}")] private static partial Regex PlaceholderRegex();
    [GeneratedRegex(@"\$\{\d+\}|\$\d+")] private static partial Regex TabStopRegex();

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
