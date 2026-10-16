using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace DevAcademy.Desktop.Controls.Debug;

/// <summary>Highlights the line the debugger is paused on.</summary>
public sealed class ExecutionLineRenderer(Func<EditorDebugTarget?> target, Func<IBrush?> brush) : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext context)
    {
        if (target()?.ExecutionLine is not { } line || brush() is not { } fill || !textView.VisualLinesValid) return;
        foreach (var v in textView.VisualLines)
        {
            if (v.FirstDocumentLine.LineNumber != line) continue;
            double top = v.GetTextLineVisualYPosition(v.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
            context.FillRectangle(fill, new Rect(0, top, Math.Max(textView.Bounds.Width, textView.DocumentHeight), v.Height));
        }
    }
}
