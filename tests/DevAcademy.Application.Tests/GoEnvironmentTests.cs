using DevAcademy.Application.Go;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

internal sealed class ScriptedGoToolRunner(string? goVersion = "1.26.5") : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public ToolRunOptions? Options { get; private set; }

    public string? Find(string name) => name == "go" && goVersion is not null ? "/fake/go" : null;

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        Task.FromResult(args.Count == 1 && args[0] == "version"
            ? new ToolOutput(0, $"go version go{goVersion} darwin/arm64\n", "")
            : new ToolOutput(1, "", "unexpected"));

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        Options = options;
        onLine("go: downloading github.com/gomlx/gomlx v0.28.8");
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

/// <summary>A package-set index with a go set <c>gomlx</c> plus an environment root with the given modules (set, module, version) present in the module cache.</summary>
public sealed class GoFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "devacademy-goapp-" + Guid.NewGuid().ToString("N"));
    public string Env => Path.Combine(Root, "env");
    public string Packages => Path.Combine(Root, "content-packages");
    public ContentPackageIndex Index { get; }
    public ContentPackageSet GoMl => Index.Find(PackageSetLanguage.Go, "gomlx")!;

    public GoFixture(params (string Set, string Module, string Version)[] installed)
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllText(Path.Combine(Packages, "index.json"), """
            [
              { "id": "gomlx", "title": "GoMLX", "language": "go", "description": "d", "packages": ["github.com/gomlx/gomlx", "github.com/gomlx/compute"] },
              { "id": "numpy", "title": "NumPy", "language": "python", "description": "d", "packages": ["numpy"] }
            ]
            """);
        var dir = Path.Combine(Packages, "go", "gomlx");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "go.mod"), """
            module devacademy.example/gomlx-set

            go 1.26.5

            require (
            	github.com/gomlx/compute v0.1.6
            	github.com/gomlx/gomlx v0.28.8
            )

            require github.com/pkg/errors v0.9.1 // indirect
            """);
        File.WriteAllText(Path.Combine(dir, "go.sum"), "x v1 h1:y\n");
        Directory.CreateDirectory(Path.Combine(Packages, "python"));
        File.WriteAllText(Path.Combine(Packages, "python", "numpy.txt"), "# c\nnumpy==2.5.4\n");
        Assert.True(ContentPackageIndex.TryLoad(Packages, out var index, out _));
        Index = index;
        Directory.CreateDirectory(Env);
        foreach (var (set, module, version) in installed)
        {
            var versions = Path.Combine(Env, set, "pkg", "mod", "cache", "download", module.Replace('/', Path.DirectorySeparatorChar), "@v");
            Directory.CreateDirectory(versions);
            File.WriteAllText(Path.Combine(versions, version + ".zip"), "zip");
        }
    }

    public GoEnvironmentService Service(IToolRunner? tools = null) => new(tools ?? new ScriptedGoToolRunner(), Env, Index);

    public void Dispose()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                foreach (var e in Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories))
                    File.SetUnixFileMode(e, File.GetUnixFileMode(e) | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.UserRead);
            Directory.Delete(Root, true);
        }
        catch (IOException) { }
    }
}

public class GoEnvironmentServiceTests
{
    private static readonly string[] Both = ["github.com/gomlx/gomlx", "github.com/gomlx/compute"];

