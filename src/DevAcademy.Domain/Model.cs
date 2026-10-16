namespace DevAcademy.Domain;

public enum TrackKind { Language, Framework, Algorithms, ComputerScience, MachineLearning }

public enum ContentStatus { Available, Planned }

public sealed record Track(
    string Id,
    TrackKind Kind,
    string Title,
    string Description,
    string? LanguageId,
    ContentStatus Status,
    IReadOnlyList<string> CourseIds);

public sealed record DeprecatedFeature(string Feature, string Status, string Note);

/// <summary>Framework metadata of a course. Its Version is the framework's, never the curriculum's.</summary>
public sealed record FrameworkInfo(
    string Id,
    string? Version,
    IReadOnlyList<string> Docs,
    string? Compatibility,
    IReadOnlyList<DeprecatedFeature> Deprecated,
    string? MigrationNotes);

/// <param name="Version">Curriculum version (semver), not a framework version.</param>
/// <param name="Requires">Ids of courses whose knowledge this course builds on.</param>
/// <param name="SourcePath">Path of course.yaml relative to the content root (for diagnostics).</param>
public sealed record Course(
    string Id,
    string Version,
    string TrackId,
    string LanguageId,
    string Title,
    string Description,
    int Level,
    IReadOnlyList<string> LessonIds,
    string SourcePath,
    IReadOnlyList<string> Requires,
    FrameworkInfo? Framework);

/// <summary>A learning path: a goal expressed as target courses; the lesson graph is derived from prerequisites.</summary>
public sealed record LearningPathDefinition(
    string Id,
    string Title,
    string Description,
    string Goal,
    IReadOnlyList<string> TargetCourseIds,
    string SourcePath,
    IReadOnlyList<PathStage>? Stages = null,
    IReadOnlyList<IReadOnlyList<string>>? Alternatives = null)
{
    /// <summary>Optional readable grouping of courses; empty when the path declares none.</summary>
    public IReadOnlyList<PathStage> StageList => Stages ?? [];

    /// <summary>"One of" groups: the learner picks one course per group; the others are not required.</summary>
    public IReadOnlyList<IReadOnlyList<string>> AlternativeGroups => Alternatives ?? [];
}

/// <summary>A titled group of courses inside a path (display only; ordering still comes from dependencies).</summary>
public sealed record PathStage(string Title, IReadOnlyList<string> CourseIds);

public enum SubjectKind { Library, Framework, Language, Runtime }

/// <summary>One compared thing. <see cref="Version"/> is the version the cells were written against.</summary>
public sealed record ComparisonSubject(string Id, string Title, SubjectKind Kind, string? Version, IReadOnlyList<string> Docs);

public sealed record ComparisonCriterion(string Id, string Title, string Description);

/// <summary>
/// A side-by-side comparison (content/comparisons/&lt;id&gt;.yaml). <see cref="Cells"/> maps criterion id to subject id to Markdown.
/// <see cref="VerdictGuidance"/> says when to choose which; a comparison never names a universal winner.
/// </summary>
public sealed record Comparison(
    string Id,
    string Title,
    IReadOnlyList<ComparisonSubject> Subjects,
    IReadOnlyList<ComparisonCriterion> Criteria,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Cells,
    string VerdictGuidance,
    IReadOnlyList<string> Sources,
    string SourcePath)
{
    public string? Cell(string criterionId, string subjectId) =>
        Cells.TryGetValue(criterionId, out var row) && row.TryGetValue(subjectId, out var text) ? text : null;
}

public enum Difficulty { Intro, Easy, Medium, Hard, Expert }

public enum FactKind { Historical, Internals, Misconception, Performance, Security, RealWorld }

public sealed record Fact(FactKind Kind, string Text, IReadOnlyList<string> Sources);

public enum QuestionType { Single, Multiple, Short, Numeric }

/// <summary>
/// Which of CorrectIndices / AcceptedAnswers / NumericAnswer is meaningful depends on Type.
/// </summary>
public sealed record QuizQuestion(
    string Id,
    QuestionType Type,
    string Prompt,
    IReadOnlyList<string> Options,
    IReadOnlyList<int> CorrectIndices,
    IReadOnlyList<string> AcceptedAnswers,
    double? NumericAnswer,
    double Tolerance,
    string Explanation);

public sealed record Quiz(double PassThreshold, IReadOnlyList<QuizQuestion> Questions);

/// <summary>Io: stdout vs expected. Harness: content script prints one line per case. Command: a real test tool runs and its report is mapped to the declared cases.</summary>
public enum EvaluationKind { Io, Harness, Command }

/// <summary>Machine-readable report a test tool produces: JUnit XML (from a file or stdout) or <c>go test -json</c> events (stdout).</summary>
public enum CommandReportFormat { Junit, GoTestJson }

