using Avalonia.Controls;
using Avalonia.Threading;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Views;

public partial class SessionView : UserControl
{
    private ExerciseSessionViewModel? _vm;

    public SessionView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.RevealRequested -= OnReveal;
            _vm = DataContext as ExerciseSessionViewModel;
            if (_vm is not null) _vm.RevealRequested += OnReveal;
        };
        // The one-line tab strip scrolls when the pane is narrow; a tab selected from code (the debugger, a hint) must still be in view.
        OutputTabs.SelectionChanged += (_, _) =>
            Dispatcher.UIThread.Post(() => OutputTabs.ContainerFromIndex(OutputTabs.SelectedIndex)?.BringIntoView(), DispatcherPriority.Background);
    }

    private void OnReveal(int line, int column) =>
        // After the editor has switched to the requested file.
        Dispatcher.UIThread.Post(() => CodeEditor.GoToLine(line, column), DispatcherPriority.Background);
}