    [Fact]
    public async Task NotInstalledSetIsNamedAndPointsToSettingsAndTheMakeTarget()
    {
        using var fx = new GoFixture();
        var check = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], GoModules: Both), default);
        Assert.Contains("GoMLX", check.Problem);
        Assert.Contains("make go-env", check.Problem);
        Assert.Equal("gomlx", check.Set!.Id);
    }

    [Fact]
    public async Task InstalledSetResolvesToItsModuleCache()
    {
        using var fx = new GoFixture(("gomlx", "github.com/gomlx/gomlx", "v0.28.8"), ("gomlx", "github.com/gomlx/compute", "v0.1.6"));
        var service = fx.Service();
        var check = await service.CheckAsync(new ExerciseRequirements(null, [], Go: ">=1.26.5", GoModules: Both), default);
        Assert.Null(check.Problem);
        var exercise = new Exercise("e", "E", "", "go", "main.go", [new("main.go", "")], [new("main.go", "")],
            EvaluationKind.Io, [], null, [], ["h"], Requires: new ExerciseRequirements(null, [], Go: ">=1.26.5", GoModules: Both));
        var plan = await service.ResolveAsync(exercise, default);
        Assert.False(plan.IsBlocked);
        Assert.Equal(Path.Combine(fx.Env, "gomlx", "pkg", "mod"), plan.Go!.ModCachePath);
    }

    [Fact]
    public async Task ModuleMissingFromTheCacheIsReported()
    {
        using var fx = new GoFixture(("gomlx", "github.com/gomlx/gomlx", "v0.28.8"));
        var check = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], GoModules: Both), default);
        Assert.Contains("github.com/gomlx/compute", check.Problem);
    }

    [Fact]
    public async Task UncoveredModuleIsNotInAnySet()
    {
        using var fx = new GoFixture();
        var check = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], GoModules: ["github.com/none/such"]), default);
        Assert.NotNull(check.Problem);
        Assert.Null(check.Set);
    }

    [Fact]
    public async Task OldGoIsRejectedAndMissingGoIsNamed()
    {
        using var fx = new GoFixture();
        Assert.Contains("1.25.0", await fx.Service(new ScriptedGoToolRunner("1.25.0")).CheckGoVersionAsync(">=1.26.5", default));
        Assert.Contains("go.dev/dl", await fx.Service(new ScriptedGoToolRunner(null)).CheckGoVersionAsync(">=1.26.5", default));
        Assert.Null(await fx.Service().CheckGoVersionAsync(">=1.26.5", default));
        Assert.Null(await fx.Service(new ScriptedGoToolRunner(null)).CheckGoVersionAsync(null, default));
    }

    [Fact]
    public void PlanInstallShowsTheExactCommandAndOfflineIsolatedEnvironment()
    {
        using var fx = new GoFixture();
        var (plan, error) = fx.Service().PlanInstall(fx.GoMl);
        Assert.Null(error);
        Assert.Equal(["mod", "download"], plan!.Args);
        Assert.Contains("go mod download", plan.CommandLine);
        Assert.Equal(Path.Combine(fx.Env, "gomlx", "pkg", "mod"), plan.Environment["GOMODCACHE"]);
        Assert.Equal("local", plan.Environment["GOTOOLCHAIN"]);
        Assert.Contains(plan.Packages, p => p.Name == "github.com/gomlx/gomlx" && p.Version == "v0.28.8");
    }

    [Fact]
    public void PlanInstallWithoutGoExplainsWhere()
    {
        using var fx = new GoFixture();
        var (plan, error) = fx.Service(new ScriptedGoToolRunner(null)).PlanInstall(fx.GoMl);
        Assert.Null(plan);
        Assert.Contains("go.dev/dl", error);
    }

    [Fact]
    public async Task InstallCopiesTheManifestAndRunsGoModDownloadInTheSetDirectory()
    {
        using var fx = new GoFixture();
        var tools = new ScriptedGoToolRunner();
        var service = fx.Service(tools);
        var (plan, _) = service.PlanInstall(fx.GoMl);
        var lines = new List<string>();
        var result = await service.InstallAsync(plan!, lines.Add, default);
        Assert.True(result.Ok);
        Assert.Equal("/fake/go mod download", Assert.Single(tools.Streaming));
        Assert.Equal(Path.Combine(fx.Env, "gomlx"), tools.Options!.WorkingDirectory);
        Assert.True(File.Exists(Path.Combine(fx.Env, "gomlx", "go.mod")));
        Assert.True(File.Exists(Path.Combine(fx.Env, "gomlx", "go.sum")));
        Assert.NotEmpty(lines);
    }

    [Fact]
    public async Task InspectListsVersionsOfInstalledModules()
    {
        using var fx = new GoFixture(("gomlx", "github.com/gomlx/gomlx", "v0.28.8"), ("gomlx", "github.com/gomlx/compute", "v0.1.6"));
        var status = await fx.Service().InspectAsync(default);
        Assert.Equal("1.26.5", status.GoVersion);
        var set = Assert.Single(status.Sets);
        Assert.True(set.Installed);
        Assert.Contains(set.Modules, m => m.Path == "github.com/gomlx/gomlx" && m.Version == "v0.28.8");
    }

    [Fact]
    public void RemoveSetDeletesAReadOnlyModuleCacheAndNothingElse()
    {
        using var fx = new GoFixture(("gomlx", "github.com/gomlx/gomlx", "v0.28.8"));
        var other = Path.Combine(fx.Env, "keep");
        Directory.CreateDirectory(other);
        if (!OperatingSystem.IsWindows())
        {
            var zip = Directory.EnumerateFiles(fx.Env, "*.zip", SearchOption.AllDirectories).Single();
            File.SetUnixFileMode(zip, UnixFileMode.UserRead);
            File.SetUnixFileMode(Path.GetDirectoryName(zip)!, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
        var result = fx.Service().RemoveSet(fx.GoMl);
        Assert.True(result.Ok, result.Error);
        Assert.False(Directory.Exists(Path.Combine(fx.Env, "gomlx")));
        Assert.True(Directory.Exists(other));
    }
}