/// <summary>
/// How a <see cref="EvaluationKind.Command"/> exercise is graded. <see cref="Run"/> is the argv (executable first; must be a tool
/// <see cref="CommandPolicy"/> allows for the language). <see cref="TestFiles"/> are the content-supplied (hidden) test files,
/// copied into the work directory below <see cref="TestsTarget"/> ("." = the root) at grading time; they are never shown in the editor.
/// A command either names the language tool in <see cref="Run"/> (<c>node --test ...</c>, <c>go test ...</c>) or, for tools installed in the
/// node content environment, a <see cref="Tool"/> (<c>vitest</c>) with <see cref="Args"/>; then <see cref="Run"/> is empty and the runner
/// builds the argv itself (see <see cref="CommandPolicy.NodeToolArgv"/>), so content never names a script path.
/// </summary>
public sealed record CommandSpec(
    IReadOnlyList<string> Run,
    CommandReportFormat Report,
    string? ReportFile,
    int? TimeoutSeconds,
    string TestsTarget,
    IReadOnlyList<ExerciseFile> TestFiles,
    string? Tool = null,
    IReadOnlyList<string>? Args = null)
{
    /// <summary>True when the command runs a tool from the node content environment.</summary>
    public bool UsesNodeTool => Tool is not null;
}

public enum OutputComparison { Exact, Trim, Tokens, Numeric }

public sealed record TestCase(
    string Id,
    string Title,
    string? Stdin,
    string? Expected,
    OutputComparison Compare,
    double Tolerance,
    bool Hidden,
    string? Hint,
    /// <summary>Command evaluation only: the reported test name (or a prefix ending in '*') this case maps to.</summary>
    string? Match = null);

/// <summary>A file with a relative '/'-separated path.</summary>
public sealed record ExerciseFile(string Path, string Content);

/// <summary>
/// One language implementation of an exercise: its own starter, reference solution and (for harness evaluation)
/// harness. Tests, prompt and hints are shared by all variants.
/// </summary>
public sealed record ExerciseVariant(
    string LanguageId,
    string EntryFile,
    IReadOnlyList<ExerciseFile> Starter,
    IReadOnlyList<ExerciseFile> Solution,
    IReadOnlyList<ExerciseFile> Harness,
    string? HarnessEntry);

/// <summary>
/// Used for both the lesson exercise and the optional challenge.
/// LanguageId/EntryFile/Starter/Solution/Harness/HarnessEntry describe the DEFAULT variant: the only one for a
/// classic single-language exercise, the first declared one when <see cref="Variants"/> is set.
/// Use <see cref="AllVariants"/> / <see cref="ForVariant"/> instead of reading them when several languages are possible.
/// </summary>
public sealed record Exercise(
    string Id,
    string Title,
    string Prompt,
    string LanguageId,
    string EntryFile,
    IReadOnlyList<ExerciseFile> Starter,
    IReadOnlyList<ExerciseFile> Solution,
    EvaluationKind Evaluation,
    IReadOnlyList<ExerciseFile> Harness,
    string? HarnessEntry,
    IReadOnlyList<TestCase> Tests,
    IReadOnlyList<string> Hints,
    CommandSpec? Command = null,
    IReadOnlyList<ExerciseVariant>? Variants = null,
    ExerciseRequirements? Requires = null,
    AcceleratorNeed Accelerator = AcceleratorNeed.None)
{
    /// <summary>True when the exercise declares a Python version or packages it needs.</summary>
    public bool HasRequirements => Requires is { HasPython: true };

    /// <summary>True when grading needs the node content environment (a <c>command.tool</c> or <c>requires.node/npmPackages</c>).</summary>
    public bool NeedsNodeEnvironment => (Command?.UsesNodeTool ?? false) || Requires is { HasNode: true };

    /// <summary>Explicit variants (non-empty) when the exercise was declared with a <c>variants</c> map.</summary>
    public bool HasExplicitVariants => Variants is { Count: > 0 };

    /// <summary>Every language variant; a classic exercise is one implicit variant.</summary>
    public IReadOnlyList<ExerciseVariant> AllVariants =>
        HasExplicitVariants ? Variants! : [new ExerciseVariant(LanguageId, EntryFile, Starter, Solution, Harness, HarnessEntry)];

    public bool IsMultiLanguage => AllVariants.Count > 1;

    public ExerciseVariant? FindVariant(string languageId) =>
        AllVariants.FirstOrDefault(v => string.Equals(v.LanguageId, languageId, StringComparison.Ordinal));

    /// <summary>
    /// The exercise with its top-level language fields set to the given variant (null = default variant).
    /// Unknown language: throws, so a caller never silently evaluates the wrong language.
    /// </summary>
    public Exercise ForVariant(string? languageId)
    {
        if (languageId is null || (!HasExplicitVariants && languageId == LanguageId)) return this;
        var v = FindVariant(languageId)
            ?? throw new ArgumentException($"Exercise '{Id}' has no variant for language '{languageId}'", nameof(languageId));
        return this with
        {
            LanguageId = v.LanguageId,
            EntryFile = v.EntryFile,
            Starter = v.Starter,
            Solution = v.Solution,
            Harness = v.Harness,
            HarnessEntry = v.HarnessEntry,
        };
    }

    /// <summary>
    /// Key under which editor drafts and revealed state of a variant are stored. Classic single-language
    /// exercises keep the bare exercise id (existing drafts stay valid); multi-language ones add the language.
    /// </summary>
    public string DraftKey(string? languageId = null) =>
        IsMultiLanguage ? $"{Id}@{languageId ?? LanguageId}" : Id;
}

