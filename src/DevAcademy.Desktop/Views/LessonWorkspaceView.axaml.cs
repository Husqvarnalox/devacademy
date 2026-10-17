using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

public partial class LessonWorkspaceView : UserControl
{
    public const double NarrowWidth = 900;
    private const double NavWidth = 232;
    /// <summary>Below this width the lesson list starts hidden so theory and editor keep a usable width; the toggle brings it back.</summary>
    public const double CompactNavWidth = 1100;
    private bool _navAutoHandled;

    private LessonWorkspaceViewModel? _vm;

    public LessonWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        SizeChanged += (_, _) => Arrange();
    }

    private void Attach()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as LessonWorkspaceViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        Arrange();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LessonWorkspaceViewModel.IsNavVisible) or nameof(LessonWorkspaceViewModel.ShowSession))
            Arrange();
    }

    /// <summary>Wide: nav | theory | editor+output. Narrow (&lt; 900 px): nav behind a toggle, editor stacked below theory.</summary>
    private void Arrange()
    {
        if (_vm is null || Bounds.Width <= 0) return;
        var narrow = Bounds.Width < NarrowWidth;
        _vm.SetNarrow(narrow);
        if (!narrow && !_navAutoHandled && Bounds.Width < CompactNavWidth)
        {
            _navAutoHandled = true;
            _vm.IsNavVisible = false;
        }

        var body = this.FindControl<Grid>("Body")!;
        var nav = this.FindControl<Border>("NavPane")!;
        var navSplit = this.FindControl<GridSplitter>("NavSplitter")!;
        var center = this.FindControl<Grid>("CenterPane")!;
        var mainSplit = this.FindControl<GridSplitter>("MainSplitter")!;
        var session = this.FindControl<Grid>("SessionPane")!;
        body.ColumnDefinitions.Clear();
        body.RowDefinitions.Clear();
        var showNav = _vm.IsNavVisible;
        var showSession = _vm.ShowSession;

        if (!narrow)
        {
            body.ColumnDefinitions.Add(new ColumnDefinition(showNav ? new GridLength(NavWidth) : new GridLength(0)) { MinWidth = showNav ? 160 : 0, MaxWidth = 420 });
            body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            body.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)) { MinWidth = 320 });
            body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            body.ColumnDefinitions.Add(new ColumnDefinition(showSession ? new GridLength(1.15, GridUnitType.Star) : new GridLength(0)) { MinWidth = showSession ? 360 : 0 });
            Place(nav, 0, 0); Place(navSplit, 0, 1); Place(center, 0, 2); Place(mainSplit, 0, 3); Place(session, 0, 4);
            navSplit.ResizeDirection = GridResizeDirection.Columns; navSplit.Width = 5; navSplit.Height = double.NaN;
            navSplit.HorizontalAlignment = HorizontalAlignment.Stretch; navSplit.VerticalAlignment = VerticalAlignment.Stretch;
            mainSplit.ResizeDirection = GridResizeDirection.Columns; mainSplit.Width = 5; mainSplit.Height = double.NaN;
            mainSplit.HorizontalAlignment = HorizontalAlignment.Stretch; mainSplit.VerticalAlignment = VerticalAlignment.Stretch;
            nav.MaxHeight = double.PositiveInfinity;
        }
        else
        {
            body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            body.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)) { MinHeight = 160 });
            body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            body.RowDefinitions.Add(new RowDefinition(showSession ? new GridLength(1.4, GridUnitType.Star) : new GridLength(0)) { MinHeight = showSession ? 260 : 0 });
            Place(nav, 0, 0); Place(center, 1, 0); Place(mainSplit, 2, 0); Place(session, 3, 0);
            Grid.SetRow(navSplit, 0);
            navSplit.IsVisible = false;
            mainSplit.ResizeDirection = GridResizeDirection.Rows; mainSplit.Height = 5; mainSplit.Width = double.NaN;
            mainSplit.HorizontalAlignment = HorizontalAlignment.Stretch; mainSplit.VerticalAlignment = VerticalAlignment.Stretch;
            nav.MaxHeight = 200;
        }
        if (!narrow) navSplit.IsVisible = showNav;
    }

    private static void Place(Control c, int row, int column)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, column);
    }
}
