using DevAcademy.Application.Workspace;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Session;
using DevAcademy.Debugging.Tests;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>
/// The debugger view model against an in-process fake DAP adapter: breakpoint hit, call stack, lazy variables, watch, console,
/// stepping, breakpoint persistence, and the "unavailable / not installed" explanations. No real adapter or network is involved.
/// </summary>
public class DebugFlowTests : IDisposable
{
    private const string Script = "total = 0\nfor i in range(3):\n    total += i\n    print(total)\nprint('done')\n";

    private readonly string _project = Util.TempDir("proj");
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_project, true); } catch (IOException) { }
    }

    private static string VenvPython => OperatingSystem.IsWindows() ? @".venv\Scripts\python.exe" : ".venv/bin/python";

    private void MakePythonProject(bool withVenv = true)
    {
        File.WriteAllText(Path.Combine(_project, "main.py"), Script);
        if (!withVenv) return;
        string py = Path.Combine(_project, VenvPython);
        Directory.CreateDirectory(Path.GetDirectoryName(py)!);
        File.WriteAllText(py, "");   // only has to exist: the probe is faked
    }

    private TestHost NewHost(FakeAdapter? fake, bool debugpyInstalled = true)
    {
        var host = new TestHost(configure: s => s.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<AppPaths>();
            var exec = sp.GetRequiredService<ExecutionOptions>();
            var resolver = new AdapterResolver(exec.CacheDirectory, probe: (_, args, _) =>
                Task.FromResult(args.Count > 1 && args[1] == "import debugpy" && !debugpyInstalled ? new ProbeResult(1, "") : new ProbeResult(0, "")));
            var svc = new DebugService(paths, exec, launcher: fake is null ? null : new FakeAdapterLauncher(fake), resolver: resolver);
            svc.Invoke = a => a();
            return svc;
        }));
        host.Services.Workspace.WatchFiles = false;
        _disposables.Add(host);
        return host;
    }

    private static async Task<ProjectWorkspaceViewModel> OpenAsync(TestHost host, string path)
    {
        var projects = new ProjectsViewModel(host.Services);
        await projects.LoadAsync();
        await projects.OpenAsync(path);
        return projects.Project!;
    }

    // Frames are added before the top user frame is selected (which sets the editor's execution line), so wait for the selection too.
    private static Task StoppedAsync(DebugViewModel d) => Util.UntilAsync(() => d.State == DebugState.Stopped && d.Frames.Count > 0 && d.SelectedFrame is not null, "a stop");

    [Fact]
    public async Task Breakpoint_hit_shows_stack_variables_then_steps_and_finishes()
    {
        MakePythonProject();
        var fake = new FakeAdapter { BodyLine = 3, Iterations = 2, ExitCode = 0 };
        var host = NewHost(fake);
        var ws = await OpenAsync(host, _project);
        var tab = await ws.OpenFileAsync(Path.Combine(_project, "main.py"));
        tab!.Debug!.Toggle(3);
        Assert.True(tab.Debug.Has(3));

        Assert.True(ws.Debug.IsAvailable);
        Assert.True(ws.Debug.IsReady);
        await ws.Debug.StartAsync();
        await StoppedAsync(ws.Debug);

        // Adapter received the path of the real file and the breakpoint at line 3.
        Assert.Contains(3, fake.Breakpoints[Path.Combine(_project, "main.py")]);
        Assert.Equal("python", (string)fake.LaunchArguments!["type"]!);

        // Call stack: the user's frame is selected, the paused line is highlighted in the editor and the call stack lists both frames.
        Assert.Equal("body", ws.Debug.SelectedFrame!.Name);
        Assert.Equal("main.py", ws.Debug.SelectedFrame.HostKey);
        Assert.Equal(2, ws.Debug.Frames.Count);
        Assert.Equal(3, tab.Debug.ExecutionLine);

        // Variables: the Locals scope loads, "items" loads its children only when expanded.
        var locals = await Util.UntilAsync(() => ws.Debug.Variables.FirstOrDefault(v => v.Name == "Locals" && v.Children.Any(c => c.Name == "i")), "locals");
        var items = locals.Children.Single(c => c.Name == "items");
        Assert.True(items.Children[0].IsPlaceholder);
        Assert.False(items.IsLoaded);
        items.IsExpanded = true;
        await Util.UntilAsync(() => items.Children.Count == 2 && items.Children[0].Name == "[0]", "lazy children");
        Assert.Equal("'a'", items.Children[0].Value);

        // Watch and debug console evaluate in the selected frame.
        ws.Debug.WatchInput = "i";
        await ws.Debug.AddWatchCommand.ExecuteAsync(null);
        Assert.Equal("0", ws.Debug.Watches.Single().Value);
        ws.Debug.ReplInput = "total + 1";
        await ws.Debug.EvaluateCommand.ExecuteAsync(null);
        Assert.Contains(ws.Debug.Console, l => l.Text == "total + 1@repl" && l.Kind == ConsoleKind.Result);
        ws.Debug.ReplInput = "boom";
        await ws.Debug.EvaluateCommand.ExecuteAsync(null);
        Assert.Contains(ws.Debug.Console, l => l.IsError && l.Text.Contains("not defined"));

        // Step over moves the paused line; the stack is rebuilt.
        await ws.Debug.StepOverCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => tab.Debug.ExecutionLine == 4 && ws.Debug.State == DebugState.Stopped, "step");

        // A breakpoint added while paused reaches the adapter.
        tab.Debug.Toggle(5);
        await Util.UntilAsync(() => fake.Breakpoints[Path.Combine(_project, "main.py")].Contains(5), "live breakpoint");

        // Continue to the next hit, then to the end.
        await ws.Debug.ContinueCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => tab.Debug.ExecutionLine == 3 && ws.Debug.State == DebugState.Stopped, "second hit");
        await ws.Debug.ContinueCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => ws.Debug.State == DebugState.NotStarted && !ws.Debug.IsActive, "end");

        Assert.Null(tab.Debug.ExecutionLine);
        Assert.Empty(ws.Debug.Frames);
        Assert.Contains(ws.Debug.Console, l => l.Text == "done" && l.Kind == ConsoleKind.Output);
        Assert.Contains(ws.Debug.Console, l => l.Text.Contains("код 0"));
        Assert.True(ws.Debug.CanStartNow);
    }

    [Fact]
    public async Task Stop_ends_a_running_session_and_resets_the_view()
    {
        MakePythonProject();
        var fake = new FakeAdapter { BodyLine = 3, Iterations = 5 };
        var host = NewHost(fake);
        var ws = await OpenAsync(host, _project);
        var tab = await ws.OpenFileAsync(Path.Combine(_project, "main.py"));
        tab!.Debug!.Toggle(3);
        await ws.Debug.StartAsync();
        await StoppedAsync(ws.Debug);
        Assert.True(ws.Debug.StopCommand.CanExecute(null));

        await ws.Debug.StopCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => !ws.Debug.IsActive, "stopped");
        Assert.Null(tab.Debug.ExecutionLine);
        Assert.Empty(ws.Debug.Frames);
        Assert.Contains("disconnect", fake.Commands);
        Assert.False(ws.Debug.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task Breakpoints_are_saved_in_the_project_session_and_restored()
    {
        MakePythonProject();
        var host = NewHost(null);
        var ws = await OpenAsync(host, _project);
        var tab = await ws.OpenFileAsync(Path.Combine(_project, "main.py"));
        tab!.Debug!.Toggle(2);
        tab.Debug.Toggle(4);
        await ws.SaveSessionAsync();

        var saved = await host.Services.Workspace.Sessions.LoadAsync(ws.Root);
        Assert.Equal([2, 4], saved.Breakpoints!["main.py"]);
        ws.Dispose();

        var reopened = await OpenAsync(host, _project);
        var tab2 = reopened.FindTab(Path.Combine(_project, "main.py"));
        Assert.NotNull(tab2);
        Assert.Equal([2, 4], tab2!.Debug!.Breakpoints);

        // A rename carries the breakpoints along.
        host.Dialogs.PromptAnswers.Enqueue("renamed.py");
        reopened.Explorer.Load();
        reopened.Explorer.Selected = reopened.Explorer.Find(Path.Combine(_project, "main.py"));
        await reopened.RenameCommand.ExecuteAsync(null);
        Assert.Equal([2, 4], tab2.Debug!.Breakpoints);
        Assert.Equal("renamed.py", tab2.Debug.Key);
    }

    [Fact]
    public async Task Language_without_an_adapter_says_why_and_never_starts()
    {
        File.WriteAllText(Path.Combine(_project, "Main.kt"), "fun main() {}\n");
        var fake = new FakeAdapter();
        var host = NewHost(fake);
        var ws = await OpenAsync(host, _project);
        await ws.OpenFileAsync(Path.Combine(_project, "Main.kt"));
        await ws.Debug.RefreshAsync();

        Assert.False(ws.Debug.IsAvailable);
        Assert.False(string.IsNullOrWhiteSpace(ws.Debug.AdapterText));
        Assert.False(ws.Debug.StartCommand.CanExecute(null));
        await ws.Debug.StartOrContinueCommand.ExecuteAsync(null);
        Assert.Equal(DebugState.NotStarted, ws.Debug.State);
        Assert.Empty(fake.Commands);
    }

    [Fact]
    public async Task Missing_debugpy_shows_the_exact_install_command_and_declining_installs_nothing()
    {
        MakePythonProject();
        var fake = new FakeAdapter();
        var host = NewHost(fake, debugpyInstalled: false);
        var ws = await OpenAsync(host, _project);

        Assert.True(ws.Debug.IsAvailable);
        Assert.False(ws.Debug.IsReady);
        Assert.True(ws.Debug.CanInstall);
        string py = Path.Combine(_project, VenvPython);
        Assert.Contains("-m pip install debugpy", ws.Debug.InstallCommandText);
        Assert.Contains("PIP_REQUIRE_VIRTUALENV=1", ws.Debug.InstallCommandText);
        Assert.Contains(py, ws.Debug.InstallCommandText);

        // Starting is refused with the reason; nothing reaches an adapter.
        await ws.Debug.StartCommand.ExecuteAsync(null);
        Assert.Equal(DebugState.NotStarted, ws.Debug.State);
        Assert.Empty(fake.Commands);

        // The install button asks first, with the command in the dialog, and does nothing on "no".
        host.Dialogs.Answer = false;
        await ws.Debug.InstallCommand.ExecuteAsync(null);
        Assert.Contains(host.Dialogs.ConfirmedMessages, m => m.Contains("pip install debugpy"));
        Assert.True(ws.Debug.CanInstall);
        Assert.False(ws.Debug.IsInstalling);
    }

    [Fact]
    public async Task Lesson_editor_debugs_a_scratch_python_program_in_a_throwaway_directory()
    {
        var fake = new FakeAdapter { BodyLine = 3, Iterations = 1, ExitCode = 0 };
        var host = NewHost(fake);
        var interpreters = await host.Services.Python.GetInterpretersAsync(false, CancellationToken.None);
        Assert.SkipWhen(interpreters.Count == 0, "no Python interpreter on this machine");

        var session = ExerciseSessionViewModel.CreateScratch(host.Services, "lesson-x", "python", Script);
        var file = session.Files.Single();
        file.Debug!.Toggle(3);
        await session.Debug.RefreshAsync();
        Assert.True(session.Debug.IsAvailable);
        Assert.True(session.CanRun);

        await session.Debug.StartAsync();
        await StoppedAsync(session.Debug);
        string program = (string)fake.LaunchArguments!["program"]!;
        Assert.StartsWith(host.Services.Debug.RunsDirectory.Replace("/private", ""), program.Replace("/private", ""));
        Assert.Equal(Script, File.ReadAllText(program));
        Assert.Equal(3, file.Debug.ExecutionLine);
        Assert.Equal(ExerciseSessionViewModel.DebugTab, session.OutputTab);

        await session.Debug.ContinueCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => !session.Debug.IsActive, "end");
        Assert.False(File.Exists(program), "the throw-away directory is removed after the run");
    }

    [Fact]
    public async Task Real_lldb_debugs_a_scratch_cpp_program_through_the_lesson_editor()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "lldb-dap is not a supported C++ debugger on Windows (MinGW/MSVC binaries); C++ debugging is macOS/Linux only.");
        using var host = new TestHost();
        host.Services.Debug.Invoke = a => a();
        var status = await host.Services.Debug.GetStatusAsync("cpp", null);
        Assert.SkipWhen(!status.IsReady, "lldb-dap is not available: " + status.Message);
        var build = await host.Services.Debug.Builder.BuildAsync("cpp", Util.TempDir("probe"), "main.cpp", CancellationToken.None);
        Assert.SkipWhen(!build.Success && build.Output.Contains("not found", StringComparison.OrdinalIgnoreCase), "no C++ compiler");

        const string Code = "#include <cstdio>\n\nint main() {\n    int total = 0;\n    for (int i = 1; i <= 3; ++i) {\n        total += i;\n    }\n    std::printf(\"%d\\n\", total);\n    return 43;\n}\n";
        var session = ExerciseSessionViewModel.CreateScratch(host.Services, "lesson-cpp", "cpp", Code);
        var file = session.Files.Single();
        file.Debug!.Toggle(6);
        await session.Debug.RefreshAsync();
        Assert.True(session.Debug.IsReady);

        await session.Debug.StartAsync();
        await Util.UntilAsync(() => session.Debug.State == DebugState.Stopped && session.Debug.Frames.Count > 0, "lldb stop", 60000);
        Assert.Equal(6, session.Debug.SelectedFrame!.Frame.Line);
        Assert.Equal(6, file.Debug.ExecutionLine);
        var locals = await Util.UntilAsync(() => session.Debug.Variables.FirstOrDefault(v => v.Children.Any(c => c.Name == "total")), "locals", 30000);
        Assert.Equal("0", locals.Children.Single(c => c.Name == "total").Value);

        session.Debug.WatchInput = "total + i";
        await session.Debug.AddWatchCommand.ExecuteAsync(null);
        Assert.Equal("1", session.Debug.Watches.Single().Value);

        await session.Debug.StepOverCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => file.Debug.ExecutionLine is { } l && l != 6 && session.Debug.State == DebugState.Stopped, "step over: line " + file.Debug.ExecutionLine + ", " + session.Debug.State + ", " + string.Join(" | ", session.Debug.Console.Select(c => c.Text)), 30000);

        file.Debug.ClearAll();
        await session.Debug.ContinueCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => !session.Debug.IsActive, "lldb end", 30000);
        Assert.Contains(session.Debug.Console, l => l.Text.Contains("код 43"));
    }
}
