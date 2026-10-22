using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

/// <summary>Concepts, former ids, track routes and progression heuristics: loading and validation.</summary>
public class LearningModelContentTests
{
    private const string Vars = "courses/py-basics/lessons/02-vars/lesson.yaml";
    private const string Hello = "courses/py-basics/lessons/01-hello/lesson.yaml";

    private static IEnumerable<ContentDiagnostic> Errors(ContentLoadResult r) => r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    private static void WriteRoute(Fixture f, string yaml)
    {
        Directory.CreateDirectory(Path.Combine(f.Root, "routes"));
        File.WriteAllText(Path.Combine(f.Root, "routes", "python.yaml"), yaml);
    }

    [Fact]
    public void ConceptsAndFormerIdsAreLoaded()
    {
        using var f = new Fixture();
        f.Edit(Hello, "next: [py.vars]", "next: [py.vars]\nformerIds: [py.old-hello]\nconcepts:\n  introduces: [py.print]");
        f.EditVars("prerequisites: [py.hello]", "prerequisites: [py.hello]\nconcepts: { introduces: [py.variable], uses: [py.print] }");
        var r = f.Load();
        Assert.Empty(Errors(r));
        var hello = r.Content.FindLesson("py.hello")!;
        Assert.Equal(["py.old-hello"], hello.PreviousIds);
        Assert.Equal(["py.print"], hello.Concepts!.Introduces);
        Assert.Equal(["py.print"], r.Content.FindLesson("py.vars")!.Concepts!.Uses);
    }

    [Fact]
    public void AConceptUsedBeforeItIsTaughtIsAnError()
    {
        using var f = new Fixture();
        f.EditVars("prerequisites: [py.hello]", "prerequisites: [py.hello]\nconcepts: { introduces: [py.variable], uses: [py.loop] }");
        Assert.Contains(Errors(f.Load()), d => d.Message.Contains("uses concept 'py.loop'"));
    }

    [Fact]
    public void AConceptTaughtByALaterLessonDoesNotCount()
    {
        using var f = new Fixture();
        // hello uses what vars introduces, but vars comes after hello.
        f.Edit(Hello, "next: [py.vars]", "next: [py.vars]\nconcepts: { uses: [py.variable] }");
        f.EditVars("prerequisites: [py.hello]", "prerequisites: [py.hello]\nconcepts: { introduces: [py.variable] }");
        Assert.Contains(Errors(f.Load()), d => d.Path.EndsWith("01-hello/lesson.yaml") && d.Message.Contains("py.variable"));
    }

    [Theory]
    [InlineData("formerIds: [py.vars]", "is the id of an existing lesson")]
    [InlineData("formerIds: [py.hello]", "lists the lesson's own id")]
    public void BadFormerIdsAreErrors(string yaml, string message)
    {
        using var f = new Fixture();
        f.Edit(Hello, "next: [py.vars]", "next: [py.vars]\n" + yaml);
        Assert.Contains(Errors(f.Load()), d => d.Message.Contains(message));
    }

    [Fact]
    public void AFormerIdClaimedTwiceIsAnError()
    {
        using var f = new Fixture();
        f.Edit(Hello, "next: [py.vars]", "next: [py.vars]\nformerIds: [py.old]");
        f.EditVars("prerequisites: [py.hello]", "prerequisites: [py.hello]\nformerIds: [py.old]");
        Assert.Contains(Errors(f.Load()), d => d.Message.Contains("also claimed"));
    }

    [Fact]
    public void ARouteWithEntriesAndACheckLoads()
    {
        using var f = new Fixture();
        f.EditVars("prerequisites: [py.hello]", "prerequisites: [py.hello]\nassessmentFor: [py.hello]");
        WriteRoute(f, "track: python\nentry: { beginner: py.hello, programmer: py.hello }\ncompetencies:\n  - { id: basics, title: Basics, check: py.vars }\n");
        var r = f.Load();
        Assert.Empty(Errors(r));
        var route = r.Content.FindRoute("python")!;
        Assert.Equal("py.hello", route.BeginnerEntry);
        Assert.Equal("py.vars", Assert.Single(route.Competencies).CheckLessonId);
    }

