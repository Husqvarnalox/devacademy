using System.Text;
using DevAcademy.AI;
using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Tutor;

public sealed record BuiltContext(IReadOnlyList<ChatMessage> Messages, IReadOnlyList<string> Sections);

/// <summary>
/// Gathers tutor context under explicit toggles. NEVER reads Exercise.Solution, Exercise.Harness, hidden test inputs/expected
/// outputs or hidden-test messages: those members are simply not accessed for anything but filtering.
/// </summary>
public sealed class TutorContextBuilder(IContentRepository content, ISubmissionRepository submissions, ISettingsRepository settings)
{
    public const int MaxCodeChars = 24_000;
    public const int MaxResultChars = 6_000;
    public const int MaxAttempts = 3;
    public const int MaxHistoryMessages = 6;

    public async Task<BuiltContext> BuildAsync(TutorRequest request, TutorContextOptions options, CancellationToken ct = default)
    {
        var sections = new List<string>();
        var sb = new StringBuilder();

        Lesson? lesson = request.LessonId is null ? null : content.Current.FindLesson(request.LessonId);
        Exercise? exercise = null;
        if (request.ExerciseId is not null)
        {
            var found = content.Current.FindExercise(request.ExerciseId);
            exercise = found?.Exercise;
            lesson ??= found?.Lesson;
        }
        exercise ??= lesson?.Exercise;

        if (options.IncludeLesson)
        {
            var profile = await settings.GetProfileAsync(ct);
            if (profile is not null)
            {
                sb.AppendLine("## Learner").AppendLine($"Level: {profile.Level}; depth: {profile.Depth}; goal: {profile.Goal}").AppendLine();
                sections.Add("Learner profile");
            }
            if (lesson is not null)
            {
                sb.AppendLine("## Lesson").AppendLine($"Title: {lesson.Title}").AppendLine($"Goal: {lesson.Goal}")
                  .AppendLine($"Difficulty: {lesson.Difficulty}");
                foreach (var o in lesson.Objectives) sb.AppendLine($"- {o}");
                sb.AppendLine();
                sections.Add("Lesson");
            }
            if (exercise is not null)
            {
                sb.AppendLine("## Exercise statement").AppendLine($"Title: {exercise.Title}").AppendLine($"Language: {exercise.LanguageId}")
                  .AppendLine(exercise.Prompt.Trim()).AppendLine();
                var publicTests = exercise.Tests.Where(t => !t.Hidden).ToList(); // hidden tests are filtered out here and nowhere else touched
                if (publicTests.Count > 0)
                {
                    sb.AppendLine("Public examples:");
                    foreach (var t in publicTests)
                        sb.AppendLine($"- {t.Title}: input {Quote(t.Stdin)} -> expected {Quote(t.Expected)}");
                    sb.AppendLine();
                }
                sections.Add("Exercise statement");
            }
        }

        var lang = exercise?.LanguageId ?? "";

        if (options.IncludeCode && request.CurrentFiles is { Count: > 0 } files)
        {
            sb.AppendLine("## Learner's current code");
            AppendFiles(sb, files, lang, MaxCodeChars);
            sections.Add($"Code ({files.Count} file{(files.Count == 1 ? "" : "s")})");
        }

        if (options.IncludeResults && (request.LastReport is not null || !string.IsNullOrWhiteSpace(request.RunOutput)))
        {
            sb.AppendLine("## Latest run / check result");
            if (request.LastReport is { } rep) AppendReport(sb, rep);
            if (!string.IsNullOrWhiteSpace(request.RunOutput))
                sb.AppendLine("Run output:").AppendLine(Fence(Truncate(request.RunOutput!, MaxResultChars), "")).AppendLine();
            sections.Add("Test results / compile errors");
        }

        if (options.IncludePreviousAttempts && lesson is not null)
        {
            var all = await submissions.GetByLessonAsync(lesson.Id, ct);
            var prev = all.Where(s => exercise is null || s.ExerciseId == exercise.Id).OrderByDescending(s => s.At).Take(MaxAttempts).ToList();
            if (prev.Count > 0)
            {
                sb.AppendLine("## Previous attempts (newest first)");
                var n = 0;
                foreach (var s in prev)
                {
                    sb.AppendLine($"Attempt {++n}: {s.Verdict}, {s.Passed}/{s.Total} tests passed");
                    if (options.IncludeCode) AppendFiles(sb, s.Files, s.LanguageId, MaxCodeChars / MaxAttempts);
                }
                sections.Add($"Previous attempts ({prev.Count})");
            }
        }

        sb.AppendLine("## Learner's message").AppendLine(request.UserMessage.Trim());

        var messages = new List<ChatMessage> { new(ChatRole.System, TutorPrompts.For(request.Scenario, request.HintLevel)) };
        if (request.History is { Count: > 0 } h)
            messages.AddRange(h.Where(m => m.Role != ChatRole.System).TakeLast(MaxHistoryMessages));
        messages.Add(new ChatMessage(ChatRole.User, sb.ToString().TrimEnd()));
        sections.Add("Your message");
        return new BuiltContext(messages, sections);
    }

    private static void AppendReport(StringBuilder sb, EvaluationReport rep)
    {
        sb.AppendLine($"Verdict: {rep.Status}; passed {rep.PassedCount}/{rep.TotalCount}");
        if (!string.IsNullOrWhiteSpace(rep.Message)) sb.AppendLine(Fence(Truncate(rep.Message!, MaxResultChars), ""));
        var budget = MaxResultChars;
        var hiddenIndex = 0;
        foreach (var c in rep.Cases)
        {
            string line;
            if (c.Hidden)
                line = $"- Hidden test {++hiddenIndex}: {(c.Passed ? "passed" : "failed")}"; // no title, message or details of hidden tests
            else
            {
                line = $"- {c.Title}: {(c.Passed ? "passed" : "failed")}";
                if (!string.IsNullOrWhiteSpace(c.Message)) line += $" ({Truncate(c.Message!, 500)})";
                if (!c.Passed && c.Detail is { } d)
                    line += $"\n  input {Quote(d.Stdin)}, expected {Quote(d.Expected)}, actual {Quote(d.Actual)}";
            }
            if ((budget -= line.Length) < 0) { sb.AppendLine("- [more cases omitted]"); break; }
            sb.AppendLine(line);
        }
        sb.AppendLine();
    }

    private static void AppendFiles(StringBuilder sb, IReadOnlyList<ExerciseFile> files, string lang, int budget)
    {
        foreach (var f in files)
        {
            if (budget <= 0) { sb.AppendLine($"[{f.Path}: omitted, size limit]"); continue; }
            var body = Truncate(f.Content, budget);
            budget -= body.Length;
            sb.AppendLine($"File: {f.Path}").AppendLine(Fence(body, lang));
        }
        sb.AppendLine();
    }

    private static string Quote(string? s) => s is null ? "(none)" : "\"" + Truncate(s.TrimEnd('\n'), 300).Replace("\n", "\\n") + "\"";

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "\n[...truncated]";

    /// <summary>Code fence longer than any backtick run inside, so learner code cannot break out of its block.</summary>
    public static string Fence(string body, string lang)
    {
        var longest = 0;
        for (int i = 0, run = 0; i < body.Length; i++)
        {
            run = body[i] == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        var ticks = new string('`', Math.Max(3, longest + 1));
        return $"{ticks}{lang}\n{body}\n{ticks}";
    }
}
