using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DevAcademy.Desktop.ViewModels.Notebooks;

namespace DevAcademy.Desktop.Views;

public partial class NotebookView : UserControl
{
    public NotebookView()
    {
        InitializeComponent();
        // Tunnel: the code editor would otherwise turn Shift+Enter / Ctrl+Enter into a newline before we see it.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, OnFocusIn, RoutingStrategies.Bubble);
    }

    private NotebookViewModel? Vm => DataContext as NotebookViewModel;

    private static NotebookCellViewModel? CellOf(object? source)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
            if (v is StyledElement { DataContext: NotebookCellViewModel cell }) return cell;
        return null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Vm is not { } vm || CellOf(e.Source) is not { } cell) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            _ = vm.RunAndAdvanceAsync(cell);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            e.Handled = true;
            vm.Select(cell);
            _ = vm.RunCellAsync(cell);
        }
    }

    private void OnFocusIn(object? sender, RoutedEventArgs e)
    {
        if (CellOf(e.Source) is { } cell && !cell.IsSelected) Vm?.Select(cell);
    }

    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { Tag: NotebookCellViewModel cell }) Vm?.Select(cell);
    }

    private void OnToggleEdit(object? sender, RoutedEventArgs e)
    {
        if (CellOf(sender) is { } cell) cell.ToggleEditing();
    }

    private void OnRenderedDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (CellOf(sender) is { } cell) cell.IsEditing = true;
    }
}
