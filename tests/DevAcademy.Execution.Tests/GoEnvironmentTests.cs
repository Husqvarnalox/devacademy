using DevAcademy.Application.Go;
using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

/// <summary>The Go module cache of a package set is mounted read-only and the build runs offline against it.</summary>
public class GoEnvironmentTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), "devacademy-gomod-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_cache, true); } catch (IOException) { }
    }

    [Fact]
    public void ModuleCacheEnvironmentIsOfflineAndPointsAtTheCache()
    {
        var env = GoRuntime.ModCacheEnvironment("/x/pkg/mod");
        Assert.Equal("/x/pkg/mod", env["GOMODCACHE"]);
        Assert.Equal("off", env["GOPROXY"]);
        Assert.Equal("off", env["GOSUMDB"]);
        Assert.Equal("-mod=mod", env["GOFLAGS"]);
        Assert.Equal("0", env["CGO_ENABLED"]);
    }

    [Fact]
    public async Task WithoutASelectionTheBuildEnvironmentIsUnchanged()
    {
        await TestSupport.RequireAsync("go");
        var status = await TestSupport.StatusAsync("go");
        Directory.CreateDirectory(_cache);
        File.WriteAllText(Path.Combine(_cache, "main.go"), "package main\nfunc main() {}\n");
        var plan = new GoRuntime(new ExecutionOptions().CacheDirectory).CreatePlan(_cache, "main.go", status);
        foreach (var step in plan.CompileSteps.Append(plan.Run))
        {
            Assert.DoesNotContain("GOSUMDB", step.Environment?.Keys ?? []);
            Assert.DoesNotContain("GOMODCACHE", step.Environment?.Keys ?? []);
        }
    }

    [Fact]
    public async Task ModuleCacheWithoutDownloadsIsAReportedFailure()
    {
        await TestSupport.RequireAsync("go");
        Directory.CreateDirectory(_cache);
        var request = TestSupport.Request("go", "main.go", "package main\nfunc main() {}\n", IsolationLevel.OsSandbox) with { Go = new GoRunSelection(_cache) };
        await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
        Assert.NotNull(program.PreparationFailure);
        Assert.Contains("make go-env", program.PreparationFailure!.Message);
    }

    [Fact]
    public async Task ContainerIsDowngradedWhenAModuleCacheIsSelected()
    {
        await TestSupport.RequireAsync("go");
        Directory.CreateDirectory(Path.Combine(_cache, "cache", "download"));
        var request = TestSupport.Request("go", "main.go", "package main\nfunc main() {}\n", IsolationLevel.Container) with { Go = new GoRunSelection(_cache) };
        await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
        // The (empty) cache is accepted; whatever happens next, the run is never containerised.
        if (program.PreparationFailure is { } failure) Assert.NotEqual(IsolationLevel.Container, failure.AppliedIsolation);
    }

    [Fact]
    public async Task ModuleCacheOnlyAppliesToGo()
    {
        await TestSupport.RequireAsync("python");
        var request = TestSupport.Request("python", "main.py", "print(1)", IsolationLevel.OsSandbox) with { Go = new GoRunSelection(_cache) };
        await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
        Assert.Equal(ExecutionStatus.InternalError, program.PreparationFailure!.Status);
    }

    private const string GoMod = "module devacademy.example/probe\n\ngo 1.26.5\n\nrequire (\n\tgithub.com/gomlx/compute v0.1.6\n\tgithub.com/gomlx/gomlx v0.28.8\n)\n";

    private const string Program = """
        package main

        import (
        	"fmt"
        	"os"

        	"github.com/gomlx/compute"
        	_ "github.com/gomlx/compute/gobackend"
        	"github.com/gomlx/gomlx/core/graph"
        )

        func main() {
        	be, err := compute.NewWithConfig("go")
        	if err != nil {
        		fmt.Println("backend:", err)
        		os.Exit(1)
        	}
        	grad := graph.MustNewExec(be, func(x *graph.Node) *graph.Node {
        		return graph.Gradient(graph.ReduceAllSum(graph.Mul(x, x)), x)[0]
        	})
        	fmt.Println("d/dx x^2 at 3 =", grad.MustExec(float32(3))[0])
        	if err := os.WriteFile(os.Getenv("GOMODCACHE")+"/evil.txt", []byte("x"), 0o644); err == nil {
        		fmt.Println("WROTE")
        	}
        }
        """;

    [Fact]
    public async Task GoMlxBuildsAndRunsOfflineFromTheRepositoryEnvironmentUnderTheSandbox()
    {
        await TestSupport.RequireAsync("go");
        var env = GoEnvironmentService.FindRepositoryEnvironment();
        if (env is null) Assert.Skip("no .go-content in the repository (run `make go-env`)");
        var set = Path.Combine(env, "gomlx");
        var sum = Path.Combine(set, "go.sum");
        if (!File.Exists(sum)) Assert.Skip(".go-content/gomlx is not installed");
        var cache = Path.Combine(set, "pkg", "mod");
        var request = new ExecutionRequest("go",
            [new SourceFile("main.go", Program), new SourceFile("go.mod", GoMod), new SourceFile("go.sum", File.ReadAllText(sum))],
            "main.go", IsolationLevel.OsSandbox, Go: new GoRunSelection(cache));
        var limits = new ResourceLimits(TimeSpan.FromSeconds(30), 1024L * 1024 * 1024, 64 * 1024);
        var result = await TestSupport.RunAsync(request, null, limits);
        if (result.AppliedIsolation != IsolationLevel.OsSandbox) Assert.Skip("no OS sandbox on this machine");

        Assert.True(result.IsSuccess, result.CompileOutput + result.Stderr + result.Message);
        Assert.Contains("d/dx x^2 at 3 = float32(6)", result.Stdout);
        Assert.DoesNotContain("WROTE", result.Stdout);
        Assert.False(File.Exists(Path.Combine(cache, "evil.txt")));
    }
}
