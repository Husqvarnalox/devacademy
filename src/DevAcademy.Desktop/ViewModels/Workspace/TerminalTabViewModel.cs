using CommunityToolkit.Mvvm.ComponentModel;
using DevAcademy.Terminal;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>A tab in the bottom panel wrapping one <see cref="ITerminalSession"/>.</summary>
public sealed partial class TerminalTabViewModel : ObservableObject, IDisposable
{
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    public TerminalTabViewModel(ITerminalSession session, int number)
    {
        Session = session;
        _baseTitle = $"{session.Name} {number}";
        _title = _baseTitle;
        _isRunning = session.IsRunning;
        session.Exited += OnExited;
        session.Screen.TitleChanged += OnTitle;
    }

    private readonly string _baseTitle;

    public ITerminalSession Session { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText))] private bool _isRunning;

    public string StatusText => IsRunning ? "" : (Session.ExitCode is { } c ? $"код выхода {c}" : "завершён");

    private void OnExited() => Post(() => { IsRunning = false; Title = _baseTitle + " (завершён)"; });

    private void OnTitle(string title) => Post(() => Title = string.IsNullOrWhiteSpace(title) ? _baseTitle : $"{_baseTitle}: {title}");

    private void Post(Action a)
    {
        if (_ui is null) a(); else _ui.Post(_ => a(), null);
    }

    public void Kill() => Session.Kill();

    public void Dispose()
    {
        Session.Exited -= OnExited;
        Session.Screen.TitleChanged -= OnTitle;
        Session.Dispose();
    }
}
