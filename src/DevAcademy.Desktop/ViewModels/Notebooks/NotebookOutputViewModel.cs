using Avalonia.Media.Imaging;
using DevAcademy.Desktop.Localization;
using DevAcademy.Notebooks.Nbformat;
using DevAcademy.Notebooks.Text;

namespace DevAcademy.Desktop.ViewModels.Notebooks;

/// <summary>One output of a cell as shown on screen: the richest MIME type that can be rendered natively (never HTML).</summary>
public sealed class NotebookOutputViewModel
{
    public NotebookOutputViewModel(OutputPresentation p, string? streamName)
    {
        Kind = p.Kind;
        var text = Loc.Of(p.TextMessage, p.Text);
        Text = p.Kind is PresentationKind.Text or PresentationKind.Error ? "" : p.Text;
        Runs = p.Kind is PresentationKind.Text or PresentationKind.Error ? AnsiText.Parse(text) : [];
        Note = p.Note is null ? null : Loc.Of(p.NoteText, p.Note);
        IsStderr = streamName == "stderr";
        if (p.Kind == PresentationKind.Image && p.ImageBytes is { } bytes)
        {
            try { Image = new Bitmap(new MemoryStream(bytes)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
            {
                Kind = PresentationKind.Text;
                Runs = [new AnsiRun(string.IsNullOrEmpty(p.Text) ? "[image]" : p.Text, null, false)];
            }
        }
    }

    public PresentationKind Kind { get; }
    public IReadOnlyList<AnsiRun> Runs { get; }
    public string Text { get; }
    public string? Note { get; }
    public Bitmap? Image { get; }
    public bool IsStderr { get; }

    public bool IsText => Kind == PresentationKind.Text;
    public bool IsError => Kind == PresentationKind.Error;
    public bool IsRuns => Kind is PresentationKind.Text or PresentationKind.Error;
    public bool IsImage => Kind == PresentationKind.Image && Image is not null;
    public bool IsMarkdown => Kind == PresentationKind.Markdown;
    public string MarkdownText => Kind == PresentationKind.Markdown ? MarkdownMath.Prepare(Text) : "";
    public bool IsLatex => Kind == PresentationKind.Latex;
    public bool IsJson => Kind == PresentationKind.Json;
    public bool HasNote => !string.IsNullOrEmpty(Note);
    public bool IsMono => Kind is PresentationKind.Latex or PresentationKind.Json;
}
