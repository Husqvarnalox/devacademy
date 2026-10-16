using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Application.Visualization;
using DevAcademy.Content;
using DevAcademy.Desktop.Markdown;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>Records every tool invocation and answers with canned output: nothing is ever installed.</summary>
internal sealed class RecordingToolRunner : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public List<string> Plain { get; } = [];
    public string? Find(string name) => name == "uv" ? "/fake/uv" : null;

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct)
    {
        Plain.Add(fileName + " " + string.Join(' ', args));
        return Task.FromResult(new ToolOutput(1, "", "not available in tests"));
    }

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        onLine("Collecting numpy");
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class PythonEnvironmentsViewModelTests
{
    private static void FakeEnv(string dataDir, string name)
    {
        string env = Path.Combine(dataDir, "python-envs", name);
        string bin = Path.Combine(env, OperatingSystem.IsWindows() ? "Scripts" : "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(env, "pyvenv.cfg"), "home = /usr/bin\nversion = 3.12.1\n");
        File.WriteAllText(Path.Combine(bin, OperatingSystem.IsWindows() ? "python.exe" : "python"), "");
        Directory.CreateDirectory(Path.Combine(env, OperatingSystem.IsWindows() ? "Lib" : Path.Combine("lib", "python3.12"), "site-packages", "pip"));
    }

    private static async Task<(TestHost host, RecordingToolRunner tools, PythonEnvironmentsViewModel vm)> SetupAsync()
    {
        var tools = new RecordingToolRunner();
        string dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        FakeEnv(dir, "demo");
        var host = new TestHost(dir, configure: s => s.AddSingleton<IToolRunner>(tools));
        var vm = new PythonEnvironmentsViewModel(host.Services);
        await vm.RefreshEnvironmentsAsync();
        Assert.Equal("demo", vm.SelectedEnvironment?.Name);
        return (host, tools, vm);
    }

    [Fact]
    public async Task Install_asks_for_confirmation_with_the_exact_command_and_the_network_notice()
    {
        var (host, tools, vm) = await SetupAsync();
        using (host)
        {
            host.Dialogs.Answer = true;
            vm.PackageText = "numpy>=2";
            await vm.InstallPackagesCommand.ExecuteAsync(null);

            Assert.Single(host.Dialogs.Confirmed);
            string msg = host.Dialogs.ConfirmedMessages[0];
            Assert.Contains("numpy>=2", msg);
            Assert.Contains("pip", msg);
            Assert.Contains("PyPI", msg);
            string call = Assert.Single(tools.Streaming);
            Assert.Contains("install", call);
            Assert.Contains("numpy>=2", call);
            Assert.Contains(Path.Combine("python-envs", "demo"), call.Split(' ')[0]);   // the env's own interpreter, not the system one
            Assert.Contains("Collecting numpy", vm.InstallLog);
        }
    }

    [Fact]
    public async Task Declining_the_confirmation_runs_nothing()
    {
        var (host, tools, vm) = await SetupAsync();
        using (host)
        {
            host.Dialogs.Answer = false;
            vm.PackageText = "numpy";
            await vm.InstallPackagesCommand.ExecuteAsync(null);

            Assert.Single(host.Dialogs.Confirmed);
            Assert.Empty(tools.Streaming);
            Assert.DoesNotContain(tools.Plain, c => c.Contains("install"));
            Assert.Equal("", vm.InstallLog);
            Assert.False(vm.IsInstalling);
        }
    }

    [Theory]
    [InlineData("--index-url=http://evil")]
    [InlineData("https://evil/x.whl")]
    [InlineData("./local")]
    public async Task Hostile_specs_are_refused_before_any_dialog_or_process(string spec)
    {
        var (host, tools, vm) = await SetupAsync();
        using (host)
        {
            vm.PackageText = spec;
            await vm.InstallPackagesCommand.ExecuteAsync(null);
            Assert.Empty(host.Dialogs.Confirmed);
            Assert.Empty(tools.Streaming);
            Assert.NotNull(vm.Message);
        }
    }

    [Fact]
    public async Task Delete_needs_confirmation_and_declining_keeps_the_env()
    {
        var (host, _, vm) = await SetupAsync();
        using (host)
        {
            host.Dialogs.Answer = false;
            await vm.DeleteCommand.ExecuteAsync(null);
            Assert.Single(host.Dialogs.Confirmed);
            Assert.True(Directory.Exists(Path.Combine(host.DataDir, "python-envs", "demo")));

            host.Dialogs.Answer = true;
            await vm.DeleteCommand.ExecuteAsync(null);
            Assert.False(Directory.Exists(Path.Combine(host.DataDir, "python-envs", "demo")));
            Assert.Empty(vm.Environments);
        }
    }

    [Fact]
    public async Task Choosing_the_active_env_persists()
    {
        var (host, _, vm) = await SetupAsync();
        using (host)
        {
            await vm.SetActiveCommand.ExecuteAsync(null);
            Assert.Equal("demo", await host.Services.Python.GetActiveNameAsync(CancellationToken.None));
            await vm.ClearActiveCommand.ExecuteAsync(null);
            Assert.Null(await host.Services.Python.GetActiveNameAsync(CancellationToken.None));
        }
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class GradientDescentViewModelTests
{
    [Fact]
    public void Every_frame_is_a_trace_step_and_play_stops_at_the_end()
    {
        var vm = new GradientDescentViewModel(GdFunctions.QuadraticId, GdOptimizerKind.Gd, 0.1);
        Assert.Equal(0, vm.Position);
        vm.StepForwardCommand.Execute(null);
        Assert.Equal(1, vm.Position);
        Assert.Equal(vm.Trace.Steps[1].Loss, vm.Frame.Current.Loss);
        vm.PlayPauseCommand.Execute(null);
        for (int i = 0; i < vm.StepCount + 2; i++) vm.Tick();
        Assert.True(vm.IsFinished);
        Assert.False(vm.IsPlaying);
        vm.ResetCommand.Execute(null);
        Assert.Equal(0, vm.Position);
    }

    [Fact]
    public void Start_point_change_recomputes_the_trace_and_bad_input_is_ignored()
    {
        var vm = new GradientDescentViewModel(GdFunctions.QuadraticId, GdOptimizerKind.Gd, 0.1);
        vm.SetStart(1, -1);
        Assert.Equal([1, -1], vm.Trace.Steps[0].Parameters);
        vm.SetStart(double.NaN, 0);
        Assert.Equal([1, -1], vm.Trace.Steps[0].Parameters);
    }

    [Fact]
    public void Directive_lists_in_the_validator_match_the_lab()
    {
        Assert.Equal(GdFunctions.Ids.OrderBy(x => x), TheoryParser.GradientDescentFunctions.OrderBy(x => x));
        foreach (string o in TheoryParser.GradientDescentOptimizers) Assert.True(GradientDescentLab.TryParseOptimizer(o, out _), o);
        foreach (GdOptimizerKind k in Enum.GetValues<GdOptimizerKind>())
            Assert.Contains(GradientDescentLab.OptimizerId(k), TheoryParser.GradientDescentOptimizers);
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class PythonRuntimesRenderTests
{
    private static void Settle() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Gradient_descent_lab_and_markdown_directive_render_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        var md = MarkdownRenderer.Render("```visualize gradient-descent function=rosenbrock optimizer=adam lr=0.02\n```\n");
        var viz = Assert.Single(md.GetLogicalDescendants().OfType<GradientDescentView>());
        Assert.Equal(GdFunctions.RosenbrockId, viz.ViewModel.SelectedFunction.Id);
        Assert.Equal(GdOptimizerKind.Adam, viz.ViewModel.SelectedOptimizer.Kind);

        var variant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = new Window { Width = 1000, Height = 800, RequestedThemeVariant = variant, Content = new GradientDescentView() };
        window.Show();
        Settle();
        var view = (GradientDescentView)window.Content!;
        Assert.NotNull(view.GetVisualDescendants().OfType<Controls.GradientPlotControl>().Single());
        Assert.NotNull(view.GetVisualDescendants().OfType<Controls.LossChartControl>().Single());
        foreach (var f in view.ViewModel.Functions)
        {
            view.ViewModel.SelectedFunction = f;
            view.ViewModel.StepForwardCommand.Execute(null);
            Settle();
        }
        foreach (var o in view.ViewModel.Optimizers)
        {
            view.ViewModel.SelectedOptimizer = o;
            Settle();
        }
        window.Close();
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Runtimes_tab_python_and_ml_sections_render_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        string dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        var tools = new RecordingToolRunner();
        using var host = new TestHost(dir, configure: s => s.AddSingleton<IToolRunner>(tools));
        var window = new MainWindow { DataContext = host.Main, Width = 1280, Height = 900 };
        window.Show();
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        await host.Main.InitializeAsync();
        await Helpers.CompleteOnboardingAsync(host);

        await host.Main.ShowSettingsAsync();
        var settings = (SettingsViewModel)host.Main.Current!;
        settings.SelectedTab = 1;
        Settle();
        Assert.NotNull(window.GetVisualDescendants().OfType<PythonEnvironmentsView>().SingleOrDefault());
        Assert.NotNull(window.GetVisualDescendants().OfType<MlAccelerationView>().SingleOrDefault());

        // Populate every list: an env on disk, hardware and a framework probe.
        string env = Path.Combine(dir, "python-envs", "demo");
        Directory.CreateDirectory(Path.Combine(env, "bin"));
        File.WriteAllText(Path.Combine(env, "pyvenv.cfg"), "home = /usr/bin\nversion = 3.12.1\n");
        File.WriteAllText(Path.Combine(env, "bin", "python"), "");
        await settings.Python.RefreshEnvironmentsAsync();
        await settings.Ml.RefreshEnvironmentsAsync();
        await settings.Ml.DetectCommand.ExecuteAsync(null);
        await settings.Ml.ProbeCommand.ExecuteAsync(null);
        settings.Python.InstallLog = "line 1\nline 2";
        settings.Python.Message = "ok";
        Settle();

        Assert.NotEmpty(settings.Ml.Hardware);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Python-окружения");
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "ML-ускорение");
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Установить пакеты…");
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Проверить в окружении…");
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        window.Close();
    }
}
