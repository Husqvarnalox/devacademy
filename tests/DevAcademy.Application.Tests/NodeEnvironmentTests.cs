using DevAcademy.Application.Evaluation;
using DevAcademy.Application.Node;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

internal sealed class ScriptedToolRunner(string? nodeVersion = "22.15.0", bool npm = true) : IToolRunner
{
    public List<string> Streaming { get; } = [];
    public string? WorkingDirectory { get; private set; }

    public string? Find(string name) => name switch { "node" when nodeVersion is not null => "/fake/node", "npm" when npm => "/fake/npm", _ => null };

    public Task<ToolOutput> RunAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, CancellationToken ct) =>
        Task.FromResult(fileName.EndsWith("node", StringComparison.Ordinal) ? new ToolOutput(0, "v" + nodeVersion + "\n", "") : new ToolOutput(1, "", "unexpected"));

    public Task<ToolOutput> RunStreamingAsync(string fileName, IReadOnlyList<string> args, ToolRunOptions? options, Action<string> onLine, CancellationToken ct)
    {
        Streaming.Add(fileName + " " + string.Join(' ', args));
        WorkingDirectory = options?.WorkingDirectory;
        onLine("added 95 packages");
        return Task.FromResult(new ToolOutput(0, "", ""));
    }
}

public sealed class NodeFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "devacademy-nodeapp-" + Guid.NewGuid().ToString("N"));
    public string Env => Path.Combine(Root, "env");
    public string Manifest => Path.Combine(Root, "manifest");

    public NodeFixture(params (string Name, string Version)[] installed)
    {
        Directory.CreateDirectory(Manifest);
        File.WriteAllText(Path.Combine(Manifest, "package.json"), """{ "name": "c", "dependencies": { "react": "19.3.1" }, "devDependencies": { "vitest": "5.0.5" } }""");
        File.WriteAllText(Path.Combine(Manifest, "package-lock.json"), "{}");
        Directory.CreateDirectory(Env);
        foreach (var (name, version) in installed)
        {
            var dir = Path.Combine(Env, "node_modules", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "package.json"), $$"""{ "name": "{{name}}", "version": "{{version}}" }""");
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }
}

public class VitestReportTests
{
    [Fact]
    public void RealVitestJunitReportIsParsedWithSuitePathNames()
    {
        var report = JUnitReportParser.Parse(Fx.Read("vitest-mixed.xml"))!;
        Assert.Equal(11, report.Tests.Count);
        Assert.Equal(ReportedOutcome.Passed, report.Tests.Single(t => t.Name == "ProductCard > показывает цену").Outcome);
        var failed = report.Tests.Single(t => t.Name == "formatPrice > рубли и копейки");
        Assert.Equal(ReportedOutcome.Failed, failed.Outcome);
        Assert.Contains("7.00", failed.Message);
        Assert.Equal(4, report.Tests.Count(t => t.Outcome == ReportedOutcome.Failed));
    }

    [Fact]
    public void CasesMapByNameAndByPrefix()
    {
        var report = JUnitReportParser.Parse(Fx.Read("vitest-mixed.xml"))!;
        Assert.Single(CommandCaseMatcher.Select(report.Tests, "ProductCard > показывает цену"));
        Assert.Equal(3, CommandCaseMatcher.Select(report.Tests, "ProductList*").Count);
        Assert.Empty(CommandCaseMatcher.Select(report.Tests, "ProductCard > нет такого теста"));
    }

    [Fact]
    public void SyntaxErrorInLearnerCodeIsOneFailedTestNamedAfterTheFile()
    {
        var report = JUnitReportParser.Parse(Fx.Read("vitest-syntax-error.xml"))!;
        var only = Assert.Single(report.Tests);
        Assert.Equal("tests/product.test.jsx", only.Name);
        Assert.Equal(ReportedOutcome.Failed, only.Outcome);
        Assert.Contains("PARSE_ERROR", only.Message);
        Assert.Empty(CommandCaseMatcher.Select(report.Tests, "ProductCard > показывает цену"));
    }
}

public class NodeEnvironmentServiceTests
{
    private static readonly string[] Packages = ["vitest", "react"];

    [Fact]
    public async Task MissingEnvironmentPointsToSettingsAndTheMakeTarget()
    {
        using var fx = new NodeFixture();
        Directory.Delete(fx.Env, true);
        var service = new NodeEnvironmentService(new ScriptedToolRunner(), fx.Env, fx.Manifest);
        var problem = await service.CheckAsync(new ExerciseRequirements(null, [], ">=22", Packages), [], default);
        Assert.NotNull(problem);
        Assert.Contains("Среды выполнения", problem);
        Assert.Contains("make node-env", problem);
    }

