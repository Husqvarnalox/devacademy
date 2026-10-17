using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
using DevAcademy.Domain;
using DevAcademy.Execution;
using DevAcademy.Infrastructure;
using DevAcademy.Notebooks.Nbformat;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>Renders real frames of the main screens to PNG for visual review. Runs only when DEVACADEMY_SCREENSHOTS points at an output dir.</summary>
public class ScreenshotTests
{
    /// <summary>Lets theme transitions (Expander chevron, hover fades) finish before a frame is captured.</summary>
    private static void Settle()
    {
        for (int i = 0; i < 4; i++)
        {
            Thread.Sleep(100);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_main_screens(string theme)
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);

        using var host = new TestHost();
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();

        void Save(string name)
        {
            Settle();
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir!, $"{theme.ToLowerInvariant()}-{name}.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        await host.Main.InitializeAsync();
        Save("1-onboarding");
        ((OnboardingViewModel)host.Main.Current!).Editor.SelectLanguage(((OnboardingViewModel)host.Main.Current!).Editor.Languages.First(l => l.IsAvailable));
        Save("1b-onboarding-selected");
        await Helpers.CompleteOnboardingAsync(host);
        Save("2-dashboard-landing");
        await host.Main.ShowCatalogAsync();
        Save("2-catalog");
        await host.Main.OpenCourseAsync(Helpers.CourseId);
        Save("3-roadmap");
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        Save("4-lesson-theory");
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        ws.SelectModeCommand.Execute(LessonMode.Practice);
        Save("5-lesson-practice");
        ws.SelectModeCommand.Execute(LessonMode.Quiz);
        Save("6-lesson-quiz");
        await host.Main.ShowDashboardAsync();
        Save("7-dashboard");
        await host.Main.ShowSettingsAsync();
        Save("8-settings");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_compact_window_screens(string theme)
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);

        using var host = new TestHost();
        var window = new MainWindow { DataContext = host.Main, Width = 1024, Height = 700 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();

        void Save(string name)
        {
            Settle();
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir!, $"{theme.ToLowerInvariant()}-{name}.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        await host.Main.InitializeAsync();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowCatalogAsync();
        Save("c1-catalog-1024");
        await host.Main.OpenCourseAsync(Helpers.CourseId);
        Save("c2-roadmap-1024");
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        Save("c3-lesson-theory-1024");
        ((LessonWorkspaceViewModel)host.Main.Current!).SelectModeCommand.Execute(LessonMode.Practice);
        Save("c4-lesson-practice-1024");
        await host.Main.ShowDashboardAsync();
        Save("c5-dashboard-1024");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_hub_screens(string theme)
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);

        using var host = new TestHost();
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();

        void Save(string name)
        {
            Settle();
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir!, $"{theme.ToLowerInvariant()}-{name}.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        await host.Main.InitializeAsync();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowProblemsAsync();
        Save("h1-problems");
        await host.Main.ShowProjectsAsync();
        Save("h2-projects");
        await host.Main.ShowSettingsAsync();
        ((SettingsViewModel)host.Main.Current!).SelectedTab = 1;
        Save("h3-settings-runtimes");
        ((SettingsViewModel)host.Main.Current!).SelectedTab = 0;
        Save("h4-settings-general");
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        ((LessonWorkspaceViewModel)host.Main.Current!).SelectModeCommand.Execute(LessonMode.Quiz);
        Save("h5-quiz");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_path_and_comparison_screens(string theme)
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);

        using var host = new TestHost(contentRoot: TestHost.PathsRoot);
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();

        void Save(string name)
        {
            Settle();
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir!, $"{theme.ToLowerInvariant()}-{name}.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        await host.Main.InitializeAsync();
        var onboarding = (OnboardingViewModel)host.Main.Current!;
        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
        Save("p1-onboarding-paths");
        await onboarding.FinishCommand.ExecuteAsync(null);
        await host.Main.ShowCatalogAsync();
        Save("p2-catalog-paths-comparisons");
        await host.Main.OpenPathAsync("python-web");
        Save("p3-path");
        await host.Main.OpenPathAsync("python-deep");
        ((PathViewModel)host.Main.Current!).Stages.SelectMany(s => s.Courses).Single(c => c.CourseId == "py-adv").TakePlacementCommand.Execute(null);
        Save("p4-path-placement");
        await host.Main.OpenCourseAsync("web-django");
        Save("p5-course-framework");
        await host.Main.OpenComparisonAsync("web-compare");
        Save("p6-comparison-wide");
        window.Width = 600;
        Save("p7-comparison-narrow");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_ai_screens(string theme)
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);

        var fake = new FakeModelProvider();
        using var host = AiKit.CreateHost(fake);
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();

        void Save(string name)
        {
            Settle();
            using var frame = window.CaptureRenderedFrame();
            using var file = File.Create(Path.Combine(dir!, $"{theme.ToLowerInvariant()}-{name}.png"));
            frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        await host.Main.InitializeAsync();
        await Helpers.CompleteOnboardingAsync(host);
        await AiKit.EnableAsync(host);

        await host.Main.ShowAiSettingsAsync();
        var ai = ((SettingsViewModel)host.Main.Current!).Ai;
        var p = ai.Providers.Single(x => x.Id == "fake");
        ai.SelectedProvider = p;
        await p.RefreshModelsCommand.ExecuteAsync(null);
        p.SelectedModel = p.Models[1];
        Save("9-settings-ai");

        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        ws.Tutor.IsExpanded = true;
        await ws.Tutor.AskCommand.ExecuteAsync(DevAcademy.Application.Tutor.TutorScenario.Explain);
        await ws.Tutor.SendCommand.ExecuteAsync(null);
        Save("10-lesson-tutor");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Capture_ml_visualizers(string theme)
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);
        var km = new KMeansViewModel(3, DevAcademy.Application.Visualization.KMeansInit.KMeansPlusPlus, 1);
        var pca = new PcaViewModel(null);
        for (int i = 0; i < 3; i++) km.StepForwardCommand.Execute(null);
        for (int i = 0; i < 3; i++) pca.StepForwardCommand.Execute(null);
        foreach (var (name, view) in new (string, Avalonia.Controls.Control)[] { ("kmeans", new KMeansView(km)), ("pca", new PcaView(pca)) })
        {
            var window = new Avalonia.Controls.Window
            {
                Content = view,
                Width = 900,
                Height = 640,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            };
            window.Show();
            Settle();
            using var frame = window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(dir!, $"viz-{name}-{theme.ToLowerInvariant()}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            window.Close();
        }
    }

    // ---------------------------------------------------------------- screens that were only class-swapped (redesign pass 2)

    private static string? ShotDir()
    {
        string? dir = Environment.GetEnvironmentVariable("DEVACADEMY_SCREENSHOTS");
        Assert.SkipWhen(string.IsNullOrEmpty(dir), "Set DEVACADEMY_SCREENSHOTS to capture screenshots.");
        Directory.CreateDirectory(dir!);
        return dir;
    }

    private static ThemeVariant Variant(string theme) => theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

    private static void Shot(Window window, string dir, string theme, string name)
    {
        Settle();
        using var frame = window.CaptureRenderedFrame();
        using var file = File.Create(Path.Combine(dir, $"{theme.ToLowerInvariant()}-{name}.png"));
        frame?.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static void ScrollTo(Window window, string name)
    {
        var target = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);
        target.BringIntoView();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_settings_tabs_and_runtime_sections(string theme)
    {
        string dir = ShotDir()!;
        using var host = new TestHost();
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
        window.Show();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowSettingsAsync();
        var settings = (SettingsViewModel)host.Main.Current!;
        settings.SelectedTab = 0;
        Shot(window, dir, theme, "s0-settings-general");
        settings.SelectedTab = 1;
        await settings.DetectRuntimesCommand.ExecuteAsync(null);
        Shot(window, dir, theme, "s1-settings-runtimes");
        foreach (var section in new[] { "LanguageServersSection", "PythonSection", "NodeSection", "GoSection", "NuGetSection", "MavenSection", "MlSection" })
        {
            ScrollTo(window, section);
            Shot(window, dir, theme, "s1-" + section.Replace("Section", "").ToLowerInvariant());
        }
        settings.SelectedTab = 2;
        Shot(window, dir, theme, "s2-settings-data");
        settings.SelectedTab = 4;
        Shot(window, dir, theme, "s4-settings-about");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_ai_settings_empty_and_configured(string theme)
    {
        string dir = ShotDir()!;
        var fake = new FakeModelProvider();
        using var host = AiKit.CreateHost(fake);
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
        window.Show();
        await host.Main.InitializeAsync();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowAiSettingsAsync();
        Shot(window, dir, theme, "a0-ai-settings-initial");
        await AiKit.EnableAsync(host);
        var ai = ((SettingsViewModel)host.Main.Current!).Ai;
        var p = ai.Providers.Single(x => x.Id == "fake");
        ai.SelectedProvider = p;
        await p.RefreshModelsCommand.ExecuteAsync(null);
        p.SelectedModel = p.Models[1];
        Shot(window, dir, theme, "a1-ai-settings-provider");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_problems_and_comparison(string theme)
    {
        string dir = ShotDir()!;
        using (var algo = new TestHost(contentRoot: TestHost.AlgorithmsRoot))
        {
            var window = new MainWindow { DataContext = algo.Main, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
            window.Show();
            await algo.Main.ShowProblemsAsync();
            Shot(window, dir, theme, "d1-problems");
            await ((ProblemsViewModel)algo.Main.Current!).ApplyFiltersAsync(DevAcademy.Domain.Difficulty.Expert, true);
            Shot(window, dir, theme, "d2-problems-empty");
            window.Close();
        }
        using var paths = new TestHost(contentRoot: TestHost.PathsRoot);
        var w2 = new MainWindow { DataContext = paths.Main, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
        w2.Show();
        await paths.Main.InitializeAsync();
        var onboarding = (OnboardingViewModel)paths.Main.Current!;
        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
        await onboarding.FinishCommand.ExecuteAsync(null);
        await paths.Main.OpenComparisonAsync("web-compare");
        Shot(w2, dir, theme, "d3-comparison");
        w2.Width = 1024; w2.Height = 700;
        Shot(w2, dir, theme, "d4-comparison-1024");
        w2.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_quiz_result_states(string theme)
    {
        string dir = ShotDir()!;
        using var host = new TestHost();
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
        window.Show();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        ws.SelectModeCommand.Execute(LessonMode.Quiz);
        var quiz = ws.Quiz!;
        Shot(window, dir, theme, "q0-quiz-initial");
        // Wrong answers first.
        foreach (var q in quiz.Questions)
        {
            switch (q.Question.Type)
            {
                case QuestionType.Single:
                case QuestionType.Multiple:
                    var wrong = Enumerable.Range(0, q.Options.Count).First(i => !q.Question.CorrectIndices.Contains(i));
                    q.Options[wrong].IsSelected = true;
                    break;
                case QuestionType.Short: q.Text = "zzz"; break;
                default: q.Text = "98765"; break;
            }
        }
        await quiz.SubmitCommand.ExecuteAsync(null);
        Shot(window, dir, theme, "q1-quiz-wrong");
        quiz.RetryCommand.Execute(null);
        Helpers.AnswerCorrectly(quiz);
        await quiz.SubmitCommand.ExecuteAsync(null);
        Shot(window, dir, theme, "q2-quiz-correct");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Capture_dialogs(string theme)
    {
        string dir = ShotDir()!;
        var dialogs = new Window[]
        {
            new ConfirmDialog("Удалить файл?", "Файл main.py будет удалён с диска. Это действие нельзя отменить.", "Удалить", "Отмена", true),
            new PromptDialog("Новый файл", "Введите имя файла в папке src.", "main.py", "Создать", "Отмена"),
            new ChoiceDialog("Сохранить изменения?", "В main.py есть несохранённые изменения. Что сделать перед закрытием?", ["Сохранить", "Не сохранять", "Отмена"]),
        };
        var names = new[] { "e1-dialog-confirm", "e2-dialog-prompt", "e3-dialog-choice" };
        for (int i = 0; i < dialogs.Length; i++)
        {
            dialogs[i].RequestedThemeVariant = Variant(theme);
            dialogs[i].Show();
            Shot(dialogs[i], dir, theme, names[i]);
            dialogs[i].Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Capture_visualizers(string theme)
    {
        string dir = ShotDir()!;
        var gd = new GradientDescentView();
        for (int i = 0; i < 12; i++) gd.ViewModel.StepForwardCommand.Execute(null);
        var km = new KMeansViewModel(3, DevAcademy.Application.Visualization.KMeansInit.KMeansPlusPlus, 1);
        var pca = new PcaViewModel(null);
        for (int i = 0; i < 3; i++) { km.StepForwardCommand.Execute(null); pca.StepForwardCommand.Execute(null); }
        foreach (var (name, view) in new (string, Control)[] { ("gradient-descent", gd), ("kmeans", new KMeansView(km)), ("pca", new PcaView(pca)) })
        {
            // Embedded the way lessons host them: a left-aligned column, not stretched across the window.
            var host = new StackPanel { Margin = new Avalonia.Thickness(32), MaxWidth = 720, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Children = { view } };
            var window = new Window { Content = host, Width = 1440, Height = name == "gradient-descent" ? 1200 : 900, RequestedThemeVariant = Variant(theme) };
            window.Show();
            Shot(window, dir, theme, "v-" + name);
            window.Close();
        }
    }

    private static async Task<(MainWindow Window, ProjectWorkspaceViewModel Ws)> OpenWorkspaceAsync(TestHost host, string project, string theme, int w, int h)
    {
        var window = new MainWindow { DataContext = host.Main, Width = w, Height = h, RequestedThemeVariant = Variant(theme) };
        window.Show();
        await host.Main.ShowProjectsAsync();
        var projects = (ProjectsViewModel)host.Main.Current!;
        await projects.OpenAsync(project);
        return (window, projects.Project!);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_projects_workspace_and_notebook(string theme)
    {
        string dir = ShotDir()!;
        var project = NotebookTests.MakeProject(out _);
        Directory.CreateDirectory(Path.Combine(project, "src"));
        File.WriteAllText(Path.Combine(project, "src", "util.py"), "VALUE = 1\n");
        var terminals = new FakeTerminalFactory();
        var service = new FakeKernelService();
        using var host = new TestHost(configure: s => s.AddSingleton<DevAcademy.Terminal.ITerminalFactory>(terminals));
        host.Services.Workspace.WatchFiles = false;
        host.Services.Workspace.Notebooks = service;
        await Helpers.CompleteOnboardingAsync(host);

        // Projects home (no project open).
        var home = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
        home.Show();
        await host.Main.ShowProjectsAsync();
        Shot(home, dir, theme, "w0-projects-home");
        home.Close();

        foreach (var (w, h, suffix) in new[] { (1440, 900, ""), (1024, 700, "-1024") })
        {
            var (window, ws) = await OpenWorkspaceAsync(host, project, theme, w, h);
            await ws.OpenFileAsync(Path.Combine(project, "main.py"));
            await ws.OpenFileAsync(Path.Combine(project, "src", "util.py"));
            if (ws.Terminals.Count == 0)
            {
                var term = ws.NewTerminal()!;
                ((FakeTerminalSession)term.Session).Screen.Feed("\u001b[32muser@host\u001b[0m:\u001b[34m~/project\u001b[0m$ python main.py\r\nhello, world\r\n\u001b[32muser@host\u001b[0m:\u001b[34m~/project\u001b[0m$ ");
            }
            ws.Explorer.Find(Path.Combine(project, "src"))!.IsExpanded = true;
            Shot(window, dir, theme, "w1-project-workspace" + suffix);
            ws.Search.TextQuery = "greet";
            await ws.Search.SearchTextAsync();
            Shot(window, dir, theme, "w2-project-search" + suffix);

            var doc = NotebookDocument.CreateNew(false);
            doc.Cells.Add(NotebookCell.NewMarkdown("# Линейная регрессия\n\nПодбираем веса $w$, чтобы минимизировать ошибку.\n\n- пункт один\n- пункт два"));
            doc.Cells.Add(NotebookCell.NewCode("import numpy as np\nx = np.linspace(0, 1, 5)\nprint('x ready')\nx"));
            doc.Cells.Add(NotebookCell.NewCode("plt.plot(x, x ** 2)\nplt.show()"));
            doc.Cells.Add(NotebookCell.NewCode("1/0"));
            doc.Cells.Add(NotebookCell.NewCode("# ещё не выполнялась\nresult = x.mean()"));
            var nbPath = Path.Combine(project, "regression" + suffix + ".ipynb");
            await NotebookStore.SaveAsync(nbPath, doc);
            await ws.OpenFileAsync(nbPath);
            var nb = ws.Notebook!;
            service.Scripts[nb.Cells[1].Source] = "print:\u001b[32mx ready\u001b[0m\nresult:array([0.  , 0.25, 0.5 , 0.75, 1.  ])";
            service.Scripts[nb.Cells[2].Source] = "png:" + NotebookTests.PngBase64();
            await nb.RunCellAsync(nb.Cells[1]);
            await nb.RunCellAsync(nb.Cells[2]);
            await nb.RunCellAsync(nb.Cells[3]);
            service.Created.Last().Variables =
            [
                new JsonObject { ["name"] = "x", ["type"] = "ndarray", ["summary"] = "dtype float64", ["shape"] = "5" },
                new JsonObject { ["name"] = "n", ["type"] = "int", ["summary"] = "5", ["shape"] = null },
            ];
            await nb.RefreshVariablesCommand.ExecuteAsync(null);
            nb.Select(nb.Cells[1]);
            Shot(window, dir, theme, "n1-notebook" + suffix);
            window.Close();
        }
        try { Directory.Delete(project, true); } catch (IOException) { }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_debug_panel(string theme)
    {
        string dir = ShotDir()!;
        string project = Path.Combine(Path.GetTempPath(), "da-dbg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(project, ".venv", "bin"));
        File.WriteAllText(Path.Combine(project, "main.py"), "total = 0\nfor i in range(3):\n    total += i\n    print(total)\nprint('done')\n");
        File.WriteAllText(Path.Combine(project, ".venv", "bin", "python"), "");
        var fake = new FakeAdapter { BodyLine = 3, Iterations = 2 };
        using var host = new TestHost(configure: s => s.AddSingleton(sp =>
        {
            var exec = sp.GetRequiredService<ExecutionOptions>();
            var resolver = new AdapterResolver(exec.CacheDirectory, probe: (_, _, _) => Task.FromResult(new ProbeResult(0, "")));
            return new DebugService(sp.GetRequiredService<AppPaths>(), exec, launcher: new FakeAdapterLauncher(fake), resolver: resolver);
        }));
        host.Services.Workspace.WatchFiles = false;
        await Helpers.CompleteOnboardingAsync(host);
        var (window, ws) = await OpenWorkspaceAsync(host, project, theme, 1440, 900);
        var tab = await ws.OpenFileAsync(Path.Combine(project, "main.py"));
        tab!.Debug!.Toggle(3);
        await ws.Debug.StartAsync();
        await Util.UntilAsync(() => { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return ws.Debug.State == DebugState.Stopped && ws.Debug.Variables.Any(v => v.Children.Count > 0); }, "stop");
        ws.Debug.WatchInput = "i";
        await ws.Debug.AddWatchCommand.ExecuteAsync(null);
        ws.Debug.ReplInput = "total";
        await ws.Debug.EvaluateCommand.ExecuteAsync(null);
        ws.ShowDebugPanel = true;
        Shot(window, dir, theme, "g1-debug-panel");
        await ws.Debug.StopCommand.ExecuteAsync(null);
        await Util.UntilAsync(() => !ws.Debug.IsActive, "end");
        window.Close();
        ws.Dispose();
        try { Directory.Delete(project, true); } catch (IOException) { }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Capture_editor_syntax(string theme)
    {
        string dir = ShotDir()!;
        var samples = new (string File, string Code)[]
        {
            ("sample.py", "import os\nfrom math import sqrt\n\n# compute the distance\n@decorator\ndef dist(a: float, b: float = 2.5) -> float:\n    \"\"\"Docstring.\"\"\"\n    if a is None or not b:\n        raise ValueError(f'bad {a!r}')\n    return sqrt(a ** 2 + b ** 2) + 0x1F\n\n\nclass Point(Base):\n    names = ['x', \"y\"]  # tuple (1, [2, {3}])\n    flag = True\n"),
            ("sample.cs", "using System;\nnamespace Demo;\n\n/// <summary>Sample.</summary>\npublic sealed class Point(int x, int y) : IFormattable\n{\n    private readonly List<string> _names = new();\n    public int Sum() => x + y;   // add\n    public string ToString(string? f, IFormatProvider? p) { return $\"{x}:{y}\"; }\n    static void Main() { var p = new Point(1, 2); Console.WriteLine(p.Sum() * 3.5m); bool ok = false; object o = null; }\n}\n"),
            ("sample.html", "<!doctype html>\n<html lang=\"ru\">\n<head><style>.card { color: #0b5f8a; margin: 4px; }</style></head>\n<body class=\"main\">\n  <!-- note -->\n  <button id=\"go\" onclick=\"run(1, 'a')\">Go</button>\n  <script>const n = 42; function run(a, b) { return a + b; }</script>\n</body>\n</html>\n"),
            ("sample.json", "{\n  \"name\": \"devacademy\",\n  \"count\": 42,\n  \"ok\": true,\n  \"none\": null,\n  \"items\": [1, 2.5, \"three\"]\n}\n"),
        };
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,*"), ColumnDefinitions = new ColumnDefinitions("*,*") };
        for (int i = 0; i < samples.Length; i++)
        {
            var editor = new CodeEditorControl { FileName = samples[i].File, Text = samples[i].Code };
            Grid.SetRow(editor, i / 2); Grid.SetColumn(editor, i % 2);
            grid.Children.Add(editor);
        }
        var window = new Window { Content = grid, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
        window.Show();
        var first = (CodeEditorControl)grid.Children[0];
        first.Editor.Select(first.Editor.Text.IndexOf("sqrt(a", StringComparison.Ordinal), 14);
        var second = (CodeEditorControl)grid.Children[1];
        second.Editor.TextArea.Caret.Offset = second.Editor.Text.IndexOf("Sum()", StringComparison.Ordinal) + 4; // next to ")" so the bracket pair is outlined
        for (int i = 0; i < 10; i++) { Thread.Sleep(100); Settle(); } // grammars tokenize on a worker thread
        Shot(window, dir, theme, "y1-editor-syntax");
        window.Close();
    }

    private static async Task TabShots(MainWindow window, string dir, string theme, string name, int presses)
    {
        for (int i = 0; i < presses; i++)
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        Settle();
        Shot(window, dir, theme, $"{name}-tab{presses}");
        await Task.CompletedTask;
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Capture_keyboard_focus(string theme)
    {
        string dir = ShotDir()!;
        using var host = new TestHost();
        var window = new MainWindow { DataContext = host.Main, Width = 1440, Height = 900, RequestedThemeVariant = Variant(theme) };
        window.Show();
        await host.Main.InitializeAsync();
        Settle();
        await TabShots(window, dir, theme, "f1-onboarding", 1);
        await TabShots(window, dir, theme, "f1-onboarding", 3);
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowCatalogAsync();
        Settle();
        await TabShots(window, dir, theme, "f2-catalog", 2);
        await TabShots(window, dir, theme, "f2-catalog", 7);
        await host.Main.OpenCourseAsync(Helpers.CourseId);
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        Settle();
        await TabShots(window, dir, theme, "f3-lesson", 6);
        await TabShots(window, dir, theme, "f3-lesson", 4);
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_focus_shows_the_accent_ring_on_a_button()
    {
        var button = new Button { Content = "OK" };
        var other = new Button { Content = "Other" };
        var window = new Window { Content = new StackPanel { Children = { button, other } }, Width = 300, Height = 200 };
        window.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://DevAcademy.Desktop/")) { Source = new Uri("avares://DevAcademy.Desktop/Styles/Theme.axaml") });
        window.Show();
        Assert.NotNull(button.FocusAdorner);
        button.Focus(NavigationMethod.Tab);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var layer = Avalonia.Controls.Primitives.AdornerLayer.GetAdornerLayer(button);
        Assert.NotNull(layer);
        var border = layer!.Children.OfType<Border>().FirstOrDefault();
        Assert.NotNull(border);
        Assert.Equal(2, border!.BorderThickness.Left);
        other.Focus(NavigationMethod.Pointer);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        button.Focus(NavigationMethod.Pointer);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(layer.Children, c => c is Border b && b.BorderThickness.Left == 2);
        window.Close();
    }
}
