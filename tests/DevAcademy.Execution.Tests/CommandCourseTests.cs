using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Node;
using DevAcademy.Content;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

/// <summary>
/// Grades the real command lessons (node:test and go test) with the real runner: the reference solution must pass every
/// declared case, the starter must not pass. Skipped when the tool is not installed.
/// </summary>
public class CommandCourseTests
{
    private static readonly Lazy<ContentSet> Content = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DevAcademy.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return ContentLoader.Load(Path.Combine(dir.FullName, "content")).Content;
    });

    private static Exercise Load(string lessonId)
    {
        var lesson = Content.Value.FindLesson(lessonId);
        Assert.NotNull(lesson);
        return lesson.Exercise!;
    }

    private static Task<EvaluationReport> Grade(Exercise ex, IReadOnlyList<ExerciseFile> files) =>
        new ExerciseEvaluationService(TestSupport.Runner).EvaluateAsync(ex, files, CancellationToken.None, IsolationLevel.ProcessLimits);

    private static string Describe(EvaluationReport r) =>
        $"{r.Status}: {r.Message} | " + string.Join("; ", r.Cases.Where(c => !c.Passed).Select(c => $"{c.TestId}: {c.Message}"));

    [Theory]
    [InlineData("javascript", "nodejs.first-steps.modules-and-node-test")]
    [InlineData("go", "go.first-steps.packages-and-go-test")]
    public async Task SolutionPassesEveryCaseAndStarterFails(string language, string lessonId)
    {
        await TestSupport.RequireAsync(language);
        var ex = Load(lessonId);
        Assert.Equal(EvaluationKind.Command, ex.Evaluation);

        var solved = await Grade(ex, ex.Solution);
        Assert.True(solved.Status == Verdict.Passed, Describe(solved));
        Assert.Equal(ex.Tests.Count, solved.PassedCount);

        var starter = await Grade(ex, ex.Starter);
        Assert.NotEqual(Verdict.Passed, starter.Status);
        Assert.NotEqual(Verdict.Error, starter.Status);
        Assert.NotEqual(Verdict.RuntimeMissing, starter.Status);
    }

    [Theory]
    [InlineData("javascript", "nodejs.first-steps.modules-and-node-test", "src/money.mjs")]
    [InlineData("go", "go.first-steps.packages-and-go-test", "stock.go")]
    public async Task BrokenSyntaxIsACompileErrorWithoutLeakingTheWorkDirectory(string language, string lessonId, string entry)
    {
        await TestSupport.RequireAsync(language);
        var ex = Load(lessonId);
        var files = ex.Solution.Select(f => f.Path == entry ? f with { Content = f.Content + "\n}}} (((\n" } : f).ToList();
        var report = await Grade(ex, files);
        Assert.True(report.Status == Verdict.CompileError, Describe(report));
        Assert.False(string.IsNullOrWhiteSpace(report.Message));
        Assert.DoesNotContain("devacademy-run", report.Message);
    }

    [Fact]
    public async Task LearnerCannotReplaceTheHiddenNodeTests()
    {
        await TestSupport.RequireAsync("javascript");
        var ex = Load("nodejs.first-steps.modules-and-node-test");
        var fake = new ExerciseFile("tests/money.test.mjs", "import test from 'node:test'; test('parsePrice > целая цена', () => {});");
        var report = await Grade(ex, ex.Starter.Append(fake).ToList());
        Assert.NotEqual(Verdict.Passed, report.Status);
        Assert.Contains(report.Cases, c => !c.Passed);
    }

    /// <summary>
    /// TypeScript exercises are graded by vitest, and the hidden typecheck.test.ts spawns the pinned tsc (and Node type stripping) from inside
    /// the OS sandbox. This proves the sandbox lets that happen and that type errors are reported as failed cases with tsc diagnostics.
    /// </summary>
    [Theory]
    [InlineData("typescript.fundamentals.unions-and-narrowing", false)]
    [InlineData("typescript.fundamentals.in-practice", true)]
    public async Task TypeScriptExerciseIsTypeCheckedByTscInsideTheSandbox(string lessonId, bool starterDoesNotCompile)
    {
        await TestSupport.RequireAsync("javascript");
        var env = NodeEnvironmentService.FindRepositoryEnvironment();
        if (env is null) Assert.Skip("node content environment not installed (make node-env)");
        var ex = Load(lessonId);
        var service = new ExerciseEvaluationService(TestSupport.Runner, node: new NodeEnvironmentService(new SystemToolRunner(), env));

        var solved = await service.EvaluateAsync(ex, ex.Solution, CancellationToken.None, IsolationLevel.OsSandbox);
        Assert.True(solved.Status == Verdict.Passed, Describe(solved));
        Assert.Equal(ex.Tests.Count, solved.PassedCount);

        var starter = await service.EvaluateAsync(ex, ex.Starter, CancellationToken.None, IsolationLevel.OsSandbox);
        Assert.NotEqual(Verdict.Passed, starter.Status);
        Assert.NotEqual(Verdict.Error, starter.Status);
        Assert.NotEqual(Verdict.RuntimeMissing, starter.Status);
        var types = Assert.Single(starter.Cases, c => c.TestId == "types-compile");
        Assert.Equal(!starterDoesNotCompile, types.Passed);
        if (starterDoesNotCompile) Assert.Contains("error TS", types.Message); // tsc ran inside the sandbox and its diagnostics reached the learner
        // the precision check (@ts-expect-error lines must still error) also runs tsc in the sandbox and fails on weak starter types
        Assert.Contains(starter.Cases, c => c.TestId == "types-precise" || c.TestId == "exhaustive");
    }
}
