using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

/// <summary>Owns its view model (it is embedded from Markdown, not navigated to) and drives playback with a timer: one tick = one trace step.</summary>
public partial class SortingVisualizerView : UserControl
{
    private readonly DispatcherTimer _timer = new();

    public SortingVisualizerView() : this(new SortingVisualizerViewModel()) { }

    public SortingVisualizerView(SortingVisualizerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _timer.Tick += (_, _) => vm.Tick();
        vm.PropertyChanged += OnVmChanged;
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); vm.IsPlaying = false; };
    }

    public SortingVisualizerViewModel ViewModel => (SortingVisualizerViewModel)DataContext!;

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SortingVisualizerViewModel.IsPlaying) or nameof(SortingVisualizerViewModel.IntervalMs))
        {
            var vm = ViewModel;
            _timer.Interval = TimeSpan.FromMilliseconds(vm.IntervalMs);
            if (vm.IsPlaying) _timer.Start(); else _timer.Stop();
        }
    }
}
