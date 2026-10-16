using DevAcademy.Domain;

namespace DevAcademy.Domain.Tests;

internal static class Make
{
    public static Lesson Lesson(string id, string[]? prereqs = null, Quiz? quiz = null, Exercise? exercise = null, string[]? next = null) =>
        new(id, "c", id, "goal", Difficulty.Intro, 10, prereqs ?? [], [], [], [], next ?? [], [], [],
            new Theory("", new Dictionary<string, string>(), []), [], quiz, exercise, null, id + ".yaml", []);

    public static Exercise Exercise(string id = "ex") =>
        new(id, "t", "", "python", "main.py", [], [], EvaluationKind.Io, [], null, [], []);

    public static Quiz Quiz(double threshold = 0.7) => new(threshold, []);

    public static QuizQuestion Q(QuestionType t, int[]? idx = null, string[]? accepted = null, double? num = null, double tol = 0) =>
        new("q", t, "p", ["a", "b", "c"], idx ?? [], accepted ?? [], num, tol, "");
}

public class QuizGraderTests
{
    [Fact]
    public void SingleRequiresExactIndex()
    {
        var q = Make.Q(QuestionType.Single, idx: [1]);
        Assert.True(QuizGrader.IsCorrect(q, QuizAnswer.Choice(1)));
        Assert.False(QuizGrader.IsCorrect(q, QuizAnswer.Choice(0)));
        Assert.False(QuizGrader.IsCorrect(q, QuizAnswer.Choice(1, 2)));
        Assert.False(QuizGrader.IsCorrect(q, null));
    }

    [Fact]
    public void MultipleRequiresExactSet()
    {
        var q = Make.Q(QuestionType.Multiple, idx: [0, 2]);
        Assert.True(QuizGrader.IsCorrect(q, QuizAnswer.Choice(2, 0)));
        Assert.False(QuizGrader.IsCorrect(q, QuizAnswer.Choice(0)));
        Assert.False(QuizGrader.IsCorrect(q, QuizAnswer.Choice(0, 1, 2)));
    }

    [Fact]
    public void ShortIsCaseInsensitiveAndTrimmed()
    {
        var q = Make.Q(QuestionType.Short, accepted: ["Def", "define"]);
        Assert.True(QuizGrader.IsCorrect(q, QuizAnswer.FromText("  DEF ")));
        Assert.True(QuizGrader.IsCorrect(q, QuizAnswer.FromText("define")));
        Assert.False(QuizGrader.IsCorrect(q, QuizAnswer.FromText("defn")));
        Assert.False(QuizGrader.IsCorrect(q, QuizAnswer.FromText("  ")));
    }

    [Theory]
    [InlineData(3.14, true)]
    [InlineData(3.1401, true)]
    [InlineData(3.15, false)]
    [InlineData(3.13, false)]
    public void NumericWithinTolerance(double given, bool expected)
    {
        var q = Make.Q(QuestionType.Numeric, num: 3.14, tol: 0.005);
        Assert.Equal(expected, QuizGrader.IsCorrect(q, QuizAnswer.FromNumber(given)));
    }

    [Fact]
    public void GradeComputesScoreAndPass()
    {
        var quiz = new Quiz(0.5, [
            Make.Q(QuestionType.Single, idx: [0]) with { Id = "a" },
            Make.Q(QuestionType.Single, idx: [1]) with { Id = "b" }]);
        var half = QuizGrader.Grade(quiz, new Dictionary<string, QuizAnswer> { ["a"] = QuizAnswer.Choice(0) });
        Assert.Equal(0.5, half.Score);
        Assert.True(half.Passed);
        Assert.False(half.Questions[1].Correct);
        var none = QuizGrader.Grade(quiz, new Dictionary<string, QuizAnswer>());
        Assert.Equal(0, none.Score);
        Assert.False(none.Passed);
    }
}

public class OutputComparerTests
{
    [Fact]
    public void ExactOnlyNormalisesCrlf()
    {
        Assert.True(OutputComparer.AreEqual("a\nb\n", "a\r\nb\r\n", OutputComparison.Exact));
        Assert.False(OutputComparer.AreEqual("a\n", "a", OutputComparison.Exact));
        Assert.False(OutputComparer.AreEqual("a \n", "a\n", OutputComparison.Exact));
    }

    [Fact]
    public void TrimIgnoresTrailingWhitespaceAndBlankLines()
    {
        Assert.True(OutputComparer.AreEqual("a\nb", "a  \r\nb\t\n\n\n", OutputComparison.Trim));
        Assert.False(OutputComparer.AreEqual("a\nb", " a\nb", OutputComparison.Trim));
        Assert.False(OutputComparer.AreEqual("a\n\nb", "a\nb", OutputComparison.Trim));
    }

    [Fact]
    public void TokensIgnoreAllWhitespaceLayout()
    {
        Assert.True(OutputComparer.AreEqual("1 2\n3", "  1\n2   3 ", OutputComparison.Tokens));
        Assert.False(OutputComparer.AreEqual("1 2 3", "1 2", OutputComparison.Tokens));
        Assert.False(OutputComparer.AreEqual("1.0", "1", OutputComparison.Tokens));
    }

    [Fact]
    public void NumericComparesTokensWithinTolerance()
    {
        Assert.True(OutputComparer.AreEqual("x = 1.0000001", "x = 1", OutputComparison.Numeric, 1e-6));
        Assert.False(OutputComparer.AreEqual("x = 1.01", "x = 1", OutputComparison.Numeric, 1e-6));
        Assert.True(OutputComparer.AreEqual("1e-3", "0.001", OutputComparison.Numeric));
        Assert.False(OutputComparer.AreEqual("abc 1", "abd 1", OutputComparison.Numeric));
        Assert.False(OutputComparer.AreEqual("1 2", "1", OutputComparison.Numeric));
    }
}

