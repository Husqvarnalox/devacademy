using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace DevAcademy.Desktop.Controls.Lsp;

/// <summary>The three non-modal popups an editor can show: hover/diagnostic text, signature help and a read-only definition peek.</summary>
public sealed class LspPopups
{
    private readonly TextView _view;

    public LspPopups(TextView view)
    {
        _view = view;
        Hover = Make();
        Signature = Make();
        Peek = Make();
    }

    public Popup Hover { get; }
    public Popup Signature { get; }
    public Popup Peek { get; }
    public IEnumerable<Popup> All => [Hover, Signature, Peek];

    private Popup Make() => new()
    {
        PlacementTarget = _view,
        Placement = PlacementMode.AnchorAndGravity,
        PlacementAnchor = PopupAnchor.BottomLeft,
        PlacementGravity = PopupGravity.BottomRight,
        IsLightDismissEnabled = false,
        Focusable = false,
        IsHitTestVisible = true,
    };

    /// <summary>Shows <paramref name="content"/> in <paramref name="popup"/> under the text rectangle <paramref name="anchor"/> (in TextView coordinates).</summary>
    public void Show(Popup popup, Control content, Rect anchor, bool above = false)
    {
        var border = new Border
        {
            Child = content,
            Padding = new Thickness(8, 6),
            MaxWidth = 560,
            MaxHeight = 320,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
        };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("PanelBrush"));
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("LineBrush"));
        popup.Child = border;
        popup.PlacementRect = anchor;
        popup.PlacementAnchor = above ? PopupAnchor.TopLeft : PopupAnchor.BottomLeft;
        popup.PlacementGravity = above ? PopupGravity.TopRight : PopupGravity.BottomRight;
        popup.IsOpen = true;
    }

    public static void Close(Popup p) { if (p.IsOpen) p.IsOpen = false; }
    public void CloseAll() { foreach (var p in All) Close(p); }

    public static TextBlock Plain(string text, bool mono = false)
    {
        var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Top };
        if (mono) tb.FontFamily = new FontFamily("Menlo, Consolas, 'DejaVu Sans Mono', monospace");
        return tb;
    }
}
