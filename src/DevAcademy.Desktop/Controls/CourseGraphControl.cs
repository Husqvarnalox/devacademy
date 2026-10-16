using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace DevAcademy.Desktop.Controls;

/// <param name="Fraction">Share of the course's lessons already known (0..1); drawn as a progress fill.</param>
public sealed record GraphNode(string Id, string Title, string Caption, double Fraction);

/// <summary>An edge from a prerequisite course to the course that needs it.</summary>
public sealed record GraphLink(string FromId, string ToId);

/// <summary>Nodes grouped in layers by dependency depth (layer 0 has no prerequisites inside the graph).</summary>
public sealed record CourseGraphModel(IReadOnlyList<IReadOnlyList<GraphNode>> Layers, IReadOnlyList<GraphLink> Links)
{
    public static CourseGraphModel Empty { get; } = new([], []);
    public int NodeCount => Layers.Sum(l => l.Count);
}

/// <summary>
/// Layered dependency diagram: one column per layer, nodes ordered inside a layer by the average position of their
/// prerequisites (fewer crossings), edges as curves with arrowheads. Drawn directly; no graph library.
/// </summary>
public sealed class CourseGraphControl : Control
{
    public const double NodeWidth = 200, NodeHeight = 50, GapX = 70, GapY = 14, Padding = 12;

    public static readonly StyledProperty<CourseGraphModel?> ModelProperty = AvaloniaProperty.Register<CourseGraphControl, CourseGraphModel?>(nameof(Model));
    public static readonly StyledProperty<ICommand?> NodeCommandProperty = AvaloniaProperty.Register<CourseGraphControl, ICommand?>(nameof(NodeCommand));

    public CourseGraphModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <summary>Executed with the node id when a node is clicked.</summary>
    public ICommand? NodeCommand { get => GetValue(NodeCommandProperty); set => SetValue(NodeCommandProperty, value); }

    static CourseGraphControl()
    {
        AffectsMeasure<CourseGraphControl>(ModelProperty);
        AffectsRender<CourseGraphControl>(ModelProperty);
    }

    public CourseGraphControl() => Cursor = new Cursor(StandardCursorType.Arrow);

    /// <summary>Node rectangles in control coordinates (exposed for tests).</summary>
    public IReadOnlyDictionary<string, Rect> Layout() => ComputeLayout(Model ?? CourseGraphModel.Empty).Rects;

    internal static (Dictionary<string, Rect> Rects, Size Size) ComputeLayout(CourseGraphModel model)
    {
        var rects = new Dictionary<string, Rect>(StringComparer.Ordinal);
        if (model.Layers.Count == 0) return (rects, new Size(0, 0));

        var preds = model.Links.GroupBy(l => l.ToId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(l => l.FromId).ToList(), StringComparer.Ordinal);
        var order = new Dictionary<string, double>(StringComparer.Ordinal);
        var arranged = new List<List<GraphNode>>();
        foreach (var layer in model.Layers)
        {
            var sorted = layer
                .Select((n, i) => (Node: n, Key: preds.TryGetValue(n.Id, out var p) && p.Count(order.ContainsKey) > 0 ? p.Where(order.ContainsKey).Average(x => order[x]) : i, Index: i))
                .OrderBy(x => x.Key).ThenBy(x => x.Index).Select(x => x.Node).ToList();
            for (var i = 0; i < sorted.Count; i++) order[sorted[i].Id] = i;
            arranged.Add(sorted);
        }

        var tallest = arranged.Max(l => l.Count);
        var totalHeight = tallest * NodeHeight + (tallest - 1) * GapY;
        for (var c = 0; c < arranged.Count; c++)
        {
            var layerHeight = arranged[c].Count * NodeHeight + (arranged[c].Count - 1) * GapY;
            var top = Padding + (totalHeight - layerHeight) / 2;
            for (var r = 0; r < arranged[c].Count; r++)
                rects[arranged[c][r].Id] = new Rect(Padding + c * (NodeWidth + GapX), top + r * (NodeHeight + GapY), NodeWidth, NodeHeight);
        }
        return (rects, new Size(2 * Padding + arranged.Count * NodeWidth + (arranged.Count - 1) * GapX, 2 * Padding + totalHeight));
    }

    protected override Size MeasureOverride(Size availableSize) => ComputeLayout(Model ?? CourseGraphModel.Empty).Size;

    private IBrush Brush(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(Color.Parse(fallback));

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var model = Model ?? CourseGraphModel.Empty;
        var (rects, _) = ComputeLayout(model);
        var line = Brush("LineBrush", "#B0B0A8");
        var edgePen = new Pen(Brush("MutedText", "#6A6A64"), 1.4);
        var panel = Brush("PanelBrush", "#FFFFFF");
        var text = Brush("MutedText", "#6A6A64");
        var done = Brush("SuccessBrush", "#2E7D32");
        var titleBrush = this.TryFindResource("TextControlForeground", ActualThemeVariant, out var fg) && fg is IBrush f ? f : Brushes.Gray;
        var typeface = new Typeface(FontFamily.Default);
        var semibold = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

        foreach (var link in model.Links)
        {
            if (!rects.TryGetValue(link.FromId, out var a) || !rects.TryGetValue(link.ToId, out var b)) continue;
            var start = new Point(a.Right, a.Center.Y);
            var end = new Point(b.Left, b.Center.Y);
            var dx = Math.Max(24, (end.X - start.X) / 2);
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(start, false);
                g.CubicBezierTo(new Point(start.X + dx, start.Y), new Point(end.X - dx, end.Y), end);
                g.EndFigure(false);
            }
            context.DrawGeometry(null, edgePen, geometry);
            var head = new StreamGeometry();
            using (var g = head.Open())
            {
                g.BeginFigure(end, true);
                g.LineTo(new Point(end.X - 8, end.Y - 4));
                g.LineTo(new Point(end.X - 8, end.Y + 4));
                g.EndFigure(true);
            }
            context.DrawGeometry(edgePen.Brush, null, head);
        }

        foreach (var layer in model.Layers)
            foreach (var node in layer)
            {
                if (!rects.TryGetValue(node.Id, out var r)) continue;
                var round = new RoundedRect(r, 6);
                context.DrawRectangle(panel, new Pen(line, 1), round);
                if (node.Fraction > 0)
                {
                    var fill = new Rect(r.X + 1, r.Bottom - 5, (r.Width - 2) * Math.Clamp(node.Fraction, 0, 1), 4);
                    context.DrawRectangle(done, null, new RoundedRect(fill, 2));
                }
                var title = new FormattedText(node.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, semibold, 13, titleBrush)
                { MaxTextWidth = NodeWidth - 16, MaxTextHeight = 18, Trimming = TextTrimming.CharacterEllipsis };
                context.DrawText(title, new Point(r.X + 8, r.Y + 6));
                var caption = new FormattedText(node.Caption, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, text)
                { MaxTextWidth = NodeWidth - 16, MaxTextHeight = 14, Trimming = TextTrimming.CharacterEllipsis };
                context.DrawText(caption, new Point(r.X + 8, r.Y + 25));
            }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (NodeCommand is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        foreach (var (id, rect) in Layout())
            if (rect.Contains(p) && NodeCommand.CanExecute(id))
            {
                NodeCommand.Execute(id);
                e.Handled = true;
                return;
            }
    }
}
