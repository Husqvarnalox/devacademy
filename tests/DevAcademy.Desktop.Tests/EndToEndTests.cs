using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.Tests;

/// <summary>The vertical slice through the real services, a real CodeRunner and the real Avalonia views.</summary>
[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class EndToEndTests
{
    private static void Settle() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static async Task<LessonWorkspaceViewModel> OpenFirstLessonAsync(TestHost host)
    {
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        return (LessonWorkspaceViewModel)host.Main.Current!;
    }

    [AvaloniaFact]
    public async Task Python_course_vertical_slice_survives_a_restart()
    {
        if (!Helpers.HasPython3()) Assert.Skip("python3 is not installed");
        using var errors = new BindingErrors();
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-e2e-" + Guid.NewGuid().ToString("N"));
        string firstId, secondId, solution;

        using (var host = new TestHost(dir))
        {
            var window = new MainWindow { DataContext = host.Main, Width = 1280, Height = 800 };
            window.Show();

            // onboarding -> python -> catalog -> roadmap
            await Helpers.CompleteOnboardingAsync(host);
            Settle();
            await host.Main.OpenCourseAsync(Helpers.CourseId);
            var roadmap = Assert.IsType<RoadmapViewModel>(host.Main.Current);
            firstId = roadmap.Lessons[0].LessonId;
            secondId = roadmap.Lessons[1].LessonId;
            Assert.Equal(LessonState.Available, roadmap.Lessons[0].State);
            Assert.Equal(LessonState.Locked, roadmap.Lessons[1].State);

            // first lesson, theory is rendered
            var ws = await OpenFirstLessonAsync(host);
            Settle();
            Assert.False(string.IsNullOrWhiteSpace(ws.TheoryMarkdown));
            Assert.Contains(window.GetVisualDescendants().OfType<StackPanel>(), p => p.Classes.Contains("md-root"));

            // type the correct solution into the real editor control, then Run via the keyboard shortcut
            var practice = ws.Practice!;
            solution = practice.Exercise!.Solution.Single(f => f.Path == practice.EntryFile).Content;
            ws.SelectModeCommand.Execute(LessonMode.Practice);
            Settle();
            var editor = window.GetVisualDescendants().OfType<CodeEditorControl>().First();
            editor.Text = solution;
            Settle();
            Assert.Equal(solution, practice.Files[0].Content);

            var area = editor.GetVisualDescendants().OfType<AvaloniaEdit.Editing.TextArea>().First();
            area.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            await Helpers.WaitUntilAsync(() => practice.ConsoleResult is not null, 15000);
            // The first lesson reads no input: Run prints exactly what its first test expects.
            var expected = practice.Exercise.Tests.First(t => !t.Hidden).Expected!;
            foreach (var line in expected.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Assert.Contains(line, practice.ConsoleResult!.Stdout);

            // submit: every test passes, progress is saved, lesson not complete yet because of the quiz
            await practice.SubmitCommand.ExecuteAsync(null);
            Assert.True(practice.Report!.IsPassed);
            Assert.All(practice.Report.Cases, c => Assert.True(c.Passed));
            Assert.Contains(practice.Report.Cases, c => c.Hidden);
            await ws.RefreshAsync();
            var saved = await host.Provider.GetRequiredService<DevAcademy.Application.Ports.IProgressRepository>().GetAsync(firstId);
            Assert.True(saved!.ExercisePassed);

            // quiz completes the lesson, next lesson becomes available
            Helpers.AnswerCorrectly(ws.Quiz!);
            await ws.Quiz!.SubmitCommand.ExecuteAsync(null);
            Assert.True(ws.Quiz.Passed);
            await ws.RefreshAsync();
            Assert.True(ws.IsCompleted);
            Assert.Equal(secondId, ws.NextLessonId);
            Assert.Equal(LessonState.Available, (await host.LearningPath.GetLessonStatusAsync(secondId))!.State);

            await ws.FlushAsync();
            window.Close();
        }

        // restart: new container over the same data dir
        using (var host = new TestHost(dir))
        {
            await host.Main.InitializeAsync();
            Assert.IsType<DashboardViewModel>(host.Main.Current);   // profile persisted: no onboarding
            Assert.Equal(LessonState.Completed, (await host.LearningPath.GetLessonStatusAsync(firstId))!.State);
            Assert.Equal(LessonState.Available, (await host.LearningPath.GetLessonStatusAsync(secondId))!.State);
            await host.Main.OpenCourseAsync(Helpers.CourseId);
            Assert.StartsWith("Пройдено 1 из ", ((RoadmapViewModel)host.Main.Current!).ProgressText);

            var ws = await OpenFirstLessonAsync(host);
            Assert.True(ws.IsCompleted);
            Assert.Equal(solution, ws.Practice!.Files[0].Content);   // draft restored
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, true); } catch (IOException) { }
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
    }

    [AvaloniaFact]
    public async Task Wrong_solution_fails_the_tests_and_never_completes_the_lesson()
    {
        if (!Helpers.HasPython3()) Assert.Skip("python3 is not installed");
        using var host = new TestHost();
        await Helpers.CompleteOnboardingAsync(host);
        var ws = await OpenFirstLessonAsync(host);
        var practice = ws.Practice!;
        practice.Files[0].Content = "print('wrong')\n";

        await practice.SubmitCommand.ExecuteAsync(null);
        var report = practice.Report!;
        Assert.False(report.IsPassed);
        Assert.Equal("Тесты не пройдены: 0 из " + report.Cases.Count, report.Verdict);

        // hidden tests expose only a numbered title (and hint), never expected or actual output
        var hidden = report.Cases.Where(c => c.Hidden).ToList();
        Assert.NotEmpty(hidden);
        Assert.All(hidden, c =>
        {
            Assert.StartsWith("Скрытый тест", c.Title);
            Assert.Null(c.Expected);
            Assert.Null(c.Actual);
        });

        await ws.RefreshAsync();
        Assert.False(ws.IsCompleted);
        var p = await host.Provider.GetRequiredService<DevAcademy.Application.Ports.IProgressRepository>().GetAsync(Helpers.FirstLessonId(host));
        Assert.False(p!.ExercisePassed);
        Assert.NotEqual(LessonState.Completed, (await host.LearningPath.GetLessonStatusAsync(Helpers.FirstLessonId(host)))!.State);
    }
}
