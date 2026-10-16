using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Ports;
using DevAcademy.Application.Services;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

internal sealed class InMemoryDrafts : IDraftRepository
{
    public Dictionary<(string Exercise, string File), string> Items { get; } = new();
    public Task<string?> GetAsync(string exerciseId, string filePath, CancellationToken ct = default) =>
        Task.FromResult(Items.TryGetValue((exerciseId, filePath), out var v) ? v : null);
    public Task SaveAsync(string exerciseId, string filePath, string content, CancellationToken ct = default)
    { Items[(exerciseId, filePath)] = content; return Task.CompletedTask; }
    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string exerciseId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(Items.Where(kv => kv.Key.Exercise == exerciseId).ToDictionary(kv => kv.Key.File, kv => kv.Value));
    public Task DeleteAsync(string exerciseId, CancellationToken ct = default)
    {
        foreach (var k in Items.Keys.Where(k => k.Exercise == exerciseId).ToList()) Items.Remove(k);
        return Task.CompletedTask;
    }
}

internal static class VariantMake
{
    public static Exercise TwoLanguages(string id = "algo.ex1", EvaluationKind kind = EvaluationKind.Io) =>
        new(id, "Max", "", "python", "main.py", [new("main.py", "# py starter")], [new("main.py", "# py solution")],
            kind, [], null,
            [Make.Test("t1", "3 9 2\n", "9\n"), Make.Test("t2", "-5 -2\n", "-2\n", hidden: true, hint: "negatives")], ["h1"], null,
            [
                new ExerciseVariant("python", "main.py", [new("main.py", "# py starter")], [new("main.py", "# py solution")], [], null),
                new ExerciseVariant("javascript", "main.js", [new("main.js", "// js starter")], [new("main.js", "// js solution")], [], null),
            ]);

    public static ContentSet Algorithms(Exercise ex, Difficulty difficulty = Difficulty.Easy, ComplexityInfo? complexity = null, string? editorial = null)
    {
        var lesson = Make.Lesson("algo.max", "algo-basics", ex: ex, difficulty: difficulty) with { Complexity = complexity, Editorial = editorial };
        var theory = Make.Lesson("algo.theory", "algo-basics");
        var lang = Make.Lesson("py.one", "py-basics", ex: Make.IoExercise("py.one.ex"));
        return new ContentSet(
            [new Track("algorithms", TrackKind.Algorithms, "Algorithms", "", null, ContentStatus.Available, ["algo-basics"]),
             new Track("python", TrackKind.Language, "Python", "", "python", ContentStatus.Available, ["py-basics"])],
            [Make.Course("algo-basics", ["algo.max", "algo.theory"]) with { TrackId = "algorithms" },
             Make.Course("py-basics", ["py.one"]) with { TrackId = "python" }],
            [lesson, theory, lang]);
    }
}

public class VariantEvaluationTests
{
    private static FakeRunner Runner() => new((stdin, _) => FakeRunner.Ok(stdin!.Split(' ', '\n', StringSplitOptions.RemoveEmptyEntries).Max(int.Parse) + "\n"));

    [Fact]
    public async Task Evaluation_runs_the_selected_variant_in_its_language_with_shared_tests()
    {
        var runner = Runner();
        var service = new ExerciseEvaluationService(runner);
        var ex = VariantMake.TwoLanguages();

        var js = await service.EvaluateAsync(ex, [new("main.js", "x")], languageId: "javascript");
        Assert.Equal(Verdict.Passed, js.Status);
        Assert.Equal("javascript", runner.LastRequest!.LanguageId);
        Assert.Equal("main.js", runner.LastRequest.EntryFile);
        Assert.Equal("javascript", js.LanguageId);
        Assert.Equal(2, js.TotalCount);

        var def = await service.EvaluateAsync(ex, [new("main.py", "x")]);
        Assert.Equal("python", runner.LastRequest.LanguageId);
        Assert.Equal("python", def.LanguageId);
    }

    [Fact]
    public async Task Unknown_variant_language_is_rejected_instead_of_run()
    {
        var runner = Runner();
        var service = new ExerciseEvaluationService(runner);
        await Assert.ThrowsAsync<ArgumentException>(() => service.EvaluateAsync(VariantMake.TwoLanguages(), [], languageId: "cobol"));
        Assert.Equal(0, runner.Prepares);
    }