    [Fact]
    public async Task MissingPackageIsNamed()
    {
        using var fx = new NodeFixture(("react", "19.3.1"));
        var service = new NodeEnvironmentService(new ScriptedToolRunner(), fx.Env, fx.Manifest);
        var problem = await service.CheckAsync(new ExerciseRequirements(null, [], ">=22", Packages), [], default);
        Assert.Contains("vitest", problem);
        Assert.DoesNotContain("react,", problem);
    }

    [Fact]
    public async Task SatisfiedRequirementsPassAndTooOldNodeFails()
    {
        using var fx = new NodeFixture(("react", "19.3.1"), ("vitest", "5.0.5"));
        var req = new ExerciseRequirements(null, [], ">=22", Packages);
        Assert.Null(await new NodeEnvironmentService(new ScriptedToolRunner("22.15.0"), fx.Env, fx.Manifest).CheckAsync(req, [], default));
        var old = await new NodeEnvironmentService(new ScriptedToolRunner("20.11.0"), fx.Env, fx.Manifest).CheckAsync(req, [], default);
        Assert.Contains("20.11.0", old);
        var none = await new NodeEnvironmentService(new ScriptedToolRunner(null), fx.Env, fx.Manifest).CheckAsync(req, [], default);
        Assert.Contains("Node.js", none);
    }

    [Fact]
    public async Task InspectReportsDeclaredPackagesWithInstalledVersions()
    {
        using var fx = new NodeFixture(("react", "19.3.1"));
        File.Copy(Path.Combine(fx.Manifest, "package.json"), Path.Combine(fx.Env, "package.json"));
        var status = await new NodeEnvironmentService(new ScriptedToolRunner(), fx.Env, fx.Manifest).InspectAsync(default);
        Assert.True(status.Installed);
        Assert.Equal("22.15.0", status.NodeVersion);
        Assert.Equal("19.3.1", status.Packages.Single(p => p.Name == "react").Version);
        Assert.Null(status.Packages.Single(p => p.Name == "vitest").Version);
    }

    [Fact]
    public void InstallPlanIsTheExactNpmCiWithoutScripts()
    {
        using var fx = new NodeFixture();
        var (plan, error) = new NodeEnvironmentService(new ScriptedToolRunner(), fx.Env, fx.Manifest).PlanInstall();
        Assert.Null(error);
        Assert.Equal(["ci", "--ignore-scripts", "--no-audit", "--no-fund"], plan!.Args);
        Assert.Contains("npm ci --ignore-scripts", plan.CommandLine);
        Assert.Equal(Path.GetFullPath(fx.Env), plan.EnvironmentPath);
    }

    [Fact]
    public void InstallPlanExplainsMissingNpmAndMissingManifest()
    {
        using var fx = new NodeFixture();
        Assert.Contains("npm не найден", new NodeEnvironmentService(new ScriptedToolRunner(npm: false), fx.Env, fx.Manifest).PlanInstall().Error);
        Assert.Contains("node-content", new NodeEnvironmentService(new ScriptedToolRunner(), fx.Env, null).PlanInstall().Error);
    }

    [Fact]
    public async Task InstallCopiesTheManifestAndRunsOnlyNpmCiInTheEnvironmentDirectory()
    {
        using var fx = new NodeFixture();
        Directory.Delete(fx.Env, true);
        var tools = new ScriptedToolRunner();
        var service = new NodeEnvironmentService(tools, fx.Env, fx.Manifest);
        var lines = new List<string>();
        var result = await service.InstallAsync(service.PlanInstall().Plan!, lines.Add, default);
        Assert.True(result.Ok);
        Assert.Equal(["/fake/npm ci --ignore-scripts --no-audit --no-fund"], tools.Streaming);
        Assert.Equal(Path.GetFullPath(fx.Env), tools.WorkingDirectory);
        Assert.True(File.Exists(Path.Combine(fx.Env, "package-lock.json")));
        Assert.Contains("added 95 packages", lines);
    }

    [Fact]
    public void ManifestIsFoundNextToTheExecutableOrInTheRepository()
    {
        using var fx = new NodeFixture();
        Assert.Null(NodeEnvironmentService.FindManifestDirectory(fx.Env));
        var shipped = Path.Combine(fx.Root, "app");
        Directory.CreateDirectory(Path.Combine(shipped, "node-content"));
        File.WriteAllText(Path.Combine(shipped, "node-content", "package-lock.json"), "{}");
        Assert.Equal(Path.Combine(shipped, "node-content"), NodeEnvironmentService.FindManifestDirectory(shipped));
    }
}

