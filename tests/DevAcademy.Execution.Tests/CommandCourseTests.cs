using DevAcademy.Application.Evaluation;
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
}