    [Fact]
    public async Task Harness_variants_use_their_own_harness_files()
    {
        var ex = VariantMake.TwoLanguages(kind: EvaluationKind.Harness) with
        {
            Variants =
            [
                new ExerciseVariant("python", "main.py", [], [], [new("test_main.py", "py harness")], "test_main.py"),
                new ExerciseVariant("javascript", "main.js", [], [], [new("test.js", "js harness")], "test.js"),
            ],
        };
        var runner = new FakeRunner((_, _) => FakeRunner.Ok(""));
        await new ExerciseEvaluationService(runner).EvaluateAsync(ex, [new("main.js", "x")], languageId: "javascript");
        Assert.Equal("test.js", runner.LastRequest!.EntryFile);
        Assert.Contains(runner.LastRequest.Files, f => f.Path == "test.js" && f.Content == "js harness");
        Assert.DoesNotContain(runner.LastRequest.Files, f => f.Path == "test_main.py");
    }

    [Fact]
    public async Task Io_cases_carry_a_wall_time_each()
    {
        var report = await new ExerciseEvaluationService(Runner()).EvaluateAsync(VariantMake.TwoLanguages(), [new("main.py", "x")]);
        Assert.All(report.Cases, c => Assert.Equal(TimeSpan.FromMilliseconds(5), c.Duration));
    }

    [Fact]
    public async Task Submissions_record_the_language_and_progress_stays_per_lesson()
    {
        var ex = VariantMake.TwoLanguages();
        var set = VariantMake.Algorithms(ex);
        var progress = new InMemoryProgress();
        var subs = new InMemorySubmissions();
        var service = new ProgressService(new InMemoryContent(set), progress, subs, new InMemorySettings(), new FixedClock(Make.T0));

        await service.RecordSubmissionAsync("algo.max", ex.Id, [new("main.py", "x")], Make.Report(Verdict.Failed, 0, 1), languageId: "python");
        var update = await service.RecordSubmissionAsync("algo.max", ex.Id, [new("main.js", "x")], Make.Report(Verdict.Passed), languageId: "javascript");

        Assert.True(update.Progress.ExercisePassed);
        Assert.Equal(2, update.Progress.Attempts);
        Assert.Equal(1, update.Progress.FailedAttempts);
        Assert.Single(progress.Items);
        Assert.Equal(["javascript", "python"], subs.Items.Values.Select(s => s.LanguageId).Order());

        // language falls back to the one the report was produced in
        await service.RecordSubmissionAsync("algo.max", ex.Id, [], Make.Report(Verdict.Failed, 0, 1) with { LanguageId = "javascript" });
        Assert.Equal(2, subs.Items.Values.Count(s => s.LanguageId == "javascript"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordSubmissionAsync("algo.max", ex.Id, [], Make.Report(Verdict.Passed), languageId: "cobol"));
    }

    [Fact]
    public void Reference_solution_is_per_variant()
    {
        var hints = new HintService(new InMemoryContent(VariantMake.Algorithms(VariantMake.TwoLanguages())), new InMemorySettings());
        Assert.Equal("// js solution", hints.GetSolution("algo.ex1", "javascript").Single().Content);
        Assert.Equal("# py solution", hints.GetSolution("algo.ex1").Single().Content);
    }
}

public class DraftServiceTests
{
    [Fact]
    public async Task Drafts_are_kept_per_language_and_reset_only_clears_one()
    {
        var repo = new InMemoryDrafts();
        var drafts = new DraftService(repo, new InMemorySettings());
        var ex = VariantMake.TwoLanguages();

        Assert.Equal("# py starter", (await drafts.LoadFilesAsync(ex, "python")).Single().Content);
        await drafts.SaveAsync(ex, "python", "main.py", "print(1)");
        await drafts.SaveAsync(ex, "javascript", "main.js", "console.log(1)");

        Assert.Equal("print(1)", (await drafts.LoadFilesAsync(ex, "python")).Single().Content);
        Assert.Equal("console.log(1)", (await drafts.LoadFilesAsync(ex, "javascript")).Single().Content);

        await drafts.ResetAsync(ex, "python");
        Assert.Equal("# py starter", (await drafts.LoadFilesAsync(ex, "python")).Single().Content);
        Assert.Equal("console.log(1)", (await drafts.LoadFilesAsync(ex, "javascript")).Single().Content);
    }

