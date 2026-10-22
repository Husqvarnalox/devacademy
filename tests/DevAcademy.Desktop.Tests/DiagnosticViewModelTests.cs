using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.Tests;

/// <summary>The "Проверка знаний" page over the paths fixture plus a small python route (two competencies, quiz checks).</summary>
[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class DiagnosticViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "devacademy-diag-" + Guid.NewGuid().ToString("N"));

    public DiagnosticViewModelTests()
    {
        CopyDir(TestHost.PathsRoot, _root);
        Directory.CreateDirectory(Path.Combine(_root, "routes"));
        File.WriteAllText(Path.Combine(_root, "routes", "python.yaml"), """
            track: python
            competencies:
              - id: basics
                title: Основы
                description: Базовый синтаксис
                check: pa.placement
              - id: advanced
                title: Продвинутое
                description: ""
                check: pa.check
                after: [basics]
            """);
        var dir = Path.Combine(_root, "courses", "py-adv", "lessons", "02-check");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "theory.md"), "# Check\n");
        File.WriteAllText(Path.Combine(dir, "lesson.yaml"), """
            id: pa.check
            title: Check
            goal: Check
            difficulty: intro
            estimatedMinutes: 10
            objectives: [Check]
            assessmentFor: [pa.one]
            quiz:
              passThreshold: 1.0
              questions:
                - id: q1
                  type: single
                  prompt: One?
                  options: ["yes", "no"]
                  answer: 0
                  explanation: Yes.
            """);
        var course = Path.Combine(_root, "courses", "py-adv", "course.yaml");
        File.WriteAllText(course, File.ReadAllText(course).Replace("lessons: [00-placement, 01-x]", "lessons: [00-placement, 01-x, 02-check]"));
    }

    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* best effort */ } }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }

    private TestHost Host() => new(contentRoot: _root);

    private static async Task<DiagnosticViewModel> OpenAsync(TestHost host)
    {
        await host.Main.OpenDiagnosticAsync("python");
        return Assert.IsType<DiagnosticViewModel>(host.Main.Current);
    }

    private static readonly string[] BasicsLessons = ["pb.one", "pb.two", "pb.three"];

    [Fact]
    public async Task Fresh_learner_sees_not_checked_rows_with_the_first_check_marked_next()
    {
        using var host = Host();
        var vm = await OpenAsync(host);
        Assert.Equal("Проверка знаний: Python", vm.Title);
        Assert.Equal("Подтверждено: 0 · Есть пробел: 0 · Не проверено: 2", vm.SummaryText);
        Assert.Equal(["Основы", "Продвинутое"], vm.Rows.Select(r => r.Title));

        var basics = vm.Rows[0];
        Assert.Equal("Не проверено", basics.StateText);
        Assert.Equal("Пройдено уроков темы: 0 из 3", basics.EvidenceText);
        Assert.True(basics.IsNext);
        Assert.Equal("Пройти проверку", basics.PrimaryText);
        Assert.False(basics.HasGapText);

        var adv = vm.Rows[1];
        Assert.False(adv.IsNext);
        Assert.Equal("Рекомендуется после: Основы", adv.GapText);
        Assert.Equal("Пройти проверку", adv.PrimaryText); // still openable
    }

    [Fact]
    public async Task Failed_quiz_is_a_gap_that_names_the_failed_part_and_can_be_retaken_or_studied()
    {
        using var host = Host();
        await host.Services.Progress.RecordQuizAttemptAsync("pa.placement",
            new Dictionary<string, QuizAnswer> { ["q1"] = QuizAnswer.Choice(1), ["q2"] = QuizAnswer.Choice(0) });
        var vm = await OpenAsync(host);
        var basics = vm.Rows[0];
        Assert.Equal("Есть пробел", basics.StateText);
        Assert.Equal("Квиз ниже порога", basics.EvidenceText);
        Assert.Equal("Пробел: 3 из 3 уроков темы не пройдены", basics.GapText);
        Assert.Equal("Пройти ещё раз", basics.PrimaryText);
        Assert.True(basics.HasGapLesson);
        Assert.Equal("Подтверждено: 0 · Есть пробел: 1 · Не проверено: 1", vm.SummaryText);

        basics.StudyGapCommand.Execute(null);
        await Task.Delay(50);
        var lesson = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        Assert.Equal("pb.one", lesson.Lesson.Id);
    }

    [Fact]
    public async Task Confirmed_by_lessons_or_by_passing_the_check_has_matching_evidence_and_a_quiet_action()
    {
        using var host = Host();
        foreach (var id in BasicsLessons) await host.Services.Progress.MarkTheoryReadAsync(id);
        var vm = await OpenAsync(host);
        var basics = vm.Rows[0];
        Assert.Equal("Подтверждено", basics.StateText);
        Assert.Equal("Все уроки темы пройдены", basics.EvidenceText);
        Assert.False(basics.IsPrimaryAction);
        Assert.Equal("Открыть проверку", basics.PrimaryText);
        Assert.True(vm.Rows[1].IsNext); // its prerequisite is confirmed now

        await host.Services.Progress.RecordQuizAttemptAsync("pa.check",
            new Dictionary<string, QuizAnswer> { ["q1"] = QuizAnswer.Choice(0) });
        vm = await OpenAsync(host);
        Assert.Equal("Проверка пройдена", vm.Rows[1].EvidenceText);
        Assert.Equal("Подтверждено: 2 · Есть пробел: 0 · Не проверено: 0", vm.SummaryText);
    }

    [Fact]
    public async Task Entry_points_course_page_and_onboarding()
    {
        using var host = Host();
        await host.Main.OpenCourseAsync("py-basics");
        Assert.True(Assert.IsType<RoadmapViewModel>(host.Main.Current).HasDiagnostic);
        await host.Main.OpenCourseAsync("js-basics");
        Assert.False(Assert.IsType<RoadmapViewModel>(host.Main.Current).HasDiagnostic); // javascript has no route

        await host.Main.OpenCourseAsync("py-basics");
        Assert.True(Assert.IsType<RoadmapViewModel>(host.Main.Current).OpenDiagnosticCommand.CanExecute(null));
        await Assert.IsType<RoadmapViewModel>(host.Main.Current).OpenDiagnosticCommand.ExecuteAsync(null);
        Assert.IsType<DiagnosticViewModel>(host.Main.Current);
    }

    [Fact]
    public async Task Onboarding_with_level_two_opens_the_diagnostic_only_for_a_routed_language_without_a_path()
    {
        using (var host = Host())
        {
            await host.Main.InitializeAsync();
            var onboarding = Assert.IsType<OnboardingViewModel>(host.Main.Current);
            onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
            onboarding.Editor.SelectedLevel = onboarding.Editor.Levels.Single(l => l.Value == 2);
            await onboarding.FinishCommand.ExecuteAsync(null);
            Assert.Equal("python", Assert.IsType<DiagnosticViewModel>(host.Main.Current).TrackId);
        }
        using (var host = Host())
        {
            await host.Main.InitializeAsync();
            var onboarding = Assert.IsType<OnboardingViewModel>(host.Main.Current);
            onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
            await onboarding.FinishCommand.ExecuteAsync(null); // level 0
            Assert.IsType<DashboardViewModel>(host.Main.Current);
        }
        using (var host = Host())
        {
            await host.Main.InitializeAsync();
            var onboarding = Assert.IsType<OnboardingViewModel>(host.Main.Current);
            onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "javascript"));
            onboarding.Editor.SelectedLevel = onboarding.Editor.Levels.Single(l => l.Value == 2);
            await onboarding.FinishCommand.ExecuteAsync(null);
            Assert.IsType<DashboardViewModel>(host.Main.Current); // no competencies for javascript
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Diagnostic_dashboard_and_course_screens_render_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        using var host = Host();
        var window = new MainWindow { DataContext = host.Main, Width = 1280, Height = 900, RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        await host.Main.InitializeAsync();
        var onboarding = (OnboardingViewModel)host.Main.Current!;
        onboarding.Editor.SelectLanguage(onboarding.Editor.Languages.Single(l => l.Track.Id == "python"));
        await onboarding.FinishCommand.ExecuteAsync(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        await host.Services.Progress.RecordQuizAttemptAsync("pa.placement",
            new Dictionary<string, QuizAnswer> { ["q1"] = QuizAnswer.Choice(1), ["q2"] = QuizAnswer.Choice(0) });
        await host.Main.OpenDiagnosticAsync("python");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.GetVisualDescendants().OfType<DiagnosticView>().SingleOrDefault());

        await host.Main.OpenCourseAsync("py-basics");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        await host.Main.ShowDashboardAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(errors.Messages);
    }
}
