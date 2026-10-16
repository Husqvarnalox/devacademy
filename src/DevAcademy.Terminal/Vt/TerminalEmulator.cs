using System.Text;

namespace DevAcademy.Terminal.Vt;

public enum CursorStyle { Block, Underline, Bar }

/// <summary>
/// A focused VT100/xterm screen model: cursor movement, erase, scroll regions, SGR (16/256/truecolor), alternate screen,
/// DEC line drawing, insert/delete, auto-wrap with deferred wrap, scrollback. UI-agnostic; guard cross-thread use with <see cref="SyncRoot"/>.
/// </summary>
public sealed partial class TerminalEmulator
{
    public const int DefaultScrollback = 5000;

    private int _cols;
    private int _rows;
    private readonly LineRing _scrollback;
    private TerminalLine[] _main;
    private TerminalLine[] _alt;
    private TerminalLine[] _screen;

    private int _row;
    private int _col;
    private bool _pendingWrap;
    private int _top;
    private int _bottom;
    private bool[] _tabs;

    private CellFlags _flags;
    private TermColor _fg;
    private TermColor _bg;

    private bool _autoWrap = true;
    private bool _originMode;
    private bool _insertMode;
    private int _lastPrinted;

    private bool _g0Special;
    private bool _g1Special;
    private bool _shiftOut;

    private SavedCursor _saved;
    private SavedCursor _savedForAlt;

    private readonly object _syncRoot = new();
    private int _version;

    public TerminalEmulator(int cols = 80, int rows = 24, int scrollbackLimit = DefaultScrollback)
    {
        _cols = Math.Max(1, cols);
        _rows = Math.Max(1, rows);
        _scrollback = new LineRing(Math.Max(0, scrollbackLimit));
        _main = NewScreen();
        _alt = NewScreen();
        _screen = _main;
        _bottom = _rows - 1;
        _tabs = NewTabStops(_cols);
        _saved = SavedCursor.Initial;
        _savedForAlt = SavedCursor.Initial;
    }

    private record struct SavedCursor(int Row, int Col, CellFlags Flags, TermColor Fg, TermColor Bg, bool Origin, bool Wrap, bool G0Special)
    {
        public static SavedCursor Initial => new(0, 0, CellFlags.None, default, default, false, false, false);
    }

    /// <summary>Lock this while reading lines from another thread than the one feeding output.</summary>
    public object SyncRoot => _syncRoot;

    /// <summary>Increases on every change to the screen, cursor or modes.</summary>
    public int Version => _version;

    public int Cols => _cols;
    public int Rows => _rows;
    public int CursorRow => _row;
    public int CursorCol => Math.Min(_col, _cols - 1);
    public bool CursorVisible { get; private set; } = true;
    public CursorStyle CursorStyle { get; private set; } = CursorStyle.Block;
    public bool IsAlternateScreen => ReferenceEquals(_screen, _alt);
    /// <summary>DECCKM: arrows/Home/End send SS3 sequences instead of CSI.</summary>
    public bool ApplicationCursorKeys { get; private set; }
    /// <summary>The application asked for bracketed paste (CSI ? 2004 h): wrap pastes in ESC[200~ … ESC[201~.</summary>
    public bool BracketedPaste { get; private set; }
    public bool MouseTrackingRequested { get; private set; }
    public string Title { get; private set; } = "";

    public int ScrollbackCount => _scrollback.Count;
    /// <summary>Lines that fell off the scrollback so far; <c>TrimmedCount + index</c> is a stable absolute row number.</summary>
    public long TrimmedCount { get; private set; }
    /// <summary>Total lines ever pushed into scrollback (grows monotonically; lets a view follow scrolled-out output).</summary>
    public long TotalScrolled { get; private set; }

    public event Action<string>? TitleChanged;
    public event Action? Bell;
    /// <summary>Bytes the terminal answers to the application (device status / attributes reports). Raised while output is processed.</summary>
    public event Action<string>? Reply;

    /// <summary>Number of addressable lines: scrollback followed by the visible screen.</summary>
    public int LineCount => _scrollback.Count + _rows;

