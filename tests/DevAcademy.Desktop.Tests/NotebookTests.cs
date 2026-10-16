using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.ViewModels.Notebooks;
using DevAcademy.Desktop.Views;
using DevAcademy.Notebooks.Kernel;
using DevAcademy.Notebooks.Nbformat;
using DevAcademy.Notebooks.Text;

namespace DevAcademy.Desktop.Tests;

/// <summary>A scripted kernel: no process, no sockets. Code is interpreted by <see cref="Script"/>.</summary>
internal sealed class FakeKernelClient(Dictionary<string, string>? scripts = null) : IKernelClient
{
    private int _count;
    private TaskCompletionSource? _interrupted;
    public KernelState State { get; private set; } = KernelState.NotStarted;
    public string? DeadReason { get; set; }
    public event Action<KernelState>? StateChanged;
    public List<string> Executed { get; } = [];
    public int Restarts { get; private set; }
    public bool ShutDown { get; private set; }
    public JsonArray Variables { get; set; } = [];
    public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Set(KernelState s) { State = s; StateChanged?.Invoke(s); }

    public Task StartAsync(CancellationToken ct) { Set(KernelState.Idle); return Task.CompletedTask; }

    public async Task<ExecuteResult> ExecuteAsync(string code, ExecuteOptions? options, Action<KernelEvent>? onEvent, CancellationToken ct)
    {
        if (options?.Silent == true)
            return new ExecuteResult("ok", null, null, null, new JsonObject
            {
                ["vars"] = new JsonObject { ["status"] = "ok", ["data"] = new JsonObject { ["application/json"] = Variables.DeepClone() } },
            });
        Executed.Add(code);
        var script = scripts is not null && scripts.TryGetValue(code, out var s) ? s : code;
        var n = ++_count;
        Set(KernelState.Busy);
        try
        {
            // Events arrive on a background thread, like from the real client.
            await Task.Run(async () =>
            {
                foreach (var line in script.Split('\n'))
                {
                    if (line.StartsWith("print:")) onEvent?.Invoke(new OutputEvent(NotebookOutput.StreamOutput("stdout", line[6..] + "\n")));
                    else if (line.StartsWith("result:"))
                        onEvent?.Invoke(new OutputEvent(new NotebookOutput { OutputType = NotebookOutput.ExecuteResult, ExecutionCount = n, Data = new JsonObject { ["text/plain"] = line[7..] } }));
                    else if (line == "wait")
                    {
                        _interrupted = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        Blocked.TrySetResult();
                        await _interrupted.Task;
                        onEvent?.Invoke(new OutputEvent(Error("KeyboardInterrupt", "")));
                        return;
                    }
                    else if (line.StartsWith("png:"))
                        onEvent?.Invoke(new OutputEvent(new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["image/png"] = line[4..], ["text/plain"] = "<Figure>" } }));
                    else if (line == "1/0")
                    {
                        onEvent?.Invoke(new OutputEvent(Error("ZeroDivisionError", "division by zero")));
                        return;
                    }
                }
            }, ct);
            var failed = script.Contains("1/0") || _interrupted is not null && script.Contains("wait");
            return failed
                ? new ExecuteResult("error", n, script.Contains("wait") ? "KeyboardInterrupt" : "ZeroDivisionError", "", null)
                : new ExecuteResult("ok", n, null, null, null);
        }
        finally { Set(KernelState.Idle); }
    }

    private static NotebookOutput Error(string name, string value) => new()
    {
        OutputType = NotebookOutput.Error,
        ErrorName = name,
        ErrorValue = value,
        Traceback = ["\u001b[31m---------------------------------------------------------------------------\u001b[0m", $"\u001b[31m{name}\u001b[0m: {value}"],
    };

    public Task InterruptAsync() { _interrupted?.TrySetResult(); return Task.CompletedTask; }
    public Task RestartAsync(CancellationToken ct) { Restarts++; _count = 0; Set(KernelState.Idle); return Task.CompletedTask; }
    public Task ShutdownAsync() { ShutDown = true; Set(KernelState.Stopped); return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeKernelService : INotebookKernelService
{
    public bool Installed { get; set; } = true;
    public List<FakeKernelClient> Created { get; } = [];
    public List<string> InstallCommands { get; } = [];
    public bool VenvOnly { get; set; } = true;
    public Dictionary<string, string> Scripts { get; } = [];

    public IKernelClient Create(string interpreter, string workingDirectory)
    {
        var k = new FakeKernelClient(Scripts);
        Created.Add(k);
        return k;
    }

    public Task<string?> GetInstalledVersionAsync(string interpreter, CancellationToken ct) => Task.FromResult<string?>(Installed ? "7.4.0" : null);

    public KernelInstallPlan? PlanInstall(string interpreter) =>
        VenvOnly ? new KernelInstallPlan(interpreter, ["-m", "pip", "install", "ipykernel==7.4.0"], $"{interpreter} -m pip install ipykernel==7.4.0", Path.GetDirectoryName(Path.GetDirectoryName(interpreter))!) : null;

    public Task<KernelInstallResult> InstallAsync(KernelInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        InstallCommands.Add(plan.CommandLine);
        onLine("Successfully installed ipykernel-7.4.0");
        Installed = true;
        return Task.FromResult(new KernelInstallResult(true, null));
    }
}

[Collection(BindingCaptureCollection.Name)]
public class NotebookTests
{
    private static string MakeProject(out string interpreter)
    {
        var dir = Path.Combine(Path.GetTempPath(), "da-nb-" + Guid.NewGuid().ToString("N"));
        var bin = Path.Combine(dir, ".venv", OperatingSystem.IsWindows() ? "Scripts" : "bin");
        Directory.CreateDirectory(bin);
        interpreter = Path.Combine(bin, OperatingSystem.IsWindows() ? "python.exe" : "python");
        File.WriteAllText(interpreter, "");
        File.WriteAllText(Path.Combine(dir, "pyvenv.cfg"), "");
        File.WriteAllText(Path.Combine(dir, ".venv", "pyvenv.cfg"), "");
        File.WriteAllText(Path.Combine(dir, "main.py"), "print('hi')\n");
        return dir;
    }

    private static NotebookViewModel MakeVm(FakeKernelService service, FakeDialogs dialogs, string interpreter, NotebookDocument? doc = null, bool withEnv = true)
    {
        var choices = new System.Collections.ObjectModel.ObservableCollection<PythonChoice>
        {
            new(PythonChoice.SystemKey, "system", null),
        };
        if (withEnv) choices.Add(new PythonChoice("env", "venv", interpreter));
        return new NotebookViewModel(Path.Combine(Path.GetTempPath(), "x-" + Guid.NewGuid().ToString("N")[..6] + ".ipynb"), doc ?? NotebookDocument.CreateNew(),
            service, dialogs, choices, c => c.InterpreterForCommand, Path.GetTempPath(), withEnv ? choices[1] : choices[0]);
    }

    [AvaloniaFact]
    public async Task Run_cell_collects_outputs_counter_history_and_variables()
    {
        var service = new FakeKernelService();
        var nb = MakeVm(service, new FakeDialogs(), "/p/.venv/bin/python");
        var cell = nb.Cells[0];
        cell.Source = "print:hi\nresult:42";
        Assert.True(nb.IsDirty);
        var kernelVars = new JsonArray { new JsonObject { ["name"] = "x", ["type"] = "int", ["summary"] = "42", ["shape"] = null } };

        Assert.True(await nb.RunCellAsync(cell));
        service.Created.Single().Variables = kernelVars;
        await nb.RefreshVariablesCommand.ExecuteAsync(null);

        Assert.Equal(1, cell.ExecutionCount);
        Assert.Equal("[1]", cell.Label);
        Assert.Equal(2, cell.Outputs.Count);
        Assert.Equal("hi\n", string.Concat(cell.Outputs[0].Runs.Select(r => r.Text)));
        Assert.Equal("42", string.Concat(cell.Outputs[1].Runs.Select(r => r.Text)));
        Assert.Equal(NotebookOutput.ExecuteResult, cell.Model.Outputs[1].OutputType);
        Assert.Equal(KernelState.Idle, nb.KernelState);
        Assert.Single(nb.History);
        Assert.True(nb.History[0].Ok);
        Assert.Equal("x", Assert.Single(nb.Variables).Name);
        await nb.DisposeAsync();
        Assert.True(service.Created[0].ShutDown);
    }

    [AvaloniaFact]
    public async Task Consecutive_stream_output_is_merged_and_clear_output_resets()
    {
        var service = new FakeKernelService();
        var nb = MakeVm(service, new FakeDialogs(), "/p/.venv/bin/python");
        var cell = nb.Cells[0];
        cell.Source = "print:a\nprint:b";
        await nb.RunCellAsync(cell);
        var only = Assert.Single(cell.Outputs);
        Assert.Equal("a\nb\n", string.Concat(only.Runs.Select(r => r.Text)));
        Assert.Equal("a\nb\n", Assert.Single(cell.Model.Outputs).Text);
        cell.ClearOutputs();
        Assert.Empty(cell.Outputs);
        Assert.Empty(cell.Model.Outputs);
        await nb.DisposeAsync();
    }

    [AvaloniaFact]
    public async Task Run_and_advance_selects_next_cell_and_adds_one_after_the_last()
    {
        var nb = MakeVm(new FakeKernelService(), new FakeDialogs(), "/p/.venv/bin/python");
        nb.Cells[0].Source = "result:1";
        await nb.RunAndAdvanceAsync(nb.Cells[0]);
        Assert.Equal(2, nb.Cells.Count);
        Assert.Same(nb.Cells[1], nb.SelectedCell);
        Assert.True(nb.Cells[1].IsSelected);
        Assert.False(nb.Cells[0].IsSelected);
        nb.Cells[1].Source = "result:2";
        await nb.RunAndAdvanceAsync(nb.Cells[0]);
        Assert.Equal(2, nb.Cells.Count);
        Assert.Same(nb.Cells[1], nb.SelectedCell);
        await nb.DisposeAsync();
    }

    [AvaloniaFact]
    public async Task Run_all_stops_at_the_first_error_and_markdown_is_rendered_not_executed()
    {
        var service = new FakeKernelService();
        var doc = NotebookDocument.CreateNew(false);
        doc.Cells.Add(NotebookCell.NewMarkdown("# Title"));
        doc.Cells.Add(NotebookCell.NewCode("result:1"));
        doc.Cells.Add(NotebookCell.NewCode("1/0"));
        doc.Cells.Add(NotebookCell.NewCode("result:3"));
        var nb = MakeVm(service, new FakeDialogs(), "/p/.venv/bin/python", doc);
        nb.Cells[0].IsEditing = true;
        await nb.RunAllAsync();

        Assert.False(nb.Cells[0].IsEditing);
        Assert.Equal(["result:1", "1/0"], service.Created.Single().Executed);
        Assert.Equal(2, nb.Cells[2].ExecutionCount);
        Assert.Equal(PresentationKind.Error, nb.Cells[2].Outputs.Single().Kind);
        Assert.Contains("ZeroDivisionError", string.Concat(nb.Cells[2].Outputs.Single().Runs.Select(r => r.Text)));
        Assert.Null(nb.Cells[3].ExecutionCount);
        Assert.False(nb.Cells[3].IsQueued);
        Assert.False(nb.IsRunningAll);
        await nb.DisposeAsync();
    }

    [AvaloniaFact]
    public async Task Interrupt_ends_a_running_cell_and_restart_asks_first()
    {
        var service = new FakeKernelService();
        var dialogs = new FakeDialogs();
        var nb = MakeVm(service, dialogs, "/p/.venv/bin/python");
        nb.Cells[0].Source = "wait";
        var run = nb.RunCellAsync(nb.Cells[0]);
        await service.Created.Single().Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(nb.Cells[0].IsRunning);
        await nb.InterruptCommand.ExecuteAsync(null);
        Assert.False(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(nb.Cells[0].IsRunning);
        Assert.Equal("KeyboardInterrupt", nb.Cells[0].Outputs.Single().Kind == PresentationKind.Error ? "KeyboardInterrupt" : "?");

        dialogs.Answer = false;
        await nb.RestartCommand.ExecuteAsync(null);
        Assert.Equal(0, service.Created[0].Restarts);
        dialogs.Answer = true;
        await nb.RestartCommand.ExecuteAsync(null);
        Assert.Equal(1, service.Created[0].Restarts);
        Assert.Empty(nb.Variables);
        await nb.DisposeAsync();
    }

    [AvaloniaFact]
    public async Task Missing_ipykernel_shows_the_exact_command_and_installs_only_after_consent()
    {
        var service = new FakeKernelService { Installed = false };
        var dialogs = new FakeDialogs { Answer = false };
        var nb = MakeVm(service, dialogs, "/p/.venv/bin/python");
        nb.Cells[0].Source = "result:1";

        Assert.False(await nb.RunCellAsync(nb.Cells[0]));
        Assert.True(nb.NeedsInstall);
        Assert.Equal("/p/.venv/bin/python -m pip install ipykernel==7.4.0", nb.InstallCommand);
        Assert.Empty(service.Created); // no kernel without ipykernel, and nothing was installed by itself
        Assert.Empty(service.InstallCommands);

        await nb.InstallKernelCommand.ExecuteAsync(null); // user declines
        Assert.Empty(service.InstallCommands);
        Assert.Contains("pip install ipykernel==7.4.0", dialogs.ConfirmedMessages.Last());

        dialogs.Answer = true;
        await nb.InstallKernelCommand.ExecuteAsync(null);
        Assert.Equal(["/p/.venv/bin/python -m pip install ipykernel==7.4.0"], service.InstallCommands);
        Assert.False(nb.NeedsInstall);
        Assert.Contains("Successfully installed", nb.InstallLog);
        Assert.True(await nb.RunCellAsync(nb.Cells[0]));
        await nb.DisposeAsync();
    }

    [AvaloniaFact]
    public async Task System_interpreter_is_refused_and_non_venv_has_no_install_command()
    {
        var service = new FakeKernelService();
        var nb = MakeVm(service, new FakeDialogs(), "", withEnv: false);
        Assert.False(await nb.RunCellAsync(nb.Cells[0]));
        Assert.Equal(DevAcademy.Desktop.Localization.Loc.T("Nb_NeedEnvironment"), nb.Message);
        Assert.Empty(service.Created);

        var service2 = new FakeKernelService { Installed = false, VenvOnly = false };
        var nb2 = MakeVm(service2, new FakeDialogs(), "/usr/bin/python3");
        Assert.False(await nb2.RunCellAsync(nb2.Cells[0]));
        Assert.True(nb2.NeedsInstall);
        Assert.Equal("", nb2.InstallCommand);
        await nb2.InstallKernelCommand.ExecuteAsync(null);
        Assert.Empty(service2.InstallCommands);
    }

    [AvaloniaFact]
    public async Task Save_writes_valid_nbformat_with_outputs_and_markdown_cells_move_and_delete()
    {
        var service = new FakeKernelService();
        var nb = MakeVm(service, new FakeDialogs(), "/p/.venv/bin/python");
        nb.Cells[0].Source = "result:7";
        await nb.RunCellAsync(nb.Cells[0]);
        nb.AddMarkdownCommand.Execute(null);
        nb.Cells[1].Source = "## Notes";
        nb.MoveUpCommand.Execute(null);
        Assert.Equal("## Notes", nb.Cells[0].Source);
        nb.DeleteCellCommand.Execute(nb.Cells[0]);
        Assert.Single(nb.Cells);

        Assert.True(await nb.SaveAsync());
        Assert.False(nb.IsDirty);
        var back = await NotebookStore.LoadAsync(nb.Path);
        var cell = Assert.Single(back.Cells);
        Assert.Equal(1, cell.ExecutionCount);
        Assert.Equal("7", Assert.Single(cell.Outputs).GetText("text/plain"));
        File.Delete(nb.Path);
        await nb.DisposeAsync();
    }

    [AvaloniaFact]
    public async Task Output_beyond_the_limit_is_truncated_with_a_notice()
    {
        var cell = new NotebookCellViewModel(NotebookCell.NewCode(), () => { }, a => a());
        cell.Receive(new OutputEvent(NotebookOutput.StreamOutput("stdout", new string('x', NotebookCellViewModel.MaxOutputChars - 10))));
        cell.Receive(new OutputEvent(NotebookOutput.StreamOutput("stdout", new string('y', 100))));
        cell.Receive(new OutputEvent(NotebookOutput.StreamOutput("stdout", "z")));
        cell.Flush();
        Assert.Equal(2, cell.Outputs.Count);
        Assert.DoesNotContain("z", cell.Model.Outputs[0].Text);
        Assert.Contains(DevAcademy.Desktop.Localization.Loc.T("Nb_OutputTruncated"), cell.Model.Outputs[1].Text);
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task Workspace_opens_ipynb_creates_notebooks_and_asks_before_losing_changes()
    {
        var project = MakeProject(out _);
        var service = new FakeKernelService();
        using var host = new TestHost();
        host.Services.Workspace.WatchFiles = false;
        host.Services.Workspace.Notebooks = service;
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowProjectsAsync();
        var projects = (ProjectsViewModel)host.Main.Current!;
        await projects.OpenAsync(project);
        var ws = projects.Project!;

        host.Dialogs.PromptAnswers.Enqueue("analysis");
        await ws.NewNotebookCommand.ExecuteAsync(null);
        var path = Path.Combine(project, "analysis.ipynb");
        Assert.True(File.Exists(path));
        Assert.NotNull(ws.Notebook);
        Assert.Equal(path, ws.Notebook!.Path);
        Assert.Empty(ws.Groups.SelectMany(g => g.Tabs)); // not opened as a text tab
        Assert.NotNull(ws.Notebook.SelectedEnvironment); // the project .venv

        ws.Notebook.Cells[0].Source = "result:1";
        Assert.True(ws.HasUnsavedChanges);
        host.Dialogs.ChoiceAnswers.Enqueue(2); // cancel
        Assert.False(await ws.ConfirmCloseAllAsync());
        host.Dialogs.ChoiceAnswers.Enqueue(0); // save
        Assert.True(await ws.ConfirmCloseAllAsync());
        Assert.Contains("result:1", await File.ReadAllTextAsync(path));

        await ws.OpenFileAsync(Path.Combine(project, "main.py"));
        Assert.NotNull(ws.Notebook); // the notebook stays an overlay; text files still open as tabs
        Assert.Single(ws.Groups.SelectMany(g => g.Tabs));
        File.WriteAllText(Path.Combine(project, "broken.ipynb"), "{ not json");
        Assert.False(await ws.OpenNotebookAsync(Path.Combine(project, "broken.ipynb")));
        Assert.Contains("broken.ipynb", ws.Status);
        Assert.True(await ws.CloseNotebookAsync());
        Assert.Null(ws.Notebook);
        ws.Dispose();
    }

    // ------------------------------------------------------------------ rendering

    private static string PngBase64()
    {
        using var bmp = new WriteableBitmap(new PixelSize(200, 90), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = bmp.Lock())
        {
            for (var y = 0; y < 90; y++)
                for (var x = 0; x < 200; x++)
                {
                    var p = fb.Address + y * fb.RowBytes + x * 4;
                    System.Runtime.InteropServices.Marshal.WriteInt32(p, unchecked((int)(0xFF000000u | (uint)((int)(255 * y / 90.0) << 16) | (uint)((int)(160 * (1 - x / 200.0)) + 60 << 8) | (uint)(255 * x / 200))));
                }
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        return Convert.ToBase64String(ms.ToArray());
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Notebook_renders_all_output_kinds_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        var project = MakeProject(out _);
        var service = new FakeKernelService();
        var doc = NotebookDocument.CreateNew(false);
        doc.Cells.Add(NotebookCell.NewMarkdown("# Линейная регрессия\n\nМы подбираем веса $w$ так, чтобы минимизировать ошибку:\n\n$$ L(w) = \\frac{1}{n}\\sum_i (y_i - w x_i)^2 $$\n\n- пункт один\n- пункт два"));
        doc.Cells.Add(NotebookCell.NewCode("import numpy as np\nx = np.linspace(0, 1, 5)\nprint('x ready')\nx"));
        doc.Cells.Add(NotebookCell.NewCode("plt.plot(x, x ** 2)\nplt.show()"));
        doc.Cells.Add(NotebookCell.NewCode("1/0"));
        doc.Cells.Add(NotebookCell.NewCode("# ещё не выполнялась\nresult = x.mean()"));
        doc.Cells.Add(NotebookCell.NewMarkdown("Пустая заметка"));

        using var host = new TestHost();
        host.Services.Workspace.WatchFiles = false;
        host.Services.Workspace.Notebooks = service;
        await Helpers.CompleteOnboardingAsync(host);
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 1300 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await host.Main.ShowProjectsAsync();
        var projects = (ProjectsViewModel)host.Main.Current!;
        await projects.OpenAsync(project);
        var ws = projects.Project!;
        await NotebookStore.SaveAsync(Path.Combine(project, "regression.ipynb"), doc);
        await ws.OpenFileAsync(Path.Combine(project, "regression.ipynb"));
        var nb = ws.Notebook!;

        service.Scripts[nb.Cells[1].Source] = "print:\u001b[32mx ready\u001b[0m\nresult:array([0.  , 0.25, 0.5 , 0.75, 1.  ])";
        service.Scripts[nb.Cells[2].Source] = "png:" + PngBase64();
        await nb.RunCellAsync(nb.Cells[1]);
        await nb.RunCellAsync(nb.Cells[2]);
        await nb.RunCellAsync(nb.Cells[3]);
        // markdown + json outputs through the model
        nb.Cells[4].Model.Outputs.Add(new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["text/markdown"] = "**Готово**: среднее найдено", ["text/plain"] = "md" } });
        nb.Cells[4].ClearOutputs();
        nb.Cells[4].Receive(new OutputEvent(new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["text/markdown"] = "**Готово**: среднее найдено", ["text/plain"] = "md" } }));
        nb.Cells[4].Receive(new OutputEvent(new NotebookOutput { OutputType = NotebookOutput.DisplayData, Data = new JsonObject { ["application/json"] = new JsonObject { ["mean"] = 0.5 } } }));
        nb.Cells[4].Flush();
        service.Created.Single().Variables =
        [
            new JsonObject { ["name"] = "x", ["type"] = "ndarray", ["summary"] = "dtype float64", ["shape"] = "5" },
            new JsonObject { ["name"] = "n", ["type"] = "int", ["summary"] = "5", ["shape"] = null },
        ];
        await nb.RefreshVariablesCommand.ExecuteAsync(null);
        nb.Select(nb.Cells[1]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<NotebookView>().SingleOrDefault();
        Assert.NotNull(view);
        Assert.True(window.GetVisualDescendants().OfType<CodeEditorControl>().Count() >= 4);
        Assert.NotEmpty(window.GetVisualDescendants().OfType<AnsiTextBlock>());
        Assert.Contains(window.GetVisualDescendants().OfType<Avalonia.Controls.Image>(), i => i.Source is not null);
        Assert.NotEmpty(window.GetVisualDescendants().OfType<DevAcademy.Desktop.Markdown.MarkdownViewer>());
        Assert.Contains(DevAcademy.Desktop.Localization.Loc.T("Nb_NoSandbox"), VisibleText(view!));
        Assert.Contains(DevAcademy.Desktop.Localization.Loc.T("Nb_MathPlanned"), VisibleText(view!));

        if (Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS") is { Length: > 0 } dir)
        {
            for (var i = 0; i < 4; i++) { Thread.Sleep(100); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
            Directory.CreateDirectory(dir);
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir, $"{theme.ToLowerInvariant()}-13-notebook.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        // The "install ipykernel" panel and the system-interpreter notice also render.
        service.Installed = false;
        nb.SelectedEnvironment = nb.Environments.First();
        await nb.RunCellAsync(nb.Cells[4]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(string.IsNullOrEmpty(nb.Message));

        Assert.Empty(errors.Messages);
        window.Close();
        ws.Dispose();
    }

    private static string VisibleText(Avalonia.Visual root) =>
        string.Join("\n", root.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
}
