using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

public sealed class Fixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "da-content-" + Guid.NewGuid().ToString("N"));

    public Fixture()
    {
        Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid"), Root);
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }

    public string LessonDir(string name) => Path.Combine(Root, "courses", "py-basics", "lessons", name);

    public void Edit(string relativePath, string oldText, string newText)
    {
        var path = Path.Combine(Root, relativePath);
        var text = File.ReadAllText(path);
        Assert.Contains(oldText, text);
        File.WriteAllText(path, text.Replace(oldText, newText));
    }

    public void EditVars(string oldText, string newText) =>
        Edit("courses/py-basics/lessons/02-vars/lesson.yaml", oldText, newText);

    public ContentLoadResult Load() => ContentLoader.Load(Root);

    public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
}

public class ContentLoaderTests
{
    [Fact]
    public void ValidFixtureLoadsWithoutDiagnostics()
    {
        using var f = new Fixture();
        var r = f.Load();
        Assert.Empty(r.Diagnostics);
        var set = r.Content;
        Assert.Equal(2, set.Tracks.Count);
        Assert.Equal(ContentStatus.Planned, set.FindTrack("javascript")!.Status);
        Assert.Equal(["py.hello", "py.vars"], set.TopologicalOrder());
        Assert.Equal("backend", set.FindPath("backend")!.Id);
        Assert.Equal("1.x", set.FindCourse("py-basics")!.Framework!.Version);
    }

    [Fact]
    public void LessonDetailsAreParsed()
    {
        using var f = new Fixture();
        var vars = f.Load().Content.FindLesson("py.vars")!;
        Assert.Equal(Difficulty.Easy, vars.Difficulty);
        Assert.Equal(4, vars.Quiz!.Questions.Count);
        Assert.Equal([0, 2], vars.Quiz.Questions[1].CorrectIndices);
        Assert.Equal(["def"], vars.Quiz.Questions[2].AcceptedAnswers);
        Assert.Equal(0.33, vars.Quiz.Questions[3].NumericAnswer);
        Assert.Equal(0.005, vars.Quiz.Questions[3].Tolerance);
        Assert.Equal(EvaluationKind.Io, vars.Exercise!.Evaluation);
        Assert.Equal(OutputComparison.Trim, vars.Exercise.Tests[0].Compare);
        Assert.True(vars.Exercise.Tests[1].Hidden);
        Assert.Equal("main.py", vars.Exercise.Starter.Single(x => x.Path == "main.py").Path);
        Assert.Equal("python", vars.Exercise.LanguageId);
        Assert.Equal(EvaluationKind.Harness, vars.Challenge!.Evaluation);
        Assert.Equal("test_main.py", vars.Challenge.Harness.Single().Path);
        Assert.Equal(FactKind.Historical, vars.Facts[0].Kind);
    }

    [Fact]
    public void RunSamplesAreExtractedWithOutput()
    {
        using var f = new Fixture();
        var sample = f.Load().Content.FindLesson("py.hello")!.Theory.Samples.Single();
        Assert.Equal("python", sample.LanguageId);
        Assert.Equal("print(\"hi\")", sample.Code);
        Assert.Equal("hi", sample.ExpectedOutput);
    }

    [Fact]
    public void MissingRootReportsErrorInsteadOfThrowing()
    {
        var r = ContentLoader.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid()));
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("catalog.yaml", d.Path);
    }

    [Fact]
    public void MalformedYamlIsReportedWithPath()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.LessonDir("02-vars"), "lesson.yaml"), "id: [unclosed\n");
        var r = f.Load();
        Assert.Contains(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Path.EndsWith("02-vars/lesson.yaml") && d.Message.Contains("invalid YAML"));
    }

    [Fact]
    public void UnknownKeyIsAnError()
    {
        using var f = new Fixture();
        f.EditVars("goal: Use variables", "goal: Use variables\ngoall: typo");
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Path.EndsWith("02-vars/lesson.yaml"));
    }
}

