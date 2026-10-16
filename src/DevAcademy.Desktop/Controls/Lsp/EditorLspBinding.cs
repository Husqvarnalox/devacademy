using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using DevAcademy.Desktop.Markdown;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.Protocol;
using LspCompletionList = DevAcademy.LanguageServices.Protocol.CompletionList;

namespace DevAcademy.Desktop.Controls.Lsp;

/// <summary>
/// Connects one AvaloniaEdit editor showing one document to a language server: forwards edits, and turns the server's answers
/// into squiggles, a completion window, hover/signature popups, go to definition and formatting. Everything here runs on the UI thread.
/// </summary>
public sealed class EditorLspBinding : IDisposable
{
    private const int MaxCompletionItems = 2000;

    private readonly TextEditor _editor;
    private readonly TextArea _area;
    private readonly TextView _view;
    private readonly TextDocument _text;
    private readonly TextDocumentBuffer _buffer;
    private readonly LspTarget _target;
    private readonly LspDocument _doc;
    private readonly LspPopups _popups;
    private readonly DiagnosticRenderer _renderer;
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(380) };
    private CompletionWindow? _window;
    private CancellationTokenSource? _completionCts, _hoverCts, _signatureCts;
    private Point _hoverPoint;
    private bool _disposed;

    private EditorLspBinding(TextEditor editor, LspTarget target, LspDocument doc, LspPopups popups, Func<string, IBrush?> brushFor)
    {
        _editor = editor;
        _area = editor.TextArea;
        _view = _area.TextView;
        _text = editor.Document;
        _buffer = new TextDocumentBuffer(_text);
        _target = target;
        _doc = doc;
        _popups = popups;
        _renderer = new DiagnosticRenderer(() => _doc.Diagnostics, () => _doc.Encoding, brushFor);
        _view.BackgroundRenderers.Add(_renderer);

        _text.Changed += OnDocumentChanged;
        _doc.DiagnosticsChanged += OnDiagnostics;
        _area.TextEntered += OnTextEntered;
        _area.Caret.PositionChanged += OnCaretMoved;
        _editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _view.PointerMoved += OnPointerMoved;
        _view.PointerExited += OnPointerExited;
        _view.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _hoverTimer.Tick += OnHoverTimer;
    }

    /// <summary>Opens the editor's current document on the right server; null when there is none (not installed, disabled, unsupported language).</summary>
    public static EditorLspBinding? TryCreate(TextEditor editor, LspTarget target, LspPopups popups, Func<string, IBrush?> brushFor)
    {
        var text = editor.Document;
        if (text is null) return null;
        var doc = target.Service.Manager.OpenDocument(target.LanguageId, target.FilePath, target.Root, () => text.Text);
        return doc is null ? null : new EditorLspBinding(editor, target, doc, popups, brushFor);
    }

    public LspDocument Document => _doc;
    public IReadOnlyList<Diagnostic> Diagnostics => _doc.Diagnostics;
    public LspTarget Target => _target;

    // ---------------------------------------------------------------- positions

    private Position ToPosition(int offset) => PositionConverter.OffsetToPosition(_buffer, Math.Clamp(offset, 0, _text.TextLength), _doc.Encoding);

    /// <summary>Document offset of an LSP position, or null when it is outside the document.</summary>
    public int? ToOffset(Position p)
    {
        if (p.Line < 0 || p.Line >= _text.LineCount) return null;
        try { return PositionConverter.PositionToOffset(_buffer, p, _doc.Encoding); }
        catch (ArgumentException) { return null; }
    }

    // ---------------------------------------------------------------- sync + diagnostics

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_disposed) return;
        _doc.ApplyEdit(_buffer, e.Offset, e.RemovedText.Text, e.InsertedText.Text);
    }

    private void OnDiagnostics(LspDocument d)
    {
        // Raised on a pool thread.
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            _view.InvalidateLayer(KnownLayer.Selection);
            _target.DiagnosticsSink?.Invoke(_target, _doc.Diagnostics);
        });
    }

    /// <summary>Pushes the current diagnostics to the sink (used right after attaching).</summary>
    public void PublishDiagnostics() => _target.DiagnosticsSink?.Invoke(_target, _doc.Diagnostics);

    // ---------------------------------------------------------------- keyboard + typing

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        switch (e.Key)
        {
            case Key.Space when ctrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                e.Handled = true;
                _ = ShowSignatureHelpAsync();
                break;
            case Key.Space when ctrl:
                e.Handled = true;
                _ = ShowCompletionAsync(null, manual: true);
                break;
            case Key.F12:
                e.Handled = true;
                _ = GoToDefinitionAtAsync(_area.Caret.Offset);
                break;
            case Key.F when e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                e.Handled = true;
                _ = FormatDocumentAsync();
                break;
            case Key.Escape:
                _popups.CloseAll();
                break;
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_disposed || string.IsNullOrEmpty(e.Text)) return;
        _popups.Hover.IsOpen = false;
        var caps = _doc.Capabilities;
        var ch = e.Text[^1];
        var s = ch.ToString();
        if (ch == ')') LspPopups.Close(_popups.Signature);
        if (caps.SignatureHelp && (caps.SignatureTriggerCharacters.Contains(s) || ch is '(' or ','))
            _ = ShowSignatureHelpAsync();
        if (_window is not null) return;
        if (caps.CompletionTriggerCharacters.Contains(s))
            _ = ShowCompletionAsync(s, manual: false);
        else if (char.IsLetter(ch) || ch == '_')
            _ = ShowCompletionAsync(null, manual: false);
    }

    private void OnCaretMoved(object? sender, EventArgs e)
    {
        if (_popups.Signature.IsOpen) _ = ShowSignatureHelpAsync();
    }

    // ---------------------------------------------------------------- completion

    private int WordStart(int caret)
    {
        var o = caret;
        while (o > 0 && (char.IsLetterOrDigit(_text.GetCharAt(o - 1)) || _text.GetCharAt(o - 1) == '_')) o--;
        return o;
    }

    public async Task ShowCompletionAsync(string? trigger, bool manual)
    {
        if (_disposed || !_doc.Capabilities.Completion && !manual) return;
        var caret = _area.Caret.Offset;
        var version = _doc.Version;
        _completionCts?.Cancel();
        var cts = _completionCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        LspCompletionList? list;
        try { list = await _doc.CompletionAsync(ToPosition(caret), trigger, cts.Token); }
        catch (Exception) { return; }
        if (_disposed || cts.IsCancellationRequested || list is null || list.Items.Count == 0) return;
        // The user kept typing: a newer request owns the popup.
        if (_doc.Version != version || _area.Caret.Offset != caret) return;
        ShowCompletionWindow(list, caret, manual);
    }

    private void ShowCompletionWindow(LspCompletionList list, int caret, bool manual)
    {
        _window?.Close();
        var start = WordStart(caret);
        var first = list.Items.Select(i => i.PrimaryEdit()).FirstOrDefault(p => p is not null);
        if (first is { } p && ToOffset(p.Range.Start) is { } so && so <= caret && so >= _text.GetLineByOffset(caret).Offset) start = Math.Min(start, so);

        var items = list.Items.Select((it, i) => (it, i))
            .OrderBy(x => x.it.SortText ?? x.it.Label, StringComparer.Ordinal).ThenBy(x => x.i)
            .Take(MaxCompletionItems).ToList();
        var window = new CompletionWindow(_area) { StartOffset = start, EndOffset = caret, CloseAutomatically = true };
        var n = items.Count;
        for (var i = 0; i < n; i++) window.CompletionList.CompletionData.Add(new LspCompletionData(items[i].it, _doc, this, n - i));
        window.Closed += (_, _) => { if (ReferenceEquals(_window, window)) _window = null; };
        _window = window;
        window.Show();
        var typed = _text.GetText(start, caret - start);
        if (typed.Length > 0) window.CompletionList.SelectItem(typed);
        if (!manual && window.CompletionList.ListBox?.ItemCount == 0) window.Close();
    }

    public bool IsCompletionOpen => _window is not null;

    /// <summary>Texts of the items in the open completion window (empty when closed).</summary>
    public IReadOnlyList<string> CompletionTexts => _window is null ? [] : _window.CompletionList.CompletionData.Select(d => d.Text).ToList();

    // ---------------------------------------------------------------- hover

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_disposed) return;
        _hoverPoint = e.GetPosition(_view);
        _hoverTimer.Stop();
        if (_popups.Hover.IsOpen && !_popups.Hover.IsPointerOver) _popups.Hover.IsOpen = false;
        _hoverTimer.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _hoverTimer.Stop();
        _hoverCts?.Cancel();
    }

    private async void OnHoverTimer(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (_disposed || !_view.IsPointerOver) return;
        try { await ShowHoverAtAsync(_hoverPoint); }
        catch (Exception) { /* hover is best-effort */ }
    }

    /// <summary>Shows diagnostics under the pointer plus the server's hover text. <paramref name="viewPoint"/> is relative to the text view.</summary>
    public async Task ShowHoverAtAsync(Point viewPoint)
    {
        var pos = _editor.GetPositionFromPoint(_view.TranslatePoint(viewPoint, _editor) ?? viewPoint);
        if (pos is null) return;
        var offset = _text.GetOffset(pos.Value.Location);
        var line = _text.GetLineByNumber(pos.Value.Line);
        if (offset >= line.EndOffset) return; // past the end of the line
        var diags = DiagnosticsAt(offset);
        Hover? hover = null;
        var version = _doc.Version;
        _hoverCts?.Cancel();
        var cts = _hoverCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (_doc.Capabilities.Hover && !char.IsWhiteSpace(_text.GetCharAt(offset)))
        {
            try { hover = await _doc.HoverAsync(ToPosition(offset), cts.Token); }
            catch (Exception) { return; }
        }
        if (_disposed || cts.IsCancellationRequested || _doc.Version != version) return;
        var (hoverText, markdown) = LspText.Flatten(hover?.Contents);
        if (diags.Count == 0 && string.IsNullOrWhiteSpace(hoverText)) { LspPopups.Close(_popups.Hover); return; }

        var panel = new StackPanel { Spacing = 6 };
        foreach (var d in diags)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var glyph = new TextBlock { Text = d.EffectiveSeverity == DiagnosticSeverity.Error ? "●" : "▲", FontSize = 11, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
            glyph.Bind(TextBlock.ForegroundProperty, glyph.GetResourceObservable(DiagnosticRenderer.Key(d.EffectiveSeverity)));
            row.Children.Add(glyph);
            var msg = LspPopups.Plain(DiagnosticText(d));
            msg.MaxWidth = 500;
            row.Children.Add(msg);
            panel.Children.Add(row);
        }
        if (!string.IsNullOrWhiteSpace(hoverText))
            panel.Children.Add(markdown ? new MarkdownViewer { Markdown = hoverText, MaxWidth = 520 } : LspPopups.Plain(hoverText, mono: true));
        _popups.Show(_popups.Hover, panel, new Rect(viewPoint.X, viewPoint.Y + 2, 1, 14));
    }

    public static string DiagnosticText(Diagnostic d)
    {
        var tail = string.Join(' ', new[] { d.Source, d.CodeText }.Where(s => !string.IsNullOrEmpty(s)));
        return string.IsNullOrEmpty(tail) ? d.Message : $"{d.Message}  ({tail})";
    }

    /// <summary>Diagnostics whose range covers <paramref name="offset"/>.</summary>
    public IReadOnlyList<Diagnostic> DiagnosticsAt(int offset)
    {
        var r = new List<Diagnostic>();
        foreach (var d in _doc.Diagnostics)
            if (DiagnosticRenderer.ToSegment(_text, d, _doc.Encoding) is { } s && offset >= s.Start && offset < s.End) r.Add(d);
        return r;
    }

    // ---------------------------------------------------------------- signature help

    public async Task ShowSignatureHelpAsync()
    {
        if (_disposed || !_doc.Capabilities.SignatureHelp) return;
        var caret = _area.Caret.Offset;
        _signatureCts?.Cancel();
        var cts = _signatureCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        SignatureHelp? help;
        try { help = await _doc.SignatureHelpAsync(ToPosition(caret), cts.Token); }
        catch (Exception) { return; }
        if (_disposed || cts.IsCancellationRequested) return;
        if (help is null || help.Signatures.Count == 0) { LspPopups.Close(_popups.Signature); return; }
        var index = Math.Clamp(help.ActiveSignature ?? 0, 0, help.Signatures.Count - 1);
        var sig = help.Signatures[index];
        var active = sig.ActiveParameter ?? help.ActiveParameter;
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(BuildSignature(sig, active));
        if (sig.Parameters is { } ps && active is { } ai && ai >= 0 && ai < ps.Count)
        {
            var (doc, md) = LspText.Flatten(ps[ai].Documentation);
            if (!string.IsNullOrWhiteSpace(doc)) panel.Children.Add(md ? new MarkdownViewer { Markdown = doc, MaxWidth = 480 } : LspPopups.Plain(doc));
        }
        if (help.Signatures.Count > 1) panel.Children.Add(new TextBlock { Text = $"{index + 1}/{help.Signatures.Count}", FontSize = 11, Opacity = 0.6 });

        var rect = _area.Caret.CalculateCaretRectangle();
        var anchor = new Rect(rect.X - _view.ScrollOffset.X, rect.Y - _view.ScrollOffset.Y, 1, Math.Max(rect.Height, 14));
        _popups.Show(_popups.Signature, panel, anchor, above: true);
    }

    private static TextBlock BuildSignature(SignatureInformation sig, int? active)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.5, FontFamily = new FontFamily("Menlo, Consolas, 'DejaVu Sans Mono', monospace"), MaxWidth = 520 };
        var label = sig.Label;
        int start = -1, end = -1;
        if (active is { } a && sig.Parameters is { } ps && a >= 0 && a < ps.Count)
        {
            var l = ps[a].Label;
            if (l.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var s = l.GetString() ?? "";
                start = s.Length == 0 ? -1 : label.IndexOf(s, StringComparison.Ordinal);
                end = start < 0 ? -1 : start + s.Length;
            }
            else if (l.ValueKind == System.Text.Json.JsonValueKind.Array && l.GetArrayLength() == 2)
            {
                start = l[0].GetInt32();
                end = l[1].GetInt32();
            }
        }
        if (start < 0 || end > label.Length || end <= start) { tb.Text = label; return tb; }
        tb.Inlines = new InlineCollection();
        tb.Inlines.Add(new Run(label[..start]));
        tb.Inlines.Add(new Run(label[start..end]) { FontWeight = FontWeight.Bold, TextDecorations = TextDecorations.Underline });
        tb.Inlines.Add(new Run(label[end..]));
        return tb;
    }

    // ---------------------------------------------------------------- definition

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_disposed || !e.GetCurrentPoint(_view).Properties.IsLeftButtonPressed) return;
        _popups.CloseAll();
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Meta) && !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        var pos = _editor.GetPositionFromPoint(_view.TranslatePoint(e.GetPosition(_view), _editor) ?? default);
        if (pos is null) return;
        e.Handled = true;
        _ = GoToDefinitionAtAsync(_text.GetOffset(pos.Value.Location));
    }

    public async Task GoToDefinitionAtAsync(int offset)
    {
        if (_disposed || !_doc.Capabilities.Definition) return;
        IReadOnlyList<DevAcademy.LanguageServices.Protocol.Location> locs;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            locs = await _doc.DefinitionAsync(ToPosition(offset), cts.Token);
        }
        catch (Exception) { return; }
        if (_disposed || locs.Count == 0) return;
        var loc = locs[0];
        var path = FileUri.ToPath(loc.Uri);
        if (SamePath(path, _doc.Path))
        {
            if (ToOffset(loc.Range.Start) is not { } o) return;
            _area.Caret.Offset = o;
            _area.Caret.BringCaretToView();
            _editor.ScrollToLine(_area.Caret.Line);
            _area.Focus();
            return;
        }
        var nav = new LspNavigation(path, loc.Range.Start.Line + 1, ColumnOf(path, loc.Range.Start) + 1);
        if (_target.Navigate?.Invoke(nav) == true) return;
        ShowPeek(nav);
    }

    private int ColumnOf(string path, Position p)
    {
        if (_doc.Encoding == PositionEncoding.Utf16) return p.Character;
        try
        {
            var lines = File.ReadLines(path).Skip(p.Line).Take(1).ToList();
            return lines.Count == 0 ? p.Character : PositionConverter.ToUtf16Column(lines[0], p.Character, _doc.Encoding);
        }
        catch (IOException) { return p.Character; }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private void ShowPeek(LspNavigation nav)
    {
        string[] lines;
        try
        {
            var info = new FileInfo(nav.FilePath);
            if (!info.Exists || info.Length > 2_000_000) return;
            lines = File.ReadAllLines(nav.FilePath);
        }
        catch (IOException) { return; }
        var from = Math.Max(0, nav.Line - 1 - 3);
        var to = Math.Min(lines.Length, nav.Line - 1 + 12);
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = $"{nav.FilePath}:{nav.Line}", FontSize = 11, Opacity = 0.65, TextTrimming = TextTrimming.CharacterEllipsis });
        var body = new StackPanel();
        for (var i = from; i < to; i++)
        {
            var row = LspPopups.Plain($"{i + 1,5}  {lines[i]}", mono: true);
            row.TextWrapping = TextWrapping.NoWrap;
            if (i == nav.Line - 1) row.FontWeight = FontWeight.Bold;
            body.Children.Add(row);
        }
        panel.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, MaxWidth = 640, MaxHeight = 260 });
        var rect = _area.Caret.CalculateCaretRectangle();
        _popups.Show(_popups.Peek, panel, new Rect(rect.X - _view.ScrollOffset.X, rect.Y - _view.ScrollOffset.Y, 1, Math.Max(rect.Height, 14)));
    }

    // ---------------------------------------------------------------- formatting

    /// <summary>Applies the server's formatting edits as one undo step; skipped if the text changed while waiting.</summary>
    public async Task<bool> FormatDocumentAsync()
    {
        if (_disposed || !_doc.Capabilities.Formatting || _editor.IsReadOnly) return false;
        var version = _doc.Version;
        IReadOnlyList<TextEdit> edits;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            edits = await _doc.FormatAsync(_editor.Options.IndentationSize, _editor.Options.ConvertTabsToSpaces, cts.Token);
        }
        catch (Exception) { return false; }
        if (_disposed || edits.Count == 0 || _doc.Version != version) return false;
        var resolved = new List<(int Start, int End, string Text)>();
        foreach (var e in edits)
        {
            if (ToOffset(e.Range.Start) is not { } s || ToOffset(e.Range.End) is not { } t || t < s) return false;
            resolved.Add((s, t, e.NewText));
        }
        var caret = _area.Caret.Offset;
        using (_text.RunUpdate())
            foreach (var r in resolved.OrderByDescending(x => x.Start))
                _text.Replace(r.Start, r.End - r.Start, r.Text);
        _area.Caret.Offset = Math.Min(caret, _text.TextLength);
        return true;
    }

    // ---------------------------------------------------------------- lifetime

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hoverTimer.Stop();
        _completionCts?.Cancel();
        _hoverCts?.Cancel();
        _signatureCts?.Cancel();
        _text.Changed -= OnDocumentChanged;
        _doc.DiagnosticsChanged -= OnDiagnostics;
        _area.TextEntered -= OnTextEntered;
        _area.Caret.PositionChanged -= OnCaretMoved;
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _view.PointerMoved -= OnPointerMoved;
        _view.PointerExited -= OnPointerExited;
        _view.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _hoverTimer.Tick -= OnHoverTimer;
        _view.BackgroundRenderers.Remove(_renderer);
        _window?.Close();
        _popups.CloseAll();
        _view.InvalidateLayer(KnownLayer.Selection);
        _doc.Dispose();
    }
}
