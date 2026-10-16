using System.Globalization;

namespace DevAcademy.Domain;

/// <summary>A learner's answer. Use the factory matching the question type.</summary>
public sealed record QuizAnswer(IReadOnlyList<int>? Selected = null, string? Text = null, double? Number = null)
{
    public static QuizAnswer Choice(params int[] selected) => new(Selected: selected);
    public static QuizAnswer FromText(string text) => new(Text: text);
    public static QuizAnswer FromNumber(double number) => new(Number: number);
}

public sealed record QuestionResult(string QuestionId, bool Correct);

public sealed record QuizResult(double Score, bool Passed, IReadOnlyList<QuestionResult> Questions);

public static class QuizGrader
{
    public static bool IsCorrect(QuizQuestion q, QuizAnswer? answer)
    {
        if (answer is null) return false;
        switch (q.Type)
        {
            case QuestionType.Single:
                return answer.Selected is { Count: 1 } s && q.CorrectIndices.Count == 1 && s[0] == q.CorrectIndices[0];
            case QuestionType.Multiple:
                return answer.Selected is { } sel
                    && sel.ToHashSet().SetEquals(q.CorrectIndices);
            case QuestionType.Short:
                var text = answer.Text?.Trim();
                return !string.IsNullOrEmpty(text)
                    && q.AcceptedAnswers.Any(a => string.Equals(a.Trim(), text, StringComparison.OrdinalIgnoreCase));
            case QuestionType.Numeric:
                return answer.Number is { } x && q.NumericAnswer is { } a2
                    && !double.IsNaN(x) && Math.Abs(x - a2) <= q.Tolerance;
            default:
                throw new ArgumentOutOfRangeException(nameof(q), q.Type, "Unknown question type");
        }
    }

    /// <summary>Score = correct / total. Unanswered questions are wrong.</summary>
    public static QuizResult Grade(Quiz quiz, IReadOnlyDictionary<string, QuizAnswer> answers)
    {
        var results = quiz.Questions
            .Select(q => new QuestionResult(q.Id, IsCorrect(q, answers.GetValueOrDefault(q.Id))))
            .ToList();
        var score = results.Count == 0 ? 0 : (double)results.Count(r => r.Correct) / results.Count;
        return new QuizResult(score, score >= quiz.PassThreshold, results);
    }
}

public static class OutputComparer
{
    public const double DefaultTolerance = 1e-6;

    public static bool AreEqual(string expected, string actual, OutputComparison mode, double tolerance = DefaultTolerance) =>
        mode switch
        {
            OutputComparison.Exact => NormalizeNewlines(expected) == NormalizeNewlines(actual),
            OutputComparison.Trim => TrimLines(expected) == TrimLines(actual),
            OutputComparison.Tokens => Tokens(expected).SequenceEqual(Tokens(actual), StringComparer.Ordinal),
            OutputComparison.Numeric => NumericEqual(Tokens(expected), Tokens(actual), tolerance),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown comparison mode"),
        };

    private static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n");

    private static string TrimLines(string s)
    {
        var lines = NormalizeNewlines(s).Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines);
    }

    private static string[] Tokens(string s) =>
        s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static bool NumericEqual(string[] expected, string[] actual, double tolerance)
    {
        if (expected.Length != actual.Length) return false;
        for (var i = 0; i < expected.Length; i++)
        {
            if (TryNumber(expected[i], out var e) && TryNumber(actual[i], out var a))
            {
                if (!(Math.Abs(e - a) <= tolerance)) return false;
            }
            else if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool TryNumber(string token, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && !double.IsNaN(value);
}

public static class CompletionRule
{
    /// <summary>
    /// Exercise passed (if the lesson has one) and best quiz score at threshold (if it has a quiz).
    /// Theory-only lessons complete when marked as read. The challenge never gates completion.
    /// </summary>
    public static bool IsSatisfied(Lesson lesson, LessonProgress progress)
    {
        if (lesson.Exercise is null && lesson.Quiz is null) return progress.TheoryRead;
        if (lesson.Exercise is not null && !progress.ExercisePassed) return false;
        if (lesson.Quiz is { } quiz && !(progress.QuizBestScore >= quiz.PassThreshold)) return false;
        return true;
    }
}

public sealed record ReviewState(int Step, DateTimeOffset? DueAt);

public static class ReviewSchedule
{
    public static readonly IReadOnlyList<int> IntervalDays = [1, 3, 7, 16, 35];

    /// <summary>
    /// State right after completion. The first interval is one day, which is also the rule for
    /// lessons with 3 or more failed attempts, so the failed-attempt count does not change it.
    /// </summary>
    public static ReviewState Initial(DateTimeOffset completedAt) =>
        new(0, completedAt.AddDays(IntervalDays[0]));

    /// <summary>Success advances one step (DueAt null after the last); failure restarts at step 0.</summary>
    public static ReviewState AfterReview(int step, bool passed, DateTimeOffset now)
    {
        if (!passed) return new(0, now.AddDays(IntervalDays[0]));
        var next = step + 1;
        return next < IntervalDays.Count
            ? new(next, now.AddDays(IntervalDays[next]))
            : new(next, null);
    }
}

public static class LessonStateCalculator
{
    public static LessonState Compute(Lesson lesson, IReadOnlyDictionary<string, LessonProgress> progress)
    {
        if (progress.TryGetValue(lesson.Id, out var p))
        {
            if (p.IsCompleted) return LessonState.Completed;
            if (p.StartedAt is not null) return LessonState.InProgress;
        }
        return MissingPrerequisites(lesson, progress).Count == 0 ? LessonState.Available : LessonState.Locked;
    }

    /// <summary>Prerequisite ids that are not completed.</summary>
    public static IReadOnlyList<string> MissingPrerequisites(Lesson lesson, IReadOnlyDictionary<string, LessonProgress> progress) =>
        lesson.Prerequisites.Where(id => !(progress.TryGetValue(id, out var p) && p.IsCompleted)).ToList();
}
