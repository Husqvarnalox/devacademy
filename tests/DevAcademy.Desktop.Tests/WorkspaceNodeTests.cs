using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>Package-manager aware Install / Dev / Test / Build buttons, dev-server URL detection and "open in browser".</summary>
public class WorkspaceNodeTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "da-node-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTerminalFactory _terminals = new();

    public WorkspaceNodeTests() => Directory.CreateDirectory(_project);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_project, true); } catch (IOException) { }
    }

    private TestHost NewHost(IWebPreview? preview = null)
    {
        var host = new TestHost(configure: s => s.AddSingleton<DevAcademy.Terminal.ITerminalFactory>(_terminals));
        host.Services.Workspace.WatchFiles = false;
        host.Services.Workspace.WebPreview = preview;
        host.Dialogs.Answer = true;
        return host;
    }

    private void Project(string? lockfile, string scripts = """{ "dev": "vite", "test": "vitest run", "build": "vite build" }""")
    {
        File.WriteAllText(Path.Combine(_project, "package.json"), "{ \"name\": \"p\", \"scripts\": " + scripts + " }");
        if (lockfile is not null) File.WriteAllText(Path.Combine(_project, lockfile), "");
    }

    private static async Task<ProjectWorkspaceViewModel> OpenAsync(TestHost host, string path)
    {
        var projects = new ProjectsViewModel(host.Services);
        await projects.LoadAsync();
        await projects.OpenAsync(path);
        return projects.Project!;
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition(), "condition was not met in time");
    }

    [Theory]
    [InlineData(null, "npm")]
    [InlineData("package-lock.json", "npm")]
    [InlineData("pnpm-lock.yaml", "pnpm")]
    [InlineData("yarn.lock", "yarn")]
    public async Task Buttons_type_the_commands_of_the_detected_package_manager(string? lockfile, string pm)
    {
        Project(lockfile);
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        Assert.True(ws.IsNodeProject);
        Assert.Contains(pm, ws.NodeManagerText);
        Assert.Equal($"{pm} install", ws.InstallNodeText);
        Assert.Equal($"{pm} run dev", ws.DevCommandText);
        Assert.Empty(_terminals.Sessions); // nothing is typed or started until a click

        await ws.InstallNodeCommand.ExecuteAsync(null);
        var term = Assert.Single(_terminals.Sessions);
        Assert.Equal($"{pm} install\r", Assert.Single(term.Written));

        await ws.TestNodeCommand.ExecuteAsync(null);
        await ws.BuildNodeCommand.ExecuteAsync(null);
        Assert.Equal([$"{pm} install\r", $"{pm} run test\r", $"{pm} run build\r"], term.Written);
        Assert.Single(_terminals.Sessions); // same terminal reused

        // The install click asked first, showing the exact command.
        Assert.Contains($"{pm} install", Assert.Single(host.Dialogs.ConfirmedMessages));
    }

    [Fact]
    public async Task Declining_the_install_confirmation_types_nothing()
    {
        Project(null);
        using var host = NewHost();
        host.Dialogs.Answer = false;
        var ws = await OpenAsync(host, _project);
        await ws.InstallNodeCommand.ExecuteAsync(null);
        Assert.Empty(_terminals.Sessions);
    }

    [Fact]
    public async Task Buttons_without_a_matching_package_json_script_are_disabled()
    {
        Project(null, """{ "start": "node s.js" }""");
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        Assert.True(ws.CanDev);               // "start" is the fallback for dev
        Assert.Equal("npm run start", ws.DevCommandText);
        Assert.False(ws.CanTestNode);
        Assert.False(ws.CanBuildNode);
        await ws.TestNodeCommand.ExecuteAsync(null);
        Assert.Empty(_terminals.Sessions);
    }

    [Fact]
    public async Task A_project_without_package_json_has_no_node_buttons()
    {
        File.WriteAllText(Path.Combine(_project, "main.py"), "print(1)\n");
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        Assert.False(ws.IsNodeProject);
        Assert.False(ws.CanDev);
    }

    [Fact]
    public async Task Dev_runs_in_its_own_terminal_and_other_buttons_never_type_into_it()
    {
        Project("pnpm-lock.yaml");
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        ws.NewTerminal();
        await ws.StartDevCommand.ExecuteAsync(null);
        Assert.Equal(2, _terminals.Sessions.Count);
        var (shell, dev) = (_terminals.Sessions[0], _terminals.Sessions[1]);
        Assert.Equal("pnpm run dev\r", Assert.Single(dev.Written));
        Assert.Empty(shell.Written);
        Assert.Same(dev, ws.ActiveSession); // the dev tab is shown

        await ws.TestNodeCommand.ExecuteAsync(null); // active terminal is the dev server: must not receive the test command
        Assert.Equal(3, _terminals.Sessions.Count);
        Assert.Single(dev.Written);
        Assert.Equal("pnpm run test\r", Assert.Single(_terminals.Sessions[2].Written));

        await ws.StartDevCommand.ExecuteAsync(null); // already running: just focus it
        Assert.Equal(3, _terminals.Sessions.Count);
        Assert.Single(dev.Written);
    }

    [Fact]
    public async Task Open_in_browser_appears_only_after_a_local_url_is_printed_and_goes_through_the_preview_abstraction()
    {
        Project(null);
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        Assert.False(ws.CanOpenDevServer);
        Assert.Equal("Dev-сервер не запущен", ws.DevStatusText);

        await ws.StartDevCommand.ExecuteAsync(null);
        var dev = _terminals.Sessions[0];
        Assert.True(ws.IsDevRunning);
        Assert.False(ws.CanOpenDevServer);
        Assert.Equal("Dev-сервер запускается…", ws.DevStatusText);

        dev.Emit("\n> p@0.1.0 dev\n> vite\n\n  \u001B[32m➜\u001B[39m  Local:   \u001B[36mhttp://localhost:\u001B[1m5173\u001B[22m/\u001B[39m\r\n");
        await WaitAsync(() => ws.CanOpenDevServer);
        Assert.Equal("http://localhost:5173/", ws.DevServer!.Url);
        Assert.Equal("Порт 5173: слушает", ws.DevStatusText);

        await ws.OpenDevServerCommand.ExecuteAsync(null);
        Assert.Equal(["http://localhost:5173/"], host.Launcher.Opened);
    }

    [Fact]
    public async Task Stop_sends_ctrl_c_to_the_dev_terminal_and_clears_the_url()
    {
        Project(null);
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        await ws.StartDevCommand.ExecuteAsync(null);
        var dev = _terminals.Sessions[0];
        dev.Emit("  Local: http://localhost:3000\r\n");
        await WaitAsync(() => ws.CanOpenDevServer);

        ws.StopDevCommand.Execute(null);
        Assert.Equal(["npm run dev\r", "\u0003"], dev.Written);
        Assert.False(ws.CanOpenDevServer);
        Assert.False(ws.IsDevRunning);

        dev.Emit("  Local: http://localhost:3999\r\n"); // late output of the stopped server is ignored
        await Task.Delay(50);
        Assert.False(ws.CanOpenDevServer);
    }

    [Fact]
    public async Task Server_exit_resets_the_indicator_and_a_restart_detects_the_new_port()
    {
        Project(null);
        using var host = NewHost();
        var ws = await OpenAsync(host, _project);
        await ws.StartDevCommand.ExecuteAsync(null);
        var dev = _terminals.Sessions[0];
        dev.Emit("Local: http://localhost:5173/\n");
        await WaitAsync(() => ws.CanOpenDevServer);

        dev.Kill();
        await WaitAsync(() => !ws.IsDevRunning);
        Assert.False(ws.CanOpenDevServer);

        await ws.StartDevCommand.ExecuteAsync(null);
        Assert.Equal(2, _terminals.Sessions.Count);
        _terminals.Sessions[1].Emit("Local: http://localhost:5174/\n");
        await WaitAsync(() => ws.CanOpenDevServer);
        Assert.Equal(5174, ws.DevServer!.Port);
    }

    [Fact]
    public async Task A_custom_web_preview_replaces_the_system_browser()
    {
        Project(null);
        var preview = new RecordingPreview();
        using var host = NewHost(preview);
        var ws = await OpenAsync(host, _project);
        await ws.StartDevCommand.ExecuteAsync(null);
        _terminals.Sessions[0].Emit("http://127.0.0.1:8080\n");
        await WaitAsync(() => ws.CanOpenDevServer);
        await ws.OpenDevServerCommand.ExecuteAsync(null);
        Assert.Equal(["http://127.0.0.1:8080"], preview.Opened);
        Assert.Empty(host.Launcher.Opened);
    }

    [Theory]
    [InlineData("http://localhost:5173/", true)]
    [InlineData("http://127.0.0.1:3000", true)]
    [InlineData("https://localhost:8443/x", true)]
    [InlineData("http://[::1]:3000/", true)]
    [InlineData("https://example.com/", false)]
    [InlineData("http://192.168.1.10:3000", false)]
    [InlineData("file:///etc/passwd", false)]
    public async Task System_browser_preview_only_opens_local_web_urls(string url, bool allowed)
    {
        var launcher = new FakeLauncher();
        Assert.Equal(allowed, await new SystemBrowserWebPreview(launcher).OpenAsync(url));
        Assert.Equal(allowed ? [url] : [], launcher.Opened);
    }

    private sealed class RecordingPreview : IWebPreview
    {
        public List<string> Opened { get; } = [];
        public string Id => "recording";
        public Task<bool> OpenAsync(string url, CancellationToken ct = default) { Opened.Add(url); return Task.FromResult(true); }
    }
}
