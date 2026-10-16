namespace DevAcademy.Terminal.Vt;

/// <summary>A terminal colour: the theme default, one of the 256 palette entries, or 24-bit RGB.</summary>
public readonly record struct TermColor(uint Value)
{
    public static TermColor Default => default;
    public static TermColor Indexed(int index) => new(0x01000000u | (uint)(index & 0xFF));
    public static TermColor Rgb(int r, int g, int b) => new(0x02000000u | (uint)((r & 0xFF) << 16 | (g & 0xFF) << 8 | (b & 0xFF)));

    public bool IsDefault => Value == 0;
    public bool IsIndexed => (Value >> 24) == 1;
    public bool IsRgb => (Value >> 24) == 2;
    public int Index => (int)(Value & 0xFF);
    public byte R => (byte)(Value >> 16);
    public byte G => (byte)(Value >> 8);
    public byte B => (byte)Value;
}

[Flags]
public enum CellFlags : byte
{
    None = 0,
    Bold = 1,
    Faint = 2,
    Italic = 4,
    Underline = 8,
    Blink = 16,
    Inverse = 32,
    Hidden = 64,
    Strike = 128,
}

/// <summary>One grid cell. <see cref="Width"/> is 1, 2 (wide glyph head) or 0 (the right half of a wide glyph).</summary>
public struct Cell
{
    public int CodePoint;
    public byte Width;
    public CellFlags Flags;
    public TermColor Fg;
    public TermColor Bg;

    public static Cell Blank(TermColor bg = default) => new() { CodePoint = ' ', Width = 1, Bg = bg };
    public readonly bool IsBlank => CodePoint == ' ' && Bg.IsDefault && Flags == CellFlags.None;
}

/// <summary>
/// A row of cells. <see cref="Version"/> changes whenever the content changes, so a renderer can cache per-row work and rebuild
/// only dirty rows; <see cref="Tag"/>/<see cref="TagVersion"/> are the renderer's cache slot.
/// </summary>
public sealed class TerminalLine
{
    private static int _counter;

    public TerminalLine(int cols, TermColor bg = default)
    {
        Cells = new Cell[cols];
        Array.Fill(Cells, Cell.Blank(bg));
        Version = Interlocked.Increment(ref _counter);
    }

    public Cell[] Cells { get; set; }
    /// <summary>True when the text continues on the next row because of auto-wrap (not a hard line break).</summary>
    public bool Wrapped { get; set; }
    public int Version { get; private set; }
    public object? Tag { get; set; }
    public int TagVersion { get; set; }

    public void Touch() => Version = Interlocked.Increment(ref _counter);

    public void Clear(TermColor bg)
    {
        Array.Fill(Cells, Cell.Blank(bg));
        Wrapped = false;
        Touch();
    }

    public void Resize(int cols, TermColor bg)
    {
        if (cols == Cells.Length) return;
        var cells = new Cell[cols];
        var keep = Math.Min(cols, Cells.Length);
        Array.Copy(Cells, cells, keep);
        for (var i = keep; i < cols; i++) cells[i] = Cell.Blank(bg);
        // Never leave the left half of a wide glyph at the new right edge.
        if (keep > 0 && keep == cols && cells[keep - 1].Width == 2) cells[keep - 1] = Cell.Blank(bg);
        Cells = cells;
        Touch();
    }
}

/// <summary>Fixed-capacity ring of lines; the oldest line is dropped when full.</summary>
internal sealed class LineRing(int capacity)
{
    private readonly TerminalLine[] _items = new TerminalLine[capacity];
    private int _head;

    public int Count { get; private set; }
    public int Capacity => capacity;

    /// <summary>Appends a line; returns true when the oldest line was dropped to make room.</summary>
    public bool Push(TerminalLine line)
    {
        if (capacity == 0) return true;
        if (Count < capacity)
        {
            _items[(_head + Count) % capacity] = line;
            Count++;
            return false;
        }
        _items[_head] = line;
        _head = (_head + 1) % capacity;
        return true;
    }

    public TerminalLine this[int index] => _items[(_head + index) % capacity];

    public TerminalLine? PopNewest()
    {
        if (Count == 0) return null;
        var i = (_head + Count - 1) % capacity;
        var line = _items[i];
        _items[i] = null!;
        Count--;
        return line;
    }

    public void Clear()
    {
        Array.Clear(_items);
        _head = 0;
        Count = 0;
    }
}
