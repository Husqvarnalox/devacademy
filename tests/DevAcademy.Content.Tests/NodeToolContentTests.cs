using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

public class NodeToolContentTests
{
    private const string Lesson = "courses/py-basics/lessons/02-vars/lesson.yaml";

    private static string Yaml(string tool = "vitest", string args = "[\"--reporter=default\", \"--reporter=junit\", \"--outputFile=report.xml\"]",
        string requires = "  requires: { node: \">=22\", npmPackages: [vitest, jsdom, react] }\n", string? reportFile = "report.xml", string report = "junit",
        string run = "") =>
        "exercise:\n  id: py.vars.cmd\n  title: Command\n  prompt: Make tests pass.\n  languageId: javascript\n  entryFile: src/a.jsx\n  evaluation: command\n" +
        "  starter: cmd/starter\n  solution: cmd/solution\n  tests_dir: cmd/tests\n  hints: [direction]\n" + requires +
        "  command:\n" + (tool.Length > 0 ? $"    tool: {tool}\n" : "") + (args.Length > 0 ? $"    args: {args}\n" : "") + run +
        $"    report: {report}\n" + (reportFile is null ? "" : $"    reportFile: {reportFile}\n") + "    testsTarget: \".\"\n" +
        "  tests:\n    - id: c1\n      title: first\n      match: \"S > first\"\n    - id: c2\n      title: second\n      match: \"S*\"\n      hidden: true\n      hint: think\n\n";

