using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DevAcademy.Application.Visualization;
using DevAcademy.Desktop.Controls;
using DevAcademy.Desktop.Markdown;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Desktop.Views;
using DevAcademy.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class VariantViewModelTests
{
    private const string Lesson = "algo.find-max";

    private static TestHost Host(FakeRunner? runner = null) =>
        new(contentRoot: TestHost.AlgorithmsRoot, configure: s =>
        {
            if (runner is not null) s.AddSingleton<DevAcademy.Execution.ICodeRunner>(runner);
        });

    private static async Task<(LessonWorkspaceViewModel Ws, ExerciseSessionViewModel Session)> OpenAsync(TestHost host)
    {
        await host.Main.OpenLessonAsync(Lesson);
        var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        return (ws, ws.Practice!);
    }

    [Fact]
    public async Task Language_selector_lists_variants_and_switching_keeps_each_languages_draft()
    {
        using var host = Host(new FakeRunner());
        var (_, session) = await OpenAsync(host);
        Assert.True(session.IsMultiLanguage);
        Assert.Equal(["python", "javascript"], session.Languages.Select(l => l.Id));
        Assert.Equal("main.py", session.EntryFile);

        session.SelectedFile!.Content = "# my python draft\n";
        await session.SelectLanguageAsync("javascript");
        Assert.Equal("javascript", session.LanguageId);
        Assert.Equal("main.js", session.EntryFile);
        Assert.DoesNotContain("my python draft", session.SelectedFile!.Content);
        session.SelectedFile.Content = "// my js draft\n";

        await session.SelectLanguageAsync("python");
        Assert.Equal("# my python draft\n", session.SelectedFile!.Content);
        await session.SelectLanguageAsync("javascript");
        Assert.Equal("// my js draft\n", session.SelectedFile!.Content);

        // The choice is remembered for the next time the exercise is opened.
        await session.FlushDraftsAsync();
        var (_, again) = await OpenAsync(host);
        Assert.Equal("javascript", again.LanguageId);
        Assert.Equal("// my js draft\n", again.SelectedFile!.Content);
    }

    [Fact]
    public async Task Reset_only_resets_the_active_language()
    {
        using var host = Host(new FakeRunner());
        var (_, session) = await OpenAsync(host);
        session.SelectedFile!.Content = "# py\n";
        await session.SelectLanguageAsync("javascript");
        session.SelectedFile!.Content = "// js\n";
        await session.FlushDraftsAsync();

        await session.ResetCommand.ExecuteAsync(null);
        Assert.DoesNotContain("// js", session.SelectedFile!.Content);
        await session.SelectLanguageAsync("python");
        Assert.Equal("# py\n", session.SelectedFile!.Content);
    }

    [Fact]
    public async Task Missing_runtime_stays_selectable_but_disables_run_and_submit_and_shows_the_hint()
    {
        using var host = Host(new FakeRunner("javascript"));
        var (_, session) = await OpenAsync(host);
        Assert.True(session.RunCommand.CanExecute(null));
        Assert.True(session.SubmitCommand.CanExecute(null));
        Assert.Null(session.RuntimeHint);

        var js = session.Languages.Single(l => l.Id == "javascript");
        Assert.False(js.IsAvailable);
        await session.SelectLanguageAsync("javascript");

        Assert.Equal("javascript", session.LanguageId);
        Assert.True(session.IsRuntimeMissing);
        Assert.False(session.RunCommand.CanExecute(null));
        Assert.False(session.SubmitCommand.CanExecute(null));
        Assert.Contains("Install javascript", session.RuntimeHint);

        await session.SelectLanguageAsync("python");
        Assert.False(session.IsRuntimeMissing);
        Assert.True(session.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task Run_uses_the_selected_language()
    {
        var runner = new FakeRunner();
        using var host = Host(runner);
        var (_, session) = await OpenAsync(host);
        await session.SelectLanguageAsync("javascript");
        await session.RunCommand.ExecuteAsync(null);
        Assert.Equal(["javascript"], runner.RunLanguages);
        Assert.Equal("ran javascript", session.ConsoleResult!.Stdout);
    }

    [Fact]
    public async Task Editorial_and_complexity_are_gated_by_solved_state_or_explicit_confirmation()
    {
        using var host = Host(new FakeRunner());
        var (ws, _) = await OpenAsync(host);
        Assert.True(ws.HasEditorial);
        Assert.False(ws.HasComplexity);   // not solved yet

        host.Dialogs.Answer = false;
        await ws.ShowEditorialCommand.ExecuteAsync(null);
        Assert.False(ws.IsEditorialVisible);
        Assert.Single(host.Dialogs.Confirmed);

        host.Dialogs.Answer = true;
        await ws.ShowEditorialCommand.ExecuteAsync(null);
        Assert.True(ws.IsEditorialVisible);
        Assert.False(ws.HasComplexity);   // confirming the spoiler never reveals "solved" material

        // Once passed, the editorial opens without a prompt and the complexity block appears.
        ws.HideEditorialCommand.Execute(null);
        var ex = ws.Lesson.Exercise!;
        await host.Services.Progress.RecordSubmissionAsync(ws.Lesson.Id, ex.Id,
            ex.ForVariant("python").Starter,
            new DevAcademy.Application.Evaluation.EvaluationReport(Verdict.Passed, [], TimeSpan.Zero, DevAcademy.Execution.IsolationLevel.ProcessLimits, null, "python"),
            languageId: "python");
        await ws.RefreshAsync();
        Assert.True(ws.HasComplexity);
        Assert.Contains("O(n)", ws.ComplexityTime);
        var asked = host.Dialogs.Confirmed.Count;
        await ws.ShowEditorialCommand.ExecuteAsync(null);
        Assert.True(ws.IsEditorialVisible);
        Assert.Equal(asked, host.Dialogs.Confirmed.Count);
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class ProblemsPageTests
{
    [Fact]
    public async Task Problems_nav_is_hidden_without_algorithm_content_and_shown_with_it()
    {
        using var python = new TestHost(contentRoot: TestHost.ValidFixtureRoot);
        Assert.False(python.Main.HasProblems);
        using var algo = new TestHost(contentRoot: TestHost.AlgorithmsRoot);
        Assert.True(algo.Main.HasProblems);
        await algo.Main.ShowProblemsAsync();
        Assert.True(algo.Main.IsProblemsSection);
    }

    [Fact]
    public async Task Problems_list_filters_by_difficulty_and_solved_state_and_opens_the_lesson()
    {
        using var host = new TestHost(contentRoot: TestHost.AlgorithmsRoot);
        await host.Main.ShowProblemsAsync();
        var page = Assert.IsType<ProblemsViewModel>(host.Main.Current);
        var row = Assert.Single(page.Items);
        Assert.Equal("algo.find-max", row.LessonId);
        Assert.Equal("Python, JavaScript", row.LanguagesText);

        await page.ApplyFiltersAsync(Difficulty.Hard, null);
        Assert.Empty(page.Items);
        Assert.True(page.IsEmpty);
        await page.ApplyFiltersAsync(Difficulty.Easy, true);
        Assert.Empty(page.Items);
        await page.ApplyFiltersAsync(Difficulty.Easy, false);
        Assert.Single(page.Items);

        await page.Items[0].OpenCommand.ExecuteAsync(null);
        Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        Assert.True(host.Main.IsProblemsSection);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Problems_page_and_lesson_with_variants_render_without_binding_errors(string theme)
    {
        using var errors = new BindingErrors();
        using var host = new TestHost(contentRoot: TestHost.AlgorithmsRoot, configure: s => s.AddSingleton<DevAcademy.Execution.ICodeRunner>(new FakeRunner("javascript")));
        var window = new MainWindow { DataContext = host.Main, Width = 1280, Height = 800 };
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await host.Main.ShowProblemsAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.GetVisualDescendants().OfType<ProblemsView>().SingleOrDefault());
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Classes.Contains("topnav") && b.IsVisible);

        await host.Main.OpenLessonAsync("algo.find-max");
        var ws = (LessonWorkspaceViewModel)host.Main.Current!;
        ws.SelectModeCommand.Execute(LessonMode.Practice);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var box = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "LanguageBox");
        Assert.Equal(2, box.ItemCount);
        await ws.Practice!.SelectLanguageAsync("javascript");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
        errors.Messages.Clear();

        // Theory of the sorting lesson embeds the visualizer. (Theory-only lessons have no session, so the
        // pre-existing null ActiveSession/Practice bindings of the workspace are expected there and not asserted.)
        await host.Main.OpenLessonAsync("algo.sorting");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.GetVisualDescendants().OfType<SortingVisualizerView>().FirstOrDefault());
        Assert.DoesNotContain(errors.Messages, m => m.Contains("Viz") || m.Contains("Sort"));
        window.Close();
    }
}

[Collection(DevAcademy.Desktop.Tests.Support.BindingCaptureCollection.Name)]
public class SortingVisualizerTests
{
    private static SortingVisualizerViewModel Vm(SortAlgorithm a = SortAlgorithm.Bubble, params int[] input) =>
        new(a, input.Length == 0 ? null : input);

    [Fact]
    public void Every_frame_is_a_trace_step_and_stepping_back_returns_the_same_frame()
    {
        var vm = Vm(SortAlgorithm.Bubble, 3, 1, 2);
        var n = vm.StepCount;
        Assert.True(n > 0);
        Assert.Equal([3, 1, 2], vm.Frame.Values);
        var seen = new List<int[]>();
        for (var i = 0; i < n; i++) { vm.StepForwardCommand.Execute(null); seen.Add(vm.Frame.Values.ToArray()); }
        Assert.Equal(n, vm.Position);
        Assert.True(vm.IsFinished);
        Assert.Equal([1, 2, 3], vm.Frame.Values);
        Assert.Equal(vm.Trace.Comparisons, int.Parse(vm.ComparisonsText.Split(' ').Last()));
        for (var i = n - 2; i >= 0; i--) { vm.StepBackCommand.Execute(null); Assert.Equal(seen[i], vm.Frame.Values.ToArray()); }
    }

    [Fact]
    public void Highlights_follow_the_last_applied_step()
    {
        var vm = Vm(SortAlgorithm.Bubble, 2, 1);
        vm.StepForwardCommand.Execute(null);     // compare(0,1)
        Assert.Equal([0, 1], vm.Frame.Compared);
        vm.StepForwardCommand.Execute(null);     // swap(0,1)
        Assert.Equal([0, 1], vm.Frame.Swapped);
        Assert.Equal([1, 2], vm.Frame.Values);
    }

    [Theory]
    [InlineData("", "Введите")]
    [InlineData("1", "от 2 до 40")]
    [InlineData("1, a", "целые")]
    [InlineData("1, 2, 500", "от 1 до 99")]
    public void Invalid_input_shows_an_error_and_keeps_the_current_array(string text, string fragment)
    {
        var vm = Vm();
        var before = vm.Frame.Values.ToArray();
        vm.InputText = text;
        vm.ApplyInputCommand.Execute(null);
        Assert.True(vm.HasError);
        Assert.Contains(fragment, vm.Error);
        Assert.Equal(before, vm.Frame.Values.ToArray());
    }

    [Fact]
    public void Valid_input_restarts_on_the_new_array_and_switching_algorithm_resets_the_position()
    {
        var vm = Vm();
        vm.InputText = "4, 3, 2, 1";
        vm.ApplyInputCommand.Execute(null);
        Assert.False(vm.HasError);
        Assert.Equal([4, 3, 2, 1], vm.Frame.Values);
        vm.StepForwardCommand.Execute(null);
        vm.SelectedAlgorithm = vm.Algorithms.Single(a => a.Algorithm == SortAlgorithm.Merge);
        Assert.Equal(0, vm.Position);
        Assert.Equal([4, 3, 2, 1], vm.Frame.Values);
        Assert.Equal(SortAlgorithm.Merge, vm.Trace.Algorithm);
    }

    [Fact]
    public void Play_advances_one_step_per_tick_and_stops_at_the_end()
    {
        var vm = Vm(SortAlgorithm.Selection, 2, 1);
        vm.PlayPauseCommand.Execute(null);
        Assert.True(vm.IsPlaying);
        for (var i = 0; i < vm.StepCount; i++) vm.Tick();
        Assert.False(vm.IsPlaying);
        Assert.True(vm.IsFinished);
        Assert.Equal(vm.StepCount, vm.Position);
    }

    [AvaloniaFact]
    public void Visualizer_renders_in_both_themes_and_the_markdown_directive_creates_it()
    {
        using var errors = new BindingErrors();
        var md = MarkdownRenderer.Render("Text\n\n```visualize sorting algorithm=merge\n5 3 8 1\n```\n\n```visualize graph\n```\n");
        var all = md.GetLogicalDescendants().OfType<Control>().ToList();
        var viz = Assert.Single(all.OfType<SortingVisualizerView>());
        Assert.Contains("md-visualizer", viz.Classes);
        Assert.Equal(SortAlgorithm.Merge, viz.ViewModel.Trace.Algorithm);
        Assert.Equal([5, 3, 8, 1], viz.ViewModel.Frame.Values);
        Assert.Single(all.OfType<TextBlock>(), t => t.Classes.Contains("md-visualizer-unknown"));

        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var window = new Window { Width = 800, Height = 600, RequestedThemeVariant = theme, Content = new SortingVisualizerView(new SortingVisualizerViewModel(SortAlgorithm.Quick)) };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var view = (SortingVisualizerView)window.Content!;
            Assert.NotNull(view.GetVisualDescendants().OfType<SortBarsControl>().Single().Frame);
            view.ViewModel.StepForwardCommand.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.Close();
        }
        Assert.True(errors.Messages.Count == 0, string.Join("\n", errors.Messages.Distinct()));
    }
}
