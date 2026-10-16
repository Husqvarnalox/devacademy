using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

public class RequirementsContentTests
{
    private const string Anchor = "  entryFile: main.py\n  evaluation: io\n";

    private static IEnumerable<string> Errors(ContentLoadResult r) =>
        r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString());

    private static Exercise Vars(ContentLoadResult r) => r.Content.FindLesson("py.vars")!.Exercise!;

    [Fact]
    public void Requires_and_accelerator_load_on_an_exercise()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  requires: { python: \">=3.11\", packages: [\"numpy>=2\", pandas] }\n  accelerator: optional\n");
        var r = f.Load();
        Assert.Empty(Errors(r));
        Exercise ex = Vars(r);
        Assert.True(ex.HasRequirements);
        Assert.Equal(">=3.11", ex.Requires!.Python);
        Assert.Equal(["numpy>=2", "pandas"], ex.Requires.Packages);
        Assert.Equal(AcceleratorNeed.Optional, ex.Accelerator);
    }

    [Fact]
    public void Lesson_level_fields_are_inherited_and_exercise_level_wins()
    {
        using var f = new Fixture();
        f.EditVars("estimatedMinutes: 20\n", "estimatedMinutes: 20\nrequires: { packages: [numpy] }\naccelerator: required\n");
        Exercise ex = Vars(f.Load());
        Assert.Equal(["numpy"], ex.Requires!.Packages);
        Assert.Equal(AcceleratorNeed.Required, ex.Accelerator);

        f.EditVars(Anchor, Anchor + "  accelerator: none\n");
        Assert.Equal(AcceleratorNeed.None, Vars(f.Load()).Accelerator);
    }

    [Fact]
    public void Default_is_no_requirements_and_no_accelerator()
    {
        using var f = new Fixture();
        Exercise ex = Vars(f.Load());
        Assert.False(ex.HasRequirements);
        Assert.Equal(AcceleratorNeed.None, ex.Accelerator);
    }

    [Theory]
    [InlineData("requires: { python: \"banana\" }")]
    [InlineData("requires: { packages: [\"--index-url=http://evil\"] }")]
    [InlineData("requires: { packages: [\"numpy\", \"NumPy>=2\"] }")]
    [InlineData("requires: { packages: [\"https://evil/x.whl\"] }")]
    [InlineData("accelerator: gpu")]
    [InlineData("requires: { pythn: \">=3.11\" }")]
    public void Bad_values_are_errors(string line)
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  " + line + "\n");
        Assert.NotEmpty(Errors(f.Load()));
    }

    [Fact]
    public void Required_accelerator_without_packages_is_only_a_warning()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  accelerator: required\n");
        var r = f.Load();
        Assert.Empty(Errors(r));
        Assert.Contains(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("accelerator 'required'"));
    }

    [Fact]
    public void The_json_schema_documents_both_fields()
    {
        string dir = AppContext.BaseDirectory;
        string? schema = null;
        for (var d = new DirectoryInfo(dir); d is not null && schema is null; d = d.Parent)
        {
            string p = Path.Combine(d.FullName, "content", "schema", "lesson.schema.json");
            if (File.Exists(p)) schema = File.ReadAllText(p);
        }
        if (schema is null) Assert.Skip("content/schema not reachable from the test output directory");
        Assert.Contains("\"requires\"", schema);
        Assert.Contains("\"accelerator\"", schema);
    }
}

public class GradientDescentDirectiveTests
{
    private static ContentLoadResult With(Fixture f, string info)
    {
        string theory = Path.Combine(f.Root, "courses", "py-basics", "lessons", "02-vars", "theory.md");
        File.WriteAllText(theory, File.ReadAllText(theory) + $"\n```{info}\n```\n");
        return f.Load();
    }

    [Theory]
    [InlineData("visualize gradient-descent")]
    [InlineData("visualize gradient-descent function=rosenbrock optimizer=adam lr=0.02")]
    [InlineData("visualize gradient-descent function=double-well lr=1e-2")]
    public void Valid_directives_pass(string info)
    {
        using var f = new Fixture();
        var r = With(f, info);
        Assert.DoesNotContain(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var found = TheoryParser.Visualizations(r.Content.FindLesson("py.vars")!.Theory.Markdown);
        Assert.Equal(info, Assert.Single(found).Info);
    }

    [Theory]
    [InlineData("visualize gradient-descent function=sphere", "unknown function 'sphere'")]
    [InlineData("visualize gradient-descent optimizer=sgd", "unknown optimizer 'sgd'")]
    [InlineData("visualize gradient-descent lr=-1", "lr must be a positive number")]
    [InlineData("visualize gradient-descent lr=abc", "lr must be a positive number")]
    [InlineData("visualize gradient-descent speed=3", "unknown argument")]
    public void Bad_directives_are_validator_errors(string info, string expected)
    {
        using var f = new Fixture();
        Assert.Contains(With(f, info).Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(expected));
    }
}
