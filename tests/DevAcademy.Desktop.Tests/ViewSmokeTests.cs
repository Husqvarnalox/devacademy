using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;

namespace DevAcademy.Desktop.Tests;

/// <summary>Every screen is instantiated in a headless window in both themes: XAML load and binding errors fail the test.</summary>
[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class ViewSmokeTests
{
    private static MainWindow Show(TestHost host, double width = 1280)
    {
        var window = new MainWindow { DataContext = host.Main, Width = width, Height = 800 };
        window.Show();
        return window;
    }

    private static void Settle(Window w) => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task All_screens_render_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        using var host = new TestHost();
        var window = Show(host);
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        await host.Main.InitializeAsync();
        Settle(window);
        Assert.IsType<OnboardingViewModel>(host.Main.Current);
        Assert.NotNull(window.GetVisualDescendants().OfType<OnboardingView>().SingleOrDefault());

        await Helpers.CompleteOnboardingAsync(host);
        Settle(window);
        Assert.NotNull(window.GetVisualDescendants().OfType<DashboardView>().SingleOrDefault());
        await host.Main.ShowCatalogAsync();
        Settle(window);
        Assert.NotNull(window.GetVisualDescendants().OfType<CatalogView>().SingleOrDefault());

        await host.Main.OpenCourseAsync(Helpers.CourseId);
        Settle(window);
        Assert.NotNull(window.GetVisualDescendants().OfType<RoadmapView>().SingleOrDefault());

        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        Settle(window);
        var lesson = window.GetVisualDescendants().OfType<LessonWorkspaceView>().SingleOrDefault();
        Assert.NotNull(lesson);
        Assert.NotNull(lesson!.GetVisualDescendants().OfType<CodeEditorControl>().FirstOrDefault());

        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        foreach (var mode in new[] { LessonMode.Practice, LessonMode.Quiz, LessonMode.Theory })
        {
            ws.SelectModeCommand.Execute(mode);
            Settle(window);
        }
        if (ws.HasChallenge) { ws.SelectModeCommand.Execute(LessonMode.Challenge); Settle(window); }

        await host.Main.ShowDashboardAsync();
        Settle(window);
        Assert.NotNull(window.GetVisualDescendants().OfType<DashboardView>().SingleOrDefault());

        await host.Main.ShowSettingsAsync();
        Settle(window);
        Assert.NotNull(window.GetVisualDescendants().OfType<SettingsView>().SingleOrDefault());

        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Ai_settings_tab_and_tutor_panel_render_in_every_state_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        var fake = new FakeModelProvider();
        using var host = AiKit.CreateHost(fake);
        var window = Show(host);
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        await host.Main.InitializeAsync();
        await Helpers.CompleteOnboardingAsync(host);

        // Lesson with AI off: the notice shows and the lesson is not blocked.
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        Settle(window);
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        Assert.NotNull(window.GetVisualDescendants().OfType<TutorPanelView>().SingleOrDefault());
        ws.Tutor.IsExpanded = true;
        Settle(window);
        Assert.True(ws.Tutor.IsOffNoticeVisible);
        await ws.Tutor.AskCommand.ExecuteAsync(DevAcademy.Application.Tutor.TutorScenario.Explain);
        Settle(window);

        // Settings -> AI tab, with a selected provider that has a key and models.
        await AiKit.EnableAsync(host);
        await host.Main.ShowAiSettingsAsync();
        Settle(window);
        Assert.NotNull(window.GetVisualDescendants().OfType<AiSettingsView>().SingleOrDefault());
        var settings = (SettingsViewModel)host.Main.Current!;
        var p = settings.Ai.Providers.Single(x => x.Id == "fake");
        settings.Ai.SelectedProvider = p;
        await p.RefreshModelsCommand.ExecuteAsync(null);
        p.SelectedModel = p.Models[1];
        p.CustomModelId = "custom";
        await p.TestModelCommand.ExecuteAsync(null);
        await settings.Ai.RefreshHistoryCommand.ExecuteAsync(null);
        Settle(window);
        settings.Ai.SelectedProvider = settings.Ai.Providers.Single(x => x.Id == DevAcademy.AI.ProviderIds.OpenAICompatible);
        Settle(window);
        host.Secrets.Available = false;
        await settings.Ai.LoadAsync();
        Settle(window);

        // Lesson with AI on: preview, streaming result, markdown.
        host.Secrets.Available = true;
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        Settle(window);
        ws = (LessonWorkspaceViewModel)host.Main.Current!;
        ws.Tutor.IsExpanded = true;
        await ws.Tutor.AskCommand.ExecuteAsync(DevAcademy.Application.Tutor.TutorScenario.Hint);
        Settle(window);
        Assert.True(ws.Tutor.IsPreviewVisible);
        await ws.Tutor.SendCommand.ExecuteAsync(null);
        Settle(window);
        Assert.True(ws.Tutor.HasMarkdown);

        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Narrow_window_collapses_navigation_and_stacks_the_editor_under_the_theory()
    {
        using var host = new TestHost();
        var window = Show(host, width: 1280);
        await host.Main.InitializeAsync();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        Settle(window);
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        Assert.False(ws.IsNarrow);
        Assert.True(ws.IsNavVisible);

        window.Width = 700;
        Settle(window);
        window.UpdateLayout();
        Assert.True(ws.IsNarrow);
        Assert.False(ws.IsNavVisible);
        var view = window.GetVisualDescendants().OfType<LessonWorkspaceView>().Single();
        var center = view.FindControl<Grid>("CenterPane")!;
        var session = view.FindControl<Grid>("SessionPane")!;
        Assert.True(session.Bounds.Top >= center.Bounds.Bottom - 1, "editor must be below the theory when narrow");

        ws.ToggleNavCommand.Execute(null);
        Assert.True(ws.IsNavVisible);
        window.Close();
    }
}
