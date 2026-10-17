using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DevAcademy.Desktop.Markdown;

/// <summary>Renders a Markdown string. LinkCommand receives the URL; SampleCommand a <see cref="CodeSampleRequest"/>.</summary>
public sealed class MarkdownViewer : ContentControl
{
    public static readonly StyledProperty<string?> MarkdownProperty = AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));
    public static readonly StyledProperty<ICommand?> LinkCommandProperty = AvaloniaProperty.Register<MarkdownViewer, ICommand?>(nameof(LinkCommand));
    public static readonly StyledProperty<ICommand?> SampleCommandProperty = AvaloniaProperty.Register<MarkdownViewer, ICommand?>(nameof(SampleCommand));

    public string? Markdown { get => GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }
    public ICommand? LinkCommand { get => GetValue(LinkCommandProperty); set => SetValue(LinkCommandProperty, value); }
    public ICommand? SampleCommand { get => GetValue(SampleCommandProperty); set => SetValue(SampleCommandProperty, value); }

    protected override Type StyleKeyOverride => typeof(ContentControl);

    static MarkdownViewer()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownViewer>((v, _) => v.Rebuild());
        LinkCommandProperty.Changed.AddClassHandler<MarkdownViewer>((v, _) => v.Rebuild());
        SampleCommandProperty.Changed.AddClassHandler<MarkdownViewer>((v, _) => v.Rebuild());
    }

    public MarkdownViewer() => ActualThemeVariantChanged += (_, _) => Rebuild();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        var options = new MarkdownRenderOptions
        {
            Theme = ResolveTheme(),
            OpenLink = url => { if (LinkCommand?.CanExecute(url) == true) LinkCommand.Execute(url); },
            OpenInEditor = SampleCommand is null ? null : s => { if (SampleCommand?.CanExecute(s) == true) SampleCommand.Execute(s); },
        };
        Content = MarkdownRenderer.Render(Markdown ?? "", options);
    }

    private MarkdownTheme ResolveTheme()
    {
        IBrush Get(string key, IBrush fallback) =>
            this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;
        var f = MarkdownTheme.Fallback;
        return new MarkdownTheme(Get("MdCodeBackground", f.CodeBackground), Get("MdBorder", f.Border), Get("MdMuted", f.Muted), Get("MdLink", f.Link),
            this.TryFindResource("MonoFont", ActualThemeVariant, out var mono) && mono is FontFamily ff ? ff : MarkdownTheme.DefaultMono);
    }
}