    private static ContentLoadResult Load(string yaml)
    {
        using var f = new Fixture();
        var path = Path.Combine(f.Root, Lesson);
        var text = File.ReadAllText(path);
        var from = text.IndexOf("exercise:\n", StringComparison.Ordinal);
        var to = text.IndexOf("challenge:\n", StringComparison.Ordinal);
        File.WriteAllText(path, text[..from] + yaml + text[to..]);
        var dir = Path.Combine(f.LessonDir("02-vars"), "cmd");
        foreach (var (rel, content) in new[] { ("starter/src/a.jsx", "export const a = 0;\n"), ("solution/src/a.jsx", "export const a = 1;\n"),
                                                ("tests/tests/a.test.jsx", "// t\n"), ("tests/vitest.config.mjs", "export default {};\n") })
        {
            var full = Path.Combine(dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        return f.Load();
    }

    private static bool HasError(ContentLoadResult r, string part) => r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(part));

    [Fact]
    public void ValidVitestExerciseLoadsCleanAndKeepsNodeRequirements()
    {
        var r = Load(Yaml());
        Assert.DoesNotContain(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var ex = r.Content.FindLesson("py.vars")!.Exercise!;
        Assert.True(ex.NeedsNodeEnvironment);
        Assert.False(ex.HasRequirements); // python requirements only
        Assert.Equal(">=22", ex.Requires!.Node);
        Assert.Equal(["vitest", "jsdom", "react"], ex.Requires.NpmPackages);
        Assert.Equal("vitest", ex.Command!.Tool);
        Assert.Equal(".", ex.Command.TestsTarget);
        Assert.Equal(["node", "node_modules/vitest/vitest.mjs", "run", "--configLoader=runner", "--reporter=default", "--reporter=junit", "--outputFile=report.xml"],
            CommandPolicyArgv(ex.Command));
    }

    private static IReadOnlyList<string> CommandPolicyArgv(CommandSpec spec)
    {
        Assert.Null(CommandPolicy.Resolve("javascript", spec, out var argv));
        return argv;
    }

    [Fact]
    public void TypeScriptVitestExerciseLoadsCleanAndResolvesToTheNodeTool()
    {
        var r = Load(Yaml().Replace("languageId: javascript", "languageId: typescript").Replace("requires: { node: \">=22\", npmPackages: [vitest, jsdom, react] }", "requires: { node: \">=22\", npmPackages: [vitest, typescript] }"));
        Assert.DoesNotContain(r.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var ex = r.Content.FindLesson("py.vars")!.Exercise!;
        Assert.Equal("typescript", ex.LanguageId);
        Assert.True(ex.NeedsNodeEnvironment);
        Assert.Null(CommandPolicy.Resolve("typescript", ex.Command!, out var argv));
        Assert.Equal(["node", "node_modules/vitest/vitest.mjs", "run", "--configLoader=runner", "--reporter=default", "--reporter=junit", "--outputFile=report.xml"], argv);
    }

    [Fact]
    public void TypeScriptCannotCombineToolWithAContentSuppliedRun() =>
        Assert.Contains(Load(Yaml(run: "    run: [\"node\", \"x.js\"]\n").Replace("languageId: javascript", "languageId: typescript")).Diagnostics,
            d => d.Severity == DiagnosticSeverity.Error);

    [Fact]
    public void UnknownToolIsRejected() => Assert.True(HasError(Load(Yaml(tool: "jest")), "jest"));

    [Theory]
    [InlineData("[\"--reporter=junit\", \"--outputFile=report.xml\", \"--watch\"]")]
    [InlineData("[\"--reporter=junit\", \"--outputFile=report.xml\", \"--ui\"]")]
    [InlineData("[\"--reporter=junit\", \"--outputFile=report.xml\", \"--coverage\"]")]
    [InlineData("[\"--reporter=junit\", \"--outputFile=../report.xml\"]")]
    [InlineData("[\"--reporter=junit\", \"--outputFile=/tmp/report.xml\"]")]
    [InlineData("[\"--reporter=junit\", \"--outputFile=report.xml\", \"--config=/etc/x.mjs\"]")]
    [InlineData("[\"--reporter=./evil.mjs\", \"--outputFile=report.xml\"]")]
    [InlineData("[\"--reporter=junit\", \"--outputFile=report.xml\", \"--root\", \"/\"]")]
    public void DangerousVitestArgumentsAreRejected(string args) =>
        Assert.Contains(Load(Yaml(args: args)).Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

    [Fact]
    public void ToolCannotBeCombinedWithAContentSuppliedRun() =>
        Assert.Contains(Load(Yaml(run: "    run: [\"node\", \"x.js\"]\n")).Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

    [Fact]
    public void ToolNeedsItsPackageInNpmPackages() =>
        Assert.True(HasError(Load(Yaml(requires: "  requires: { node: \">=22\", npmPackages: [react] }\n")), "needs requires.npmPackages to list 'vitest'"));

    [Fact]
    public void ToolWithoutRequiresIsRejected() =>
        Assert.True(HasError(Load(Yaml(requires: "")), "command.tool needs requires.npmPackages"));

    [Fact]
    public void ReportMustBeJunitToTheDeclaredFile()
    {
        Assert.True(HasError(Load(Yaml(reportFile: null)), "report"));
        Assert.True(HasError(Load(Yaml(args: "[\"--reporter=junit\", \"--outputFile=other.xml\"]")), "--outputFile=report.xml"));
    }

    [Theory]
    [InlineData("  requires: { node: \"banana\", npmPackages: [vitest] }\n", "requires.node")]
    [InlineData("  requires: { node: \">=22\", npmPackages: [vitest, vitest] }\n", "twice")]
    [InlineData("  requires: { node: \">=22\", npmPackages: [\"vitest@latest\"] }\n", "not a plain npm package name")]
    [InlineData("  requires: { node: \">=22\", npmPackages: [\"https://evil.example/x.tgz\", vitest] }\n", "not a plain npm package name")]
    [InlineData("  requires: { node: \">=22\", npmPackages: [\"../x\", vitest] }\n", "not a plain npm package name")]
    public void BadNodeRequirementsAreRejected(string requires, string message) =>
        Assert.True(HasError(Load(Yaml(requires: requires)), message));

    [Fact]
    public void NodeRequirementsOnAPythonExerciseAreRejected()
    {
        using var f = new Fixture();
        f.EditVars("  entryFile: main.py\n  evaluation: io\n", "  entryFile: main.py\n  evaluation: io\n  requires: { npmPackages: [react] }\n");
        Assert.Contains(f.Load().Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("only apply to javascript"));
    }
}
