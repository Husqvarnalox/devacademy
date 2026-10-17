using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

public class PackageSetValidationTests
{
    private const string Anchor = "  entryFile: main.py\n  evaluation: io\n";

    private static ContentPackageIndex Index() => new(
    [
        new("numpy", "NumPy", PackageSetLanguage.Python, "", ["numpy"]),
        new("pandas", "pandas", PackageSetLanguage.Python, "", ["numpy", "pandas"]),
        new("fastapi", "FastAPI", PackageSetLanguage.Python, "", ["fastapi", "httpx"]),
        new("gomlx", "GoMLX", PackageSetLanguage.Go, "", ["github.com/gomlx/gomlx", "github.com/gomlx/compute"]),
        new("aspnetcore-testing", "ASP.NET Core testing", PackageSetLanguage.NuGet, "", ["Microsoft.AspNetCore.TestHost"]),
    ]);

    private static string[] Errors(IEnumerable<ContentDiagnostic> d) =>
        d.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => x.ToString()).ToArray();

    [Fact]
    public void An_exercise_covered_by_one_set_is_fine()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  requires: { packages: [\"numpy>=2\", pandas] }\n");
        Assert.Empty(Errors(ContentValidator.Validate(f.Load().Content, Index())));
    }

    [Fact]
    public void Go_modules_covered_by_one_go_set_are_fine()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  requires: { go: \">=1.26.5\", goModules: [github.com/gomlx/gomlx, github.com/gomlx/compute] }\n");
        // The fixture exercise is Python, so the only complaint is the language, never set coverage.
        Assert.All(Errors(ContentValidator.Validate(f.Load().Content, Index())), e => Assert.Contains("only apply to go", e));
    }

    [Fact]
    public void A_go_module_no_set_offers_is_an_error_and_a_bad_path_is_rejected()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  requires: { goModules: [github.com/none/such] }\n");
        Assert.Contains(Errors(ContentValidator.Validate(f.Load().Content, Index())), e => e.Contains("github.com/none/such"));
        using var g = new Fixture();
        g.EditVars(Anchor, Anchor + "  requires: { goModules: [\"not a module\"] }\n");
        Assert.Contains(Errors(ContentValidator.Validate(g.Load().Content, Index())), e => e.Contains("plain Go module path"));
    }

    [Fact]
    public void NuGet_packages_need_a_set_a_plain_id_and_a_csharp_exercise()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  requires: { nugetPackages: [Microsoft.AspNetCore.TestHost] }\n");
        // The fixture exercise is Python, so the only complaints are the language and the missing project, never set coverage.
        Assert.DoesNotContain(Errors(ContentValidator.Validate(f.Load().Content, Index())), e => e.Contains("ни один набор"));
        Assert.Contains(Errors(ContentValidator.Validate(f.Load().Content, Index())), e => e.Contains("only apply to csharp"));
        using var g = new Fixture();
        g.EditVars(Anchor, Anchor + "  requires: { nugetPackages: [Some.Other.Package] }\n");
        Assert.Contains(Errors(ContentValidator.Validate(g.Load().Content, Index())), e => e.Contains("Some.Other.Package"));
        using var h = new Fixture();
        h.EditVars(Anchor, Anchor + "  requires: { nugetPackages: [\"not a package\"] }\n");
        Assert.Contains(Errors(ContentValidator.Validate(h.Load().Content, Index())), e => e.Contains("plain NuGet package id"));
    }

    [Fact]
    public void Packages_split_across_sets_are_an_error()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  requires: { packages: [numpy, fastapi] }\n");
        var error = Assert.Single(Errors(ContentValidator.Validate(f.Load().Content, Index())));
        Assert.Contains("ни один набор пакетов", error);
        Assert.Contains("numpy, fastapi", error);
    }

    [Fact]
    public void A_package_no_set_offers_is_an_error()
    {
        using var f = new Fixture();
        f.EditVars(Anchor, Anchor + "  requires: { packages: [torch] }\n");
        Assert.Contains("torch", Assert.Single(Errors(ContentValidator.Validate(f.Load().Content, Index()))));
    }

    [Fact]
    public void A_missing_index_is_only_a_warning_and_only_when_packages_are_required()
    {
        using var f = new Fixture();
        var nowhere = Path.Combine(f.Root, "no-index-here");
        Directory.CreateDirectory(nowhere);
        Assert.DoesNotContain(ContentValidator.Validate(f.Load().Content, null, nowhere), d => d.Message.Contains("package-set"));

        f.EditVars(Anchor, Anchor + "  requires: { packages: [numpy] }\n");
        var diagnostics = ContentValidator.Validate(f.Load().Content, null, nowhere);
        Assert.Empty(Errors(diagnostics));
        Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("package-set index not found"));
    }

    [Fact]
    public void Every_requirement_of_the_shipped_content_is_covered_by_a_shipped_set()
    {
        var packages = ContentPackageIndex.TryLoadDefault();
        Assert.NotNull(packages);
        var contentRoot = Path.Combine(Path.GetDirectoryName(packages.Directory!)!, "..", "content");
        if (!Directory.Exists(contentRoot)) Assert.Skip("repository content not found");
        var result = ContentLoader.Load(Path.GetFullPath(contentRoot));
        Assert.DoesNotContain(Errors(ContentValidator.Validate(result.Content, packages)), e => e.Contains("набор"));
    }

    [Fact]
    public void The_shipped_index_matches_its_manifests()
    {
        var packages = ContentPackageIndex.TryLoadDefault();
        Assert.NotNull(packages);
        Assert.NotEmpty(packages.For(PackageSetLanguage.Node));
        Assert.NotEmpty(packages.For(PackageSetLanguage.Python));
        Assert.NotEmpty(packages.For(PackageSetLanguage.Go));
        foreach (var set in packages.Sets)
        {
            var pins = packages.ReadPins(set).Select(p => ContentPackageIndex.NormalizeName(set.Language, p.Name)).ToHashSet();
            foreach (var name in set.Packages)
                Assert.True(pins.Contains(ContentPackageIndex.NormalizeName(set.Language, name)), $"{set.Key}: '{name}' is not pinned by its manifest");
            if (set.Language == PackageSetLanguage.Go)
                Assert.True(File.Exists(Path.Combine(packages.SourcePath(set)!, "go.sum")), $"{set.Key}: go.sum is missing");
            if (set.Language == PackageSetLanguage.Node)
                Assert.True(File.Exists(Path.Combine(packages.SourcePath(set)!, "package-lock.json")), $"{set.Key}: package-lock.json is missing");
        }
    }
}
