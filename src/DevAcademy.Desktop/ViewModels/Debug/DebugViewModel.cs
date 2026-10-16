using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Dap;
using DevAcademy.Debugging.Session;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>
/// The debugger for one host (project workspace or lesson Practice): adapter availability and the explicit install action, starting a
/// session, the stopped/running state and everything the panel shows (call stack, lazy variable tree, watches, console).
/// A debug run is NOT sandboxed; <see cref="NoSandboxText"/> says so wherever the panel or the Debug button is shown.
/// </summary>
public sealed partial class DebugViewModel : ObservableObject, IDisposable
{
    private readonly DebugService _svc;
    private readonly IDialogService _dialogs;
    private readonly IDebugHost _host;
    private DebugSession? _session;
    private DebugPreparation? _prep;
    private Dictionary<string, string> _keyByPath = new(PathComparer);
    private AdapterStatus _adapter = new(AdapterState.Unavailable, "");
    private CancellationTokenSource? _startCts;
    private int _generation;
    private bool _disposed;

    private static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public DebugViewModel(DebugService service, IDialogService dialogs, IDebugHost host)
    {
        _svc = service;
        _dialogs = dialogs;
        _host = host;
        host.Breakpoints.Changed += OnBreakpointsChanged;
        StatusText = AdapterText;
    }

    /// <summary>The user started a debug run: the host should show the debug panel (also when the start fails and the panel explains why).</summary>
    public event Action? Attention;

    public static string NoSandboxText => Loc.T("Dbg_NoSandbox");

    public ObservableCollection<StackFrameItem> Frames { get; } = [];
    public ObservableCollection<VariableNode> Variables { get; } = [];
    public ObservableCollection<VariableNode> Watches { get; } = [];
    public ObservableCollection<ConsoleLine> Console { get; } = [];

    [ObservableProperty] private DebugState _state = DebugState.NotStarted;
    [ObservableProperty] private StackFrameItem? _selectedFrame;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _watchInput = "";
    [ObservableProperty] private string _replInput = "";
    [ObservableProperty] private bool _isBusy;
    /// <summary>The explicit adapter install is running.</summary>
    [ObservableProperty] private bool _isInstalling;

    public bool IsActive => State is DebugState.Starting or DebugState.Running or DebugState.Stopped;
    public bool IsStopped => State == DebugState.Stopped;
    public bool IsRunning => State == DebugState.Running;
    public bool IsAvailable => _host.DisabledReason is null && _svc.Supports(_host.LanguageId);
    public bool IsReady => IsAvailable && _adapter.IsReady;
    public bool CanStartNow => IsAvailable && !IsActive && !IsBusy && !IsInstalling;
    public string AdapterText => _host.DisabledReason is { } r ? r : !_svc.Supports(_host.LanguageId) ? DebugService.NoAdapterMessage(_host.LanguageId) : _adapter.Message;
    public bool CanInstall => IsAvailable && _adapter is { State: AdapterState.NotInstalled, Plan: not null } && !IsActive && !IsInstalling;
    public string InstallCommandText => _adapter.Plan?.Display ?? "";
    public bool HasBreakpoints => _host.Breakpoints.HasAny;