public class CompletionRuleTests
{
    private static readonly LessonProgress None = new("l");

    [Fact]
    public void TheoryOnlyNeedsRead()
    {
        var l = Make.Lesson("l");
        Assert.False(CompletionRule.IsSatisfied(l, None));
        Assert.True(CompletionRule.IsSatisfied(l, None with { TheoryRead = true }));
    }

    [Fact]
    public void ExerciseAndQuizBothRequired()
    {
        var l = Make.Lesson("l", quiz: Make.Quiz(0.7), exercise: Make.Exercise());
        Assert.False(CompletionRule.IsSatisfied(l, None with { ExercisePassed = true }));
        Assert.False(CompletionRule.IsSatisfied(l, None with { QuizBestScore = 1 }));
        Assert.False(CompletionRule.IsSatisfied(l, None with { ExercisePassed = true, QuizBestScore = 0.69 }));
        Assert.True(CompletionRule.IsSatisfied(l, None with { ExercisePassed = true, QuizBestScore = 0.7 }));
    }

    [Fact]
    public void ReadingNeverSubstitutesForQuiz() =>
        Assert.False(CompletionRule.IsSatisfied(Make.Lesson("l", quiz: Make.Quiz()), None with { TheoryRead = true }));

    [Fact]
    public void QuizOnlyAndExerciseOnly()
    {
        Assert.True(CompletionRule.IsSatisfied(Make.Lesson("l", quiz: Make.Quiz()), None with { QuizBestScore = 0.8 }));
        Assert.True(CompletionRule.IsSatisfied(Make.Lesson("l", exercise: Make.Exercise()), None with { ExercisePassed = true }));
    }
}

public class ReviewScheduleTests
{
    private static readonly DateTimeOffset T = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IntervalsAreFixed() => Assert.Equal([1, 3, 7, 16, 35], ReviewSchedule.IntervalDays);

    [Fact]
    public void FirstReviewIsOneDayAfterCompletion()
    {
        var s = ReviewSchedule.Initial(T);
        Assert.Equal(0, s.Step);
        Assert.Equal(T.AddDays(1), s.DueAt);
    }

    [Fact]
    public void SuccessAdvancesThroughAllIntervalsThenEnds()
    {
        var step = 0;
        var expectedDays = new[] { 3, 7, 16, 35 };
        foreach (var days in expectedDays)
        {
            var s = ReviewSchedule.AfterReview(step, true, T);
            Assert.Equal(step + 1, s.Step);
            Assert.Equal(T.AddDays(days), s.DueAt);
            step = s.Step;
        }
        var last = ReviewSchedule.AfterReview(step, true, T);
        Assert.Null(last.DueAt);
    }

    [Fact]
    public void FailureResetsToStepZero()
    {
        var s = ReviewSchedule.AfterReview(3, false, T);
        Assert.Equal(0, s.Step);
        Assert.Equal(T.AddDays(1), s.DueAt);
    }
}

public class LessonStateTests
{
    private static Dictionary<string, LessonProgress> Map(params LessonProgress[] p) => p.ToDictionary(x => x.LessonId);

    [Fact]
    public void LockedUntilAllPrerequisitesCompleted()
    {
        var l = Make.Lesson("c", ["a", "b"]);
        var done = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(LessonState.Locked, LessonStateCalculator.Compute(l, Map()));
        var partial = Map(new LessonProgress("a", CompletedAt: done), new LessonProgress("b", StartedAt: done));
        Assert.Equal(LessonState.Locked, LessonStateCalculator.Compute(l, partial));
        Assert.Equal(["b"], LessonStateCalculator.MissingPrerequisites(l, partial));
        var all = Map(new LessonProgress("a", CompletedAt: done), new LessonProgress("b", CompletedAt: done));
        Assert.Equal(LessonState.Available, LessonStateCalculator.Compute(l, all));
    }

    [Fact]
    public void StartedAndCompletedOverrideLocking()
    {
        var l = Make.Lesson("c", ["a"]);
        var t = DateTimeOffset.UnixEpoch;
        Assert.Equal(LessonState.InProgress, LessonStateCalculator.Compute(l, Map(new LessonProgress("c", StartedAt: t))));
        Assert.Equal(LessonState.Completed, LessonStateCalculator.Compute(l, Map(new LessonProgress("c", StartedAt: t, CompletedAt: t))));
    }

    [Fact]
    public void NoPrerequisitesIsAvailable() =>
        Assert.Equal(LessonState.Available, LessonStateCalculator.Compute(Make.Lesson("x"), Map()));
}

public class ContentSetTests
{
    [Fact]
    public void TopologicalOrderPutsPrerequisitesFirstAndIsStable()
    {
        var set = new ContentSet([], [], [Make.Lesson("c", ["a"]), Make.Lesson("b"), Make.Lesson("a")]);
        Assert.Equal(["b", "a", "c"], set.TopologicalOrder());
    }

    [Fact]
    public void TopologicalOrderAppendsCycleMembers()
    {
        var set = new ContentSet([], [], [Make.Lesson("x", ["y"]), Make.Lesson("y", ["x"]), Make.Lesson("z")]);
        Assert.Equal(["z", "x", "y"], set.TopologicalOrder());
    }
}
