using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using DevAcademy.Terminal;
using DevAcademy.Terminal.Vt;

namespace DevAcademy.Desktop.Controls;

/// <summary>
/// Renders a <see cref="ITerminalSession"/> as a monospace grid. There is one control for the whole terminal (no per-cell controls). Each
/// emulator row caches its measured text runs against <see cref="TerminalLine.Version"/>, so on output only the rows that changed are
/// re-laid-out; every other row is replayed from its cache.
/// </summary>
public sealed class TerminalControl : Control
{
    public static readonly StyledProperty<ITerminalSession?> SessionProperty =
        AvaloniaProperty.Register<TerminalControl, ITerminalSession?>(nameof(Session));
    public static readonly StyledProperty<double> TerminalFontSizeProperty =
        AvaloniaProperty.Register<TerminalControl, double>(nameof(TerminalFontSize), 13);

    private const double Padding = 4;
    private static readonly FontFamily Mono = new("Menlo, Consolas, 'DejaVu Sans Mono', monospace");

    private ITerminalSession? _session;
    private double _cellW = 8, _cellH = 16;
    private int _cols = 80, _rows = 24;
    private int _scrollOffset;
    private long _lastScrolled;
    private int _invalidatePending;
    private int _rowsLaidOut;
    private (long Row, int Col)? _anchor, _caret;
    private bool _selecting;
    private bool _dark;

    static TerminalControl()
    {
        FocusableProperty.OverrideDefaultValue<TerminalControl>(true);
        SessionProperty.Changed.AddClassHandler<TerminalControl>((c, e) => c.OnSessionChanged(e.OldValue as ITerminalSession, e.NewValue as ITerminalSession));
        TerminalFontSizeProperty.Changed.AddClassHandler<TerminalControl>((c, _) => { c.MeasureCell(); c.ApplyGridSize(); c.InvalidateVisual(); });
        AffectsRender<TerminalControl>(SessionProperty);
    }

    public TerminalControl()
    {
        Cursor = new Cursor(StandardCursorType.Ibeam);
        ClipToBounds = true;
        MeasureCell();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        var menu = new ContextMenu();
        var copy = new MenuItem { Header = "Копировать" };
        copy.Click += async (_, _) => await CopyAsync();
        var paste = new MenuItem { Header = "Вставить" };
        paste.Click += async (_, _) => await PasteAsync();
        menu.Items.Add(copy);
        menu.Items.Add(paste);
        ContextMenu = menu;
    }

    public ITerminalSession? Session { get => GetValue(SessionProperty); set => SetValue(SessionProperty, value); }
    public double TerminalFontSize { get => GetValue(TerminalFontSizeProperty); set => SetValue(TerminalFontSizeProperty, value); }

    /// <summary>Visible grid size (what the PTY was told).</summary>
    public (int Cols, int Rows) GridSize => (_cols, _rows);
    /// <summary>Rows whose cached layout was rebuilt by the last render (the rest were replayed from cache).</summary>
    public int RowsLaidOutInLastRender => _rowsLaidOut;
    public bool HasSelection => _anchor is not null && _caret is not null && _anchor != _caret;

    // ---------------------------------------------------------------- session wiring

    private void OnSessionChanged(ITerminalSession? old, ITerminalSession? next)
    {
        if (old is not null) old.Changed -= OnSessionOutput;
        _session = next;
        _scrollOffset = 0;
        _anchor = _caret = null;
        if (next is not null)
        {
            lock (next.Screen.SyncRoot) _lastScrolled = next.Screen.TotalScrolled;
            next.Changed += OnSessionOutput;
            ApplyGridSize();
        }
        InvalidateVisual();
    }

