using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using DevAcademy.Desktop.Markdown;

namespace DevAcademy.Desktop.Tests;

public class MarkdownRendererTests
{
    private static List<Control> All(Control root) => root.GetLogicalDescendants().OfType<Control>().Prepend(root).ToList();

    private static string PlainText(TextBlock t) =>
        t.Text ?? string.Concat((t.Inlines ?? []).OfType<Run>().Select(r => r.Text));

    private static IEnumerable<Button> LinkButtons(Control root) =>
        All(root).OfType<TextBlock>().SelectMany(t => t.Inlines ?? []).OfType<InlineUIContainer>()
            .Select(i => i.Child).OfType<Button>().Where(b => b.Classes.Contains("md-link"));

    [AvaloniaFact]
    public void Renders_headings_paragraphs_lists_and_code_without_a_webview()
    {
        var root = MarkdownRenderer.Render("# Title\n\n## Sub\n\nHello **bold** and `code`.\n\n- one\n- two\n\n```python\nprint(1)\n```\n");
        var all = All(root);
        Assert.Contains(root.Classes, c => c == "md-root");
        Assert.Contains(all.OfType<TextBlock>(), t => t.Classes.Contains("md-h1") && PlainText(t) == "Title");
        Assert.Contains(all.OfType<TextBlock>(), t => t.Classes.Contains("md-h2"));
        Assert.Contains(all, c => c.Classes.Contains("md-p"));
        Assert.Contains(all, c => c.Classes.Contains("md-list"));
        var code = Assert.Single(all, c => c.Classes.Contains("md-codeblock"));
        Assert.Contains(all.OfType<TextBlock>(), t => t.Classes.Contains("md-code-text") && PlainText(t) == "print(1)");
        Assert.NotNull(code);
    }

    [AvaloniaFact]
    public void Links_invoke_the_open_callback_with_the_url()
    {
        string? opened = null;
        var root = MarkdownRenderer.Render("See [the docs](https://docs.python.org/3/) now.", new MarkdownRenderOptions { OpenLink = u => opened = u });
        var button = Assert.Single(LinkButtons(root));
        Assert.Equal("https://docs.python.org/3/", button.Tag);
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("https://docs.python.org/3/", opened);
    }

    [AvaloniaFact]
    public void Run_blocks_get_an_open_in_editor_button_and_plain_blocks_do_not()
    {
        CodeSampleRequest? got = null;
        var opts = new MarkdownRenderOptions { OpenInEditor = r => got = r };
        var root = MarkdownRenderer.Render("```python run\nprint('hi')\n```\n\n```python\nx = 1\n```\n", opts);
        var button = Assert.Single(All(root).OfType<Button>(), b => b.Classes.Contains("md-open-in-editor"));
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new CodeSampleRequest("python", "print('hi')"), got);
    }

    [AvaloniaFact]
    public void Output_blocks_are_marked_and_tables_become_grids()
    {
        var root = MarkdownRenderer.Render("```output\nhi\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n");
        var all = All(root);
        Assert.Contains(all, c => c.Classes.Contains("md-output"));
        Assert.Contains(all.OfType<Grid>(), g => g.Classes.Contains("md-table"));
    }

    [AvaloniaFact]
    public void Section_comments_become_localized_headings_and_are_not_shown_raw()
    {
        var root = MarkdownRenderer.Render("<!-- section: simple -->\n\nText\n");
        var all = All(root);
        var title = Assert.Single(all.OfType<TextBlock>(), t => t.Classes.Contains("md-section"));
        Assert.Equal("Просто", PlainText(title));
        Assert.DoesNotContain(all.OfType<TextBlock>(), t => t.Text?.Contains("section:") == true);
    }

    [AvaloniaFact]
    public void Inline_links_fit_the_body_line_so_wrapped_text_is_not_clipped()
    {
        // Go lesson 01 summary: the link wraps onto the last line, and a taller link pushed that line's text out of the paragraph.
        var root = MarkdownRenderer.Render(
            "Это основа: любая следующая программа в курсе устроена так же, меняется только содержимое `main`. " +
            "В следующем уроке вы научитесь читать сообщения компилятора и исправлять ошибки: [Как читать сообщения об ошибках](lesson:go.first-steps.compiler-errors).");
        var window = new Window { Content = root, Width = 760, Height = 400 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var paragraph = Assert.Single(All(root).OfType<SelectableTextBlock>(), t => t.Classes.Contains("md-p"));
        var button = Assert.Single(LinkButtons(root));
        Assert.True(button.Bounds.Height <= paragraph.LineHeight, $"link is {button.Bounds.Height}px tall, line is {paragraph.LineHeight}px");
        Assert.Equal(FontWeight.Normal, ((TextBlock)button.Content!).FontWeight);
        Assert.True(TextBlock.GetBaselineOffset(button) > 0, "link is not aligned to the text baseline");
        window.Close();
    }

    [AvaloniaFact]
    public void Width_is_capped_for_readable_line_length()
    {
        var root = MarkdownRenderer.Render("text");
        Assert.True(root.MaxWidth is > 600 and <= 800);
    }
}
