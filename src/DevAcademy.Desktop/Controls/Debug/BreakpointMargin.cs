using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace DevAcademy.Desktop.Controls.Debug;

/// <summary>Gutter column left of the line numbers: a click toggles a breakpoint; red dots mark breakpoints, an arrow marks the paused line.</summary>
public sealed class BreakpointMargin : AbstractMargin
{
    public const double MarginWidth = 18;
    private readonly Func<IBrush?> _breakpointBrush;
    private readonly Func<IBrush?> _arrowBrush;
    private EditorDebugTarget? _target;
    private int _hoverLine;

    public BreakpointMargin(Func<IBrush?> breakpointBrush, Func<IBrush?> arrowBrush)
    {
        _breakpointBrush = breakpointBrush;
        _arrowBrush = arrowBrush;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public EditorDebugTarget? Target
    {
        get => _target;
        set
        {
            if (_target is not null) _target.Invalidated -= InvalidateVisual;
            _target = value;
            if (_target is not null) _target.Invalidated += InvalidateVisual;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(MarginWidth, 0);

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView is not null) oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView is not null) newTextView.VisualLinesChanged += OnVisualLinesChanged;
        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var view = TextView;
        if (view is null || !view.VisualLinesValid || _target is null) return;
        var bp = _breakpointBrush();
        var arrow = _arrowBrush();
        foreach (var line in view.VisualLines)
        {
            int number = line.FirstDocumentLine.LineNumber;
            double top = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
            double height = line.TextLines[0].Height;
            var center = new Point(MarginWidth / 2, top + height / 2);
            double r = Math.Min(MarginWidth, height) / 2 - 3;
            if (_target.Has(number) && bp is not null) context.DrawEllipse(bp, null, center, r, r);
            else if (number == _hoverLine && bp is not null)
                context.DrawEllipse(null, new Pen(bp, 1), center, r, r);
            if (_target.ExecutionLine == number && arrow is not null)
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(3, center.Y - r), true);
                    c.LineTo(new Point(MarginWidth - 3, center.Y));
                    c.LineTo(new Point(3, center.Y + r));
                    c.EndFigure(true);
                }
                context.DrawGeometry(arrow, null, g);
            }
        }
    }

    private int LineAt(PointerEventArgs e)
    {
        var view = TextView;
        if (view is null || !view.VisualLinesValid) return 0;
        double y = e.GetPosition(this).Y + view.VerticalOffset;
        var line = view.GetVisualLineFromVisualTop(y);
        return line?.FirstDocumentLine.LineNumber ?? 0;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_target is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        int line = LineAt(e);
        if (line > 0) { _target.Toggle(line); e.Handled = true; }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        int line = LineAt(e);
        if (line == _hoverLine) return;
        _hoverLine = line;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverLine = 0;
        InvalidateVisual();
    }
}
