using DevAcademy.Application.Services;
using DevAcademy.Desktop.Services;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.Tests;

public class ViewModelTests
{
    [Fact]
    public async Task First_launch_opens_onboarding_and_planned_languages_cannot_be_selected()
    {
        using var host = new TestHost(contentRoot: TestHost.ValidFixtureRoot);
        await host.Main.InitializeAsync();
        var onb = Assert.IsType<OnboardingViewModel>(host.Main.Current);
        Assert.False(host.Main.ShowNavigation);
        Assert.False(onb.FinishCommand.CanExecute(null));

        var planned = onb.Editor.Languages.First(l => !l.IsAvailable);
        Assert.Equal("Планируется", planned.StatusText);
        onb.Editor.SelectLanguage(planned);
        Assert.False(onb.Editor.HasLanguage);

        onb.Editor.SelectLanguage(onb.Editor.Languages.First(l => l.IsAvailable));
        Assert.True(onb.FinishCommand.CanExecute(null));
    }

    [Fact]
    public async Task Finishing_onboarding_persists_the_profile_and_opens_the_dashboard_next_time()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        using (var host = new TestHost(dir))
        {
            await Helpers.CompleteOnboardingAsync(host);
            Assert.IsType<DashboardViewModel>(host.Main.Current);
            Assert.True(host.Main.IsDashboardSection);
            Assert.False(host.Main.IsCatalogSection);
            var profile = await host.Services.Settings.GetProfileAsync();
            Assert.NotNull(profile);
            Assert.Contains(Helpers.CourseId, profile!.EnrolledCourseIds);
        }
        using (var again = new TestHost(dir))
        {
            await again.Main.InitializeAsync();
            Assert.IsType<DashboardViewModel>(again.Main.Current);
            Assert.True(again.Main.IsDashboardSection);
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task Catalog_groups_tracks_and_planned_tracks_have_no_courses_to_open()
    {
        using var host = new TestHost();
        var catalog = new CatalogViewModel(host.Services, host.Main);
        var all = catalog.Groups.SelectMany(g => g.Tracks).ToList();
        var python = all.Single(t => t.Track.Id == "python");
        Assert.True(python.IsAvailable);
        Assert.True(python.HasCourses);
        Assert.Contains(all, t => !t.IsAvailable && !t.HasCourses && t.StatusText == "Планируется");
        Assert.All(all.Where(t => !t.IsAvailable), t => Assert.Empty(t.Courses));
        Assert.Equal("Языки программирования", catalog.Groups[0].Title);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Roadmap_shows_first_lesson_available_rest_locked_with_reason_and_counts()
    {
        using var host = new TestHost();
        await host.Main.OpenCourseAsync(Helpers.CourseId);
        var roadmap = Assert.IsType<RoadmapViewModel>(host.Main.Current);
        Assert.Equal(LessonState.Available, roadmap.Lessons[0].State);
        Assert.All(roadmap.Lessons.Skip(1), l => Assert.True(l.IsLocked));
        Assert.False(string.IsNullOrWhiteSpace(roadmap.Lessons[1].WhyLocked));
        Assert.StartsWith("Пройдено 0 из ", roadmap.ProgressText);
        Assert.DoesNotContain('%', roadmap.ProgressText);
    }

    [Fact]
    public async Task Opening_a_locked_lesson_needs_explicit_confirmation()
    {
        using var host = new TestHost();
        await host.Main.OpenCourseAsync(Helpers.CourseId);
        var locked = ((RoadmapViewModel)host.Main.Current!).Lessons[1];

        host.Dialogs.Answer = false;
        await host.Main.OpenLessonAsync(locked.LessonId);
        Assert.IsType<RoadmapViewModel>(host.Main.Current);
        Assert.Single(host.Dialogs.Confirmed);

        host.Dialogs.Answer = true;
        await host.Main.OpenLessonAsync(locked.LessonId);
        var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        Assert.Equal(locked.LessonId, ws.Lesson.Id);
    }

    [Fact]
    public async Task Drafts_are_autosaved_after_the_debounce_and_restored()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-ui-" + Guid.NewGuid().ToString("N"));
        var lessonId = "";
        using (var host = new TestHost(dir))
        {
            lessonId = Helpers.FirstLessonId(host);
            await host.Main.OpenLessonAsync(lessonId);
            var ws = (LessonWorkspaceViewModel)host.Main.Current!;
            var file = ws.Practice!.Files[0];
            file.Content = "print('draft v1')";
            file.Content = "print('draft v2')";
            var exId = ws.Lesson.Exercise!.Id;
            Assert.Empty(await host.Services.Drafts.GetAllAsync(exId));   // debounced: nothing yet
            await Helpers.WaitUntilAsync(() => host.Services.Drafts.GetAllAsync(exId).Result.Count == 1, 4000);
            Assert.Equal("print('draft v2')", (await host.Services.Drafts.GetAllAsync(exId)).Single().Value);
        }
        using (var again = new TestHost(dir))
        {
            await again.Main.OpenLessonAsync(lessonId);
            var ws = (LessonWorkspaceViewModel)again.Main.Current!;
            Assert.Equal("print('draft v2')", ws.Practice!.Files[0].Content);
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task Reset_to_starter_needs_confirmation_and_drops_the_draft()
    {
        using var host = new TestHost();
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var s = ((LessonWorkspaceViewModel)host.Main.Current!).Practice!;
        var starter = s.Files[0].Content;
        s.Files[0].Content = "changed";
        await s.FlushDraftsAsync();

        host.Dialogs.Answer = false;
        await s.ResetCommand.ExecuteAsync(null);
        Assert.Equal("changed", s.Files[0].Content);

        host.Dialogs.Answer = true;
        await s.ResetCommand.ExecuteAsync(null);
        Assert.Equal(starter, s.Files[0].Content);
        Assert.Empty(await host.Services.Drafts.GetAllAsync(s.Exercise!.Id));
    }

    [Fact]
    public async Task Hints_are_progressive_and_the_solution_needs_its_own_confirmation()
    {
        using var host = new TestHost();
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var s = ((LessonWorkspaceViewModel)host.Main.Current!).Practice!;
        Assert.Empty(s.Hints);
        await s.RevealHintCommand.ExecuteAsync(null);
        Assert.Single(s.Hints);
        await s.RevealHintCommand.ExecuteAsync(null);
        Assert.Equal(2, s.Hints.Count);
        Assert.Equal(2, s.RevealedCount);

        host.Dialogs.Answer = false;
        await s.ShowSolutionCommand.ExecuteAsync(null);
        Assert.False(s.IsSolutionVisible);
        Assert.Empty(s.SolutionFiles);
        host.Dialogs.Answer = true;
        await s.ShowSolutionCommand.ExecuteAsync(null);
        Assert.True(s.IsSolutionVisible);
        Assert.NotEmpty(s.SolutionFiles);
    }

    [Fact]
    public async Task Run_sample_opens_a_scratch_editor_that_is_not_the_exercise()
    {
        using var host = new TestHost();
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        var practice = ws.Practice;
        ws.OpenSampleCommand.Execute(new DevAcademy.Desktop.Markdown.CodeSampleRequest("python", "print(1)"));
        Assert.True(ws.IsScratchActive);
        Assert.True(ws.ActiveSession!.IsScratch);
        Assert.Equal("print(1)", ws.ActiveSession.Files[0].Content);
        Assert.False(ws.ActiveSession.CanSubmit);
        ws.CloseScratchCommand.Execute(null);
        Assert.Same(practice, ws.ActiveSession);
    }

    [Fact]
    public async Task Fact_sources_go_through_the_launcher()
    {
        using var host = new TestHost();
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        var link = ws.Facts.SelectMany(f => f.Sources).First();
        link.OpenCommand.Execute(null);
        await Helpers.WaitUntilAsync(() => host.Launcher.Opened.Count == 1);
        Assert.Equal(link.Url, Assert.Single(host.Launcher.Opened));
    }

    [Fact]
    public async Task Theme_choice_is_applied_persisted_and_restored()
    {
        using var host = new TestHost();
        var settings = new SettingsViewModel(host.Services, host.Main, host.Main.InitializeAsync);
        await settings.LoadAsync();
        settings.SelectedTheme = settings.Themes.Single(t => t.Value == ThemeChoice.Dark);
        Assert.Equal(ThemeChoice.Dark, host.Theme.Applied.Last());
        await Helpers.WaitUntilAsync(() => host.Services.Settings.GetAsync(SettingsViewModel.ThemeKey).Result == "Dark");

        var again = new SettingsViewModel(host.Services, host.Main, host.Main.InitializeAsync);
        await again.LoadAsync();
        Assert.Equal(ThemeChoice.Dark, again.SelectedTheme.Value);
    }

    [Fact]
    public async Task Profile_edited_in_settings_is_saved()
    {
        using var host = new TestHost();
        var settings = new SettingsViewModel(host.Services, host.Main, host.Main.InitializeAsync);
        await settings.LoadAsync();
        settings.Profile.SelectLanguage(settings.Profile.Languages.First(l => l.IsAvailable));
        settings.Profile.SelectedLevel = settings.Profile.Levels[2];
        await settings.SaveProfileCommand.ExecuteAsync(null);
        var saved = await host.Services.Settings.GetProfileAsync();
        Assert.Equal(2, saved!.Level);
    }

    [Fact]
    public async Task Export_import_backup_restore_all_ask_for_confirmation_where_destructive()
    {
        using var host = new TestHost();
        await Helpers.CompleteOnboardingAsync(host);
        var settings = new SettingsViewModel(host.Services, host.Main, host.Main.InitializeAsync);
        await settings.LoadAsync();

        var json = Path.Combine(host.DataDir, "export.json");
        host.Files.SavePath = json;
        await settings.ExportCommand.ExecuteAsync(null);
        Assert.True(File.Exists(json));

        host.Dialogs.Answer = false;
        host.Files.OpenPath = json;
        await settings.ImportCommand.ExecuteAsync(null);
        Assert.DoesNotContain("Импорт завершён", settings.DataMessage ?? "");
        host.Dialogs.Answer = true;
        await settings.ImportCommand.ExecuteAsync(null);
        Assert.Contains("Импорт завершён", settings.DataMessage);

        var db = Path.Combine(host.DataDir, "copy.db");
        host.Files.SavePath = db;
        await settings.BackupCommand.ExecuteAsync(null);
        Assert.True(File.Exists(db));

        host.Dialogs.Answer = false;
        host.Files.OpenPath = db;
        var before = host.Dialogs.Confirmed.Count;
        await settings.RestoreCommand.ExecuteAsync(null);
        Assert.Equal(before + 1, host.Dialogs.Confirmed.Count);
        Assert.NotEqual("Данные восстановлены.", settings.DataMessage);
    }

    [Fact]
    public async Task TypeScript_types_provisioning_asks_first_and_does_nothing_when_declined()
    {
        using var host = new TestHost();
        var settings = new SettingsViewModel(host.Services, host.Main, host.Main.InitializeAsync);
        host.Dialogs.Answer = false;
        await settings.ProvisionTypesCommand.ExecuteAsync(null);
        Assert.Single(host.Dialogs.Confirmed);
        Assert.Null(settings.ProvisionOutput);
    }

    [Fact]
    public async Task Runtimes_table_lists_every_language()
    {
        using var host = new TestHost();
        var settings = new SettingsViewModel(host.Services, host.Main, host.Main.InitializeAsync);
        await settings.DetectRuntimesCommand.ExecuteAsync(null);
        Assert.Contains(settings.Runtimes, r => r.Language == "python");
    }

    [Fact]
    public async Task Dashboard_reflects_enrolled_course_counts_only()
    {
        using var host = new TestHost(contentRoot: TestHost.ValidFixtureRoot);
        await Helpers.CompleteOnboardingAsync(host);
        var dash = new DashboardViewModel(host.Services, host.Main);
        await dash.LoadAsync();
        var row = Assert.Single(dash.Courses);
        Assert.StartsWith("0 из ", row.CountText);
        Assert.Equal(5, dash.Solved.Count);
        Assert.NotEmpty(dash.Days);
        Assert.NotEmpty(dash.Recommendations);
    }
}

public class AppInfoTests
{
    [Theory]
    [InlineData("0.1.0+abc123456", "0.1.0", "abc123456")]
    [InlineData("1.2.3", "1.2.3", null)]
    [InlineData("1.2.3+", "1.2.3", null)]
    [InlineData(null, "0.0.0", null)]
    public void Parses_informational_version(string? input, string version, string? commit) =>
        Assert.Equal((version, commit), DevAcademy.Desktop.Services.AppInfo.ParseInformationalVersion(input));

    [Fact]
    public void Current_reports_the_product_version_and_platform()
    {
        var info = DevAcademy.Desktop.Services.AppInfo.Current;
        Assert.Matches(@"^\d+\.\d+\.\d+", info.Version);
        Assert.Contains("(", info.Platform);
    }
}
