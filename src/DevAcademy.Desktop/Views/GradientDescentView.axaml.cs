using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

/// <summary>Owns its view model (embedded from Markdown) and drives playback with a timer: one tick = one trace step.</summary>
public partial class GradientDescentView : UserControl
{
    private readonly DispatcherTimer _timer = new();

    public GradientDescentView() : this(new GradientDescentViewModel()) { }

    public GradientDescentView(GradientDescentViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _timer.Tick += (_, _) => vm.Tick();
        vm.PropertyChanged += OnVmChanged;
        Plot.StartPicked += vm.SetStart;
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); vm.IsPlaying = false; };
    }

    public GradientDescentViewModel ViewModel => (GradientDescentViewModel)DataContext!;

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GradientDescentViewModel.IsPlaying) or nameof(GradientDescentViewModel.IntervalMs))
        {
            var vm = ViewModel;
            _timer.Interval = TimeSpan.FromMilliseconds(vm.IntervalMs);
            if (vm.IsPlaying) _timer.Start(); else _timer.Stop();
        }
    }
}
