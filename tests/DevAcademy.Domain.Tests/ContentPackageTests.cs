using DevAcademy.Domain;

namespace DevAcademy.Domain.Tests;

public class ContentPackageIndexTests
{
    private static ContentPackageSet Node(string id, params string[] packages) => new(id, id, PackageSetLanguage.Node, "", packages);
    private static ContentPackageSet Py(string id, params string[] packages) => new(id, id, PackageSetLanguage.Python, "", packages);

    private static ContentPackageIndex Index(params ContentPackageSet[] sets) => new(sets);

    [Fact]
    public void AnExerciseIsCoveredByOneSetThatHasAllItsPackages()
    {
        var index = Index(Node("react", "react", "react-dom", "vitest"), Node("express", "express", "vitest"));
        Assert.Equal("react", index.Resolve(PackageSetLanguage.Node, ["vitest", "react"]).Set!.Id);
        Assert.Equal("express", index.Resolve(PackageSetLanguage.Node, ["express"]).Set!.Id);
    }

    [Fact]
    public void TheSmallestCoveringSetWinsAndTiesGoToTheEarlierEntry()
    {
        var index = Index(Node("big", "a", "b", "c"), Node("small", "a", "b"), Node("twin", "a", "b"));
        Assert.Equal("small", index.Resolve(PackageSetLanguage.Node, ["a"]).Set!.Id);
        Assert.Equal("big", index.Resolve(PackageSetLanguage.Node, ["c"]).Set!.Id);
    }

    [Fact]
    public void PackagesSplitAcrossSetsHaveNoCoveringSet()
    {
        var index = Index(Node("react", "react", "vitest"), Node("express", "express", "vitest"));
        var resolution = index.Resolve(PackageSetLanguage.Node, ["react", "express"]);
        Assert.Null(resolution.Set);
        Assert.Contains("react, express", resolution.Error);
        Assert.Null(index.Resolve(PackageSetLanguage.Node, ["left-pad"]).Set);
    }

    [Fact]
    public void ALanguageOnlyLooksAtItsOwnSets()
    {
        var index = Index(Node("react", "numpy"), Py("numpy", "numpy"));
        Assert.Equal(PackageSetLanguage.Python, index.Resolve(PackageSetLanguage.Python, ["numpy"]).Set!.Language);
        Assert.Null(index.Resolve(PackageSetLanguage.Python, ["react"]).Set);
    }

    [Fact]
    public void PythonNamesAreComparedNormalised()
    {
        var index = Index(Py("django", "Django"), Py("typing", "typing_extensions"));
        Assert.Equal("django", index.Resolve(PackageSetLanguage.Python, ["django"]).Set!.Id);
        Assert.Equal("typing", index.Resolve(PackageSetLanguage.Python, ["typing-extensions"]).Set!.Id);
    }

    [Fact]
    public void RequiredPackagesComeFromRequiresAndTheCommandTool()
    {
        var vitest = new CommandSpec([], CommandReportFormat.Junit, "report.xml", 60, ".", [new ExerciseFile("t.test.ts", "")], "vitest", []);
        var node = new Exercise("e", "E", "", "typescript", "a.ts", [], [], EvaluationKind.Command, [], null, [], [], vitest,
            Requires: new ExerciseRequirements(null, [], ">=22", ["typescript", "vitest"]));
        Assert.Equal(["typescript", "vitest"], ContentPackageIndex.RequiredNodePackages(node));

        var toolOnly = node with { Requires = new ExerciseRequirements(null, [], ">=22", ["typescript"]) };
        Assert.Equal(["typescript", "vitest"], ContentPackageIndex.RequiredNodePackages(toolOnly));

        var python = Make.Exercise() with { Requires = new ExerciseRequirements(">=3.12", ["Django>=6.1", "pandas>=3.0"]) };
        Assert.Equal(["django", "pandas"], ContentPackageIndex.RequiredPythonPackages(python));
        Assert.Empty(ContentPackageIndex.RequiredNodePackages(python));
        Assert.Empty(ContentPackageIndex.RequiredPythonPackages(Make.Exercise()));
    }

    [Fact]
    public void IndexJsonIsParsedAndMalformedOnesAreRejected()
    {
        const string good = """[ { "id": "react", "title": "React", "language": "node", "description": "d", "packages": ["react"] } ]""";
        Assert.True(ContentPackageIndex.TryParse(good, null, out var index, out _));
        Assert.Equal("react", Assert.Single(index.Sets).Id);

        Assert.False(ContentPackageIndex.TryParse("""[ { "id": "../x", "title": "t", "language": "node", "packages": ["a"] } ]""", null, out _, out var idError));
        Assert.Contains("../x", idError);
        Assert.False(ContentPackageIndex.TryParse("""[ { "id": "a", "title": "t", "language": "ruby", "packages": ["a"] } ]""", null, out _, out _));
        Assert.False(ContentPackageIndex.TryParse("""[ { "id": "a", "title": "t", "language": "node", "packages": [] } ]""", null, out _, out _));
        Assert.False(ContentPackageIndex.TryParse("""[ { "id": "a", "title": "t", "language": "node", "packages": ["x"] }, { "id": "a", "title": "t", "language": "node", "packages": ["y"] } ]""", null, out _, out var dup));
        Assert.Contains("duplicate", dup);
        Assert.False(ContentPackageIndex.TryParse("{}", null, out _, out _));
        Assert.False(ContentPackageIndex.TryParse("not json", null, out _, out _));
    }

    [Fact]
    public void PinsAreReadFromRequirementsFilesAndNpmManifests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-pins-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "python"));
            Directory.CreateDirectory(Path.Combine(dir, "node", "react"));
            File.WriteAllText(Path.Combine(dir, "python", "pandas.txt"), "# c\nnumpy==2.5.4\n-r other.txt\npandas == 3.0.6  # why\n");
            File.WriteAllText(Path.Combine(dir, "node", "react", "package.json"), """{ "dependencies": { "react": "19.3.1" }, "devDependencies": { "vitest": "5.0.5" } }""");
            var index = new ContentPackageIndex([Py("pandas", "pandas"), Node("react", "react")], dir);
            Assert.Equal([new PinnedPackage("numpy", "2.5.4"), new PinnedPackage("pandas", "3.0.6")], index.ReadPins(index.Sets[0]));
            Assert.Equal([new PinnedPackage("react", "19.3.1"), new PinnedPackage("vitest", "5.0.5")], index.ReadPins(index.Sets[1]));
            Assert.Empty(new ContentPackageIndex([Py("x", "x")], dir).ReadPins(Py("x", "x")));
        }
        finally { Directory.Delete(dir, true); }
    }
}
