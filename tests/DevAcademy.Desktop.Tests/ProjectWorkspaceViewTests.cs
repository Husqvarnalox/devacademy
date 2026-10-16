using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>The Projects screen with an open project, editor tabs and a terminal panel renders in both themes without binding errors.</summary>
[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class ProjectWorkspaceViewTests
{
    private static string MakeProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "da-view-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "main.py"), "def greet(name):\n    return f'hello, {name}'\n\n\nprint(greet('world'))\n");
        File.WriteAllText(Path.Combine(dir, "requirements.txt"), "pytest>=8\n");
        File.WriteAllText(Path.Combine(dir, "src", "util.py"), "VALUE = 1\n");
        return dir;
    }

    private static async Task<(MainWindow Window, ProjectWorkspaceViewModel Ws, FakeTerminalFactory Terminals)> ShowAsync(TestHost host, TestHostSetup setup, string theme)
    {
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await host.Main.InitializeAsync();
        await host.Main.ShowProjectsAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var projects = (ProjectsViewModel)host.Main.Current!;
        await projects.OpenAsync(setup.Project);
        var ws = projects.Project!;
        await ws.OpenFileAsync(Path.Combine(setup.Project, "main.py"));
        await ws.OpenFileAsync(Path.Combine(setup.Project, "src", "util.py"));
        var term = ws.NewTerminal()!;
        var fake = (FakeTerminalSession)term.Session;
        fake.Screen.Feed("\u001b[32muser@host\u001b[0m:\u001b[34m~/project\u001b[0m$ python main.py\r\nhello, world\r\n\u001b[32muser@host\u001b[0m:\u001b[34m~/project\u001b[0m$ ");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, ws, setup.Terminals);
    }

    public sealed record TestHostSetup(string Project, FakeTerminalFactory Terminals);

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Projects_screen_with_terminal_renders_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        var project = MakeProject();
        var terminals = new FakeTerminalFactory();
        using var host = new TestHost(configure: s => s.AddSingleton<DevAcademy.Terminal.ITerminalFactory>(terminals));
        host.Services.Workspace.WatchFiles = false;

        // Home first (no project open), then the workspace.
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.Show();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowProjectsAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.GetVisualDescendants().OfType<ProjectsView>().SingleOrDefault());
        window.Close();
        await host.Main.ShowCatalogAsync();

        var (w, ws, _) = await ShowAsync(host, new TestHostSetup(project, terminals), theme);
        Assert.NotNull(w.GetVisualDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());
        Assert.NotNull(w.GetVisualDescendants().OfType<TerminalControl>().SingleOrDefault());
        Assert.NotEmpty(w.GetVisualDescendants().OfType<CodeEditorControl>());

        ws.Explorer.ShowIgnored = true;
        await ws.ToggleSplitCommand.ExecuteAsync(null);
        ws.Search.TextQuery = "greet";
        await ws.Search.SearchTextAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(ws.IsSplit);
        Assert.NotEmpty(ws.Search.TextResults);

        // Leaving and coming back keeps the open project (and its terminals).
        await host.Main.ShowDashboardAsync();
        await host.Main.ShowProjectsAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Same(ws, ((ProjectsViewModel)host.Main.Current!).Project);
        Assert.Single(ws.Terminals);

        w.Close();
        ws.Dispose();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();   // flush pending UI work so it cannot leak into the next test's capture
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        try { Directory.Delete(project, true); } catch (IOException) { }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_project_workspace_screen(string theme)
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);
        var project = MakeProject();
        var terminals = new FakeTerminalFactory();
        using var host = new TestHost(configure: s => s.AddSingleton<DevAcademy.Terminal.ITerminalFactory>(terminals));
        host.Services.Workspace.WatchFiles = false;
        await Helpers.CompleteOnboardingAsync(host);
        var (window, ws, _) = await ShowAsync(host, new TestHostSetup(project, terminals), theme);
        ws.Explorer.Find(Path.Combine(project, "src"))!.IsExpanded = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        using var file = File.Create(Path.Combine(dir!, $"{theme.ToLowerInvariant()}-11-project-workspace.png"));
        frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        window.Close();
        ws.Dispose();
        try { Directory.Delete(project, true); } catch (IOException) { }
    }

    /// <summary>The same screen with a REAL pseudo-terminal (the user's shell): proves the PTY output reaches the rendered control.</summary>
    [AvaloniaFact]
    public async Task Real_shell_output_is_rendered_by_the_terminal_control()
    {
        if (OperatingSystem.IsWindows()) return;
        var project = MakeProject();
        using var host = new TestHost();
        host.Services.Workspace.WatchFiles = false;
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.Show();
        await host.Main.InitializeAsync();
        await host.Main.ShowProjectsAsync();
        var projects = (ProjectsViewModel)host.Main.Current!;
        await projects.OpenAsync(project);
        var ws = projects.Project!;
        var tab = ws.NewTerminal()!;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        tab.Session.Write("echo real-pty-$((20+22))\r");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        string Screen() { lock (tab.Session.Screen.SyncRoot) return tab.Session.Screen.GetScreenText(); }
        while (!Screen().Contains("real-pty-42") && DateTime.UtcNow < deadline) { await Task.Delay(50); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
        Assert.Contains("real-pty-42", Screen());
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        if (Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS") is { Length: > 0 } dir)
        {
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir, "real-pty-project-workspace.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        window.Close();
        ws.Dispose();
        try { Directory.Delete(project, true); } catch (IOException) { }
    }
}
