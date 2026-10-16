using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

/// <summary>A copy of the "paths" fixture (stages, alternatives, placement quiz, comparison) to mutate per test.</summary>
public sealed class PathsFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "da-paths-" + Guid.NewGuid().ToString("N"));

    public PathsFixture() => Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "paths"), Root);

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }

    public void Edit(string relativePath, string oldText, string newText)
    {
        var path = Path.Combine(Root, relativePath);
        var text = File.ReadAllText(path);
        Assert.Contains(oldText, text);
        File.WriteAllText(path, text.Replace(oldText, newText));
    }

    public ContentLoadResult Load() => ContentLoader.Load(Root);

    public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
}

public class PathContentTests
{
    private const string Web = "paths/python-web.yaml";

    [Fact]
    public void FixtureLoadsCleanWithStagesAlternativesAndComparison()
    {
        using var f = new PathsFixture();
        var r = f.Load();
        Assert.DoesNotContain(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var path = r.Content.FindPath("python-web")!;
        Assert.Equal(["Foundation", "Web"], path.StageList.Select(s => s.Title));
        Assert.Equal(["web-django", "web-fastapi"], path.StageList[1].CourseIds);
        Assert.Single(path.AlternativeGroups);
        Assert.Equal(["web-django", "web-fastapi"], path.AlternativeGroups[0]);
        Assert.Empty(r.Content.FindPath("python-deep")!.StageList);
        Assert.NotNull(r.Content.FindComparison("web-compare"));
    }

    [Theory]
    [InlineData("courses: [web-django, web-fastapi]", "courses: [web-django, nope]", "unknown course 'nope'")]
    [InlineData("  - [web-django, web-fastapi]", "  - [web-django, nope]", "alternatives[0] references unknown course 'nope'")]
    [InlineData("  - [web-django, web-fastapi]", "  - [web-django]", "needs at least two courses")]
    [InlineData("  - [web-django, web-fastapi]", "  - [web-django, web-django]", "lists 'web-django' twice")]
    [InlineData("  - title: Web\n    courses: [web-django, web-fastapi]", "  - title: Web\n    courses: [web-django, web-fastapi, py-basics]", "more than one stage")]
    [InlineData("  - [web-django, web-fastapi]", "  - [web-django, web-fastapi]\n  - [web-fastapi, py-adv]", "more than one alternatives group")]
    public void InvalidPathIsAnError(string from, string to, string message)
    {
        using var f = new PathsFixture();
        f.Edit(Web, from, to);
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(message));
    }

    [Fact]
    public void AlternativeThatIsATargetIsAnError()
    {
        using var f = new PathsFixture();
        f.Edit(Web, "targets: [py-basics]", "targets: [py-basics, web-django]");
        Assert.Contains(f.Load().Diagnostics, d => d.Message.Contains("is also a target"));
    }

    [Fact]
    public void AlternativeRequiredByAnotherCourseOfThePathIsAnError()
    {
        using var f = new PathsFixture();
        f.Edit("courses/web-fastapi/course.yaml", "requires: [py-basics]", "requires: [py-basics, web-django]");
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("of the same group"));
    }

    [Fact]
    public void CourseDependencyCycleIsAnError()
    {
        using var f = new PathsFixture();
        f.Edit("courses/py-basics/course.yaml", "lessons:", "requires: [web-django]\nlessons:");
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("course dependency cycle"));
    }

    [Fact]
    public void PlacementLessonCreditingItselfOrNothingIsStillValidated()
    {
        using var f = new PathsFixture();
        f.Edit("courses/py-adv/lessons/00-placement/lesson.yaml", "assessmentFor: [pb.one, pb.two, pb.three]", "assessmentFor: [pb.one, pa.placement]");
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}

public class ComparisonContentTests
{
    private const string File = "comparisons/web-compare.yaml";

    [Fact]
    public void ComparisonLoadsWithAllFields()
    {
        using var f = new PathsFixture();
        var c = f.Load().Content.FindComparison("web-compare")!;
        Assert.Equal(3, c.Subjects.Count);
        Assert.Equal(SubjectKind.Framework, c.Subjects[0].Kind);
        Assert.Equal("5.2", c.Subjects[0].Version);
        Assert.Equal(2, c.Criteria.Count);
        Assert.Equal("Flask is small.", c.Cell("scope", "flask"));
        Assert.Contains("choose by the task", c.VerdictGuidance);
    }

    [Theory]
    [InlineData("    flask: \"Flask is small.\"\n", "", "cell for criterion 'scope' and subject 'flask' is missing")]
    [InlineData("sources: [https://docs.djangoproject.com/en/5.2/]", "sources: [http://example.com/]", "sources entry is not an https URL")]
    [InlineData("docs: [https://fastapi.tiangolo.com/]", "docs: [ftp://x/]", "docs entry is not an https URL")]
    [InlineData("id: fastapi,", "id: django,", "duplicate subject id 'django'")]
    [InlineData("id: async,", "id: scope,", "duplicate criterion id 'scope'")]
    [InlineData("id: web-compare", "id: other-name", "must match the file name")]
    [InlineData("kind: framework, version: \"0.115\"", "kind: gadget, version: \"0.115\"", "kind must be one of")]
    public void InvalidComparisonIsAnError(string from, string to, string message)
    {
        using var f = new PathsFixture();
        f.Edit(File, from, to);
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(message));
    }

    [Fact]
    public void CellForUnknownSubjectIsAnError()
    {
        using var f = new PathsFixture();
        f.Edit(File, "    flask: \"Flask is small.\"\n", "    flask: \"Flask is small.\"\n    ghost: \"x\"\n");
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("unknown subject 'ghost'"));
    }

    [Fact]
    public void MissingVersionAndVerdictThatNamesNobodyAreWarnings()
    {
        using var f = new PathsFixture();
        f.Edit(File, ", version: \"3.1\"", "");
        f.Edit(File, "Django, FastAPI or Flask: choose by the task.", "Choose by the task.");
        var d = f.Load().Diagnostics;
        Assert.DoesNotContain(d, x => x.Severity == DiagnosticSeverity.Error);
        Assert.Contains(d, x => x.Severity == DiagnosticSeverity.Warning && x.Message.Contains("does not state the version"));
        Assert.Contains(d, x => x.Severity == DiagnosticSeverity.Warning && x.Message.Contains("never mentions"));
    }

    [Fact]
    public void LiveComparisonsAndPathsHaveNoErrors()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content"));
        if (!Directory.Exists(Path.Combine(root, "comparisons"))) return;
        var r = ContentLoader.Load(root);
        Assert.Contains(r.Content.Comparisons, c => c.Id == "python-web-frameworks");
        Assert.Contains(r.Content.Comparisons, c => c.Id == "node-backend-frameworks");
        Assert.DoesNotContain(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && (d.Path.StartsWith("comparisons") || d.Path.StartsWith("paths")));
    }
}