    [Theory]
    [InlineData("track: python\nentry: { beginner: nope }\n", "unknown lesson 'nope'")]
    [InlineData("track: python\ncompetencies:\n  - { id: a, title: A, check: py.hello }\n", "has no assessmentFor")]
    [InlineData("track: python\ncompetencies:\n  - { id: a, title: A, check: py.vars, after: [b] }\n", "unknown competency 'b'")]
    [InlineData("track: javascript\n", "must match the file name")]
    public void InvalidRoutesAreErrors(string yaml, string message)
    {
        using var f = new Fixture();
        f.EditVars("prerequisites: [py.hello]", "prerequisites: [py.hello]\nassessmentFor: [py.hello]");
        WriteRoute(f, yaml);
        Assert.Contains(Errors(f.Load()), d => d.Message.Contains(message));
    }

    [Fact]
    public void CompetencyOrderMustBeAcyclic()
    {
        var hello = Lesson("h", "c");
        var set = new ContentSet(
            [new Track("t", TrackKind.Language, "T", "", null, ContentStatus.Available, ["c"])],
            [Course("c", 0, ["h", "x", "y"])],
            [hello, Lesson("x", "c", assessmentFor: ["h"]), Lesson("y", "c", assessmentFor: ["h"])],
            routes: [new TrackRoute("t", null, null, [new Competency("a", "A", "", "x", ["b"]), new Competency("b", "B", "", "y", ["a"])], "routes/t.yaml")]);
        Assert.Contains(ContentValidator.Validate(set), d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("cycle"));
    }

    [Fact]
    public void ProgressionHeuristicsAreWarningsNotErrors()
    {
        var set = new ContentSet(
            [new Track("t", TrackKind.Language, "T", "", null, ContentStatus.Available, ["zero", "pro"])],
            [Course("zero", 0, ["a", "b", "c"]), Course("pro", 4, ["p"])],
            [Lesson("a", "zero"), Lesson("b", "zero"), Lesson("c", "zero", ["p"], Difficulty.Hard), Lesson("p", "pro")]);
        var d = ContentValidator.Validate(set).Where(x => x.Path.StartsWith("zero/")).ToList();
        Assert.All(d, x => Assert.Equal(DiagnosticSeverity.Warning, x.Severity));
        Assert.Contains(d, x => x.Path == "zero/b.yaml" && x.Message.Contains("no prerequisites"));
        Assert.Contains(d, x => x.Path == "zero/c.yaml" && x.Message.Contains("difficulty 'hard'"));
        Assert.Contains(d, x => x.Path == "zero/c.yaml" && x.Message.Contains("more advanced"));
    }

    [Fact]
    public void ACheckMayUseAnExerciseInsteadOfAQuiz()
    {
        var ex = new Exercise("e", "E", "", "python", "main.py", [new("main.py", "")], [new("main.py", "print(1)")], EvaluationKind.Io, [], null,
            [new TestCase("a", "A", "", "1", OutputComparison.Trim, 0, false, null), new TestCase("b", "B", "", "1", OutputComparison.Trim, 0, true, "h")], ["h"]);
        var set = new ContentSet([], [Course("c", 1, ["h", "x"])], [Lesson("h", "c"), Lesson("x", "c", assessmentFor: ["h"]) with { Exercise = ex }]);
        Assert.DoesNotContain(ContentValidator.Validate(set), d => d.Message.Contains("assessmentFor requires"));
    }

    private static Course Course(string id, int level, string[] lessons) =>
        new(id, "1.0.0", "t", "python", id, "", level, lessons, id + "/course.yaml", [], null);

    private static Lesson Lesson(string id, string course, string[]? prereqs = null, Difficulty difficulty = Difficulty.Easy, string[]? assessmentFor = null) =>
        new(id, course, id, "goal", difficulty, 10, prereqs ?? [], [], [], [], [], [], [], new Theory("", new Dictionary<string, string>(), []), [],
            assessmentFor is null ? null : new Quiz(0.5, [new QuizQuestion("q1", QuestionType.Single, "p", ["a", "b"], [1], [], null, 0, "e")]),
            null, null, course + "/" + id + ".yaml", assessmentFor ?? []);
}
