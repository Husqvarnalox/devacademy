using YamlDotNet.Serialization;

namespace DevAcademy.Content;

// YAML shapes. Unknown keys are rejected by the deserializer so typos surface as diagnostics.
// Numbers that may be written as 1e-6 and polymorphic fields are read as strings/objects and converted explicitly.

internal sealed class CatalogDto
{
    public List<TrackDto>? Tracks { get; set; }
}

internal sealed class TrackDto
{
    public string? Id { get; set; }
    public string? Kind { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? LanguageId { get; set; }
    public string? Status { get; set; }
    public List<string>? Courses { get; set; }
}

internal sealed class CourseDto
{
    public string? Id { get; set; }
    public string? Version { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? LanguageId { get; set; }
    public int? Level { get; set; }
    public string? Track { get; set; }
    public List<string>? Lessons { get; set; }
    public List<string>? Requires { get; set; }
    public FrameworkDto? Framework { get; set; }
}

internal sealed class FrameworkDto
{
    public string? Id { get; set; }
    public string? Version { get; set; }
    public List<string>? Docs { get; set; }
    public string? Compatibility { get; set; }
    public List<DeprecatedDto>? Deprecated { get; set; }
    public string? MigrationNotes { get; set; }
}

internal sealed class DeprecatedDto
{
    public string? Feature { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
}

internal sealed class TemplateDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? LanguageId { get; set; }
    public string? Description { get; set; }
    public string? Files { get; set; }
    public List<string>? PostCreate { get; set; }
    public Dictionary<string, string>? Scripts { get; set; }
}

internal sealed class LessonDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Goal { get; set; }
    public string? Difficulty { get; set; }
    public int? EstimatedMinutes { get; set; }
    public List<string>? Prerequisites { get; set; }
    public List<string>? Objectives { get; set; }
    public List<string>? MasteryCriteria { get; set; }
    public List<string>? Related { get; set; }
    public List<string>? Next { get; set; }
    public List<string>? SeeAlso { get; set; }
    public List<string>? AssessmentFor { get; set; }
    public List<string>? FormerIds { get; set; }
    public ConceptsDto? Concepts { get; set; }
    public List<string>? Tags { get; set; }
    public List<FactDto>? Facts { get; set; }
    public QuizDto? Quiz { get; set; }
    public ExerciseDto? Exercise { get; set; }
    public ExerciseDto? Challenge { get; set; }
    public ComplexityDto? Complexity { get; set; }
    public string? Editorial { get; set; }
    /// <summary>Default for the lesson's exercise and challenge when they declare none of their own.</summary>
    public RequiresDto? Requires { get; set; }
    public string? Accelerator { get; set; }
}

internal sealed class ConceptsDto
{
    public List<string>? Introduces { get; set; }
    public List<string>? Uses { get; set; }
}

internal sealed class RouteDto
{
    public string? Track { get; set; }
    public RouteEntryDto? Entry { get; set; }
    public List<CompetencyDto>? Competencies { get; set; }
}

internal sealed class RouteEntryDto
{
    public string? Beginner { get; set; }
    public string? Programmer { get; set; }
}

internal sealed class CompetencyDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Check { get; set; }
    public List<string>? After { get; set; }
}

internal sealed class RequiresDto
{
    public string? Python { get; set; }
    public List<string>? Packages { get; set; }
    /// <summary>javascript exercises: Node.js version specifier (">=22") and npm package names of the node content environment.</summary>
    public string? Node { get; set; }
    public List<string>? NpmPackages { get; set; }
    /// <summary>go exercises: Go version specifier (">=1.26.5") and module paths that a go package set must provide.</summary>
    public string? Go { get; set; }
    public List<string>? GoModules { get; set; }
    /// <summary>csharp exercises: NuGet package ids that a nuget package set must provide.</summary>
    public List<string>? NugetPackages { get; set; }
    /// <summary>java exercises: Maven coordinates (<c>group:artifact</c>) that a maven package set must provide.</summary>
    public List<string>? MavenArtifacts { get; set; }
    /// <summary>Per-exercise memory limit of a run in MB (256-1024; default 256) for libraries whose import alone is close to the default, e.g. PyTorch or a Spring Boot context (Java).</summary>
    public int? MemoryMb { get; set; }
}

internal sealed class ComplexityDto
{
    public string? Time { get; set; }
    public string? Space { get; set; }
    public string? Explanation { get; set; }
}

internal sealed class VariantDto
{
    public string? EntryFile { get; set; }
    public string? Starter { get; set; }
    public string? Solution { get; set; }
    public string? Harness { get; set; }
    public string? HarnessEntry { get; set; }
}

internal sealed class FactDto
{
    public string? Kind { get; set; }
    public string? Text { get; set; }
    public List<string>? Sources { get; set; }
}

internal sealed class QuizDto
{
    public string? PassThreshold { get; set; }
    public List<QuestionDto>? Questions { get; set; }
}

internal sealed class QuestionDto
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Prompt { get; set; }
    public List<string>? Options { get; set; }
    public object? Answer { get; set; }
    public string? Tolerance { get; set; }
    public string? Explanation { get; set; }
}

internal sealed class ExerciseDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Prompt { get; set; }
    public string? LanguageId { get; set; }
    public string? EntryFile { get; set; }
    public string? Starter { get; set; }
    public string? Solution { get; set; }
    public string? Evaluation { get; set; }
    public string? Harness { get; set; }
    public string? HarnessEntry { get; set; }
    public List<TestDto>? Tests { get; set; }
    public List<string>? Hints { get; set; }
    public CommandDto? Command { get; set; }
    /// <summary>Command evaluation: directory (relative to the lesson) with the hidden test files copied in at grading time.</summary>
    [YamlMember(Alias = "tests_dir", ApplyNamingConventions = false)]
    public string? TestsDir { get; set; }
    public Dictionary<string, VariantDto>? Variants { get; set; }
    public RequiresDto? Requires { get; set; }
    public string? Accelerator { get; set; }
}

internal sealed class CommandDto
{
    public List<string>? Run { get; set; }
    /// <summary>Instead of <c>run</c>: a tool of the node content environment (vitest) with <c>args</c>.</summary>
    public string? Tool { get; set; }
    public List<string>? Args { get; set; }
    public string? Report { get; set; }
    public string? ReportFile { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? TestsTarget { get; set; }
}

internal sealed class TestDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Stdin { get; set; }
    public string? Expected { get; set; }
    /// <summary>Alternative to inline <c>stdin</c>: path (relative to the lesson directory) of a UTF-8 file.</summary>
    public string? StdinFile { get; set; }
    /// <summary>Alternative to inline <c>expected</c>.</summary>
    public string? ExpectedFile { get; set; }
    /// <summary>Command evaluation: the reported test name this case maps to (a trailing '*' makes it a prefix).</summary>
    public string? Match { get; set; }
    public string? Compare { get; set; }
    public string? Tolerance { get; set; }
    public bool? Hidden { get; set; }
    public string? Hint { get; set; }
}
