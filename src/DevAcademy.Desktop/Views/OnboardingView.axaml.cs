using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DevAcademy.Desktop.Views;

public partial class OnboardingView : UserControl
{
    private bool _userInteracted;

    public OnboardingView()
    {
        InitializeComponent();
        // The preselected goal/level/depth rows ask to be brought into view while the page lays out, which would open it
        // scrolled to the middle. Keep the page at the top until the learner scrolls, clicks or presses a key.
        AddHandler(PointerPressedEvent, (_, _) => _userInteracted = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, (_, _) => _userInteracted = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, _) => _userInteracted = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        Scroller.ScrollChanged += (_, _) =>
        {
            if (!_userInteracted && Scroller.Offset.Y > 0) Scroller.Offset = new Vector(Scroller.Offset.X, 0);
        };
    }
}
