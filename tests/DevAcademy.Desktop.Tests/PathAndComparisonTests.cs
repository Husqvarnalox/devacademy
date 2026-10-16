using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.Tests;

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class PathViewModelTests
{
    private static TestHost Host() => new(contentRoot: TestHost.PathsRoot);

    private static async Task<OnboardingViewModel> OnboardingAsync(TestHost host)
    {
        await host.Main.InitializeAsync();
        return Assert.IsType<OnboardingViewModel>(host.Main.Current);
    }

    private static async Task<PathViewModel> OpenAsync(TestHost host, string id)
    {
        await host.Main.OpenPathAsync(id);
        return Assert.IsType<PathViewModel>(host.Main.Current);
    }

    private static async Task CompleteAsync(TestHost host, params string[] lessonIds)
    {
        foreach (var id in lessonIds) await host.Services.Progress.MarkTheoryReadAsync(id);
    }

    [Fact]
    public async Task Onboarding_offers_only_the_paths_of_the_chosen_language_plus_no_route()
    {
        using var host = Host();
        var onboarding = await OnboardingAsync(host);
        Assert.False(onboarding.HasPaths); // nothing before a language is chosen

        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
        Assert.Equal(["Python deep", "Python web", "Без маршрута"], onboarding.Paths.Select(p => p.Title));
        Assert.Null(onboarding.Paths[^1].PathId);
        Assert.True(onboarding.Paths[^1].IsSelected); // "no route" is the default

        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "javascript"));
        Assert.Equal(["JS path", "Без маршрута"], onboarding.Paths.Select(p => p.Title));
    }

    [Fact]
    public async Task Choosing_no_route_goes_to_the_catalog_and_follows_nothing()
    {
        using var host = Host();
        var onboarding = await OnboardingAsync(host);
        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
        await onboarding.FinishCommand.ExecuteAsync(null);
        Assert.IsType<CatalogViewModel>(host.Main.Current);
        Assert.Empty(await host.Services.Path.GetFollowedPathIdsAsync());
    }

    [Fact]
    public async Task Choosing_a_path_follows_it_and_opens_the_path_screen()
    {
        using var host = Host();
        var onboarding = await OnboardingAsync(host);
        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
        onboarding.Paths.Single(p => p.PathId == "python-web").PickCommand.Execute(null);
        Assert.Equal("python-web", onboarding.SelectedPath!.PathId);
        await onboarding.FinishCommand.ExecuteAsync(null);

        var path = Assert.IsType<PathViewModel>(host.Main.Current);
        Assert.Equal("python-web", path.PathId);
        Assert.True(path.IsFollowed);
        Assert.Equal(["python-web"], await host.Services.Path.GetFollowedPathIdsAsync());
    }

    [Fact]
    public async Task Catalog_lists_paths_and_comparisons_and_several_paths_can_be_followed()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowCatalogAsync();
        var catalog = Assert.IsType<CatalogViewModel>(host.Main.Current);
        Assert.Equal(["js-path", "python-deep", "python-web"], catalog.Paths.Select(p => p.Id));
        Assert.Equal(["web-compare"], catalog.Comparisons.Select(c => c.Id));

        await catalog.Paths.Single(p => p.Id == "python-web").ToggleFollowCommand.ExecuteAsync(null);
        await catalog.Paths.Single(p => p.Id == "python-deep").ToggleFollowCommand.ExecuteAsync(null);
        Assert.Equal(["python-web", "python-deep"], await host.Services.Path.GetFollowedPathIdsAsync());

        var web = await OpenAsync(host, "python-web");
        Assert.Equal(["Python deep"], web.OtherPaths.Select(p => p.Title)); // switch between followed paths
        await web.OtherPaths[0].OpenCommand.ExecuteAsync(null);
        Assert.Equal("python-deep", Assert.IsType<PathViewModel>(host.Main.Current).PathId);
    }

    [Fact]
    public async Task Path_screen_shows_stages_courses_lessons_and_counts()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);
        var path = await OpenAsync(host, "python-web");

        Assert.Equal(["Foundation", "Web"], path.Stages.Select(s => s.Title));
        Assert.Equal(["Python Basics"], path.Stages[0].Courses.Select(c => c.Title));
        Assert.Equal(["Web Django", "Web FastAPI"], path.Stages[1].Courses.Select(c => c.Title));
        var django = path.Stages[1].Courses[0];
        Assert.True(django.IsSelected);
        Assert.False(django.IsNotChosen);
        Assert.True(path.Stages[1].Courses[1].IsNotChosen); // second alternative is not required
        Assert.Equal("Пройдено 0 из 4 уроков", path.ProgressText);
        Assert.False(path.HasCredited);
        Assert.True(path.HasNextStep);
        Assert.Equal("Basics one", path.NextStepTitle);
        Assert.True(path.HasGraph);
        Assert.Equal(2, path.Graph.Layers.Count);
    }

    [Fact]
    public async Task Choosing_an_alternative_is_persisted_and_changes_the_required_course()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);
        var path = await OpenAsync(host, "python-web");
        var group = Assert.Single(path.Alternatives);
        Assert.True(group.Choices[0].IsSelected);

        await group.ChooseCommand.ExecuteAsync(group.Choices[1]);

        var reopened = await OpenAsync(host, "python-web");
        Assert.True(reopened.Alternatives[0].Choices[1].IsSelected);
        Assert.True(reopened.Stages[1].Courses[1].IsSelected);
        Assert.True(reopened.Stages[1].Courses[0].IsNotChosen);
    }

    [Fact]
    public async Task Lessons_done_elsewhere_are_marked_already_known_and_counted()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);
        await CompleteAsync(host, "pb.one", "pb.two");
        var path = await OpenAsync(host, "python-deep");

        var basics = path.Stages.SelectMany(s => s.Courses).Single(c => c.CourseId == "py-basics");
        Assert.Equal("2 из 3 уроков", basics.ProgressText);
        Assert.Equal("уже освоено", basics.Lessons[0].KnownText);
        Assert.Equal("уже освоено", basics.Lessons[1].KnownText);
        Assert.Null(basics.Lessons[2].KnownText);
        Assert.Contains("2", path.KnownText);
        Assert.Contains("2 из 5", path.ProgressText);
    }

    [Fact]
    public async Task Placement_is_offered_only_where_a_lesson_has_an_assessment()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);

        var web = await OpenAsync(host, "python-web");
        Assert.All(web.Stages.SelectMany(s => s.Courses), c => Assert.False(c.HasPlacement));

        var deep = await OpenAsync(host, "python-deep");
        var courses = deep.Stages.SelectMany(s => s.Courses).ToList();
        Assert.True(courses.Single(c => c.CourseId == "py-adv").HasPlacement);
        Assert.All(courses.Where(c => c.CourseId != "py-adv"), c => Assert.False(c.HasPlacement));

        await host.Main.OpenCourseAsync("py-basics");
        Assert.Empty(Assert.IsType<RoadmapViewModel>(host.Main.Current).Placements);
        await host.Main.OpenCourseAsync("py-adv");
        Assert.Single(Assert.IsType<RoadmapViewModel>(host.Main.Current).Placements);
    }

    [Fact]
    public async Task Passing_the_placement_credits_lessons_through_the_path_screen()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);
        var path = await OpenAsync(host, "python-deep");
        var adv = path.Stages.SelectMany(s => s.Courses).Single(c => c.CourseId == "py-adv");
        adv.TakePlacementCommand.Execute(null);
        var check = Assert.IsType<PlacementViewModel>(path.PlacementCheck);

        Helpers.AnswerCorrectly(check.Quiz);
        await check.Quiz.SubmitCommand.ExecuteAsync(null);
        await Helpers.WaitUntilAsync(() => check.Outcome is not null);

        Assert.True(check.Credited);
        Assert.Contains("3", check.Outcome);
        Assert.True(path.HasCredited);
        var basics = path.Stages.SelectMany(s => s.Courses).Single(c => c.CourseId == "py-basics");
        Assert.All(basics.Lessons, l => Assert.Equal("Засчитано по проверке знаний", l.KnownText));
        Assert.Equal(3, (await host.Services.Path.GetCoursePathAsync("py-basics"))!.Completed);
    }

    [Fact]
    public async Task Failing_the_placement_credits_nothing()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);
        var path = await OpenAsync(host, "python-deep");
        path.Stages.SelectMany(s => s.Courses).Single(c => c.CourseId == "py-adv").TakePlacementCommand.Execute(null);
        var check = path.PlacementCheck!;

        foreach (var q in check.Quiz.Questions) q.Options[q.Question.CorrectIndices.Contains(0) ? 1 : 0].IsSelected = true;
        await check.Quiz.SubmitCommand.ExecuteAsync(null);
        await Helpers.WaitUntilAsync(() => check.Outcome is not null);

        Assert.False(check.Credited);
        Assert.Contains("не изменился", check.Outcome);
        Assert.False(path.HasCredited);
        Assert.Equal(0, path.Stages.SelectMany(s => s.Courses).Single(c => c.CourseId == "py-basics").Lessons.Count(l => l.IsCompleted));
    }

    [Fact]
    public async Task Roadmap_header_shows_framework_metadata_only_when_present()
    {
        using var host = Host();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.OpenCourseAsync("web-django");
        var fw = Assert.IsType<RoadmapViewModel>(host.Main.Current).Framework!;
        Assert.Equal("django 5.2", fw.Title);
        Assert.Contains("1.0.0", fw.CurriculumText);
        Assert.Equal("Python 3.10 - 3.13", fw.Compatibility);
        Assert.Equal("USE_L10N", Assert.Single(fw.Deprecated).Feature);
        Assert.True(fw.HasMigrationNotes);
        Assert.Equal(["https://docs.djangoproject.com/en/5.2/"], fw.Docs);

        await host.Main.OpenCourseAsync("py-basics");
        var plain = Assert.IsType<RoadmapViewModel>(host.Main.Current);
        Assert.False(plain.HasFramework);
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class ComparisonViewModelTests
{
    private static async Task<ComparisonViewModel> OpenAsync(TestHost host)
    {
        await host.Main.OpenComparisonAsync("web-compare");
        return Assert.IsType<ComparisonViewModel>(host.Main.Current);
    }

    [Fact]
    public async Task Wide_shows_every_subject_and_narrow_shows_one_with_a_picker()
    {
        using var host = new TestHost(contentRoot: TestHost.PathsRoot);
        var vm = await OpenAsync(host);
        Assert.Equal(3, vm.Subjects.Count);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(["Django 5.2", "FastAPI 0.115", "Flask 3.1"], vm.Subjects.Select(s => s.Header));

        vm.UpdateWidth(1400);
        Assert.False(vm.IsNarrow);
        Assert.Equal(3, vm.VisibleSubjects.Count);

        vm.UpdateWidth(560);
        Assert.True(vm.IsNarrow);
        Assert.Equal(["Django"], vm.VisibleSubjects.Select(s => s.Title)); // first subject by default

        vm.SelectedSubject = vm.Subjects[2];
        Assert.Equal(["Flask"], vm.VisibleSubjects.Select(s => s.Title));

        vm.UpdateWidth(1400);
        Assert.Equal(3, vm.VisibleSubjects.Count);
    }

    [Fact]
    public async Task Threshold_depends_on_the_number_of_subjects()
    {
        using var host = new TestHost(contentRoot: TestHost.PathsRoot);
        var vm = await OpenAsync(host);
        var fits = ComparisonViewModel.CriterionColumn + ComparisonViewModel.SubjectColumn * 3;
        vm.UpdateWidth(fits);
        Assert.False(vm.IsNarrow);
        vm.UpdateWidth(fits - 1);
        Assert.True(vm.IsNarrow);
    }

    [Fact]
    public async Task Cells_follow_subject_order_and_links_open_through_the_launcher()
    {
        using var host = new TestHost(contentRoot: TestHost.PathsRoot);
        var vm = await OpenAsync(host);
        Assert.Equal(["django", "fastapi", "flask"], vm.Rows[0].Cells.Select(c => c.SubjectId));
        Assert.Equal("Flask is small.", vm.Rows[0].Cells[2].Markdown);
        await vm.OpenLinkCommand.ExecuteAsync(vm.Sources[0]);
        Assert.Equal([vm.Sources[0]], host.Launcher.Opened);
    }

    [Fact]
    public async Task Unknown_comparison_does_not_navigate()
    {
        using var host = new TestHost(contentRoot: TestHost.PathsRoot);
        await Helpers.CompleteOnboardingAsync(host);
        var before = host.Main.Current;
        await host.Main.OpenComparisonAsync("nope");
        Assert.Same(before, host.Main.Current);
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class PathViewSmokeTests
{
    private static void Settle() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Path_comparison_and_framework_screens_render_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        using var host = new TestHost(contentRoot: TestHost.PathsRoot);
        var window = new MainWindow { DataContext = host.Main, Width = 1280, Height = 900, RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();

        await host.Main.InitializeAsync();
        Settle();
        var onboarding = (OnboardingViewModel)host.Main.Current!;
        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
        Settle();

        await onboarding.FinishCommand.ExecuteAsync(null);
        await host.Services.Path.SetFollowedAsync("python-deep", true);
        await host.Main.ShowCatalogAsync();
        Settle();
        Assert.NotNull(window.GetVisualDescendants().OfType<CatalogView>().SingleOrDefault());

        await host.Main.OpenPathAsync("python-web");
        Settle();
        var view = window.GetVisualDescendants().OfType<PathView>().Single();
        Assert.NotNull(view.GetVisualDescendants().OfType<CourseGraphControl>().SingleOrDefault());

        await host.Main.OpenPathAsync("python-deep");
        Settle();
        var deep = (PathViewModel)host.Main.Current!;
        deep.Stages.SelectMany(s => s.Courses).Single(c => c.CourseId == "py-adv").TakePlacementCommand.Execute(null);
        Settle();
        Assert.NotNull(window.GetVisualDescendants().OfType<QuizView>().FirstOrDefault());

        await host.Main.OpenCourseAsync("web-django");
        Settle();
        Assert.NotNull(window.GetVisualDescendants().OfType<RoadmapView>().SingleOrDefault());

        await host.Main.OpenCourseAsync("py-adv");
        Settle();

        await host.Main.OpenComparisonAsync("web-compare");
        Settle();
        var comparison = window.GetVisualDescendants().OfType<ComparisonView>().Single();
        Assert.False(((ComparisonViewModel)host.Main.Current!).IsNarrow);
        Assert.Equal(2 + 3, comparison.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("gridhead")));

        window.Width = 600; // narrow: one subject at a time
        Settle();
        var vm = (ComparisonViewModel)host.Main.Current!;
        Assert.True(vm.IsNarrow);
        Assert.Equal(2 + 1, comparison.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("gridhead")));
        vm.SelectedSubject = vm.Subjects[1];
        Settle();

        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        window.Close();
    }
}
