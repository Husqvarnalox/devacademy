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

/// <summary>
/// A package-set index on disk (react: react, react-dom, vitest; typescript: typescript, vitest; python numpy) plus an environment root with the
/// given (set, package, version) triples installed as <c>&lt;root&gt;/&lt;set&gt;/node_modules/&lt;package&gt;</c>.
/// </summary>
public sealed class NodeFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "devacademy-nodeapp-" + Guid.NewGuid().ToString("N"));
    public string Env => Path.Combine(Root, "env");
    public string Packages => Path.Combine(Root, "content-packages");
    public ContentPackageIndex Index { get; }
    public ContentPackageSet React => Index.Find(PackageSetLanguage.Node, "react")!;
    public ContentPackageSet TypeScript => Index.Find(PackageSetLanguage.Node, "typescript")!;

    public NodeFixture(params (string Set, string Name, string Version)[] installed)
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllText(Path.Combine(Packages, "index.json"), """
            [
              { "id": "react", "title": "React", "language": "node", "description": "d", "packages": ["react", "react-dom", "vitest"] },
              { "id": "typescript", "title": "TypeScript", "language": "node", "description": "d", "packages": ["typescript", "vitest"] },
              { "id": "numpy", "title": "NumPy", "language": "python", "description": "d", "packages": ["numpy"] }
            ]
            """);
        Manifest("react", """{ "name": "c", "dependencies": { "react": "19.3.1", "react-dom": "19.3.1" }, "devDependencies": { "vitest": "5.0.5" } }""");
        Manifest("typescript", """{ "name": "c", "devDependencies": { "typescript": "7.0.2", "vitest": "5.0.5" } }""");
        Directory.CreateDirectory(Path.Combine(Packages, "python"));
        File.WriteAllText(Path.Combine(Packages, "python", "numpy.txt"), "# c\nnumpy==2.5.4\n");
        Assert.True(ContentPackageIndex.TryLoad(Packages, out var index, out _));
        Index = index;
        Directory.CreateDirectory(Env);
        foreach (var (set, name, version) in installed)
        {
            var dir = Path.Combine(Env, set, "node_modules", name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "package.json"), $$"""{ "name": "{{name}}", "version": "{{version}}" }""");
        }
    }

    private void Manifest(string set, string packageJson)
    {
        var dir = Path.Combine(Packages, "node", set);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "package.json"), packageJson);
        File.WriteAllText(Path.Combine(dir, "package-lock.json"), "{}");
    }

    public NodeEnvironmentService Service(IToolRunner? tools = null) => new(tools ?? new ScriptedToolRunner(), Env, Index);

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
    public void RealTypeCheckFailureCarriesTscDiagnosticsOnceAndMapsToCases()
    {
        // Captured from vitest 5 running a TypeScript exercise whose hidden typecheck.test.ts reports tsc 7 diagnostics.
        var report = JUnitReportParser.Parse(Fx.Read("vitest-typecheck.xml"))!;
        var compile = report.Tests.Single(t => t.Name == "типы > код проходит tsc --strict");
        Assert.Equal(ReportedOutcome.Failed, compile.Outcome);
        Assert.Contains("src/geometry.ts(4,26): error TS7006: Parameter 'a' implicitly has an 'any' type.", compile.Message);
        // vitest repeats the message in the failure body; it must not be shown twice
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(compile.Message!, "tsc нашёл ошибки типов:"));

        var precise = Assert.Single(CommandCaseMatcher.Select(report.Tests, "типы > типы точные*"));
        Assert.Contains("TS2578", precise.Message);
        Assert.Equal(ReportedOutcome.Failed, precise.Outcome);
        Assert.Single(CommandCaseMatcher.Select(report.Tests, "типы > код проходит tsc*"));
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
    public async Task NotInstalledSetIsNamedAndPointsToSettingsAndTheMakeTarget()
    {
        using var fx = new NodeFixture();
        var check = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], ">=22", Packages), [], default);
        Assert.NotNull(check.Problem);
        Assert.Contains("«React»", check.Problem);
        Assert.Contains("Среды выполнения", check.Problem);
        Assert.Contains("make node-env", check.Problem);
        Assert.Equal(fx.React, check.Set);
    }

    [Fact]
    public async Task MissingPackageIsNamed()
    {
        using var fx = new NodeFixture(("react", "react", "19.3.1"));
        var check = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], ">=22", Packages), [], default);
        Assert.Contains("vitest", check.Problem);
        Assert.DoesNotContain("react,", check.Problem);
    }

    [Fact]
    public async Task OnlyTheExercisesOwnSetCounts()
    {
        // vitest is installed in the typescript set, but a react exercise must use the react set.
        using var fx = new NodeFixture(("typescript", "vitest", "5.0.5"), ("typescript", "typescript", "7.0.2"));
        var react = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], null, Packages), [], default);
        Assert.Equal(fx.React, react.Set);
        Assert.Contains("не установлен", react.Problem);
        var ts = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], null, ["vitest", "typescript"]), [], default);
        Assert.Null(ts.Problem);
        Assert.Equal(fx.TypeScript, ts.Set);
    }

    [Fact]
    public async Task NoCoveringSetIsAnErrorAndMissingIndexIsExplained()
    {
        using var fx = new NodeFixture();
        var none = await fx.Service().CheckAsync(new ExerciseRequirements(null, [], null, ["react", "typescript"]), [], default);
        Assert.Contains("ни один набор", none.Problem);
        Assert.Null(none.Set);
        var noIndex = await new NodeEnvironmentService(new ScriptedToolRunner(), fx.Env).CheckAsync(new ExerciseRequirements(null, [], null, Packages), [], default);
        Assert.Contains("content-packages", noIndex.Problem);
    }

    [Fact]
    public async Task SatisfiedRequirementsPassAndTooOldNodeFails()
    {
        using var fx = new NodeFixture(("react", "react", "19.3.1"), ("react", "vitest", "5.0.5"));
        var req = new ExerciseRequirements(null, [], ">=22", Packages);
        Assert.Null((await fx.Service(new ScriptedToolRunner("22.15.0")).CheckAsync(req, [], default)).Problem);
        var old = await fx.Service(new ScriptedToolRunner("20.11.0")).CheckAsync(req, [], default);
        Assert.Contains("20.11.0", old.Problem);
        var none = await fx.Service(new ScriptedToolRunner(null)).CheckAsync(req, [], default);
        Assert.Contains("Node.js", none.Problem);
    }

    [Fact]
    public async Task ResolveMountsOnlyTheExercisesSetAndOffersTheMissingOne()
    {
        using var fx = new NodeFixture(("react", "react", "19.3.1"), ("react", "vitest", "5.0.5"));
        var service = fx.Service();
        var ok = await service.ResolveAsync(ReactExercise(), default);
        Assert.False(ok.IsBlocked);
        Assert.Equal(Path.Combine(Path.GetFullPath(fx.Env), "react"), ok.Node!.EnvironmentPath);

        using var empty = new NodeFixture();
        var blocked = await empty.Service().ResolveAsync(ReactExercise(), default);
        Assert.True(blocked.IsBlocked);
        Assert.Equal(empty.React, blocked.InstallSet);
        Assert.Null(blocked.Node);
    }

    private static Exercise ReactExercise() => new("e", "E", "", "javascript", "main.js", [new("main.js", "")], [new("main.js", "")],
        EvaluationKind.Io, [], null, [], ["h"], Requires: new ExerciseRequirements(null, [], ">=22", Packages));

    [Fact]
    public async Task InspectReportsEachSetWithItsInstalledVersions()
    {
        using var fx = new NodeFixture(("react", "react", "19.3.1"));
        var status = await fx.Service().InspectAsync(default);
        Assert.Equal("22.15.0", status.NodeVersion);
        var react = status.Sets.Single(s => s.Set.Id == "react");
        Assert.True(react.Installed);
        Assert.Equal("19.3.1", react.Packages.Single(p => p.Name == "react").Version);
        Assert.Null(react.Packages.Single(p => p.Name == "vitest").Version);
        var ts = status.Sets.Single(s => s.Set.Id == "typescript");
        Assert.False(ts.Installed);
        Assert.Empty(ts.Packages);
        Assert.DoesNotContain(status.Sets, s => s.Set.Language == PackageSetLanguage.Python);
    }

    [Fact]
    public void InstallPlanIsTheExactNpmCiOfOneSetWithoutScripts()
    {
        using var fx = new NodeFixture();
        var (plan, error) = fx.Service().PlanInstall(fx.React);
        Assert.Null(error);
        Assert.Equal(["ci", "--ignore-scripts", "--no-audit", "--no-fund"], plan!.Args);
        Assert.Contains("npm ci --ignore-scripts", plan.CommandLine);
        Assert.Equal(Path.Combine(Path.GetFullPath(fx.Env), "react"), plan.EnvironmentPath);
        Assert.Contains(new PinnedPackage("react", "19.3.1"), plan.Packages);
        Assert.Contains(new PinnedPackage("vitest", "5.0.5"), plan.Packages);
    }

    [Fact]
    public void InstallPlanExplainsMissingNpmAndMissingManifest()
    {
        using var fx = new NodeFixture();
        Assert.Contains("npm не найден", fx.Service(new ScriptedToolRunner(npm: false)).PlanInstall(fx.React).Error);
        Directory.Delete(Path.Combine(fx.Packages, "node", "react"), true);
        Assert.Contains("content-packages/node/react", fx.Service().PlanInstall(fx.React).Error);
    }

    [Fact]
    public async Task InstallCopiesTheManifestAndRunsOnlyNpmCiInThatSetsDirectory()
    {
        using var fx = new NodeFixture();
        var tools = new ScriptedToolRunner();
        var service = fx.Service(tools);
        var lines = new List<string>();
        var result = await service.InstallAsync(service.PlanInstall(fx.React).Plan!, lines.Add, default);
        Assert.True(result.Ok);
        Assert.Equal(["/fake/npm ci --ignore-scripts --no-audit --no-fund"], tools.Streaming);
        Assert.Equal(Path.Combine(Path.GetFullPath(fx.Env), "react"), tools.WorkingDirectory);
        Assert.True(File.Exists(Path.Combine(fx.Env, "react", "package-lock.json")));
        Assert.False(Directory.Exists(Path.Combine(fx.Env, "typescript")));
        Assert.Contains("added 95 packages", lines);
    }

    [Fact]
    public void RemoveDeletesOnlyThatSetsDirectory()
    {
        using var fx = new NodeFixture(("react", "react", "19.3.1"), ("typescript", "vitest", "5.0.5"));
        var service = fx.Service();
        Assert.True(service.RemoveSet(fx.React).Ok);
        Assert.False(Directory.Exists(Path.Combine(fx.Env, "react")));
        Assert.True(service.IsInstalled(fx.TypeScript));
        Assert.True(service.RemoveSet(fx.React).Ok); // already gone is fine
    }

    [Fact]
    public async Task VersionOnlyRequirementDoesNotNeedTheNodeEnvironment()
    {
        // Regression: requires.node alone (built-in modules only) used to demand node_modules and block the exercise.
        using var fx = new NodeFixture();
        Directory.Delete(fx.Env, true);
        var exercise = new Exercise("e", "E", "", "javascript", "main.js", [new("main.js", "")], [new("main.js", "")],
            EvaluationKind.Io, [], null, [], ["h"], Requires: new ExerciseRequirements(null, [], ">=22.7"));
        Assert.False(exercise.NeedsNodeEnvironment);
        Assert.True(exercise.HasNodeRequirements);

        var ok = await fx.Service(new ScriptedToolRunner("22.15.0")).ResolveAsync(exercise, default);
        Assert.False(ok.IsBlocked);
        Assert.Null(ok.Node);
        var old = await fx.Service(new ScriptedToolRunner("22.6.0")).ResolveAsync(exercise, default);
        Assert.Contains("Нужен Node.js >=22.7", old.BlockedMessage);
    }

    [Fact]
    public void PackageIndexIsFoundNextToTheExecutableOrInTheRepository()
    {
        using var fx = new NodeFixture();
        Assert.Null(ContentPackageIndex.FindDirectory(fx.Env));
        var shipped = Path.Combine(fx.Root, "app");
        Directory.CreateDirectory(Path.Combine(shipped, "content-packages"));
        File.WriteAllText(Path.Combine(shipped, "content-packages", "index.json"), "[]");
        Assert.Equal(Path.Combine(shipped, "content-packages"), ContentPackageIndex.FindDirectory(shipped));
        var repo = Path.Combine(fx.Root, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "tools", "content-packages"));
        Directory.CreateDirectory(Path.Combine(repo, "src", "bin"));
        File.WriteAllText(Path.Combine(repo, "tools", "content-packages", "index.json"), "[]");
        Assert.Equal(Path.Combine(repo, "tools", "content-packages"), ContentPackageIndex.FindDirectory(Path.Combine(repo, "src", "bin")));
    }

    [Fact]
    public void RepositoryEnvironmentNeedsAnInstalledSet()
    {
        using var fx = new NodeFixture(("react", "react", "19.3.1"));
        Assert.True(NodeEnvironmentService.HasInstalledSet(fx.Env));
        Assert.False(NodeEnvironmentService.HasInstalledSet(Path.Combine(fx.Root, "nothing")));
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