public class NodeGradingTests
{
    private static Exercise VitestExercise() => new("rx", "React", "", "javascript", "src/a.jsx", [new("src/a.jsx", "// todo")], [new("src/a.jsx", "// ok")],
        EvaluationKind.Command, [], null,
        [new("c1", "title", null, null, OutputComparison.Trim, 0, false, null, "formatPrice > рубли и копейки"),
         new("c2", "title", null, null, OutputComparison.Trim, 0, true, "hint", "ProductList*")], ["h"],
        new CommandSpec([], CommandReportFormat.Junit, "report.xml", 60, ".", [new ExerciseFile("tests/p.test.jsx", "// hidden")], "vitest",
            ["--reporter=default", "--reporter=junit", "--outputFile=report.xml"]),
        Requires: new ExerciseRequirements(null, [], ">=22", ["vitest", "react"]));

    private static ExecutionResult Done(int exit) =>
        new(exit == 0 ? ExecutionStatus.Success : ExecutionStatus.RuntimeError, exit, "", "", "", TimeSpan.FromMilliseconds(5), null, IsolationLevel.OsSandbox);

    [Fact]
    public async Task CommandIsBuiltFromTheToolNotFromContentAndTheEnvironmentIsPassedOn()
    {
        var runner = new FakeRunner((_, _) => Done(1));
        runner.OutputFiles["report.xml"] = Fx.Read("vitest-mixed.xml");
        var node = new NodeRunSelection("/env");
        var report = await new CommandEvaluator(runner).EvaluateAsync(VitestExercise(), [new("src/a.jsx", "x")], IsolationLevel.OsSandbox, default, null, node);

        Assert.Equal(["node", "node_modules/vitest/vitest.mjs", "run", "--configLoader=runner", "--reporter=default", "--reporter=junit", "--outputFile=report.xml"], runner.LastRequest!.Command);
        Assert.Equal(node, runner.LastRequest.Node);
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.False(report.Cases.Single(c => c.TestId == "c1").Passed);
        Assert.False(report.Cases.Single(c => c.TestId == "c2").Passed); // a ProductList test fails in the captured report
    }

    [Fact]
    public async Task SyntaxErrorReportIsACompileError()
    {
        var runner = new FakeRunner((_, _) => Done(1));
        runner.OutputFiles["report.xml"] = Fx.Read("vitest-syntax-error.xml");
        var report = await new CommandEvaluator(runner).EvaluateAsync(VitestExercise(), [new("src/a.jsx", "x")], IsolationLevel.OsSandbox, default, null, new NodeRunSelection("/env"));
        Assert.Equal(Verdict.CompileError, report.Status);
    }

    [Fact]
    public async Task ServiceRefusesToRunWhenTheEnvironmentIsBlockedAndPassesItOtherwise()
    {
        var runner = new FakeRunner((_, _) => Done(0));
        runner.OutputFiles["report.xml"] = Fx.Read("vitest-mixed.xml");

        var blocked = new ExerciseEvaluationService(runner, null, new FixedNode(new NodeRunPlan(null, "нет пакетов")));
        var refused = await blocked.EvaluateAsync(VitestExercise(), [new("src/a.jsx", "x")]);
        Assert.Equal(Verdict.RuntimeMissing, refused.Status);
        Assert.Equal("нет пакетов", refused.Message);
        Assert.Equal(0, runner.Prepares);

        var withoutResolver = await new ExerciseEvaluationService(runner).EvaluateAsync(VitestExercise(), [new("src/a.jsx", "x")]);
        Assert.Equal(Verdict.RuntimeMissing, withoutResolver.Status);
        Assert.Equal(0, runner.Prepares);

        var node = new NodeRunSelection("/env");
        await new ExerciseEvaluationService(runner, null, new FixedNode(new NodeRunPlan(node))).EvaluateAsync(VitestExercise(), [new("src/a.jsx", "x")]);
        Assert.Equal(1, runner.Prepares);
        Assert.Equal(node, runner.LastRequest!.Node);
    }

    private sealed class FixedNode(NodeRunPlan plan) : INodeEnvironmentResolver
    {
        public Task<NodeRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct) => Task.FromResult(plan);
    }
}
