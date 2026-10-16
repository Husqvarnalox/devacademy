using Avalonia.Controls;
using Avalonia.Threading;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

/// <summary>Owns its view model (embedded from Markdown) and drives playback with a timer: one tick = one stage.</summary>
public partial class PcaView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(PcaViewModel.IntervalMs) };

    public PcaView() : this(new PcaViewModel()) { }

    public PcaView(PcaViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _timer.Tick += (_, _) => vm.Tick();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PcaViewModel.IsPlaying)) return;
            if (vm.IsPlaying) _timer.Start(); else _timer.Stop();
        };
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); vm.IsPlaying = false; };
    }

    public PcaViewModel ViewModel => (PcaViewModel)DataContext!;
}
