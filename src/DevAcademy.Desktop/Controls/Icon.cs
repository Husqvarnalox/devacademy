using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DevAcademy.Desktop.Controls;

/// <summary>
/// A Lucide icon (see Styles/Icons.axaml) drawn as a round-capped stroke in the inherited foreground colour.
/// <c>&lt;ctl:Icon Kind="play" /&gt;</c>; Size is the rendered edge in px (the geometry is authored on a 24 px grid).
/// </summary>
public sealed class Icon : Control
{
    public static readonly StyledProperty<string?> KindProperty = AvaloniaProperty.Register<Icon, string?>(nameof(Kind));
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<Icon, double>(nameof(Size), 16);
    public static readonly StyledProperty<double> StrokeWidthProperty = AvaloniaProperty.Register<Icon, double>(nameof(StrokeWidth), 1.75);
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<Icon, IBrush?>(nameof(Stroke));

    private Geometry? _geometry;

    static Icon()
    {
        AffectsRender<Icon>(KindProperty, StrokeWidthProperty, StrokeProperty, TextElement.ForegroundProperty);
        AffectsMeasure<Icon>(SizeProperty);
        KindProperty.Changed.AddClassHandler<Icon>((i, _) => i.Resolve());
    }

    public string? Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }
    /// <summary>Overrides the inherited foreground (used for status markers that carry their own colour).</summary>
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Resolve();
    }

    private void Resolve()
    {
        _geometry = null;
        if (Kind is { Length: > 0 } k && this.TryFindResource("Icon." + k, ActualThemeVariant, out var g) && g is Geometry geometry)
            _geometry = geometry;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (_geometry is null) return;
        var brush = Stroke ?? TextElement.GetForeground(this);
        if (brush is null) return;
        var scale = Size / 24.0;
        var pen = new Pen(brush, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            context.DrawGeometry(null, pen, _geometry);
    }
}
