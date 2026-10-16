using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using DevAcademy.LanguageServices.Protocol;

namespace DevAcademy.Desktop.Controls.Lsp;

/// <summary>Draws wavy underlines for the server's diagnostics, coloured from the active theme's Danger/Warn/Accent brushes.</summary>
public sealed class DiagnosticRenderer(Func<IReadOnlyList<Diagnostic>> diagnostics, Func<PositionEncoding> encoding, Func<string, IBrush?> brushFor) : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Resolves a diagnostic's range to document offsets; null when it no longer fits the document.</summary>
    public static (int Start, int End)? ToSegment(TextDocument doc, Diagnostic d, PositionEncoding enc)
    {
        try
        {
            var buf = new TextDocumentBuffer(doc);
            if (d.Range.Start.Line >= doc.LineCount) return null;
            var start = PositionConverter.PositionToOffset(buf, d.Range.Start, enc);
            var end = PositionConverter.PositionToOffset(buf, d.Range.End, enc);
            if (end <= start)
            {
                // Zero-width marks (e.g. "missing ;") are widened to the next character, or to the previous one at the line end.
                var line = doc.GetLineByOffset(Math.Min(start, doc.TextLength));
                if (start < line.EndOffset) end = start + 1;
                else if (start > line.Offset) { end = start; start--; }
                else return null;
            }
            return (start, Math.Min(end, doc.TextLength));
        }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    public void Draw(TextView textView, DrawingContext context)
    {
        var doc = textView.Document;
        var list = diagnostics();
        if (doc is null || list.Count == 0 || !textView.VisualLinesValid) return;
        var enc = encoding();
        foreach (var d in list)
        {
            if (ToSegment(doc, d, enc) is not { } seg || seg.End <= seg.Start) continue;
            var brush = brushFor(Key(d.EffectiveSeverity));
            if (brush is null) continue;
            var pen = new Pen(brush, 1.2);
            foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = seg.Start, EndOffset = seg.End }))
                DrawWave(context, pen, r.Left, r.Right, r.Bottom - 1.5);
        }
    }

    public static string Key(DiagnosticSeverity s) => s switch
    {
        DiagnosticSeverity.Error => "DangerBrush",
        DiagnosticSeverity.Warning => "WarnBrush",
        DiagnosticSeverity.Information => "AccentText",
        _ => "MutedText",
    };

    private static void DrawWave(DrawingContext ctx, Pen pen, double x0, double x1, double y)
    {
        if (x1 - x0 < 2) x1 = x0 + 2;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x0, y), false);
            var up = false;
            for (var x = x0 + 2; x < x1 + 2; x += 2, up = !up)
                c.LineTo(new Point(Math.Min(x, x1), up ? y : y + 2));
            c.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, g);
    }
}