public class ContentValidatorTests
{
    private static void AssertError(Fixture f, string messagePart) =>
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(messagePart));

    [Fact]
    public void DuplicateLessonId() { using var f = new Fixture(); f.EditVars("id: py.vars\n", "id: py.hello\n"); AssertError(f, "duplicate lesson id 'py.hello'"); }

    [Fact]
    public void DuplicateTestId() { using var f = new Fixture(); f.EditVars("id: t2", "id: t1"); AssertError(f, "duplicate test id 't1'"); }

    [Fact]
    public void DuplicateQuestionId() { using var f = new Fixture(); f.EditVars("id: q2", "id: q1"); AssertError(f, "duplicate question id 'q1'"); }

    [Fact]
    public void MissingPrerequisite() { using var f = new Fixture(); f.EditVars("prerequisites: [py.hello]", "prerequisites: [py.nope]"); AssertError(f, "unknown lesson 'py.nope'"); }

    [Fact]
    public void PrerequisiteCycleReportsPath()
    {
        using var f = new Fixture();
        f.Edit("courses/py-basics/lessons/01-hello/lesson.yaml", "difficulty: intro", "difficulty: intro\nprerequisites: [py.vars]");
        var cycle = f.Load().Diagnostics.Where(d => d.Message.StartsWith("prerequisite cycle")).ToList();
        Assert.Single(cycle);
        Assert.Contains("py.hello -> py.vars -> py.hello", cycle[0].Message);
    }

    [Fact]
    public void MissingTheorySection()
    {
        using var f = new Fixture();
        var path = Path.Combine(f.LessonDir("02-vars"), "theory.md");
        File.WriteAllText(path, File.ReadAllText(path).Replace("<!-- section: mistakes -->", "<!-- section: other -->"));
        AssertError(f, "missing or empty required section 'mistakes'");
    }

    [Fact]
    public void NoHiddenTest() { using var f = new Fixture(); f.EditVars("hidden: true\n      hint: handle any text", "hint: handle any text"); AssertError(f, "needs at least one hidden test"); }

    [Fact]
    public void IoTestWithoutExpected() { using var f = new Fixture(); f.EditVars("      expected: \"yo\\n\"\n", ""); AssertError(f, "io test 't2' has no 'expected'"); }

    [Fact]
    public void QuizIndexOutOfRange() { using var f = new Fixture(); f.EditVars("answer: 1\n", "answer: 7\n"); AssertError(f, "answer index 7 is out of range"); }

    [Fact]
    public void HistoricalFactWithoutSource()
    {
        using var f = new Fixture();
        f.EditVars("    sources: [https://docs.python.org/3/faq/general.html]\n", "");
        AssertError(f, "needs at least one https source");
    }

    [Fact]
    public void NonHttpsSource() { using var f = new Fixture(); f.EditVars("https://docs.python.org", "http://docs.python.org"); AssertError(f, "not an https URL"); }

    [Fact]
    public void BrokenLessonLink()
    {
        using var f = new Fixture();
        var path = Path.Combine(f.LessonDir("02-vars"), "theory.md");
        File.WriteAllText(path, File.ReadAllText(path).Replace("lesson:py.hello", "lesson:py.ghost"));
        AssertError(f, "lesson:py.ghost");
    }

    [Fact]
    public void MissingLessonDirectory()
    {
        using var f = new Fixture();
        f.Edit("courses/py-basics/course.yaml", "[01-hello, 02-vars]", "[01-hello, 02-vars, 03-ghost]");
        AssertError(f, "lessons/03-ghost");
    }

    [Fact]
    public void MissingStarterEntryFile()
    {
        using var f = new Fixture();
        File.Delete(Path.Combine(f.LessonDir("02-vars"), "exercise", "starter", "main.py"));
        AssertError(f, "starter has no entry file 'main.py'");
    }

    [Fact]
    public void HarnessEntryMissing() { using var f = new Fixture(); f.EditVars("harnessEntry: test_main.py", "harnessEntry: nope.py"); AssertError(f, "has no 'nope.py'"); }

    [Fact]
    public void HarnessCollisionWithStarterFile()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.LessonDir("02-vars"), "exercise", "starter", "test_main.py"), "x");
        AssertError(f, "collides with a starter/solution file");
    }

    [Fact]
    public void AvailableTrackWithoutCourses() { using var f = new Fixture(); f.Edit("catalog.yaml", "status: planned", "status: available"); AssertError(f, "has no courses"); }

    [Fact]
    public void UnknownCourseRequirementAndBadFrameworkDocs()
    {
        using var f = new Fixture();
        f.Edit("courses/py-basics/course.yaml", "framework:", "requires: [ghost]\nframework:");
        f.Edit("courses/py-basics/course.yaml", "https://docs.python.org/3/", "ftp://x");
        AssertError(f, "requires references unknown course 'ghost'");
        AssertError(f, "framework.docs entry is not an https URL");
    }

    [Fact]
    public void CourseRequiresCycle()
    {
        using var f = new Fixture();
        Directory.CreateDirectory(Path.Combine(f.Root, "courses", "other"));
        File.WriteAllText(Path.Combine(f.Root, "courses", "other", "course.yaml"),
            "id: other\nversion: 1.0.0\ntitle: O\nlanguageId: python\nlevel: 1\ntrack: python\nrequires: [py-basics]\nlessons: []\n");
        f.Edit("catalog.yaml", "courses: [py-basics]", "courses: [py-basics, other]");
        f.Edit("courses/py-basics/course.yaml", "framework:", "requires: [other]\nframework:");
        AssertError(f, "course dependency cycle");
    }

    [Fact]
    public void PathTargetMustExist() { using var f = new Fixture(); f.Edit("paths/backend.yaml", "[py-basics]", "[nope]"); AssertError(f, "unknown course 'nope'"); }

    [Fact]
    public void AssessmentForNeedsQuizAndKnownLessons()
    {
        using var f = new Fixture();
        f.Edit("courses/py-basics/lessons/01-hello/lesson.yaml", "difficulty: intro", "difficulty: intro\nassessmentFor: [py.nope]");
        AssertError(f, "assessmentFor requires the lesson to have a quiz");
        AssertError(f, "assessmentFor references unknown lesson 'py.nope'");
    }

    [Fact]
    public void CommandEvaluationNeedsCommandButNoTests()
    {
        using var f = new Fixture();
        f.EditVars("evaluation: io\n  hints: [direction, problem]", "evaluation: command\n  hints: [direction, problem]");
        AssertError(f, "command evaluation needs 'command'");
        f.EditVars("evaluation: command", "evaluation: command\n  command: npx vitest run");
        Assert.DoesNotContain(f.Load().Diagnostics, d => d.Message.Contains("hidden test") && d.Message.Contains("py.vars.ex1"));
    }

    [Fact]
    public void HintCountOutOfRange() { using var f = new Fixture(); f.EditVars("hints: [direction, problem]", "hints: []"); AssertError(f, "needs 1-4 hints"); }
}
