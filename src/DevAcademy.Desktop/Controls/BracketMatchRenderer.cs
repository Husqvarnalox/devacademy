using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace DevAcademy.Desktop.Controls;

/// <summary>Quietly highlights the bracket next to the caret and its partner (ignores brackets inside strings and comments: a hint, not a parser).</summary>
internal sealed class BracketMatchRenderer : IBackgroundRenderer
{
    private const string Open = "([{";
    private const string Close = ")]}";
    private const int MaxScan = 20_000;

    private readonly TextArea _area;
    private (int A, int B)? _pair;

    public IBrush? Fill { get; set; }
    public IPen? Outline { get; set; }

    public BracketMatchRenderer(TextArea area)
    {
        _area = area;
        area.Caret.PositionChanged += (_, _) => Update();
        area.TextView.DocumentChanged += (_, _) => Update();
    }

    public KnownLayer Layer => KnownLayer.Selection;

    private void Update()
    {
        var previous = _pair;
        _pair = Find();
        if (previous != _pair) _area.TextView.InvalidateLayer(Layer);
    }

    private (int, int)? Find()
    {
        var doc = _area.Document;
        if (doc is null) return null;
        int caret = _area.Caret.Offset;
        foreach (int at in new[] { caret - 1, caret })
        {
            if (at < 0 || at >= doc.TextLength) continue;
            char c = doc.GetCharAt(at);
            int oi = Open.IndexOf(c), ci = Close.IndexOf(c);
            if (oi >= 0 && Scan(doc, at, 1, c, Close[oi]) is { } m1) return (at, m1);
            if (ci >= 0 && Scan(doc, at, -1, c, Open[ci]) is { } m2) return (m2, at);
        }
        return null;
    }

    private static int? Scan(TextDocument doc, int start, int step, char same, char other)
    {
        int depth = 0;
        for (int i = start, n = 0; i >= 0 && i < doc.TextLength && n < MaxScan; i += step, n++)
        {
            char c = doc.GetCharAt(i);
            if (c == same) depth++;
            else if (c == other && --depth == 0) return i;
        }
        return null;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_pair is not { } pair || Fill is null || textView.Document is null) return;
        foreach (int offset in new[] { pair.A, pair.B })
        {
            var segment = new TextSegment { StartOffset = offset, Length = 1 };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                drawingContext.DrawRectangle(Fill, Outline, new Rect(rect.Position, rect.Size));
        }
    }
}