    private void RaiseState()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(CanStartNow));
        OnPropertyChanged(nameof(AdapterText));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(InstallCommandText));
        OnPropertyChanged(nameof(HasBreakpoints));
        StartCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        StepOverCommand.NotifyCanExecuteChanged();
        StepIntoCommand.NotifyCanExecuteChanged();
        StepOutCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
    }

    partial void OnStateChanged(DebugState value) => RaiseState();
    partial void OnIsBusyChanged(bool value) => RaiseState();
    partial void OnIsInstallingChanged(bool value) => RaiseState();

    // ---------------------------------------------------------------- availability

    /// <summary>Re-checks which adapter this host's language would use (never installs anything).</summary>
    public async Task RefreshAsync()
    {
        if (_disposed || IsActive) return;
        string? lang = _host.LanguageId;
        if (_host.DisabledReason is null && _svc.Supports(lang))
        {
            string? py = lang == "python" ? await _host.GetPythonInterpreterAsync(CancellationToken.None).ConfigureAwait(true) : null;
            _adapter = await _svc.GetStatusAsync(lang, py).ConfigureAwait(true);
        }
        else _adapter = new AdapterStatus(AdapterState.Unavailable, AdapterText);
        if (!IsActive) StatusText = AdapterText;
        RaiseState();
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (_adapter.Plan is not { } plan) return;
        string message = Loc.F("Dbg_InstallMessage", plan.Display);
        if (!await _dialogs.ConfirmAsync(Loc.T("Dbg_InstallTitle"), message, Loc.T("Dbg_InstallConfirm"))) return;
        IsInstalling = true;
        StatusText = Loc.T("Dbg_Installing");
        try
        {
            var (ok, output) = await _svc.InstallAsync(plan, () => true).ConfigureAwait(true);
            if (!ok) AppendConsole(output.Length == 0 ? Loc.T("Dbg_InstallFailed") : output, ConsoleKind.Error);
            else AppendConsole(Loc.T("Dbg_Installed"), ConsoleKind.Info);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendConsole(ex.Message, ConsoleKind.Error);
        }
        finally { IsInstalling = false; }
        await RefreshAsync().ConfigureAwait(true);
    }

    // ---------------------------------------------------------------- start / end

    [RelayCommand(CanExecute = nameof(CanStartNow))]
    public async Task StartAsync()
    {
        if (!CanStartNow) return;
        Attention?.Invoke();
        await RefreshAsync().ConfigureAwait(true);
        if (!_adapter.IsReady)
        {
            StatusText = AdapterText;
            AppendConsole(AdapterText, ConsoleKind.Error);
            return;
        }
        IsBusy = true;
        State = DebugState.Starting;
        ResetView();
        Console.Clear();
        StatusText = Loc.T("Dbg_Preparing");
        var cts = _startCts = new CancellationTokenSource();
        try
        {
            _svc.PurgeStaleRuns();
            var prep = _prep = await _host.PrepareAsync(cts.Token).ConfigureAwait(true);
            if (prep.Launch is not { } launch)
            {
                AppendConsole(prep.Error ?? Loc.T("Dbg_PrepareFailed"), ConsoleKind.Error);
                EndRun(Loc.T("Dbg_PrepareFailed"));
                return;
            }
            _keyByPath = new Dictionary<string, string>(PathComparer);
            foreach (var (key, path) in prep.PathByKey) _keyByPath[NormPath(path)] = key;

            var adapter = _adapter.Adapter!;
            var options = new DebugSessionOptions(adapter.Definition, adapter, LaunchConfigurations.Create(adapter.Definition, launch), launch.WorkingDirectory);
            var session = _session = new DebugSession(options, _svc.Launcher);
            session.SeedBreakpoints(CurrentBreakpoints());
            session.StateChanged += s => Marshal(() => OnSessionState(session, s));
            session.Stopped += s => Marshal(() => OnStopped(session, s));
            session.Resumed += () => Marshal(() => OnResumed(session));
            session.Output += o => Marshal(() => OnOutput(session, o));
            session.Terminated += t => Marshal(() => OnTerminated(session, t));
            StatusText = Loc.T("Dbg_Starting");
            await session.StartAsync(cts.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cts.IsCancellationRequested)
        {
            AppendConsole(ex.Message, ConsoleKind.Error);
            await StopSessionQuietlyAsync().ConfigureAwait(true);
            EndRun(Loc.T("Dbg_StartFailed"));
        }
        catch (OperationCanceledException)
        {
            await StopSessionQuietlyAsync().ConfigureAwait(true);
            EndRun(Loc.T("Dbg_Stopped"));
        }
        finally { IsBusy = false; }
    }

    private async Task StopSessionQuietlyAsync()
    {
        var s = _session;
        if (s is null) return;
        try { await s.StopAsync().ConfigureAwait(true); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* the adapter is already gone */ }
    }

    [RelayCommand(CanExecute = nameof(IsActive))]
    private async Task StopAsync()
    {
        _startCts?.Cancel();
        await StopSessionQuietlyAsync().ConfigureAwait(true);
        if (_session is null && IsActive) EndRun(Loc.T("Dbg_Stopped"));
    }

    private void OnTerminated(DebugSession session, DebugTermination t)
    {
        if (!ReferenceEquals(session, _session)) return;
        if (t.Error is { Length: > 0 } err) AppendConsole(err, ConsoleKind.Error);
        string text = t.ExitCode is { } code ? Loc.F("Dbg_ExitedCode", code) : Loc.T("Dbg_Exited");
        AppendConsole(text, ConsoleKind.Info);
        EndRun(text);
        var gone = session;
        _ = Task.Run(async () => { try { await gone.DisposeAsync(); } catch (Exception) { /* best effort */ } });
    }

    private void EndRun(string status)
    {
        _generation++;
        _session = null;
        ResetView();
        _svc.CleanupRunDirectory(_prep?.CleanupDirectory);
        _prep = null;
        State = DebugState.NotStarted;
        StatusText = status;
        IsBusy = false;
        RaiseState();
    }

    private void ResetView()
    {
        _host.Breakpoints.ClearExecution();
        Frames.Clear();
        Variables.Clear();
        SelectedFrame = null;
    }

    // ---------------------------------------------------------------- session events

    private void Marshal(Action a) => _svc.Invoke(() => { if (!_disposed) a(); });

    private void OnSessionState(DebugSession session, DebugState s)
    {
        if (!ReferenceEquals(session, _session) || s == DebugState.Terminated) return;
        State = s;
        if (s == DebugState.Running) StatusText = Loc.T("Dbg_Running");
    }

    private void OnResumed(DebugSession session)
    {
        if (!ReferenceEquals(session, _session)) return;
        _generation++;
        State = DebugState.Running;
        StatusText = Loc.T("Dbg_Running");
        _host.Breakpoints.ClearExecution();
        Frames.Clear();
        Variables.Clear();
        SelectedFrame = null;
    }

    private void OnStopped(DebugSession session, DebugStop stop)
    {
        if (!ReferenceEquals(session, _session)) return;
        _generation++;
        State = DebugState.Stopped;
        StatusText = Loc.F("Dbg_Paused", stop.Description ?? stop.Reason);
        Frames.Clear();
        foreach (var f in stop.Frames) Frames.Add(new StackFrameItem(f, KeyFor(f)));
        SelectedFrame = null;
        SelectedFrame = Frames.FirstOrDefault(f => f.IsUserCode) ?? Frames.FirstOrDefault();
        _ = RefreshWatchesAsync();
    }

    private void OnOutput(DebugSession session, OutputEventArgs o)
    {
        if (!ReferenceEquals(session, _session)) return;
        if (o.Category is "telemetry") return;
        var kind = o.Category == "stderr" ? ConsoleKind.Error : o.Category == "console" ? ConsoleKind.Info : ConsoleKind.Output;
        AppendConsole(o.Text.TrimEnd('\r', '\n'), kind);
    }

    private void AppendConsole(string text, ConsoleKind kind)
    {
        foreach (string line in text.Split('\n'))
        {
            Console.Add(new ConsoleLine(line.TrimEnd('\r'), kind));
        }
        while (Console.Count > 2000) Console.RemoveAt(0);
    }

    // ---------------------------------------------------------------- frames, variables, watches

    partial void OnSelectedFrameChanged(StackFrameItem? value)
    {
        _host.Breakpoints.ClearExecution();
        Variables.Clear();
        if (value is null) return;
        if (value.HostKey is { } key && value.Frame.Line > 0)
        {
            _host.Breakpoints.Get(key).SetExecutionLine(value.Frame.Line);
            _ = _host.RevealAsync(key, value.Frame.Line);
        }
        _ = LoadScopesAsync(value);
    }

    private async Task LoadScopesAsync(StackFrameItem frame)
    {
        int gen = _generation;
        var session = _session;
        if (session is null) return;
        try
        {
            var scopes = await session.GetScopesAsync(frame.Frame.Id).ConfigureAwait(true);
            if (gen != _generation || !ReferenceEquals(session, _session)) return;
            Variables.Clear();
            bool first = true;
            foreach (var scope in scopes)
            {
                var node = new VariableNode(scope.Name, "", null, scope.VariablesReference, r => LoadVariablesAsync(session, gen, r));
                Variables.Add(node);
                if (first && !scope.Expensive) node.IsExpanded = true;
                first = false;
            }
        }
        catch (Exception ex) when (ex is DapException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            if (gen == _generation) { Variables.Clear(); Variables.Add(new VariableNode("", ex.Message, null, 0, null) { IsError = true }); }
        }
    }

    private async Task<IReadOnlyList<DapVariable>> LoadVariablesAsync(DebugSession session, int gen, int reference)
    {
        if (gen != _generation || !ReferenceEquals(session, _session)) return [];
        return await session.GetVariablesAsync(reference).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddWatchAsync()
    {
        string expr = WatchInput.Trim();
        if (expr.Length == 0) return;
        WatchInput = "";
        var session = _session;
        var node = new VariableNode(expr, "", null, 0, r => session is null ? Task.FromResult<IReadOnlyList<DapVariable>>([]) : LoadVariablesAsync(session, _generation, r));
        Watches.Add(node);
        if (IsStopped) await EvaluateWatchAsync(node).ConfigureAwait(true);
    }

    [RelayCommand]
    private void RemoveWatch(VariableNode? node)
    {
        if (node is not null) Watches.Remove(node);
    }

    private async Task RefreshWatchesAsync()
    {
        foreach (var w in Watches.ToList()) await EvaluateWatchAsync(w).ConfigureAwait(true);
    }

    private async Task EvaluateWatchAsync(VariableNode node)
    {
        if (_session is not { } s || SelectedFrame is not { } frame) return;
        try
        {
            var r = await s.EvaluateAsync(node.Name, frame.Frame.Id, "watch").ConfigureAwait(true);
            node.Update(r.Result, r.Type, r.VariablesReference);
        }
        catch (Exception ex) when (ex is DapException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            node.Update(ex.Message, null, 0, isError: true);
        }
    }

    /// <summary>Debug Console input: evaluates in the selected frame while paused.</summary>
    [RelayCommand]
    private async Task EvaluateAsync()
    {
        string expr = ReplInput.Trim();
        if (expr.Length == 0) return;
        ReplInput = "";
        AppendConsole("> " + expr, ConsoleKind.Echo);
        if (_session is null || SelectedFrame is not { } frame || !IsStopped)
        {
            AppendConsole(Loc.T("Dbg_ReplNeedsPause"), ConsoleKind.Error);
            return;
        }
        try
        {
            var r = await _session.EvaluateAsync(expr, frame.Frame.Id, "repl").ConfigureAwait(true);
            AppendConsole(r.Result, ConsoleKind.Result);
            await RefreshWatchesAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is DapException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            AppendConsole(ex.Message, ConsoleKind.Error);
        }
    }

    /// <summary>F5: continue when paused, start when idle, nothing while running.</summary>
    [RelayCommand]
    private async Task StartOrContinueAsync()
    {
        if (IsStopped) await ContinueAsync().ConfigureAwait(true);
        else if (!IsActive) await StartAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void ClearConsole() => Console.Clear();

    [RelayCommand]
    private void SelectFrame(StackFrameItem? frame)
    {
        if (frame is not null) SelectedFrame = frame;
    }

    // ---------------------------------------------------------------- stepping

    [RelayCommand(CanExecute = nameof(IsStopped))] private Task ContinueAsync() => Resume(s => s.ContinueAsync());
    [RelayCommand(CanExecute = nameof(IsStopped))] private Task StepOverAsync() => Resume(s => s.StepOverAsync());
    [RelayCommand(CanExecute = nameof(IsStopped))] private Task StepIntoAsync() => Resume(s => s.StepIntoAsync());
    [RelayCommand(CanExecute = nameof(IsStopped))] private Task StepOutAsync() => Resume(s => s.StepOutAsync());
    [RelayCommand(CanExecute = nameof(IsRunning))] private Task PauseAsync() => Resume(s => s.PauseAsync());

    private async Task Resume(Func<DebugSession, Task> action)
    {
        if (_session is not { } s) return;
        try { await action(s).ConfigureAwait(true); }
        catch (Exception ex) when (ex is DapException or ObjectDisposedException or InvalidOperationException)
        {
            AppendConsole(ex.Message, ConsoleKind.Error);
        }
    }

    // ---------------------------------------------------------------- breakpoints

    private IReadOnlyDictionary<string, IReadOnlyList<SourceBreakpoint>> CurrentBreakpoints()
    {
        var map = new Dictionary<string, IReadOnlyList<SourceBreakpoint>>(StringComparer.Ordinal);
        if (_prep is null) return map;
        foreach (var (key, target) in _host.Breakpoints.All)
            if (_prep.PathByKey.TryGetValue(key, out var path) && target.Breakpoints.Count > 0)
                map[path] = target.Breakpoints.Select(l => new SourceBreakpoint(l)).ToList();
        return map;
    }

    private void OnBreakpointsChanged(string key)
    {
        OnPropertyChanged(nameof(HasBreakpoints));
        if (_session is not { } s || _prep is null || s.State is not (DebugState.Running or DebugState.Stopped)) return;
        if (!_prep.PathByKey.TryGetValue(key, out var path)) return;
        var lines = _host.Breakpoints.Get(key).Breakpoints.Select(l => new SourceBreakpoint(l)).ToList();
        _ = SendBreakpointsAsync(s, path, lines);
    }

    private async Task SendBreakpointsAsync(DebugSession s, string path, IReadOnlyList<SourceBreakpoint> lines)
    {
        try { await s.SetBreakpointsAsync(path, lines).ConfigureAwait(true); }
        catch (Exception ex) when (ex is DapException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            AppendConsole(ex.Message, ConsoleKind.Error);
        }
    }

    [RelayCommand]
    private void ClearBreakpoints()
    {
        foreach (var (_, t) in _host.Breakpoints.All.ToList()) t.ClearAll();
    }

    // ---------------------------------------------------------------- paths

    private string? KeyFor(DapStackFrame f) =>
        f.Source?.Path is { Length: > 0 } p && _keyByPath.TryGetValue(NormPath(p), out var key) ? key : null;

    /// <summary>Debuggers report the real path; macOS temp dirs live under /private while callers may know them without it.</summary>
    internal static string NormPath(string p)
    {
        try { p = Path.GetFullPath(p); } catch (ArgumentException) { /* keep as is */ }
        if (OperatingSystem.IsMacOS() && p.StartsWith("/private/", StringComparison.Ordinal)) p = p["/private".Length..];
        return p;
    }

    /// <summary>Ends a running session and waits for it (leaving the lesson).</summary>
    public async Task ShutdownAsync()
    {
        if (IsActive || _session is not null) await StopAsync().ConfigureAwait(true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.Breakpoints.Changed -= OnBreakpointsChanged;
        _startCts?.Cancel();
        var s = _session;
        _session = null;
        if (s is not null) _ = Task.Run(async () => { try { await s.StopAsync(); } catch (Exception) { /* closing */ } });
        _svc.CleanupRunDirectory(_prep?.CleanupDirectory);
    }
}
