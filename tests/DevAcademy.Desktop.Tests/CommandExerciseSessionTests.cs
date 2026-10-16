using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;
using DevAcademy.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests;

/// <summary>The multi-file command exercise (Node.js lesson "modules and node:test") in the session view model.</summary>
public class CommandExerciseSessionTests
{
    private const string Lesson = "nodejs.first-steps.modules-and-node-test";

    private static TestHost Host() =>
        new(configure: s => s.AddSingleton<DevAcademy.Execution.ICodeRunner>(new FakeRunner()));

    private static async Task<ExerciseSessionViewModel> OpenAsync(TestHost host)
    {
        await host.Main.OpenLessonAsync(Lesson);
        var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        return Assert.IsType<ExerciseSessionViewModel>(ws.Practice);
    }

    [Fact]
    public async Task Files_are_the_learner_files_with_the_entry_selected_and_hidden_tests_absent()
    {
        using var host = Host();
        var session = await OpenAsync(host);

        Assert.Equal(EvaluationKind.Command, session.Exercise!.Evaluation);
        Assert.Equal(["src/cart.mjs", "src/money.mjs"], session.Files.Select(f => f.Path).Order().ToList());
        Assert.True(session.HasMultipleFiles);
        Assert.Equal("src/money.mjs", session.EntryFile);
        Assert.Equal("src/money.mjs", session.SelectedFile!.Path);
        Assert.DoesNotContain(session.Files, f => f.Path.Contains("test", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(session.SnapshotFiles(), f => f.Path.Contains(".test.", StringComparison.Ordinal));
        // The hidden test files exist in the content, just not in the editor.
        Assert.NotEmpty(session.Exercise.Command!.TestFiles);
    }

    [Fact]
    public async Task Command_exercises_can_be_submitted_but_not_run()
    {
        using var host = Host();
        var session = await OpenAsync(host);
        Assert.False(session.CanRun);
        Assert.True(session.CanSubmit);
        Assert.False(session.RunCommand.CanExecute(null));
        Assert.True(session.SubmitCommand.CanExecute(null));
    }

    [Fact]
    public async Task Drafts_are_kept_per_file_path()
    {
        using var host = Host();
        var session = await OpenAsync(host);
        var money = session.Files.Single(f => f.Path == "src/money.mjs");
        var cart = session.Files.Single(f => f.Path == "src/cart.mjs");
        var cartStarter = cart.Content;
        money.Content = "// my money draft\n";
        await session.FlushDraftsAsync();

        var again = await OpenAsync(host);
        Assert.Equal("// my money draft\n", again.Files.Single(f => f.Path == "src/money.mjs").Content);
        Assert.Equal(cartStarter, again.Files.Single(f => f.Path == "src/cart.mjs").Content);

        again.Files.Single(f => f.Path == "src/cart.mjs").Content = "// my cart draft\n";
        await again.FlushDraftsAsync();
        var third = await OpenAsync(host);
        Assert.Equal("// my money draft\n", third.Files.Single(f => f.Path == "src/money.mjs").Content);
        Assert.Equal("// my cart draft\n", third.Files.Single(f => f.Path == "src/cart.mjs").Content);
    }
}
