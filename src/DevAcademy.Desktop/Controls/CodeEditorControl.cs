using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using Avalonia.Threading;
using DevAcademy.Desktop.Controls.Debug;
using DevAcademy.Desktop.Controls.Lsp;

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

    /// <summary>Optional: connects the editor to a language server (see <see cref="Lsp.LspTarget"/>).</summary>
    public static readonly StyledProperty<LspTarget?> LspTargetProperty = AvaloniaProperty.Register<CodeEditorControl, LspTarget?>(nameof(LspTarget));

    /// <summary>Optional: breakpoints and the paused line of this file (see <see cref="EditorDebugTarget"/>). Adds the gutter column.</summary>
    public static readonly StyledProperty<EditorDebugTarget?> DebugTargetProperty = AvaloniaProperty.Register<CodeEditorControl, EditorDebugTarget?>(nameof(DebugTarget));

    private readonly TextEditor _editor;
    private BreakpointMargin? _bpMargin;
    private readonly ExecutionLineRenderer _executionRenderer;
    private AvaloniaEdit.Document.TextDocument? _watchedDocument;
    private readonly LspPopups _popups;
    private EditorLspBinding? _lsp;
    private int _lspGeneration;
    private SearchPanel? _search;
    private TextMate.Installation? _textMate;
    private RegistryOptions? _registry;
    private bool _syncing;
    private readonly BracketMatchRenderer _brackets;

    static CodeEditorControl()
    {
        TextProperty.Changed.AddClassHandler<CodeEditorControl>((c, e) => c.PushText(e.NewValue as string));
        FileNameProperty.Changed.AddClassHandler<CodeEditorControl>((c, _) => c.UpdateGrammar());
        DocumentProperty.Changed.AddClassHandler<CodeEditorControl>((c, e) =>
        {
            if (e.NewValue is TextDocument doc) c._editor.Document = doc;
            c.RebuildLsp();
        });
        IsReadOnlyProperty.Changed.AddClassHandler<CodeEditorControl>((c, e) => c._editor.IsReadOnly = e.NewValue is true);
        LspTargetProperty.Changed.AddClassHandler<CodeEditorControl>((c, _) => c.RebuildLsp());
        DebugTargetProperty.Changed.AddClassHandler<CodeEditorControl>((c, e) => c.OnDebugTargetChanged(e.OldValue as EditorDebugTarget, e.NewValue as EditorDebugTarget));
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
        _popups = new LspPopups(_editor.TextArea.TextView);
        _brackets = new BracketMatchRenderer(_editor.TextArea);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_brackets);
        _executionRenderer = new ExecutionLineRenderer(() => DebugTarget, () => BrushFor("DebugLineBrush") ?? new SolidColorBrush(Color.FromArgb(70, 255, 213, 0)));
        _editor.TextArea.TextView.BackgroundRenderers.Add(_executionRenderer);
        _editor.TextArea.TextView.DocumentChanged += (_, _) => WatchDocument();
        var host = new Grid();
        host.Children.Add(_editor);
        foreach (var p in _popups.All) host.Children.Add(p);
        Content = host;
        ActualThemeVariantChanged += (_, _) => { ApplyTheme(); ApplyChrome(); };
    }

    public TextEditor Editor => _editor;
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    public TextDocument? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public LspPopups Popups => _popups;
    public EditorDebugTarget? DebugTarget { get => GetValue(DebugTargetProperty); set => SetValue(DebugTargetProperty, value); }
    public LspTarget? LspTarget { get => GetValue(LspTargetProperty); set => SetValue(LspTargetProperty, value); }
    /// <summary>The live language-server binding, or null when there is no target or no usable server.</summary>
    public EditorLspBinding? Lsp => _lsp;

    /// <summary>Applies the language server's formatting to the whole document (Shift+Alt+F).</summary>
    public Task<bool> FormatDocumentAsync() => _lsp?.FormatDocumentAsync() ?? Task.FromResult(false);

    private IBrush? BrushFor(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) ? v as IBrush : null;

    /// <summary>
    /// Drops the current binding at once (so edits caused by switching files are never sent to the old file) and attaches a new
    /// one on the next dispatcher turn, when sibling bindings (Text/Document) have settled.
    /// </summary>
    private void RebuildLsp()
    {
        _lsp?.Dispose();
        _lsp = null;
        var generation = ++_lspGeneration;
        if (LspTarget is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != _lspGeneration || _lsp is not null || !IsAttachedToVisualTree()) return;
            AttachLsp();
        }, DispatcherPriority.Background);
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private void AttachLsp()
    {
        var target = LspTarget;
        if (target is null || _editor.Document is null) return;
        if (target.Document is not null && !ReferenceEquals(target.Document, _editor.Document)) return;
        _lsp = EditorLspBinding.TryCreate(_editor, target, _popups, BrushFor);
        _lsp?.PublishDiagnostics();
    }

    private void OnDebugTargetChanged(EditorDebugTarget? oldTarget, EditorDebugTarget? newTarget)
    {
        if (oldTarget is not null) oldTarget.Invalidated -= OnDebugInvalidated;
        if (newTarget is not null) newTarget.Invalidated += OnDebugInvalidated;
        var margins = _editor.TextArea.LeftMargins;
        if (newTarget is null)
        {
            if (_bpMargin is not null) { margins.Remove(_bpMargin); _bpMargin = null; }
        }
        else
        {
            if (_bpMargin is null)
            {
                _bpMargin = new BreakpointMargin(() => BrushFor("DangerBrush") ?? Brushes.Red, () => BrushFor("WarnBrush") ?? Brushes.Gold);
                margins.Insert(0, _bpMargin);
            }
            _bpMargin.Target = newTarget;
        }
        WatchDocument();
        _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
    }

    private void OnDebugInvalidated()
    {
        Dispatcher.UIThread.Post(() => _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background), DispatcherPriority.Background);
    }

    /// <summary>Shifts breakpoints with edits so they stay on their statement.</summary>
    private void WatchDocument()
    {
        if (_watchedDocument is not null) _watchedDocument.Changing -= OnDocumentChanging;
        _watchedDocument = DebugTarget is null ? null : _editor.Document;
        if (_watchedDocument is not null) _watchedDocument.Changing += OnDocumentChanging;
    }

    private void OnDocumentChanging(object? sender, AvaloniaEdit.Document.DocumentChangeEventArgs e)
    {
        if (DebugTarget is not { } target || _watchedDocument is null || target.Breakpoints.Count == 0) return;
        // Programmatic text pushes (switching files, reloading from disk) and whole-document replacement keep the breakpoint lines as they are.
        if (_syncing || (e.Offset == 0 && e.RemovalLength > 0 && e.RemovalLength >= _watchedDocument.TextLength)) return;
        // Changing fires before the edit: the offset still maps to the line it applies to.
        var line = _watchedDocument.GetLineByOffset(Math.Min(e.Offset, _watchedDocument.TextLength));
        string removed = e.RemovalLength > 0 ? _watchedDocument.GetText(e.Offset, e.RemovalLength) : "";
        string inserted = e.InsertedText?.Text ?? "";
        target.ApplyEdit(line.LineNumber, CountBreaks(removed), CountBreaks(inserted), e.Offset == line.Offset && inserted.EndsWith('\n'));
    }

    private static int CountBreaks(string s)
    {
        int n = 0;
        foreach (char c in s) if (c == '\n') n++;
        return n;
    }

    /// <summary>Scrolls a 1-based line into view without taking focus (the paused line).</summary>
    public void RevealLine(int line)
    {
        var doc = _editor.Document;
        if (doc is null) return;
        _editor.ScrollToLine(Math.Clamp(line, 1, Math.Max(1, doc.LineCount)));
    }

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
    public int GoToLine(int line, int column = 1)
    {
        var doc = _editor.Document;
        var target = Math.Clamp(line, 1, Math.Max(1, doc.LineCount));
        _editor.TextArea.Caret.Line = target;
        _editor.TextArea.Caret.Column = Math.Clamp(column, 1, doc.GetLineByNumber(target).Length + 1);
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
        ApplyChrome();
        PushText(Text);
        UpdateGrammar();
        RebuildLsp();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _lsp?.Dispose();
        _lsp = null;
        _lspGeneration++;
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
            // The registry supplies grammars only; token colours come from the app's own themes (SyntaxThemes).
            _registry = new RegistryOptions(IsDark ? ThemeName.DarkPlus : ThemeName.LightPlus);
            _textMate = _editor.InstallTextMate(_registry);
            _textMate.SetTheme(SyntaxThemes.For(IsDark));
            ApplyChrome();
        }
        catch (Exception ex)
        {
            // Highlighting is cosmetic: the editor stays usable without it.
            System.Diagnostics.Debug.WriteLine($"TextMate unavailable: {ex.Message}");
            _textMate = null;
        }
    }

    /// <summary>
    /// The TextMate theme paints its own editor background; the app's tokens win so the editor sits in the page:
    /// background = Surface, current line = SurfaceSunken, gutter text = TextMuted, caret = Text, selection = Selection,
    /// matching brackets = Line. Token colours come from <see cref="SyntaxThemes"/>.
    /// </summary>
    private void ApplyChrome()
    {
        if (BrushFor("SurfaceBrush") is { } surface) _editor.Background = surface;
        if (BrushFor("TextBrush") is { } text) _editor.Foreground = text;
        if (BrushFor("TextMutedBrush") is { } muted) _editor.LineNumbersForeground = muted;
        if (BrushFor("SurfaceSunkenBrush") is { } sunken)
        {
            _editor.TextArea.TextView.CurrentLineBackground = sunken;
            _editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
        }
        if (BrushFor("TextBrush") is { } caret) _editor.TextArea.Caret.CaretBrush = caret;
        if (BrushFor("SelectionBrush") is { } selection)
        {
            _editor.TextArea.SelectionBrush = selection;
            _editor.TextArea.SelectionForeground = null;
            _editor.TextArea.SelectionBorder = null;
            _editor.TextArea.SelectionCornerRadius = 0;
        }
        _brackets.Fill = BrushFor("LineBrush");
        _brackets.Outline = BrushFor("LineStrongBrush") is { } strong ? new Pen(strong, 1) : null;
        _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
        if (this.TryFindResource("MonoFont", ActualThemeVariant, out var mono) && mono is FontFamily ff) _editor.FontFamily = ff;
    }

    private bool IsDark => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

    private void ApplyTheme()
    {
        if (_textMate is null || _registry is null) return;
        try
        {
            _textMate.SetTheme(SyntaxThemes.For(IsDark));
            ApplyChrome();
            Dispatcher.UIThread.Post(ApplyChrome, DispatcherPriority.Background);
        }
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
