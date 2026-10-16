using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;
using DevAcademy.Execution;
using DevAcademy.Terminal;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>
/// JavaScript/TypeScript projects: package-manager aware Install / Dev / Test / Build buttons. Each one types a visible command into a terminal
/// after an explicit click; nothing is run in the background. The dev server gets its own terminal tab, its local URL is read from that
/// terminal's output, and "open in browser" goes through <see cref="IWebPreview"/> (the system browser today).
/// </summary>
public sealed partial class ProjectWorkspaceViewModel
{
    private static readonly string[] NodeProjectFiles = ["package.json", "package-lock.json", "npm-shrinkwrap.json", "pnpm-lock.yaml", "yarn.lock"];

    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private TerminalTabViewModel? _devTerminal;
    private DevServerUrlDetector? _devDetector;
    private IWebPreview? _defaultPreview;

    /// <summary>Null when the project has no package.json.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNodeProject), nameof(NodeManagerText), nameof(InstallNodeText), nameof(DevCommandText), nameof(TestNodeText), nameof(BuildCommandText), nameof(CanDev), nameof(CanTestNode), nameof(CanBuildNode))]
    private JsProjectInfo? _nodeProject;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDevUrl), nameof(DevStatusText), nameof(CanOpenDevServer))] private DevServerEndpoint? _devServer;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDevRunning), nameof(DevStatusText), nameof(CanOpenDevServer))] private bool _devActive;

    public bool IsNodeProject => NodeProject is not null;
    public string NodeManagerText => NodeProject is { } p ? Loc.F("Ws_NodeManager", p.ManagerName) : "";
    public string InstallNodeText => NodeProject?.InstallCommand ?? "";
    public string DevCommandText => NodeProject?.DevScript is { } s ? NodeProject.ScriptCommand(s) : "";
    public string TestNodeText => NodeProject is { } p && p.HasScript("test") ? p.ScriptCommand("test") : "";
    public string BuildCommandText => NodeProject is { } p && p.HasScript("build") ? p.ScriptCommand("build") : "";
    public bool CanDev => NodeProject?.DevScript is not null;
    public bool CanTestNode => NodeProject?.HasScript("test") == true;
    public bool CanBuildNode => NodeProject?.HasScript("build") == true;

    public bool IsDevRunning => DevActive;
    public bool HasDevUrl => DevServer is not null;
    public bool CanOpenDevServer => DevActive && DevServer is not null;
    public string DevStatusText => !DevActive ? Loc.T("Ws_DevStopped")
        : DevServer is { } s ? Loc.F("Ws_DevListening", s.Port) : Loc.T("Ws_DevStarting");

    public IWebPreview WebPreview => _services.Workspace.WebPreview ?? (_defaultPreview ??= new SystemBrowserWebPreview(_services.Launcher));

    /// <summary>Re-reads package.json and the lockfiles (the project may have changed on disk or the user may have switched package manager).</summary>
    public void RefreshNodeProject() => NodeProject = JsProjects.Detect(Root);

    private bool IsDevTerminal(TerminalTabViewModel tab) => ReferenceEquals(tab, _devTerminal) && tab.IsRunning;

    [RelayCommand]
    private async Task InstallNodeAsync()
    {
        RefreshNodeProject();
        if (NodeProject is not { } p) return;
        var command = p.InstallCommand;
        if (!await _services.Dialogs.ConfirmAsync(Loc.T("Ws_InstallTitle"), Loc.F("Ws_NodeInstallMessage", command), Loc.T("Ws_Install"))) return;
        SendToTerminal(command, execute: true);
    }

    [RelayCommand]
    private async Task TestNodeAsync() => await RunNodeScriptAsync("test");

    [RelayCommand]
    private async Task BuildNodeAsync() => await RunNodeScriptAsync("build");

    private async Task RunNodeScriptAsync(string script)
    {
        RefreshNodeProject();
        if (NodeProject is not { } p || !p.HasScript(script)) return;
        if (!await SaveAllAsync()) return;
        SendToTerminal(p.ScriptCommand(script), execute: true);
    }

    [RelayCommand]
    private async Task StartDevAsync()
    {
        if (_devTerminal is { IsRunning: true } running && DevActive) { ActiveTerminal = running; return; }
        RefreshNodeProject();
        if (NodeProject is not { } p || p.DevScript is not { } script) return;
        if (!await SaveAllAsync()) return;
        var tab = NewTerminal();
        if (tab is null) return;
        AttachDevTerminal(tab);
        tab.Session.Write(p.ScriptCommand(script) + "\r");
    }

    /// <summary>Ctrl+C to the dev terminal: the same thing the user would press. The tab stays open so the shutdown output remains readable.</summary>
    [RelayCommand]
    private void StopDev()
    {
        if (_devTerminal is { IsRunning: true } tab) tab.Session.Write("\u0003");
        // The URL belongs to the process that is going away; the next start prints its own.
        DevServer = null;
        DevActive = false;
    }

    [RelayCommand]
    private async Task OpenDevServerAsync()
    {
        if (DevServer is not { } server) return;
        if (!await WebPreview.OpenAsync(server.Url)) Status = Loc.F("Error_OpenLink", server.Url);
    }

    private void AttachDevTerminal(TerminalTabViewModel tab)
    {
        DetachDevTerminal();
        _devTerminal = tab;
        _devDetector = new DevServerUrlDetector();
        DevServer = null;
        DevActive = true;
        tab.Session.Output += OnDevOutput;
        tab.PropertyChanged += OnDevTerminalProperty;
    }

    private void DetachDevTerminal()
    {
        if (_devTerminal is { } old)
        {
            old.Session.Output -= OnDevOutput;
            old.PropertyChanged -= OnDevTerminalProperty;
        }
        _devTerminal = null;
        _devDetector = null;
        DevServer = null;
        DevActive = false;
    }

    private void OnDevTerminalProperty(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalTabViewModel.IsRunning) && sender is TerminalTabViewModel { IsRunning: false } tab && ReferenceEquals(tab, _devTerminal))
            DetachDevTerminal();
    }

    // Raised on the terminal's reader thread.
    private void OnDevOutput(string chunk)
    {
        try
        {
            var detector = _devDetector;
            if (detector?.Feed(chunk) is not { } endpoint) return;
            PostToUi(() =>
            {
                if (_devDetector != detector || !DevActive) return;
                DevServer ??= endpoint; // the first URL wins; HMR and request logs repeat it
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* detection must never break the terminal's read loop */ }
    }

    private void PostToUi(Action a)
    {
        if (_ui is null) a(); else _ui.Post(_ => a(), null);
    }

    private void DisposeDev() => DetachDevTerminal();
}