/// <summary>Optional complexity annotation of an algorithm problem; shown after the exercise is passed.</summary>
public sealed record ComplexityInfo(string Time, string Space, string? Explanation);

/// <summary>A runnable ```lang run block with the expected stdout of the following ```output block, if any.</summary>
public sealed record CodeSample(string LanguageId, string Code, string? ExpectedOutput);

/// <param name="Sections">Section name (simple, precise, ...) to Markdown body.</param>
public sealed record Theory(
    string Markdown,
    IReadOnlyDictionary<string, string> Sections,
    IReadOnlyList<CodeSample> Samples);

/// <param name="SourcePath">Path of lesson.yaml relative to the content root (for diagnostics).</param>
public sealed record Lesson(
    string Id,
    string CourseId,
    string Title,
    string Goal,
    Difficulty Difficulty,
    int EstimatedMinutes,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> Objectives,
    IReadOnlyList<string> MasteryCriteria,
    IReadOnlyList<string> Related,
    IReadOnlyList<string> Next,
    IReadOnlyList<string> SeeAlso,
    IReadOnlyList<string> Tags,
    Theory Theory,
    IReadOnlyList<Fact> Facts,
    Quiz? Quiz,
    Exercise? Exercise,
    Exercise? Challenge,
    string SourcePath,
    IReadOnlyList<string> AssessmentFor,
    ComplexityInfo? Complexity = null,
    string? Editorial = null);

public enum DiagnosticSeverity { Warning, Error }

public sealed record ContentDiagnostic(DiagnosticSeverity Severity, string Path, string Message)
{
    public override string ToString() => $"{Severity.ToString().ToUpperInvariant()} {Path}: {Message}";
}

/// <summary>How a lesson became completed. Assessment = credited after a placement check, not practised.</summary>
public enum CompletionSource { Exercise, Quiz, TheoryRead, Assessment, Imported }

public enum LessonState { Locked, Available, InProgress, Completed }

/// <summary>Persisted learner progress for one lesson. Keyed by lesson id only.</summary>
public sealed record LessonProgress(
    string LessonId,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    double? QuizBestScore = null,
    bool ExercisePassed = false,
    bool ChallengePassed = false,
    bool TheoryRead = false,
    int Attempts = 0,
    int FailedAttempts = 0,
    int ReviewStep = 0,
    DateTimeOffset? NextReviewAt = null,
    DateTimeOffset? LastReviewedAt = null,
    CompletionSource? CompletionSource = null)
{
    public bool IsCompleted => CompletedAt is not null;
}

public sealed record QuizAttempt(string LessonId, double Score, bool Passed, DateTimeOffset At);

public enum Verdict { Passed, Failed, CompileError, RuntimeMissing, Error }

public sealed record Submission(
    string Id,
    string LessonId,
    string ExerciseId,
    string LanguageId,
    IReadOnlyList<ExerciseFile> Files,
    Verdict Verdict,
    int Passed,
    int Total,
    TimeSpan Duration,
    DateTimeOffset At);

public enum LearningDepth { Quick, Standard, Deep }

public sealed record LearnerProfile(
    string Goal,
    int Level,
    LearningDepth Depth,
    IReadOnlyList<string> EnrolledCourseIds,
    string UiLanguage = "en");

/// <summary>
/// A project template loaded from <c>content/templates/&lt;id&gt;</c>. <see cref="FilesDirectory"/> is an absolute
/// directory copied verbatim (with <c>{{name}}</c> substituted in text files). <see cref="PostCreate"/> commands are
/// only ever displayed to the user; the application never runs them. <see cref="Scripts"/> maps the workspace button
/// ("dev", "test", "build") to a <c>package.json</c> script name of the template's files (Node templates only).
/// </summary>
public sealed record ProjectTemplate(
    string Id,
    string Title,
    string LanguageId,
    string Description,
    string FilesDirectory,
    IReadOnlyList<string> PostCreate,
    IReadOnlyDictionary<string, string>? Scripts = null)
{
    public IReadOnlyDictionary<string, string> ScriptMap { get; } = Scripts ?? new Dictionary<string, string>();
}
