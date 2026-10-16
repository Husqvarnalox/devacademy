using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Debugging.Adapters;
using DevAcademy.Debugging.Session;
using DevAcademy.Debugging.Tests;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>The debug panel in a stopped state (stack, variables, watch, console, execution-line highlight, breakpoint gutter) renders in both themes without binding errors.</summary>
[Collection(BindingCaptureCollection.Name)]
public class DebugRenderTests
{
    private const string Script = "total = 0\nfor i in range(3):\n    total += i\n    print(total)\nprint('done')\n";

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Stopped_debug_panel_renders_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        string project = Path.Combine(Path.GetTempPath(), "da-dbg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(project, ".venv", "bin"));
        File.WriteAllText(Path.Combine(project, "main.py"), Script);
        File.WriteAllText(Path.Combine(project, ".venv", "bin", "python"), "");
        var fake = new FakeAdapter { BodyLine = 3, Iterations = 2 };
        using var host = new TestHost(configure: s => s.AddSingleton(sp =>
        {
            var exec = sp.GetRequiredService<ExecutionOptions>();
            var resolver = new AdapterResolver(exec.CacheDirectory, probe: (_, _, _) => Task.FromResult(new ProbeResult(0, "")));
            var svc = new DebugService(sp.GetRequiredService<AppPaths>(), exec, launcher: new FakeAdapterLauncher(fake), resolver: resolver);
            return svc;
        }));
        host.Services.Workspace.WatchFiles = false;
        await Helpers.CompleteOnboardingAsync(host);

        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await host.Main.ShowProjectsAsync();
        var projects = (ProjectsViewModel)host.Main.Current!;
        await projects.OpenAsync(project);
        var ws = projects.Project!;
        var tab = await ws.OpenFileAsync(Path.Combine(project, "main.py"));
        tab!.Debug!.Toggle(3);
        await ws.Debug.StartAsync();
        await Util.UntilAsync(() => { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return ws.Debug.State == DebugState.Stopped && ws.Debug.Variables.Any(v => v.Children.Count > 0); }, "stop: " + theme + " " + ws.Debug.State + " " + ws.Debug.StatusText + " | " + string.Join(" | ", ws.Debug.Console.Select(c => c.Text)));
        ws.Debug.WatchInput = "i";
        await ws.Debug.AddWatchCommand.ExecuteAsync(null);
        ws.Debug.ReplInput = "total";
        await ws.Debug.EvaluateCommand.ExecuteAsync(null);
        ws.ShowDebugPanel = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var panel = window.GetVisualDescendants().OfType<DebugPanelView>().SingleOrDefault();
        Assert.NotNull(panel);
        Assert.NotEmpty(window.GetVisualDescendants().OfType<CodeEditorControl>());

        if (Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir, $"{theme.ToLowerInvariant()}-12-debugger.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        await ws.Debug.StopCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => !ws.Debug.IsActive, "end");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();
        ws.Dispose();
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        try { Directory.Delete(project, true); } catch (IOException) { }
    }
}
