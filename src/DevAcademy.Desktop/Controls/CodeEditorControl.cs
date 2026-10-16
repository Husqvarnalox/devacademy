using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace DevAcademy.Desktop.Controls;

/// <summary>AvaloniaEdit editor with TextMate highlighting chosen from FileName and the current theme variant.</summary>
public sealed class CodeEditorControl : ContentControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CodeEditorControl, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<string?> FileNameProperty = AvaloniaProperty.Register<CodeEditorControl, string?>(nameof(FileName));
    /// <summary>Optional: edit this document instead of <see cref="Text"/> (used by project tabs so undo history survives tab switches).</summary>
    public static readonly StyledProperty<TextDocument?> DocumentProperty = AvaloniaProperty.Register<CodeEditorControl, TextDocument?>(nameof(Document));
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<CodeEditorControl, bool>(nameof(IsReadOnly));

    private readonly TextEditor _editor;
    private SearchPanel? _search;
    private TextMate.Installation? _textMate;
    private RegistryOptions? _registry;
    private bool _syncing;

    static CodeEditorControl()
    {
        TextProperty.Changed.AddClassHandler<CodeEditorControl>((c, e) => c.PushText(e.NewValue as string));
        FileNameProperty.Changed.AddClassHandler<CodeEditorControl>((c, _) => c.UpdateGrammar());
        DocumentProperty.Changed.AddClassHandler<CodeEditorControl>((c, e) =>
        {
            if (e.NewValue is TextDocument doc) c._editor.Document = doc;
        });
        IsReadOnlyProperty.Changed.AddClassHandler<CodeEditorControl>((c, e) => c._editor.IsReadOnly = e.NewValue is true);
    }

    public CodeEditorControl()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            FontFamily = new FontFamily("Menlo, Consolas, 'DejaVu Sans Mono', monospace"),
            FontSize = 13,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Padding = new Thickness(4, 4, 8, 4),
            Name = "Editor",
        };
        _editor.Options.IndentationSize = 4;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.HighlightCurrentLine = true;
        _editor.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            try { SetCurrentValue(TextProperty, _editor.Text); }
            finally { _syncing = false; }
        };
        try { _search = SearchPanel.Install(_editor); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Search panel unavailable: {ex.Message}"); }
        Content = _editor;
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
    }

    public TextEditor Editor => _editor;
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    public TextDocument? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>Opens the find panel (Ctrl/Cmd+F is handled by the panel itself); with <paramref name="replace"/> the replace box is shown.</summary>
    public void OpenSearch(bool replace)
    {
        if (_search is null) return;
        _search.IsReplaceMode = replace;
        _search.Open();
        if (_editor.SelectionLength > 0 && !_editor.Text.Substring(_editor.SelectionStart, _editor.SelectionLength).Contains('\n'))
            _search.SearchPattern = _editor.Text.Substring(_editor.SelectionStart, _editor.SelectionLength);
    }

    /// <summary>Moves the caret to a 1-based line (clamped) and scrolls it into view.</summary>
    public int GoToLine(int line)
    {
        var doc = _editor.Document;
        var target = Math.Clamp(line, 1, Math.Max(1, doc.LineCount));
        _editor.TextArea.Caret.Line = target;
        _editor.TextArea.Caret.Column = 1;
        _editor.ScrollToLine(target);
        _editor.TextArea.Focus();
        return target;
    }

    public int CurrentLine => _editor.TextArea.Caret.Line;

    protected override Type StyleKeyOverride => typeof(ContentControl);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureTextMate();
        PushText(Text);
        UpdateGrammar();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _textMate?.Dispose();
        _textMate = null;
    }

    private void PushText(string? text)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            if (!string.Equals(_editor.Text, text ?? "", StringComparison.Ordinal)) _editor.Text = text ?? "";
        }
        finally { _syncing = false; }
    }

    private void EnsureTextMate()
    {
        if (_textMate is not null) return;
        try
        {
            _registry = new RegistryOptions(IsDark ? ThemeName.DarkPlus : ThemeName.LightPlus);
            _textMate = _editor.InstallTextMate(_registry);
        }
        catch (Exception ex)
        {
            // Highlighting is cosmetic: the editor stays usable without it.
            System.Diagnostics.Debug.WriteLine($"TextMate unavailable: {ex.Message}");
            _textMate = null;
        }
    }

    private bool IsDark => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

    private void ApplyTheme()
    {
        if (_textMate is null || _registry is null) return;
        try { _textMate.SetTheme(_registry.LoadTheme(IsDark ? ThemeName.DarkPlus : ThemeName.LightPlus)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"TextMate theme failed: {ex.Message}"); }
    }

    private void UpdateGrammar()
    {
        if (_textMate is null || _registry is null || string.IsNullOrEmpty(FileName)) return;
        try
        {
            var ext = Path.GetExtension(FileName);
            var language = _registry.GetLanguageByExtension(ext);
            if (language is null) { _textMate.SetGrammar(null); return; }
            _textMate.SetGrammar(_registry.GetScopeByLanguageId(language.Id));
            ApplyTheme();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"TextMate grammar failed: {ex.Message}"); }
    }
}
