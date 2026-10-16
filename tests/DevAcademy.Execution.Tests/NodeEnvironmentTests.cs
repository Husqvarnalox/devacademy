using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

/// <summary>The node content environment is exposed as a read-only <c>node_modules</c> link; a run can use it but never change it.</summary>
public class NodeEnvironmentTests : IDisposable
{
    private readonly string _env = Path.Combine(Path.GetTempPath(), "devacademy-nodeenv-" + Guid.NewGuid().ToString("N"));

    public NodeEnvironmentTests()
    {
        var pkg = Path.Combine(_env, "node_modules", "pkg");
        Directory.CreateDirectory(pkg);
        File.WriteAllText(Path.Combine(pkg, "package.json"), """{ "name": "pkg", "version": "1.2.3", "main": "index.js" }""");
        File.WriteAllText(Path.Combine(pkg, "index.js"), "module.exports = { answer: 42 };");
    }

    public void Dispose()
    {
        try { Directory.Delete(_env, true); } catch (IOException) { }
    }

    private const string Probe = """
        const fs = require("fs");
        const path = require("path");
        console.log("answer", require("pkg").answer);
        for (const target of ["node_modules/pkg/index.js", "node_modules/pkg/new.txt", "node_modules/evil.txt"]) {
          try { fs.writeFileSync(target, "x"); console.log("WROTE", target); }
          catch (e) { console.log("denied", target); }
        }
        try { fs.rmSync("node_modules/pkg/index.js"); console.log("REMOVED"); } catch (e) { console.log("denied remove"); }
        """;

    private ExecutionRequest Request(IReadOnlyList<SourceFile>? files = null) =>
        new("javascript", files ?? [new SourceFile("main.js", Probe)], "main.js", IsolationLevel.OsSandbox, Node: new NodeRunSelection(_env));

    [Fact]
    public async Task EnvironmentIsReadableButNotWritableUnderTheSandbox()
    {
        await TestSupport.RequireAsync("javascript");
        var result = await TestSupport.RunAsync(Request());
        if (result.AppliedIsolation != IsolationLevel.OsSandbox) Assert.Skip("no OS sandbox on this machine");

        Assert.True(result.IsSuccess, result.Stderr + result.Message);
        Assert.Contains("answer 42", result.Stdout);
        Assert.DoesNotContain("WROTE", result.Stdout);
        Assert.DoesNotContain("REMOVED", result.Stdout);
        Assert.Equal("""{ "name": "pkg", "version": "1.2.3", "main": "index.js" }""", File.ReadAllText(Path.Combine(_env, "node_modules", "pkg", "package.json")));
        Assert.Equal("module.exports = { answer: 42 };", File.ReadAllText(Path.Combine(_env, "node_modules", "pkg", "index.js")));
        Assert.False(File.Exists(Path.Combine(_env, "node_modules", "evil.txt")));
    }

    [Fact]
    public async Task CleaningUpTheWorkDirectoryLeavesTheEnvironmentIntact()
    {
        await TestSupport.RequireAsync("javascript");
        await using (var program = await TestSupport.Runner.PrepareAsync(Request(), CancellationToken.None))
        {
            Assert.Null(program.PreparationFailure);
            Assert.True(Directory.Exists(Path.Combine(program.WorkingDirectory, "node_modules", "pkg")));
        }
        Assert.True(File.Exists(Path.Combine(_env, "node_modules", "pkg", "index.js")));
    }

    [Fact]
    public async Task LearnerFilesCannotLiveInNodeModules()
    {
        await TestSupport.RequireAsync("javascript");
        var files = new[] { new SourceFile("main.js", "1"), new SourceFile("node_modules/pkg/index.js", "module.exports = 1") };
        await Assert.ThrowsAsync<ArgumentException>(async () => { await using var _ = await TestSupport.Runner.PrepareAsync(Request(files), CancellationToken.None); });
    }

    [Fact]
    public async Task MissingNodeModulesIsAReportedFailureNotACrash()
    {
        await TestSupport.RequireAsync("javascript");
        var request = Request() with { Node = new NodeRunSelection(Path.Combine(_env, "nope")) };
        await using var program = await TestSupport.Runner.PrepareAsync(request, CancellationToken.None);
        Assert.NotNull(program.PreparationFailure);
    }
}
