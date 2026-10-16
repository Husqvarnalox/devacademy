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
            else grid.RowDefinitions.Add(new RowDefinition(len));
            var section = this.FindControl<Border>(names[i])!;
            Grid.SetRow(section, IsHorizontal ? 0 : i);
            Grid.SetColumn(section, IsHorizontal ? i : 0);
            section.Margin = IsHorizontal ? new Thickness(2, 2, 0, 0) : new Thickness(0, 2, 0, 0);
        }
    }
}
