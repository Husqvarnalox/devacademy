using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

/// <summary>Call stack, variables, watch and debug console of a <see cref="DebugViewModel"/>. Vertical in a side column, horizontal in the lesson's output tabs.</summary>
public partial class DebugPanelView : UserControl
{
    public static readonly StyledProperty<bool> IsHorizontalProperty = AvaloniaProperty.Register<DebugPanelView, bool>(nameof(IsHorizontal));

    private DebugViewModel? _vm;

    public DebugPanelView()
    {
        InitializeComponent();
        IsHorizontalProperty.Changed.AddClassHandler<DebugPanelView>((v, _) => v.Arrange());
        DataContextChanged += (_, _) => Attach(DataContext as DebugViewModel);
        this.FindControl<TextBox>("WatchBox")!.KeyDown += (_, e) => EnterTo(e, () => _vm?.AddWatchCommand.Execute(null));
        this.FindControl<TextBox>("ReplBox")!.KeyDown += (_, e) => EnterTo(e, () => _vm?.EvaluateCommand.Execute(null));
        Arrange();
        // Whole rows only: the lists are capped at the largest whole number of rows that fits their pane, so no row is ever half visible at rest.
        Snap("FramePane", "FrameList", ListRowHeight);
        Snap("VariablePane", "VariableTree", TreeRowHeight);
        Snap("WatchPane", "WatchTree", TreeRowHeight);
        var scroll = this.FindControl<ScrollViewer>("SectionsScroll")!;
        scroll.SizeChanged += (_, _) => FitSections();
    }

    /// <summary>Row heights: fixed by the item style for the call stack, the natural header height of a mono tree item for variables and watches.</summary>
    private const double ListRowHeight = 28, TreeRowHeight = 31;
    /// <summary>Smallest comfortable section height; below it the whole panel scrolls instead of squeezing the sections.</summary>
    private const double SectionMin = 112;

    private void Snap(string pane, string list, double row)
    {
        var p = this.FindControl<Panel>(pane)!;
        var l = this.FindControl<Control>(list)!;
        p.SizeChanged += (_, e) => l.MaxHeight = Math.Max(row, Math.Floor(e.NewSize.Height / row) * row);
    }

    private void FitSections()
    {
        var scroll = this.FindControl<ScrollViewer>("SectionsScroll")!;
        var sections = this.FindControl<Grid>("Sections")!;
        var min = IsHorizontal ? SectionMin : SectionMin * 4;
        sections.Height = Math.Max(scroll.Bounds.Height, min);
    }

    public bool IsHorizontal { get => GetValue(IsHorizontalProperty); set => SetValue(IsHorizontalProperty, value); }

    private static void EnterTo(KeyEventArgs e, Action action)
    {
        if (e.Key != Key.Enter) return;
        action();
        e.Handled = true;
    }

    private void Attach(DebugViewModel? vm)
    {
        if (_vm is not null) _vm.Console.CollectionChanged -= OnConsoleChanged;
        _vm = vm;
        if (_vm is not null) _vm.Console.CollectionChanged += OnConsoleChanged;
    }

    private void OnConsoleChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => this.FindControl<ScrollViewer>("ConsoleScroll")?.ScrollToEnd(), DispatcherPriority.Background);

    private void OnRemoveWatch(object? sender, RoutedEventArgs e)
    {
        if (_vm is not null && this.FindControl<TreeView>("WatchTree")?.SelectedItem is VariableNode node) _vm.RemoveWatchCommand.Execute(node);
    }

    /// <summary>Four sections in a column (side panel) or in a row (wide bottom tab).</summary>
    private void Arrange()
    {
        var grid = this.FindControl<Grid>("Sections")!;
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        string[] names = ["StackSection", "VariablesSection", "WatchSection", "ConsoleSection"];
        double[] weights = [1, 2, 2, 2];
        for (int i = 0; i < names.Length; i++)
        {
            var len = new GridLength(weights[i], GridUnitType.Star);
            if (IsHorizontal) grid.ColumnDefinitions.Add(new ColumnDefinition(len));
            else grid.RowDefinitions.Add(new RowDefinition(len) { MinHeight = SectionMin });
            var section = this.FindControl<Border>(names[i])!;
            Grid.SetRow(section, IsHorizontal ? 0 : i);
            Grid.SetColumn(section, IsHorizontal ? i : 0);
            section.Margin = IsHorizontal ? new Thickness(2, 2, 0, 0) : new Thickness(0, 2, 0, 0);
            section.MinHeight = SectionMin;
        }
        FitSections();
    }
}
