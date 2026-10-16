using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

/// <summary>Owns its view model (embedded from Markdown) and drives playback with a timer: one tick = one frame.</summary>
public partial class KMeansView : UserControl
{
    private readonly DispatcherTimer _timer = new();

    public KMeansView() : this(new KMeansViewModel()) { }

    public KMeansView(KMeansViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _timer.Tick += (_, _) => vm.Tick();
        vm.PropertyChanged += OnVmChanged;
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); vm.IsPlaying = false; };
    }

    public KMeansViewModel ViewModel => (KMeansViewModel)DataContext!;

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(KMeansViewModel.IsPlaying) or nameof(KMeansViewModel.IntervalMs))
        {
            var vm = ViewModel;
            _timer.Interval = TimeSpan.FromMilliseconds(vm.IntervalMs);
            if (vm.IsPlaying) _timer.Start(); else _timer.Stop();
        }
    }
}