    private void OnSessionOutput()
    {
        // Called on the reader thread: coalesce into at most one pending UI invalidation.
        if (Interlocked.Exchange(ref _invalidatePending, 1) == 1) return;
        Dispatcher.UIThread.Post(() =>
        {
            Volatile.Write(ref _invalidatePending, 0);
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_session is not null) _session.Changed -= OnSessionOutput;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_session is not null)
        {
            _session.Changed -= OnSessionOutput;
            _session.Changed += OnSessionOutput;
            ApplyGridSize();
        }
    }

    // ---------------------------------------------------------------- metrics and resize

    private Typeface Face(bool bold, bool italic) =>
        new(Mono, italic ? FontStyle.Italic : FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal);

    private void MeasureCell()
    {
        var sample = new FormattedText("MMMMMMMMMM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face(false, false), TerminalFontSize, Brushes.Black);
        _cellW = Math.Max(1, sample.Width / 10);
        _cellH = Math.Max(1, Math.Ceiling(sample.Height));
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 80 * _cellW + 2 * Padding : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 24 * _cellH + 2 * Padding : availableSize.Height);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        ApplyGridSize(size);
        return size;
    }

    private void ApplyGridSize() => ApplyGridSize(Bounds.Size);

    private void ApplyGridSize(Size size)
    {
        if (size.Width < _cellW * 2 || size.Height < _cellH * 2) return;
        var cols = Math.Max(2, (int)((size.Width - 2 * Padding) / _cellW));
        var rows = Math.Max(2, (int)((size.Height - 2 * Padding) / _cellH));
        if (cols == _cols && rows == _rows && (_session is null || (_session.Screen.Cols == cols && _session.Screen.Rows == rows))) return;
        _cols = cols;
        _rows = rows;
        _session?.Resize(cols, rows);
    }

    // ---------------------------------------------------------------- colours

    private static readonly uint[] Ansi16Dark =
    [
        0x1B1E21, 0xE06C75, 0x7FC77A, 0xE5C07B, 0x61AFEF, 0xC678DD, 0x56B6C2, 0xC8CCD0,
        0x5C6370, 0xFF8A93, 0x9BE08F, 0xF2D48F, 0x84C4FF, 0xDB9AF0, 0x7FD0DC, 0xFFFFFF,
    ];

    private static readonly uint[] Ansi16Light =
    [
        0x24292E, 0xB3261E, 0x1E7B34, 0x8A5A00, 0x1F5C9E, 0x8E3B9E, 0x167C86, 0x6A6A64,
        0x586069, 0xD0302A, 0x2E9A47, 0xA06E00, 0x2F78C4, 0xA654B8, 0x1F95A0, 0x24292E,
    ];

    private static uint Rgb(byte r, byte g, byte b) => (uint)(r << 16 | g << 8 | b);

    private uint Resolve(TermColor c, bool isFg)
    {
        if (c.IsRgb) return Rgb(c.R, c.G, c.B);
        if (c.IsIndexed)
        {
            var i = c.Index;
            if (i < 16) return (_dark ? Ansi16Dark : Ansi16Light)[i];
            if (i < 232)
            {
                var n = i - 16;
                static byte Level(int v) => (byte)(v == 0 ? 0 : 55 + v * 40);
                return Rgb(Level(n / 36), Level(n / 6 % 6), Level(n % 6));
            }
            var gray = (byte)(8 + (i - 232) * 10);
            return Rgb(gray, gray, gray);
        }
        return isFg ? DefaultFg : DefaultBg;
    }

    private uint DefaultFg => _dark ? 0xD7DADDu : 0x24292Eu;
    private uint DefaultBg => _dark ? 0x1B1E21u : 0xFBFBFAu;

