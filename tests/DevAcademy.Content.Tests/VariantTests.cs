using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

/// <summary>Copy of Fixtures/algorithms (multi-language problem) that tests may edit.</summary>
public sealed class AlgoFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "da-algo-" + Guid.NewGuid().ToString("N"));

    public AlgoFixture() => Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "algorithms"), Root);

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }

    public string Lesson(string name) => Path.Combine(Root, "courses", "algo-basics", "lessons", name);
    public string FindMaxYaml => Path.Combine(Lesson("01-find-max"), "lesson.yaml");

    public void EditYaml(string oldText, string newText)
    {
        var text = File.ReadAllText(FindMaxYaml);
        Assert.Contains(oldText, text);
        File.WriteAllText(FindMaxYaml, text.Replace(oldText, newText));
    }

    public ContentLoadResult Load() => ContentLoader.Load(Root);

    public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
}

public class VariantTests
{
    private static IEnumerable<string> Errors(ContentLoadResult r) =>
        r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString());

    [Fact]
    public void Variants_fixture_loads_cleanly_with_two_languages()
    {
        using var f = new AlgoFixture();
        var r = f.Load();
        Assert.Empty(r.Diagnostics);
        var ex = r.Content.FindLesson("algo.find-max")!.Exercise!;
        Assert.True(ex.HasExplicitVariants);
        Assert.True(ex.IsMultiLanguage);
        Assert.Equal(["python", "javascript"], ex.AllVariants.Select(v => v.LanguageId));
        // top-level fields mirror the first declared variant
        Assert.Equal("python", ex.LanguageId);
        Assert.Equal("main.py", ex.EntryFile);
        var js = ex.FindVariant("javascript")!;
        Assert.Equal("main.js", js.EntryFile);
        Assert.Contains("Math.max", js.Solution.Single().Content);
        Assert.Equal("main.js", ex.ForVariant("javascript").EntryFile);
        Assert.Equal(2, ex.Tests.Count);
        Assert.Throws<ArgumentException>(() => ex.ForVariant("cobol"));
        Assert.NotEqual(ex.DraftKey("python"), ex.DraftKey("javascript"));
    }

    [Fact]
    public void Complexity_and_editorial_are_parsed()
    {
        using var f = new AlgoFixture();
        var lesson = f.Load().Content.FindLesson("algo.find-max")!;
        Assert.Equal(new ComplexityInfo("O(n)", "O(1)", "One pass, one variable."), lesson.Complexity);
        Assert.Contains("Initialise the maximum", lesson.Editorial);
        Assert.Null(f.Load().Content.FindLesson("algo.sorting")!.Complexity);
    }

    [Fact]
    public void A_classic_exercise_is_one_implicit_variant()
    {
        using var f = new Fixture();
        var ex = f.Load().Content.FindLesson("py.vars")!.Exercise!;
        Assert.False(ex.HasExplicitVariants);
        Assert.False(ex.IsMultiLanguage);
        var only = Assert.Single(ex.AllVariants);
        Assert.Equal("python", only.LanguageId);
        Assert.Same(ex, ex.ForVariant(null));
        Assert.Equal(ex.Id, ex.DraftKey());
    }

    [Fact]
    public void Top_level_language_fields_next_to_variants_are_rejected()
    {
        using var f = new AlgoFixture();
        f.EditYaml("  evaluation: io\n", "  evaluation: io\n  entryFile: main.py\n");
        Assert.Contains(Errors(f.Load()), e => e.Contains("'entryFile' cannot be combined with 'variants'"));
    }

    [Fact]
    public void Missing_variant_directory_or_entry_file_is_reported_per_language()
    {
        using var f = new AlgoFixture();
        Directory.Delete(Path.Combine(f.Lesson("01-find-max"), "exercise", "javascript", "starter"), true);
        File.Move(Path.Combine(f.Lesson("01-find-max"), "exercise", "python", "solution", "main.py"),
            Path.Combine(f.Lesson("01-find-max"), "exercise", "python", "solution", "other.py"));
        var errors = Errors(f.Load()).ToList();
        Assert.Contains(errors, e => e.Contains("variant 'javascript': starter directory is missing"));
        Assert.Contains(errors, e => e.Contains("variant 'python': solution has no entry file 'main.py'"));
    }

    [Fact]
    public void Variant_without_entryFile_and_empty_variants_are_errors()
    {
        using var f = new AlgoFixture();
        f.EditYaml("      entryFile: main.js\n", "");
        Assert.Contains(Errors(f.Load()), e => e.Contains("variants[javascript].entryFile"));

        using var g = new AlgoFixture();
        var text = File.ReadAllText(g.FindMaxYaml);
        File.WriteAllText(g.FindMaxYaml, text[..text.IndexOf("  variants:", StringComparison.Ordinal)] + "  variants: {}\n");
        Assert.Contains(Errors(g.Load()), e => e.Contains("'variants' needs at least one language"));
    }

    [Fact]
    public void Harness_variants_need_their_own_harness_entry()
    {
        using var f = new AlgoFixture();
        f.EditYaml("evaluation: io", "evaluation: harness");
        var errors = Errors(f.Load()).ToList();
        Assert.Contains(errors, e => e.Contains("variant 'python': harness evaluation needs 'harnessEntry'"));
        Assert.Contains(errors, e => e.Contains("variant 'javascript': harness evaluation needs 'harnessEntry'"));
    }

    [Fact]
    public void Editorial_problems_are_reported()
    {
        using var missing = new AlgoFixture();
        File.Delete(Path.Combine(missing.Lesson("01-find-max"), "editorial.md"));
        Assert.Contains(Errors(missing.Load()), e => e.Contains("editorial file 'editorial.md' not found"));

        using var escape = new AlgoFixture();
        escape.EditYaml("editorial: editorial.md", "editorial: ../../course.yaml");
        Assert.Contains(Errors(escape.Load()), e => e.Contains("must stay inside the lesson directory"));

        using var noExercise = new AlgoFixture();
        File.WriteAllText(Path.Combine(noExercise.Lesson("02-sorting"), "lesson.yaml"),
            File.ReadAllText(Path.Combine(noExercise.Lesson("02-sorting"), "lesson.yaml")) + "editorial: ed.md\n");
        File.WriteAllText(Path.Combine(noExercise.Lesson("02-sorting"), "ed.md"), "x");
        Assert.Contains(Errors(noExercise.Load()), e => e.Contains("editorial requires the lesson to have an exercise"));
    }

    [Fact]
    public void Complexity_needs_time_and_space()
    {
        using var f = new AlgoFixture();
        f.EditYaml("  space: O(1)\n", "");
        Assert.Contains(Errors(f.Load()), e => e.Contains("complexity.space"));
    }

    [Fact]
    public void Unknown_keys_inside_a_variant_are_rejected()
    {
        using var f = new AlgoFixture();
        f.EditYaml("      entryFile: main.js\n", "      entryFile: main.js\n      langauge: js\n");
        Assert.Contains(Errors(f.Load()), e => e.Contains("invalid YAML"));
    }
}

public class VisualizeDirectiveTests
{
    [Fact]
    public void Valid_directive_in_fixture_has_no_problems()
    {
        using var f = new AlgoFixture();
        var theory = f.Load().Content.FindLesson("algo.sorting")!.Theory.Markdown;
        var found = Assert.Single(TheoryParser.Visualizations(theory));
        Assert.Null(found.Error);
        Assert.Equal("visualize sorting algorithm=merge", found.Info);
    }

    [Theory]
    [InlineData("visualize graphs", "unknown visualization")]
    [InlineData("visualize sorting algorithm=heap", "unknown sorting algorithm 'heap'")]
    [InlineData("visualize sorting speed=3", "unknown argument")]
    public void Bad_directives_are_validator_errors(string info, string expected)
    {
        using var f = new AlgoFixture();
        var theory = Path.Combine(f.Lesson("02-sorting"), "theory.md");
        File.WriteAllText(theory, File.ReadAllText(theory).Replace("visualize sorting algorithm=merge", info));
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(expected));
    }
}
