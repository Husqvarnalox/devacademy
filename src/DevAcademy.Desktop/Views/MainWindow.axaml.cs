using Avalonia.Controls;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Services.Holder.Window = this;
        };
    }

    private bool _closeConfirmed;

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || DataContext is not MainViewModel vm) return;
        if (vm.HasUnsavedProjectChanges)
        {
            // Async confirmation: cancel this close, ask, then close again if the user agreed.
            e.Cancel = true;
            if (!await vm.ConfirmProjectCloseAsync()) return;
        }
        vm.Shutdown();
        _closeConfirmed = true;
        Close();
    }
}