    [Fact]
    public async Task Classic_exercises_keep_using_the_bare_exercise_id()
    {
        var repo = new InMemoryDrafts();
        var drafts = new DraftService(repo, new InMemorySettings());
        var ex = Make.IoExercise("classic");
        await drafts.SaveAsync(ex, "python", "main.py", "v1");
        Assert.Equal("v1", repo.Items[("classic", "main.py")]);
        // a draft written by the old code path is still found
        repo.Items[("classic", "main.py")] = "legacy";
        Assert.Equal("legacy", (await drafts.LoadFilesAsync(ex, "python")).Single().Content);
    }

    [Fact]
    public async Task Preferred_language_is_remembered_and_falls_back_to_default()
    {
        var settings = new InMemorySettings();
        var drafts = new DraftService(new InMemoryDrafts(), settings);
        var ex = VariantMake.TwoLanguages();
        Assert.Equal("python", await drafts.GetLanguageAsync(ex));
        await drafts.SetLanguageAsync(ex, "javascript");
        Assert.Equal("javascript", await drafts.GetLanguageAsync(ex));
        settings.Items["exercise.language." + ex.Id] = "removed-language";
        Assert.Equal("python", await drafts.GetLanguageAsync(ex));
    }
}

public class ProblemServiceTests
{
    private static (ProblemService Service, InMemoryProgress Progress) Setup(ContentSet set)
    {
        var content = new InMemoryContent(set);
        var progress = new InMemoryProgress();
        return (new ProblemService(content, progress, new LearningPathService(content, progress, new FixedClock(Make.T0))), progress);
    }

    [Fact]
    public async Task Lists_only_exercise_lessons_of_algorithm_tracks_with_filters()
    {
        var (service, progress) = Setup(VariantMake.Algorithms(VariantMake.TwoLanguages(), Difficulty.Medium));
        Assert.True(service.HasProblems);
        var all = await service.ListAsync();
        var item = Assert.Single(all);
        Assert.Equal("algo.max", item.Lesson.Id);
        Assert.Equal(["python", "javascript"], item.Languages);
        Assert.False(item.Solved);

        Assert.Empty(await service.ListAsync(difficulty: Difficulty.Easy));
        Assert.Single(await service.ListAsync(difficulty: Difficulty.Medium, solved: false));
        Assert.Empty(await service.ListAsync(solved: true));

        progress.Items["algo.max"] = new LessonProgress("algo.max", ExercisePassed: true);
        Assert.True((await service.ListAsync(solved: true)).Single().Solved);
        Assert.Empty(await service.ListAsync(solved: false));
    }

    [Fact]
    public void No_algorithm_track_means_no_problems()
    {
        var set = new ContentSet(
            [new Track("python", TrackKind.Language, "Python", "", "python", ContentStatus.Available, ["py-basics"])],
            [Make.Course("py-basics", ["py.one"]) with { TrackId = "python" }],
            [Make.Lesson("py.one", "py-basics", ex: Make.IoExercise())]);
        Assert.False(Setup(set).Service.HasProblems);
    }

    [Fact]
    public async Task Complexity_and_editorial_stay_locked_until_solved_or_confirmed()
    {
        var set = VariantMake.Algorithms(VariantMake.TwoLanguages(), complexity: new ComplexityInfo("O(n)", "O(1)", "why"), editorial: "# Razbor");
        var (service, progress) = Setup(set);

        Assert.True(service.HasEditorial("algo.max"));
        Assert.Null(await service.GetComplexityAsync("algo.max"));
        Assert.Null(await service.GetEditorialAsync("algo.max", confirmedSpoiler: false));
        Assert.Equal("# Razbor", await service.GetEditorialAsync("algo.max", confirmedSpoiler: true));

        progress.Items["algo.max"] = new LessonProgress("algo.max", ExercisePassed: true);
        Assert.Equal(new ProblemComplexity("O(n)", "O(1)", "why"), await service.GetComplexityAsync("algo.max"));
        Assert.Equal("# Razbor", await service.GetEditorialAsync("algo.max", confirmedSpoiler: false));
    }

    [Fact]
    public async Task Lessons_without_complexity_or_editorial_return_nothing()
    {
        var (service, progress) = Setup(VariantMake.Algorithms(VariantMake.TwoLanguages()));
        progress.Items["algo.max"] = new LessonProgress("algo.max", ExercisePassed: true);
        Assert.False(service.HasEditorial("algo.max"));
        Assert.Null(await service.GetComplexityAsync("algo.max"));
        Assert.Null(await service.GetEditorialAsync("algo.max", true));
    }
}
