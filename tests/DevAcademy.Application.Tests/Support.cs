using DevAcademy.Application.Ports;
using DevAcademy.Domain;
using DevAcademy.Application.Evaluation;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class InMemoryContent(ContentSet set) : IContentRepository
{
    public ContentSet Current => set;
    public IReadOnlyList<ContentDiagnostic> Diagnostics => [];
    public void Reload() { }
}

internal sealed class InMemoryProgress : IProgressRepository
{
    public Dictionary<string, LessonProgress> Items { get; } = new();
    public List<QuizAttempt> Attempts { get; } = [];
    public Task<IReadOnlyList<LessonProgress>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LessonProgress>>(Items.Values.ToList());
    public Task<LessonProgress?> GetAsync(string lessonId, CancellationToken ct = default) => Task.FromResult(Items.GetValueOrDefault(lessonId));
    public Task SaveAsync(LessonProgress progress, CancellationToken ct = default) { Items[progress.LessonId] = progress; return Task.CompletedTask; }
    public Task AddQuizAttemptAsync(QuizAttempt attempt, CancellationToken ct = default) { Attempts.Add(attempt); return Task.CompletedTask; }
    public Task<IReadOnlyList<QuizAttempt>> GetQuizAttemptsAsync(string lessonId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<QuizAttempt>>(Attempts.Where(a => a.LessonId == lessonId).ToList());
}

internal sealed class InMemorySubmissions : ISubmissionRepository
{
    public Dictionary<string, Submission> Items { get; } = new();
    public Task<bool> AddAsync(Submission s, CancellationToken ct = default) => Task.FromResult(Items.TryAdd(s.Id, s));
    public Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Submission>>(Items.Values.ToList());
    public Task<IReadOnlyList<Submission>> GetByLessonAsync(string lessonId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Submission>>(Items.Values.Where(s => s.LessonId == lessonId).ToList());
}

internal sealed class InMemorySettings : ISettingsRepository
{
    public Dictionary<string, string> Items { get; } = new();
    public LearnerProfile? Profile { get; set; }
    public Dictionary<string, int> Hints { get; } = new();
    public Task<string?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult(Items.GetValueOrDefault(key));
    public Task SetAsync(string key, string value, CancellationToken ct = default) { Items[key] = value; return Task.CompletedTask; }
    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Items));
    public Task<LearnerProfile?> GetProfileAsync(CancellationToken ct = default) => Task.FromResult(Profile);
    public Task SaveProfileAsync(LearnerProfile profile, CancellationToken ct = default) { Profile = profile; return Task.CompletedTask; }
    public Task<int> GetRevealedHintLevelAsync(string exerciseId, CancellationToken ct = default) => Task.FromResult(Hints.GetValueOrDefault(exerciseId));
    public Task SetRevealedHintLevelAsync(string exerciseId, int level, CancellationToken ct = default) { Hints[exerciseId] = level; return Task.CompletedTask; }
}

internal sealed class FakeProgram(Func<string?, IReadOnlyDictionary<string, string>?, ExecutionResult> run, ExecutionResult? failure = null) : IPreparedProgram
{
    public string LanguageId => "python";
    public string WorkingDirectory => "/tmp/fake";
    public ExecutionResult? PreparationFailure => failure;
    public string CompileOutput => "";
    public int Runs { get; private set; }
    public List<IReadOnlyDictionary<string, string>?> Environments { get; } = [];

    /// <summary>Files the "test tool" left in the work directory (e.g. a report file), by relative path.</summary>
    public Dictionary<string, string> OutputFiles { get; } = new();
    public string? TryReadFile(string relativePath, int maxBytes) => OutputFiles.GetValueOrDefault(relativePath);