    private static Color ToColor(uint rgb, byte alpha = 255) => Color.FromArgb(alpha, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static readonly Dictionary<uint, IBrush> Brushes_ = [];

    private static IBrush Brush(uint rgb)
    {
        lock (Brushes_)
        {
            if (!Brushes_.TryGetValue(rgb, out var b))
            {
                b = new ImmutableSolidColorBrush(ToColor(rgb));
                Brushes_[rgb] = b;
            }
            return b;
        }
    }

    // ---------------------------------------------------------------- row cache

    private sealed class Run
    {
        public int Col, Cells;
        public uint Bg;
        public bool DrawBg;
        public FormattedText? Text;
        public bool Underline, Strike;
        public uint Fg;
    }

    private sealed class RowLayout
    {
        public int Version = -1;
        public int Cols;
        public bool Dark;
        public double Size;
        public List<Run> Runs = [];
    }

    private RowLayout Layout(TerminalLine line)
    {
        var cached = line.Tag as RowLayout;
        if (cached is not null && cached.Version == line.Version && cached.Cols == line.Cells.Length && cached.Dark == _dark && cached.Size == TerminalFontSize)
            return cached;
        _rowsLaidOut++;
        var layout = cached ?? new RowLayout();
        layout.Runs.Clear();
        layout.Version = line.Version;
        layout.Cols = line.Cells.Length;
        layout.Dark = _dark;
        layout.Size = TerminalFontSize;
        var cells = line.Cells;
        var sb = new System.Text.StringBuilder();
        var col = 0;
        while (col < cells.Length)
        {
            ref readonly var first = ref cells[col];
            var (fg, bg) = Colors(first);
            var flags = first.Flags & ~CellFlags.Blink;
            var start = col;
            sb.Clear();
            while (col < cells.Length)
            {
                ref readonly var cell = ref cells[col];
                var (f2, b2) = Colors(cell);
                if (f2 != fg || b2 != bg || (cell.Flags & ~CellFlags.Blink) != flags) break;
                // Re-sync the grid at every non-ASCII glyph: fallback fonts need not be exactly one cell wide.
                if (cell.CodePoint > 0x7F && col > start) break;
                if (cell.Width != 0)
                {
                    var cp = cell.CodePoint is > 0 and <= 0x10FFFF && !(cell.CodePoint is >= 0xD800 and <= 0xDFFF) ? cell.CodePoint : ' ';
                    sb.Append(char.ConvertFromUtf32(cp));
                }
                col += Math.Max(1, (int)cell.Width == 0 ? 1 : cell.Width);
                if (cell.CodePoint > 0x7F) break;
            }
            var text = sb.ToString();
            var hidden = flags.HasFlag(CellFlags.Hidden);
            var drawBg = bg != DefaultBg;
            var visibleText = !hidden && text.Any(ch => ch != ' ');
            var run = new Run { Col = start, Cells = col - start, Bg = bg, DrawBg = drawBg, Fg = fg, Underline = flags.HasFlag(CellFlags.Underline), Strike = flags.HasFlag(CellFlags.Strike) };
            if (visibleText)
            {
                var fgFinal = flags.HasFlag(CellFlags.Faint) ? Blend(fg, bg) : fg;
                run.Fg = fgFinal;
                run.Text = new FormattedText(text.TrimEnd(' '), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    Face(flags.HasFlag(CellFlags.Bold), flags.HasFlag(CellFlags.Italic)), TerminalFontSize, Brush(fgFinal));
            }
            if (run.DrawBg || run.Text is not null || run.Underline || run.Strike) layout.Runs.Add(run);
        }
        line.Tag = layout;
        line.TagVersion = line.Version;
        return layout;
    }

    private (uint Fg, uint Bg) Colors(in Cell cell)
    {
        var fg = Resolve(cell.Fg, true);
        var bg = Resolve(cell.Bg, false);
        if (cell.Flags.HasFlag(CellFlags.Inverse)) (fg, bg) = (bg, fg);
        return (fg, bg);
    }

    private static uint Blend(uint fg, uint bg) =>
        Rgb((byte)(((fg >> 16 & 0xFF) + (bg >> 16 & 0xFF)) / 2), (byte)(((fg >> 8 & 0xFF) + (bg >> 8 & 0xFF)) / 2), (byte)(((fg & 0xFF) + (bg & 0xFF)) / 2));

    // ---------------------------------------------------------------- render

    public override void Render(DrawingContext context)
    {
        _dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        _rowsLaidOut = 0;
        context.FillRectangle(Brush(DefaultBg), new Rect(Bounds.Size));
        if (_session is not { } session) return;
        var screen = session.Screen;

        lock (screen.SyncRoot)
        {
            // Keep the scrolled-back view anchored to its content while new lines arrive.
            var scrolled = screen.TotalScrolled;
            if (_scrollOffset > 0 && scrolled > _lastScrolled) _scrollOffset += (int)(scrolled - _lastScrolled);
            _lastScrolled = scrolled;
            if (screen.IsAlternateScreen) _scrollOffset = 0;
            _scrollOffset = Math.Clamp(_scrollOffset, 0, screen.ScrollbackCount);

            var rows = Math.Min(_rows, screen.Rows);
            var first = screen.LineCount - screen.Rows - _scrollOffset;
            for (var r = 0; r < rows; r++)
            {
                var idx = first + r;
                if (idx < 0 || idx >= screen.LineCount) continue;
                var line = screen.GetLine(idx);
                var layout = Layout(line);
                var y = Padding + r * _cellH;
                foreach (var run in layout.Runs)
                {
                    var x = Padding + run.Col * _cellW;
                    if (run.DrawBg) context.FillRectangle(Brush(run.Bg), new Rect(x, y, run.Cells * _cellW, _cellH));
                }
                foreach (var run in layout.Runs)
                {
                    var x = Padding + run.Col * _cellW;
                    if (run.Text is not null) context.DrawText(run.Text, new Point(x, y));
                    if (run.Underline) context.FillRectangle(Brush(run.Fg), new Rect(x, y + _cellH - 2, run.Cells * _cellW, 1));
                    if (run.Strike) context.FillRectangle(Brush(run.Fg), new Rect(x, y + _cellH / 2, run.Cells * _cellW, 1));
                }
                DrawSelectionRow(context, screen, idx, y);
            }

            if (_scrollOffset == 0 && screen.CursorVisible && screen.CursorRow < rows)
                DrawCursor(context, screen);
            DrawScrollIndicator(context, screen);
        }
    }

    private void DrawSelectionRow(DrawingContext context, TerminalEmulator screen, int lineIndex, double y)
    {
        if (!HasSelection) return;
        var (a, b) = OrderedSelection();
        var abs = screen.TrimmedCount + lineIndex;
        if (abs < a.Row || abs > b.Row) return;
        var from = abs == a.Row ? a.Col : 0;
        var to = abs == b.Row ? b.Col : _cols - 1;
        if (to < from) return;
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(90, 90, 150, 235)), new Rect(Padding + from * _cellW, y, (to - from + 1) * _cellW, _cellH));
    }

    private void DrawCursor(DrawingContext context, TerminalEmulator screen)
    {
        var x = Padding + screen.CursorCol * _cellW;
        var y = Padding + screen.CursorRow * _cellH;
        var color = ToColor(_dark ? 0xE6E8EAu : 0x24292Eu);
        if (!IsFocused)
        {
            context.DrawRectangle(null, new Pen(new SolidColorBrush(color), 1), new Rect(x + 0.5, y + 0.5, _cellW - 1, _cellH - 1));
            return;
        }
        var cursor = new SolidColorBrush(color, 0.75);
        switch (screen.CursorStyle)
        {
            case CursorStyle.Bar: context.FillRectangle(cursor, new Rect(x, y, 2, _cellH)); break;
            case CursorStyle.Underline: context.FillRectangle(cursor, new Rect(x, y + _cellH - 2, _cellW, 2)); break;
            default: context.FillRectangle(cursor, new Rect(x, y, _cellW, _cellH)); break;
        }
    }

    private void DrawScrollIndicator(DrawingContext context, TerminalEmulator screen)
    {
        if (screen.ScrollbackCount == 0 || screen.IsAlternateScreen) return;
        var total = (double)screen.LineCount;
        var h = Math.Max(16, Bounds.Height * screen.Rows / total);
        var top = (Bounds.Height - h) * (1 - (double)_scrollOffset / screen.ScrollbackCount);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(_scrollOffset > 0 ? (byte)150 : (byte)60, 128, 128, 128)), new Rect(Bounds.Width - 5, top, 3, h));
    }

    // ---------------------------------------------------------------- keyboard

    private static TermKey Map(Key key) => key switch
    {
        Key.Up => TermKey.Up,
        Key.Down => TermKey.Down,
        Key.Left => TermKey.Left,
        Key.Right => TermKey.Right,
        Key.Home => TermKey.Home,
        Key.End => TermKey.End,
        Key.PageUp => TermKey.PageUp,
        Key.PageDown => TermKey.PageDown,
        Key.Insert => TermKey.Insert,
        Key.Delete => TermKey.Delete,
        Key.Back => TermKey.Backspace,
        Key.Tab => TermKey.Tab,
        Key.Enter or Key.Return => TermKey.Enter,
        Key.Escape => TermKey.Escape,
        Key.F1 => TermKey.F1,
        Key.F2 => TermKey.F2,
        Key.F3 => TermKey.F3,
        Key.F4 => TermKey.F4,
        Key.F5 => TermKey.F5,
        Key.F6 => TermKey.F6,
        Key.F7 => TermKey.F7,
        Key.F8 => TermKey.F8,
        Key.F9 => TermKey.F9,
        Key.F10 => TermKey.F10,
        Key.F11 => TermKey.F11,
        Key.F12 => TermKey.F12,
        _ => TermKey.None,
    };

    private static TermMods Mods(KeyModifiers m) =>
        (m.HasFlag(KeyModifiers.Shift) ? TermMods.Shift : 0) | (m.HasFlag(KeyModifiers.Alt) ? TermMods.Alt : 0) | (m.HasFlag(KeyModifiers.Control) ? TermMods.Ctrl : 0);

    private static char? LetterOf(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (char)('a' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        Key.OemOpenBrackets => '[',
        Key.OemCloseBrackets => ']',
        Key.OemBackslash or Key.OemPipe => '\\',
        Key.Space => ' ',
        Key.OemMinus => '_',
        Key.OemQuestion => '/',
        _ => null,
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_session is null) return;

        var meta = e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Clipboard: Cmd+C / Cmd+V (macOS), Ctrl+Shift+C / Ctrl+Shift+V and Shift+Insert (elsewhere). Plain Ctrl+C always goes to the process.
        if ((meta && e.Key == Key.C) || (ctrl && shift && e.Key == Key.C)) { _ = CopyAsync(); e.Handled = true; return; }
        if ((meta && e.Key == Key.V) || (ctrl && shift && e.Key == Key.V) || (shift && e.Key == Key.Insert)) { _ = PasteAsync(); e.Handled = true; return; }
        if (meta) return; // other Cmd shortcuts belong to the application
        if (shift && e.Key == Key.PageUp && !_session.Screen.IsAlternateScreen) { ScrollBy(_rows - 1); e.Handled = true; return; }
        if (shift && e.Key == Key.PageDown && !_session.Screen.IsAlternateScreen) { ScrollBy(-(_rows - 1)); e.Handled = true; return; }

        var term = Map(e.Key);
        string? bytes = null;
        if (term != TermKey.None)
        {
            bool appCursor;
            lock (_session.Screen.SyncRoot) appCursor = _session.Screen.ApplicationCursorKeys;
            bytes = KeyEncoder.Encode(term, Mods(e.KeyModifiers), appCursor);
        }
        else if (ctrl && LetterOf(e.Key) is { } ch)
        {
            bytes = KeyEncoder.EncodeControl(ch);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !ctrl && LetterOf(e.Key) is { } alt)
        {
            bytes = KeyEncoder.EncodeAlt(shift ? char.ToUpperInvariant(alt).ToString() : alt.ToString());
        }

        if (bytes is null) return; // printable text arrives through OnTextInput
        _scrollOffset = 0;
        _anchor = _caret = null;
        _session.Write(bytes);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_session is null || string.IsNullOrEmpty(e.Text)) return;
        if (e.Text.All(char.IsControl)) return;
        _scrollOffset = 0;
        _session.Write(e.Text);
        e.Handled = true;
    }

    /// <summary>Test and automation hook: types text exactly as if the user had typed it.</summary>
    public void SendText(string text) => _session?.Write(text);

    // ---------------------------------------------------------------- clipboard

    public string? GetSelectedText()
    {
        if (_session is null || !HasSelection) return null;
        var (a, b) = OrderedSelection();
        lock (_session.Screen.SyncRoot) return _session.Screen.GetText(a.Row, a.Col, b.Row, b.Col);
    }

    public async Task CopyAsync()
    {
        var text = GetSelectedText();
        if (string.IsNullOrEmpty(text)) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }

    public async Task PasteAsync()
    {
        if (_session is null) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text)) return;
        Paste(text);
    }

    /// <summary>Sends pasted text with bracketed-paste markers when the application asked for them.</summary>
    public void Paste(string text)
    {
        if (_session is null) return;
        bool bracketed;
        lock (_session.Screen.SyncRoot) bracketed = _session.Screen.BracketedPaste;
        _scrollOffset = 0;
        _session.Write(KeyEncoder.Paste(text, bracketed));
    }

    // ---------------------------------------------------------------- mouse: selection and scrolling

    private (long Row, int Col) CellAt(Point p)
    {
        var col = Math.Clamp((int)((p.X - Padding) / _cellW), 0, _cols - 1);
        var row = Math.Clamp((int)((p.Y - Padding) / _cellH), 0, _rows - 1);
        if (_session is null) return (row, col);
        lock (_session.Screen.SyncRoot)
        {
            var first = _session.Screen.LineCount - _session.Screen.Rows - _scrollOffset;
            return (_session.Screen.TrimmedCount + first + row, col);
        }
    }

    private ((long Row, int Col) A, (long Row, int Col) B) OrderedSelection()
    {
        var a = _anchor!.Value;
        var b = _caret!.Value;
        return (a.Row < b.Row || (a.Row == b.Row && a.Col <= b.Col)) ? (a, b) : (b, a);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        _selecting = true;
        _anchor = _caret = CellAt(point.Position);
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_selecting) return;
        var p = e.GetPosition(this);
        if (p.Y < 0) ScrollBy(1); else if (p.Y > Bounds.Height) ScrollBy(-1);
        _caret = CellAt(p);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_selecting) return;
        _selecting = false;
        e.Pointer.Capture(null);
        if (!HasSelection) _anchor = _caret = null;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_session is null) return;
        var lines = (int)Math.Round(e.Delta.Y * 3);
        if (lines == 0) return;
        bool alt;
        lock (_session.Screen.SyncRoot) alt = _session.Screen.IsAlternateScreen;
        if (alt)
        {
            // Full-screen apps (less, vim) have no scrollback; the wheel acts as arrow keys.
            var seq = KeyEncoder.Encode(lines > 0 ? TermKey.Up : TermKey.Down, TermMods.None, false)!;
            _session.Write(string.Concat(Enumerable.Repeat(seq, Math.Abs(lines))));
        }
        else ScrollBy(lines);
        e.Handled = true;
    }

    private void ScrollBy(int lines)
    {
        if (_session is null) return;
        int max;
        lock (_session.Screen.SyncRoot) max = _session.Screen.ScrollbackCount;
        _scrollOffset = Math.Clamp(_scrollOffset + lines, 0, max);
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
    protected override void OnLostFocus(FocusChangedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }
}
