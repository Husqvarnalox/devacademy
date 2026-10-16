using DevAcademy.Terminal;
using DevAcademy.Terminal.Vt;

namespace DevAcademy.Desktop.Tests.Support;

/// <summary>A terminal session that only records what was typed into it. No process is started.</summary>
public sealed class FakeTerminalSession(TerminalStartOptions options) : ITerminalSession
{
    public TerminalStartOptions Options { get; } = options;
    public List<string> Written { get; } = [];
    public string Name => "fake";
    public TerminalEmulator Screen { get; } = new(80, 24);
    public bool IsRunning { get; private set; } = true;
    public int? ExitCode { get; private set; }
    public int? ProcessId => null;
    public event Action? Changed { add { } remove { } }
    public event Action? Exited;
    public void Write(string text) => Written.Add(text);
    public void Resize(int cols, int rows) { }
    public void Kill() { IsRunning = false; ExitCode = 137; Exited?.Invoke(); }
    public void Dispose() { }
}

public sealed class FakeTerminalFactory : ITerminalFactory
{
    public List<FakeTerminalSession> Sessions { get; } = [];
    public ITerminalSession Start(TerminalStartOptions options)
    {
        var s = new FakeTerminalSession(options);
        Sessions.Add(s);
        return s;
    }
}
