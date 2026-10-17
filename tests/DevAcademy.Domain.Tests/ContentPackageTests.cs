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

    private static ContentPackageSet Go(string id, params string[] modules) => new(id, id, PackageSetLanguage.Go, "", modules);

    [Fact]
    public void GoSetsResolveByModulePathAndStayApartFromOtherLanguages()
    {
        var index = Index(Go("gomlx", "github.com/gomlx/gomlx", "github.com/gomlx/compute"), Node("react", "github.com/gomlx/gomlx"));
        Assert.Equal("gomlx", index.Resolve(PackageSetLanguage.Go, ["github.com/gomlx/compute"]).Set!.Id);
        Assert.Null(index.Resolve(PackageSetLanguage.Go, ["github.com/other/mod"]).Set);
        Assert.True(ContentPackageIndex.TryParse(
            """[ { "id": "gomlx", "title": "t", "language": "go", "packages": ["github.com/gomlx/gomlx"] } ]""", null, out var parsed, out _));
        Assert.Equal(PackageSetLanguage.Go, parsed.Sets[0].Language);
    }

    [Fact]
    public void RequiredGoModulesComeFromRequires()
    {
        var exercise = Make.Exercise() with { Requires = new ExerciseRequirements(null, [], Go: ">=1.26.5", GoModules: ["a.io/x", "a.io/x", "a.io/y"]) };
        Assert.Equal(["a.io/x", "a.io/y"], ContentPackageIndex.RequiredGoModules(exercise));
        Assert.True(exercise.NeedsGoEnvironment);
        var versionOnly = Make.Exercise() with { Requires = new ExerciseRequirements(null, [], Go: ">=1.26.5") };
        Assert.True(versionOnly.HasGoRequirements);
        Assert.False(versionOnly.NeedsGoEnvironment);
        Assert.Empty(ContentPackageIndex.RequiredGoModules(Make.Exercise()));
    }

    [Fact]
    public void GoPinsAreReadFromGoModWithoutIndirectRequirements()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-gopins-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "go", "gomlx"));
            File.WriteAllText(Path.Combine(dir, "go", "gomlx", "go.mod"),
                "module m\n\ngo 1.26.5\n\nrequire (\n\tgithub.com/gomlx/compute v0.1.6\n\tgithub.com/gomlx/gomlx v0.28.8\n)\n\nrequire (\n\tgithub.com/pkg/errors v0.9.1 // indirect\n)\n");
            var set = Go("gomlx", "github.com/gomlx/gomlx", "github.com/gomlx/compute");
            var index = new ContentPackageIndex([set], dir);
            Assert.Equal([new PinnedPackage("github.com/gomlx/compute", "v0.1.6"), new PinnedPackage("github.com/gomlx/gomlx", "v0.28.8")], index.ReadPins(set));
        }
        finally { Directory.Delete(dir, true); }
    }

    private static ContentPackageSet NuGet(string id, params string[] packages) => new(id, id, PackageSetLanguage.NuGet, "", packages);

    [Fact]
    public void NuGetSetsResolveByCaseInsensitivePackageIdAndStayApartFromOtherLanguages()
    {
        var index = Index(NuGet("aspnetcore-testing", "Microsoft.AspNetCore.TestHost"), Go("gomlx", "github.com/gomlx/gomlx"));
        Assert.Equal("aspnetcore-testing", index.Resolve(PackageSetLanguage.NuGet, ["microsoft.aspnetcore.testhost"]).Set!.Id);
        Assert.Null(index.Resolve(PackageSetLanguage.NuGet, ["Other.Package"]).Set);
        Assert.Null(index.Resolve(PackageSetLanguage.Go, ["Microsoft.AspNetCore.TestHost"]).Set);
        Assert.True(ContentPackageIndex.TryParse(
            """[ { "id": "aspnetcore-testing", "title": "t", "language": "nuget", "packages": ["Microsoft.AspNetCore.TestHost"] } ]""", null, out var parsed, out _));
        Assert.Equal(PackageSetLanguage.NuGet, parsed.Sets[0].Language);
    }

    [Fact]
    public void RequiredNuGetPackagesComeFromRequires()
    {
        var exercise = Make.Exercise() with { Requires = new ExerciseRequirements(null, [], NugetPackages: ["A.B", "A.B", "C.D"]) };
        Assert.Equal(["A.B", "C.D"], ContentPackageIndex.RequiredNuGetPackages(exercise));
        Assert.True(exercise.NeedsNuGetEnvironment);
        Assert.False(Make.Exercise().NeedsNuGetEnvironment);
        Assert.Empty(ContentPackageIndex.RequiredNuGetPackages(Make.Exercise()));
    }

    [Fact]
    public void NuGetPinsAreReadFromTheRestoreProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devacademy-nugetpins-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "nuget", "aspnetcore-testing"));
            File.WriteAllText(Path.Combine(dir, "nuget", "aspnetcore-testing", "restore.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Microsoft.AspNetCore.TestHost\" Version=\"10.0.10\" /></ItemGroup></Project>");
            var set = NuGet("aspnetcore-testing", "Microsoft.AspNetCore.TestHost");
            Assert.Equal([new PinnedPackage("Microsoft.AspNetCore.TestHost", "10.0.10")], new ContentPackageIndex([set], dir).ReadPins(set));
        }
        finally { Directory.Delete(dir, true); }
    }
}