    public Task<ExecutionResult> RunAsync(string? stdin, ResourceLimits limits, CancellationToken ct, IReadOnlyDictionary<string, string>? extraEnvironment = null)
    {
        Runs++;
        Environments.Add(extraEnvironment);
        return Task.FromResult(run(stdin, extraEnvironment));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeRunner(Func<string?, IReadOnlyDictionary<string, string>?, ExecutionResult> run, ExecutionResult? failure = null) : ICodeRunner
{
    public int Prepares { get; private set; }
    public ExecutionRequest? LastRequest { get; private set; }
    public FakeProgram? Program { get; private set; }

    /// <summary>Files every prepared program "finds" in its work directory after the run (e.g. the test tool's report).</summary>
    public Dictionary<string, string> OutputFiles { get; } = new();

    public Task<IPreparedProgram> PrepareAsync(ExecutionRequest request, CancellationToken ct)
    {
        Prepares++;
        LastRequest = request;
        Program = new FakeProgram(run, failure);
        foreach (var (path, content) in OutputFiles) Program.OutputFiles[path] = content;
        return Task.FromResult<IPreparedProgram>(Program);
    }

    public Task<ExecutionResult> RunOnceAsync(ExecutionRequest request, string? stdin, ResourceLimits limits, CancellationToken ct) =>
        Task.FromResult(run(stdin, null));

    public Task<IReadOnlyList<RuntimeStatus>> DetectRuntimesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RuntimeStatus>>([]);

    public static ExecutionResult Ok(string stdout, ExecutionStatus status = ExecutionStatus.Success) =>
        new(status, status == ExecutionStatus.Success ? 0 : 1, stdout, "", "", TimeSpan.FromMilliseconds(5), null, IsolationLevel.OsSandbox);
}

internal static class Make
{
    public static readonly DateTimeOffset T0 = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public static TestCase Test(string id, string stdin, string expected, bool hidden = false, string? hint = null,
        OutputComparison cmp = OutputComparison.Trim) =>
        new(id, "title " + id, stdin, expected, cmp, 1e-6, hidden, hint);

    public static Exercise IoExercise(string id = "ex1") =>
        new(id, "Echo", "", "python", "main.py", [new("main.py", "pass")], [new("main.py", "print(input())")], EvaluationKind.Io, [], null,
            [Test("t1", "a\n", "A\n"), Test("t2", "secret-in\n", "SECRET-IN\n", hidden: true, hint: "think about case")], ["h1", "h2", "h3"]);

    public static Exercise HarnessExercise(string id = "hx") =>
        new(id, "Harness", "", "python", "main.py", [], [],
            EvaluationKind.Harness, [new("test_main.py", "harness")], "test_main.py",
            [new TestCase("a", "A", null, null, OutputComparison.Trim, 0, false, "hint a"), new TestCase("b", "B", null, null, OutputComparison.Trim, 0, true, "hint b")], ["h"]);

    public static Lesson Lesson(string id, string course, string[]? prereqs = null, bool quiz = false, Exercise? ex = null,
        string[]? next = null, string[]? assessmentFor = null, Difficulty difficulty = Difficulty.Easy) =>
        new(id, course, "Title " + id, "goal", difficulty, 10, prereqs ?? [], [], [], [], next ?? [], [], [],
            new Theory("", new Dictionary<string, string>(), []), [],
            quiz ? new Quiz(0.5, [new QuizQuestion("q1", QuestionType.Single, "p", ["a", "b"], [1], [], null, 0, "")]) : null,
            ex, null, id + "/lesson.yaml", assessmentFor ?? []);

    public static Course Course(string id, string[] lessons, string[]? requires = null) =>
        new(id, "1.0.0", "t", "python", "Course " + id, "", 0, lessons, id + "/course.yaml", requires ?? [], null);

    public static IReadOnlyDictionary<string, QuizAnswer> RightAnswer => new Dictionary<string, QuizAnswer> { ["q1"] = QuizAnswer.Choice(1) };
    public static IReadOnlyDictionary<string, QuizAnswer> WrongAnswer => new Dictionary<string, QuizAnswer> { ["q1"] = QuizAnswer.Choice(0) };

    public static EvaluationReport Report(Verdict v, int passed = 1, int total = 1) =>
        new(v, Enumerable.Range(0, total).Select(i => new Evaluation.CaseResult("t" + i, "t", false, i < passed, null, null)).ToList(),
            TimeSpan.FromMilliseconds(10), IsolationLevel.OsSandbox);
}
