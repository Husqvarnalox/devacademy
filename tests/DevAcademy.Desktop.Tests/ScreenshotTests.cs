using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;

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
        await Helpers.CompleteOnboardingAsync(host);
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
}