    /// <summary>Line by index into scrollback + screen (0 = oldest scrollback line).</summary>
    public TerminalLine GetLine(int index)
    {
        if (index < _scrollback.Count) return _scrollback[index];
        return _screen[Math.Clamp(index - _scrollback.Count, 0, _rows - 1)];
    }

    public TerminalLine ScreenLine(int row) => _screen[row];

    private TerminalLine[] NewScreen()
    {
        var s = new TerminalLine[_rows];
        for (var i = 0; i < _rows; i++) s[i] = new TerminalLine(_cols);
        return s;
    }

    private static bool[] NewTabStops(int cols)
    {
        var t = new bool[cols];
        for (var i = 8; i < cols; i += 8) t[i] = true;
        return t;
    }

    private void Bump() => _version++;

    // ------------------------------------------------------------------ text extraction

    /// <summary>Visible screen as text, one line per row, trailing blanks trimmed (used by tests and "copy screen").</summary>
    public string GetScreenText()
    {
        var sb = new StringBuilder();
        for (var r = 0; r < _rows; r++)
        {
            if (r > 0) sb.Append('\n');
            sb.Append(LineText(_screen[r], 0, _cols, trimEnd: true));
        }
        return sb.ToString();
    }

    public string GetScreenLineText(int row) => LineText(_screen[row], 0, _cols, trimEnd: true);

