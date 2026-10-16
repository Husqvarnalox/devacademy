using System.Text;

namespace DevAcademy.Terminal.Vt;

public sealed partial class TerminalEmulator
{
    private enum State { Ground, Escape, EscapeIntermediate, CsiEntry, CsiParam, CsiIntermediate, Osc, StringIgnore }

    private const int MaxParams = 32;

    private State _state = State.Ground;
    private readonly int[] _params = new int[MaxParams];
    private readonly bool[] _colon = new bool[MaxParams];
    private int _paramCount;
    private bool _paramStarted;
    private bool _nextIsColon;
    private char _privateMarker;
    private char _intermediate;
    private readonly StringBuilder _osc = new();
    private readonly Decoder _utf8 = new UTF8Encoding(false, false).GetDecoder();
    private char _highSurrogate;

    /// <summary>Feeds raw bytes from the PTY (UTF-8). Incomplete sequences are carried to the next call.</summary>
    public void Feed(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length) + 1];
        var n = _utf8.GetChars(bytes, chars, flush: false);
        Process(chars.AsSpan(0, n));
    }

    /// <summary>Feeds text directly (tests, local echo).</summary>
    public void Feed(string text) => Process(text.AsSpan());

    private void Process(ReadOnlySpan<char> chars)
    {
        foreach (var ch in chars)
        {
            if (char.IsHighSurrogate(ch)) { _highSurrogate = ch; continue; }
            if (char.IsLowSurrogate(ch))
            {
                if (_highSurrogate != 0) { Step(char.ConvertToUtf32(_highSurrogate, ch)); _highSurrogate = '\0'; }
                continue;
            }
            _highSurrogate = '\0';
            Step(ch);
        }
        Bump();
    }

    private void Step(int cp)
    {
        // Controls that act in every state.
        if (cp == 0x18 || cp == 0x1A) { _state = State.Ground; return; }
        if (cp == 0x1B)
        {
            if (_state == State.Osc) FinishOsc();
            _state = State.Escape;
            _intermediate = '\0';
            return;
        }

        switch (_state)
        {
            case State.Ground: Ground(cp); break;
            case State.Escape: EscapeState(cp); break;
            case State.EscapeIntermediate:
                if (cp < 0x20) Execute(cp);
                else if (cp is >= 0x20 and <= 0x2F) _intermediate = (char)cp;
                else { DispatchEscapeIntermediate((char)cp); _state = State.Ground; }
                break;
            case State.CsiEntry:
            case State.CsiParam:
            case State.CsiIntermediate:
                CsiState(cp);
                break;
            case State.Osc:
                if (cp == 0x07) { FinishOsc(); _state = State.Ground; }
                else if (cp >= 0x20) _osc.Append(char.ConvertFromUtf32(cp));
                break;
            case State.StringIgnore:
                if (cp == 0x07) _state = State.Ground;
                break;
        }
    }

    private void Ground(int cp)
    {
        if (cp < 0x20) Execute(cp);
        else if (cp == 0x7F) { }
        else Print(cp);
    }

    private void Execute(int cp)
    {
        switch (cp)
        {
            case 0x07: RaiseBell(); break;
            case 0x08: // BS
                if (_col > 0) _col--;
                _pendingWrap = false;
                break;
            case 0x09: // HT
                {
                    var c = _col + 1;
                    while (c < _cols - 1 && !_tabs[c]) c++;
                    _col = Math.Min(c, _cols - 1);
                    _pendingWrap = false;
                    break;
                }
            case 0x0A:
            case 0x0B:
            case 0x0C: // LF VT FF
                LineFeed();
                _pendingWrap = false;
                break;
            case 0x0D: // CR
                _col = 0;
                _pendingWrap = false;
                break;
            case 0x0E: _shiftOut = true; break;
            case 0x0F: _shiftOut = false; break;
        }
    }

    private void EscapeState(int cp)
    {
        if (cp < 0x20) { Execute(cp); return; }
        if (cp is >= 0x20 and <= 0x2F) { _intermediate = (char)cp; _state = State.EscapeIntermediate; return; }
        switch ((char)cp)
        {
            case '[':
                _state = State.CsiEntry;
                _paramCount = 0; _paramStarted = false; _nextIsColon = false; _privateMarker = '\0'; _intermediate = '\0';
                return;
            case ']': _osc.Clear(); _state = State.Osc; return;
            case 'P': case 'X': case '^': case '_': _state = State.StringIgnore; return;
        }
        DispatchEscape((char)cp);
        _state = State.Ground;
    }

    private void DispatchEscape(char final)
    {
        switch (final)
        {
            case '7': SaveCursor(); break;
            case '8': RestoreCursor(); break;
            case 'D': LineFeed(); _pendingWrap = false; break;
            case 'E': _col = 0; LineFeed(); _pendingWrap = false; break;
            case 'M': ReverseIndex(); _pendingWrap = false; break;
            case 'H': if (_col < _tabs.Length) _tabs[_col] = true; break;
            case 'c': Reset(); break;
            case '=': case '>': break; // keypad modes
            case '\\': break;          // string terminator
        }
    }

    private void DispatchEscapeIntermediate(char final)
    {
        switch (_intermediate)
        {
            case '(': _g0Special = final == '0'; break;
            case ')': _g1Special = final == '0'; break;
            case '#' when final == '8': // DECALN
                foreach (var l in _screen) { for (var c = 0; c < l.Cells.Length; c++) l.Cells[c] = new Cell { CodePoint = 'E', Width = 1 }; l.Touch(); }
                break;
        }
    }

    // ------------------------------------------------------------------ CSI

    private void CsiState(int cp)
    {
        if (cp < 0x20) { Execute(cp); return; }
        if (cp >= '0' && cp <= '9')
        {
            if (_state == State.CsiIntermediate) return;
            _state = State.CsiParam;
            if (!_paramStarted) { StartParam(); }
            var i = _paramCount - 1;
            if (i < MaxParams) _params[i] = Math.Min(_params[i] < 0 ? 0 : _params[i], 99999) * 10 + (cp - '0');
            return;
        }
        if (cp is ';' or ':')
        {
            if (_state == State.CsiIntermediate) return;
            _state = State.CsiParam;
            if (!_paramStarted) StartParam(); // empty parameter before the separator
            _paramStarted = false;
            _nextIsColon = cp == ':';
            return;
        }
        if (cp is '<' or '=' or '>' or '?')
        {
            if (_state == State.CsiEntry) { _privateMarker = (char)cp; _state = State.CsiParam; }
            return;
        }
        if (cp is >= 0x20 and <= 0x2F) { _intermediate = (char)cp; _state = State.CsiIntermediate; return; }
        if (cp is >= 0x40 and <= 0x7E)
        {
            DispatchCsi((char)cp);
            _state = State.Ground;
        }
        else _state = State.Ground;
    }

    private void StartParam()
    {
        if (_paramCount < MaxParams)
        {
            _params[_paramCount] = -1;
            _colon[_paramCount] = _nextIsColon;
            _paramCount++;
        }
        _paramStarted = true;
        _nextIsColon = false;
    }

    /// <summary>Parameter i, or <paramref name="def"/> when absent/empty.</summary>
    private int P(int i, int def) => i < _paramCount && _params[i] >= 0 ? _params[i] : def;

    /// <summary>Count for movement/insert/delete commands: absent or 0 means 1.</summary>
    private int N(int i = 0) => Math.Max(1, P(i, 1));

    private void DispatchCsi(char final)
    {
        if (_privateMarker == '?') { DispatchPrivateCsi(final); return; }
        if (_privateMarker == '>')
        {
            if (final == 'c') RaiseReply("\e[>0;10;0c");
            return;
        }
        if (_privateMarker != '\0') return;
        if (_intermediate == ' ')
        {
            if (final == 'q')
            {
                CursorStyle = P(0, 0) switch { 3 or 4 => CursorStyle.Underline, 5 or 6 => CursorStyle.Bar, _ => CursorStyle.Block };
            }
            return;
        }
        if (_intermediate != '\0') return;

        switch (final)
        {
            case '@': InsertChars(N()); break;
            case 'A': _row = Math.Max(_row - N(), _row >= _top ? _top : 0); _pendingWrap = false; break;
            case 'B': _row = Math.Min(_row + N(), _row <= _bottom ? _bottom : _rows - 1); _pendingWrap = false; break;
            case 'C': case 'a': _col = Math.Min(_col + N(), _cols - 1); _pendingWrap = false; break;
            case 'D': _col = Math.Max(_col - N(), 0); _pendingWrap = false; break;
            case 'E': _row = Math.Min(_row + N(), _rows - 1); _col = 0; _pendingWrap = false; break;
            case 'F': _row = Math.Max(_row - N(), 0); _col = 0; _pendingWrap = false; break;
            case 'G': case '`': _col = Math.Clamp(N() - 1, 0, _cols - 1); _pendingWrap = false; break;
            case 'H': case 'f': MoveTo(N(0) - 1, N(1) - 1); break;
            case 'I': for (var k = N(); k > 0; k--) Execute(0x09); break;
            case 'J': EraseDisplay(P(0, 0)); break;
            case 'K': EraseLine(P(0, 0)); break;
            case 'L': InsertLines(N()); break;
            case 'M': DeleteLines(N()); break;
            case 'P': DeleteChars(N()); break;
            case 'S': ScrollUp(N()); break;
            case 'T': ScrollDown(N()); break;
            case 'X': EraseCells(_row, _col, _col + N()); break;
            case 'Z':
                for (var k = N(); k > 0; k--)
                {
                    var c = _col - 1;
                    while (c > 0 && !_tabs[c]) c--;
                    _col = Math.Max(c, 0);
                }
                break;
            case 'b': if (_lastPrinted != 0) for (var k = Math.Min(N(), 10000); k > 0; k--) Print(_lastPrinted); break;
            case 'c': if (P(0, 0) == 0) RaiseReply("\e[?1;2c"); break;
            case 'd': _row = _originMode ? Math.Clamp(N() - 1 + _top, _top, _bottom) : Math.Clamp(N() - 1, 0, _rows - 1); _pendingWrap = false; break;
            case 'e': _row = Math.Min(_row + N(), _rows - 1); _pendingWrap = false; break;
            case 'g':
                if (P(0, 0) == 3) Array.Clear(_tabs);
                else if (P(0, 0) == 0 && _col < _tabs.Length) _tabs[_col] = false;
                break;
            case 'h': SetAnsiMode(true); break;
            case 'l': SetAnsiMode(false); break;
            case 'm': Sgr(); break;
            case 'n':
                if (P(0, 0) == 5) RaiseReply("\e[0n");
                else if (P(0, 0) == 6) RaiseReply($"\e[{(_originMode ? _row - _top : _row) + 1};{CursorCol + 1}R");
                break;
            case 'r':
                {
                    var top = N(0) - 1;
                    var bottom = P(1, _rows) <= 0 ? _rows - 1 : Math.Min(P(1, _rows), _rows) - 1;
                    if (top < bottom) { _top = top; _bottom = bottom; }
                    MoveTo(0, 0);
                    break;
                }
            case 's': SaveCursor(); break;
            case 'u': RestoreCursor(); break;
        }
    }

    private void SetAnsiMode(bool on)
    {
        for (var i = 0; i < Math.Max(1, _paramCount); i++)
            if (P(i, 0) == 4) _insertMode = on;
    }

    private void DispatchPrivateCsi(char final)
    {
        if (final is not ('h' or 'l')) return;
        var on = final == 'h';
        for (var i = 0; i < _paramCount; i++)
        {
            switch (P(i, 0))
            {
                case 1: ApplicationCursorKeys = on; break;
                case 6: _originMode = on; MoveTo(0, 0); break;
                case 7: _autoWrap = on; break;
                case 25: CursorVisible = on; break;
                case 47: case 1047: if (on) EnterAlternate(false, false); else LeaveAlternate(false); break;
                case 1048: if (on) SaveCursor(); else RestoreCursor(); break;
                case 1049: if (on) EnterAlternate(true, true); else LeaveAlternate(true); break;
                case 2004: BracketedPaste = on; break;
                case 1000: case 1002: case 1003: case 1006: MouseTrackingRequested = on; break;
            }
        }
    }

    // ------------------------------------------------------------------ SGR

    private void Sgr()
    {
        if (_paramCount == 0) { ResetPen(); return; }
        for (var i = 0; i < _paramCount; i++)
        {
            var p = P(i, 0);
            switch (p)
            {
                case 0: ResetPen(); break;
                case 1: _flags |= CellFlags.Bold; break;
                case 2: _flags |= CellFlags.Faint; break;
                case 3: _flags |= CellFlags.Italic; break;
                case 4:
                    if (i + 1 < _paramCount && _colon[i + 1]) { if (P(i + 1, 0) == 0) _flags &= ~CellFlags.Underline; else _flags |= CellFlags.Underline; i++; }
                    else _flags |= CellFlags.Underline;
                    break;
                case 5: case 6: _flags |= CellFlags.Blink; break;
                case 7: _flags |= CellFlags.Inverse; break;
                case 8: _flags |= CellFlags.Hidden; break;
                case 9: _flags |= CellFlags.Strike; break;
                case 21: _flags |= CellFlags.Underline; break;
                case 22: _flags &= ~(CellFlags.Bold | CellFlags.Faint); break;
                case 23: _flags &= ~CellFlags.Italic; break;
                case 24: _flags &= ~CellFlags.Underline; break;
                case 25: _flags &= ~CellFlags.Blink; break;
                case 27: _flags &= ~CellFlags.Inverse; break;
                case 28: _flags &= ~CellFlags.Hidden; break;
                case 29: _flags &= ~CellFlags.Strike; break;
                case >= 30 and <= 37: _fg = TermColor.Indexed(p - 30); break;
                case 38: if (ExtendedColor(ref i, out var f)) _fg = f; break;
                case 39: _fg = default; break;
                case >= 40 and <= 47: _bg = TermColor.Indexed(p - 40); break;
                case 48: if (ExtendedColor(ref i, out var b)) _bg = b; break;
                case 49: _bg = default; break;
                case >= 90 and <= 97: _fg = TermColor.Indexed(p - 90 + 8); break;
                case >= 100 and <= 107: _bg = TermColor.Indexed(p - 100 + 8); break;
            }
        }
    }

    private void ResetPen()
    {
        _flags = CellFlags.None;
        _fg = default;
        _bg = default;
    }

    /// <summary>Parses the arguments after 38/48 in either "38;5;n" / "38;2;r;g;b" or "38:5:n" / "38:2:[cs]:r:g:b" form.</summary>
    private bool ExtendedColor(ref int i, out TermColor color)
    {
        color = default;
        if (i + 1 >= _paramCount) { i = _paramCount; return false; }
        if (_colon[i + 1])
        {
            var args = new List<int>();
            var j = i + 1;
            while (j < _paramCount && _colon[j]) { args.Add(_params[j]); j++; }
            i = j - 1;
            if (args.Count >= 2 && args[0] == 5) { color = TermColor.Indexed(Math.Max(0, args[1])); return true; }
            if (args[0] == 2)
            {
                var o = args.Count >= 5 ? 2 : 1;
                if (args.Count >= o + 3) { color = TermColor.Rgb(Math.Max(0, args[o]), Math.Max(0, args[o + 1]), Math.Max(0, args[o + 2])); return true; }
            }
            return false;
        }
        var kind = P(i + 1, 0);
        if (kind == 5)
        {
            if (i + 2 >= _paramCount) { i = _paramCount; return false; }
            color = TermColor.Indexed(Math.Clamp(P(i + 2, 0), 0, 255));
            i += 2;
            return true;
        }
        if (kind == 2)
        {
            if (i + 4 >= _paramCount) { i = _paramCount; return false; }
            color = TermColor.Rgb(Math.Clamp(P(i + 2, 0), 0, 255), Math.Clamp(P(i + 3, 0), 0, 255), Math.Clamp(P(i + 4, 0), 0, 255));
            i += 4;
            return true;
        }
        i += 1;
        return false;
    }

    // ------------------------------------------------------------------ OSC

    private void FinishOsc()
    {
        var text = _osc.ToString();
        _osc.Clear();
        var sep = text.IndexOf(';');
        if (sep < 0) return;
        if (text[..sep] is "0" or "2") RaiseTitle(text[(sep + 1)..]);
    }
}
