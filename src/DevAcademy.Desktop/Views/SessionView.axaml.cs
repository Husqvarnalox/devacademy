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
    }

    private void OnReveal(int line, int column) =>
        // After the editor has switched to the requested file.
        Dispatcher.UIThread.Post(() => CodeEditor.GoToLine(line, column), DispatcherPriority.Background);
}