    /// <summary>Everything including scrollback.</summary>
    public string GetAllText()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < LineCount; i++)
        {
            var line = GetLine(i);
            sb.Append(LineText(line, 0, line.Cells.Length, trimEnd: true));
            if (i < LineCount - 1 && !line.Wrapped) sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>Text between two (absolute row, column) positions, end inclusive. Rows that wrapped are joined without a newline.</summary>
    public string GetText(long startAbs, int startCol, long endAbs, int endCol)
    {
        if (endAbs < startAbs || (endAbs == startAbs && endCol < startCol)) (startAbs, startCol, endAbs, endCol) = (endAbs, endCol, startAbs, startCol);
        var sb = new StringBuilder();
        for (var abs = startAbs; abs <= endAbs; abs++)
        {
            var index = (int)(abs - TrimmedCount);
            if (index < 0 || index >= LineCount) continue;
            var line = GetLine(index);
            var from = abs == startAbs ? startCol : 0;
            var to = abs == endAbs ? endCol + 1 : line.Cells.Length;
            var lastRow = abs == endAbs;
            var text = LineText(line, from, Math.Min(to, line.Cells.Length), trimEnd: !lastRow || to >= line.Cells.Length);
            sb.Append(text);
            if (!lastRow && !line.Wrapped) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string LineText(TerminalLine line, int from, int to, bool trimEnd)
    {
        var sb = new StringBuilder(Math.Max(0, to - from));
        for (var c = Math.Max(0, from); c < to && c < line.Cells.Length; c++)
        {
            ref readonly var cell = ref line.Cells[c];
            if (cell.Width == 0) continue;
            sb.Append(char.ConvertFromUtf32(cell.CodePoint is > 0 and <= 0x10FFFF && !(cell.CodePoint is >= 0xD800 and <= 0xDFFF) ? cell.CodePoint : ' '));
        }
        return trimEnd ? sb.ToString().TrimEnd(' ') : sb.ToString();
    }

    // ------------------------------------------------------------------ resize

    public void Resize(int cols, int rows)
    {
        cols = Math.Max(1, cols);
        rows = Math.Max(1, rows);
        if (cols == _cols && rows == _rows) return;

        var main = ReferenceEquals(_screen, _main);
        ResizeWidth(_main, cols);
        ResizeWidth(_alt, cols);

        // Main screen keeps its history: shrinking pushes top rows into scrollback, growing pulls them back.
        var newMain = new TerminalLine[rows];
        if (rows < _rows)
        {
            var drop = Math.Max(0, _row - (rows - 1));   // rows that must leave through the top to keep the cursor visible
            for (var i = 0; i < drop; i++) PushScrollback(_main[i]);
            var keepFrom = drop;
            for (var i = 0; i < rows; i++) newMain[i] = _main[keepFrom + i];
            if (main) _row -= drop;
        }
        else
        {
            var pull = 0;
            var wantPull = rows - _rows;
            if (main) while (pull < wantPull && _scrollback.Count > 0) pull++;
            var pulled = new TerminalLine[pull];
            for (var i = pull - 1; i >= 0; i--)
            {
                pulled[i] = _scrollback.PopNewest()!;
                pulled[i].Resize(cols, default);
            }
            for (var i = 0; i < pull; i++) newMain[i] = pulled[i];
            for (var i = 0; i < _rows; i++) newMain[pull + i] = _main[i];
            for (var i = pull + _rows; i < rows; i++) newMain[i] = new TerminalLine(cols);
            if (main) _row += pull;
        }
        _main = newMain;

        var newAlt = new TerminalLine[rows];
        for (var i = 0; i < rows; i++) newAlt[i] = i < _alt.Length ? _alt[i] : new TerminalLine(cols);
        _alt = newAlt;
        _screen = main ? _main : _alt;

        _cols = cols;
        _rows = rows;
        _top = 0;
        _bottom = rows - 1;
        _row = Math.Clamp(_row, 0, rows - 1);
        _col = Math.Min(_col, cols - 1);
        _pendingWrap = false;
        var tabs = NewTabStops(cols);
        Array.Copy(_tabs, tabs, Math.Min(_tabs.Length, cols));
        _tabs = tabs;
        foreach (var l in _main) l.Touch();
        foreach (var l in _alt) l.Touch();
        Bump();
    }

    private static void ResizeWidth(TerminalLine[] screen, int cols)
    {
        foreach (var l in screen) l.Resize(cols, default);
    }

    private void PushScrollback(TerminalLine line)
    {
        if (_scrollback.Push(line)) TrimmedCount++;
        TotalScrolled++;
    }

    // ------------------------------------------------------------------ cursor and scrolling primitives

    private Cell BlankCell() => Cell.Blank(_bg);

    private void ClampCursor()
    {
        _row = Math.Clamp(_row, 0, _rows - 1);
        _col = Math.Clamp(_col, 0, _cols - 1);
    }

    private void MoveTo(int row, int col)
    {
        if (_originMode) row = Math.Clamp(row + _top, _top, _bottom);
        else row = Math.Clamp(row, 0, _rows - 1);
        _row = row;
        _col = Math.Clamp(col, 0, _cols - 1);
        _pendingWrap = false;
    }

    private void LineFeed()
    {
        if (_row == _bottom) ScrollUp(1);
        else if (_row < _rows - 1) _row++;
    }

    private void ReverseIndex()
    {
        if (_row == _top) ScrollDown(1);
        else if (_row > 0) _row--;
    }

    /// <summary>Scrolls the region up: top lines leave (into scrollback for a full main screen), blank lines appear at the bottom.</summary>
    private void ScrollUp(int n)
    {
        n = Math.Clamp(n, 1, _bottom - _top + 1);
        var toScrollback = ReferenceEquals(_screen, _main) && _top == 0;
        for (var i = 0; i < n; i++)
        {
            var removed = _screen[_top];
            if (toScrollback) PushScrollback(removed);
            Array.Copy(_screen, _top + 1, _screen, _top, _bottom - _top);
            _screen[_bottom] = toScrollback ? new TerminalLine(_cols, _bg) : Recycle(removed);
        }
    }

    private void ScrollDown(int n)
    {
        n = Math.Clamp(n, 1, _bottom - _top + 1);
        for (var i = 0; i < n; i++)
        {
            var removed = _screen[_bottom];
            Array.Copy(_screen, _top, _screen, _top + 1, _bottom - _top);
            _screen[_top] = Recycle(removed);
        }
    }

    private TerminalLine Recycle(TerminalLine line)
    {
        // The line object may still be referenced by a renderer cache keyed on identity: hand out a fresh one.
        return new TerminalLine(_cols, _bg);
    }

    // ------------------------------------------------------------------ printing

    private static readonly Dictionary<int, int> DecSpecial = new()
    {
        ['`'] = '◆',
        ['a'] = '▒',
        ['f'] = '°',
        ['g'] = '±',
        ['h'] = '▒',
        ['i'] = '␋',
        ['j'] = '┘',
        ['k'] = '┐',
        ['l'] = '┌',
        ['m'] = '└',
        ['n'] = '┼',
        ['o'] = '⎺',
        ['p'] = '⎻',
        ['q'] = '─',
        ['r'] = '⎼',
        ['s'] = '⎽',
        ['t'] = '├',
        ['u'] = '┤',
        ['v'] = '┴',
        ['w'] = '┬',
        ['x'] = '│',
        ['y'] = '≤',
        ['z'] = '≥',
        ['{'] = 'π',
        ['|'] = '≠',
        ['}'] = '£',
        ['~'] = '·',
    };

    private void Print(int cp)
    {
        if ((_shiftOut ? _g1Special : _g0Special) && DecSpecial.TryGetValue(cp, out var mapped)) cp = mapped;
        var width = CharWidth.Of(cp);
        if (width == 0) return; // combining marks are dropped (no grapheme clusters in cells)
        _lastPrinted = cp;

        if (_pendingWrap)
        {
            if (_autoWrap)
            {
                _screen[_row].Wrapped = true;
                _col = 0;
                LineFeed();
            }
            _pendingWrap = false;
        }
        if (width == 2 && _col >= _cols - 1)
        {
            if (!_autoWrap) return;
            _screen[_row].Cells[_col] = BlankCell();
            _screen[_row].Wrapped = true;
            _col = 0;
            LineFeed();
        }

        var line = _screen[_row];
        var cells = line.Cells;
        if (_col >= cells.Length) return;
        if (_insertMode) ShiftRight(line, _col, width);
        ClearWideOverlap(cells, _col);
        if (width == 2) ClearWideOverlap(cells, _col + 1);
        cells[_col] = new Cell { CodePoint = cp, Width = (byte)width, Flags = _flags, Fg = _fg, Bg = _bg };
        if (width == 2 && _col + 1 < cells.Length)
            cells[_col + 1] = new Cell { CodePoint = ' ', Width = 0, Flags = _flags, Fg = _fg, Bg = _bg };
        line.Touch();

        _col += width;
        if (_col >= _cols)
        {
            _col = _cols - 1;
            _pendingWrap = true;
        }
    }

    private static void ClearWideOverlap(Cell[] cells, int col)
    {
        if (col >= cells.Length) return;
        if (cells[col].Width == 0 && col > 0) cells[col - 1] = Cell.Blank(cells[col - 1].Bg);
        if (cells[col].Width == 2 && col + 1 < cells.Length) cells[col + 1] = Cell.Blank(cells[col + 1].Bg);
    }

    private void ShiftRight(TerminalLine line, int from, int n)
    {
        var cells = line.Cells;
        for (var c = cells.Length - 1; c >= from + n; c--) cells[c] = cells[c - n];
        for (var c = from; c < Math.Min(cells.Length, from + n); c++) cells[c] = BlankCell();
    }

    // ------------------------------------------------------------------ erase / insert / delete

    private void EraseCells(int row, int from, int to)
    {
        var line = _screen[row];
        var cells = line.Cells;
        from = Math.Max(0, from);
        to = Math.Min(to, cells.Length);
        if (from >= to) return;
        // Erasing half of a wide glyph erases the other half too.
        if (from > 0 && cells[from].Width == 0) cells[from - 1] = BlankCell();
        if (to < cells.Length && cells[to].Width == 0) cells[to] = BlankCell();
        for (var c = from; c < to; c++) cells[c] = BlankCell();
        if (to >= cells.Length) line.Wrapped = false;
        line.Touch();
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 0:
                EraseCells(_row, _col, _cols);
                for (var r = _row + 1; r < _rows; r++) EraseCells(r, 0, _cols);
                break;
            case 1:
                for (var r = 0; r < _row; r++) EraseCells(r, 0, _cols);
                EraseCells(_row, 0, _col + 1);
                break;
            case 2:
                for (var r = 0; r < _rows; r++) EraseCells(r, 0, _cols);
                break;
            case 3:
                _scrollback.Clear();
                break;
        }
    }

    private void EraseLine(int mode)
    {
        switch (mode)
        {
            case 0: EraseCells(_row, _col, _cols); break;
            case 1: EraseCells(_row, 0, _col + 1); break;
            case 2: EraseCells(_row, 0, _cols); break;
        }
    }

    private void InsertChars(int n)
    {
        var line = _screen[_row];
        n = Math.Min(n, _cols - _col);
        ShiftRight(line, _col, n);
        line.Touch();
        _pendingWrap = false;
    }

    private void DeleteChars(int n)
    {
        var line = _screen[_row];
        var cells = line.Cells;
        n = Math.Min(n, cells.Length - _col);
        for (var c = _col; c < cells.Length - n; c++) cells[c] = cells[c + n];
        for (var c = cells.Length - n; c < cells.Length; c++) cells[c] = BlankCell();
        line.Touch();
        _pendingWrap = false;
    }

    private void InsertLines(int n)
    {
        if (_row < _top || _row > _bottom) return;
        n = Math.Min(n, _bottom - _row + 1);
        for (var i = 0; i < n; i++)
        {
            Array.Copy(_screen, _row, _screen, _row + 1, _bottom - _row);
            _screen[_row] = new TerminalLine(_cols, _bg);
        }
        _col = 0;
        _pendingWrap = false;
    }

    private void DeleteLines(int n)
    {
        if (_row < _top || _row > _bottom) return;
        n = Math.Min(n, _bottom - _row + 1);
        for (var i = 0; i < n; i++)
        {
            Array.Copy(_screen, _row + 1, _screen, _row, _bottom - _row);
            _screen[_bottom] = new TerminalLine(_cols, _bg);
        }
        _col = 0;
        _pendingWrap = false;
    }

    // ------------------------------------------------------------------ alternate screen / save restore

    private void SaveCursor() => _saved = new SavedCursor(_row, _col, _flags, _fg, _bg, _originMode, _pendingWrap, _g0Special);

    private void RestoreCursor()
    {
        _row = Math.Clamp(_saved.Row, 0, _rows - 1);
        _col = Math.Clamp(_saved.Col, 0, _cols - 1);
        _flags = _saved.Flags;
        _fg = _saved.Fg;
        _bg = _saved.Bg;
        _originMode = _saved.Origin;
        _pendingWrap = _saved.Wrap;
        _g0Special = _saved.G0Special;
    }

    private void EnterAlternate(bool saveCursor, bool clear)
    {
        if (IsAlternateScreen) return;
        if (saveCursor) _savedForAlt = new SavedCursor(_row, _col, _flags, _fg, _bg, _originMode, _pendingWrap, _g0Special);
        _screen = _alt;
        if (clear) foreach (var l in _alt) l.Clear(default);
        _top = 0;
        _bottom = _rows - 1;
        foreach (var l in _alt) l.Touch();
    }

    private void LeaveAlternate(bool restoreCursor)
    {
        if (!IsAlternateScreen) return;
        _screen = _main;
        _top = 0;
        _bottom = _rows - 1;
        foreach (var l in _main) l.Touch();
        if (restoreCursor)
        {
            var current = _saved;
            _saved = _savedForAlt;
            RestoreCursor();
            _saved = current;
        }
        ClampCursor();
    }

    /// <summary>Full reset (RIS).</summary>
    private void Reset()
    {
        _flags = CellFlags.None;
        _fg = default;
        _bg = default;
        _autoWrap = true;
        _originMode = false;
        _insertMode = false;
        _pendingWrap = false;
        _g0Special = _g1Special = _shiftOut = false;
        ApplicationCursorKeys = false;
        BracketedPaste = false;
        MouseTrackingRequested = false;
        CursorVisible = true;
        CursorStyle = CursorStyle.Block;
        if (IsAlternateScreen) LeaveAlternate(false);
        _top = 0;
        _bottom = _rows - 1;
        _tabs = NewTabStops(_cols);
        foreach (var l in _main) l.Clear(default);
        _row = _col = 0;
        _saved = SavedCursor.Initial;
    }

    private void RaiseReply(string text) => Reply?.Invoke(text);
    private void RaiseTitle(string title) { Title = title; TitleChanged?.Invoke(title); }
    private void RaiseBell() => Bell?.Invoke();
}
