using System.Text;
using DevAcademy.Content;
using DevAcademy.Domain;

namespace DevAcademy.Content.Tests;

/// <summary>Builds a lesson whose exercise is replaced by a command exercise (JavaScript, node:test) with its own starter/solution/tests dirs.</summary>
file static class CommandLesson
{
    public const string Lesson = "courses/py-basics/lessons/02-vars/lesson.yaml";

    public static string Yaml(string run = "[\"node\", \"--test\", \"--test-reporter=junit\", \"--test-reporter-destination=report.xml\", \"tests/*.test.mjs\"]",
        string report = "junit", string? reportFile = "report.xml", string? timeout = "30", string tests = DefaultTests)
    {
        var sb = new StringBuilder();
        sb.AppendLine("exercise:");
        sb.AppendLine("  id: py.vars.cmd");
        sb.AppendLine("  title: Command");
        sb.AppendLine("  prompt: Make tests pass.");
        sb.AppendLine("  languageId: javascript");
        sb.AppendLine("  entryFile: src/a.mjs");
        sb.AppendLine("  evaluation: command");
        sb.AppendLine("  starter: cmd/starter");
        sb.AppendLine("  solution: cmd/solution");
        sb.AppendLine("  tests_dir: cmd/tests");
        sb.AppendLine("  hints: [direction]");
        sb.AppendLine("  command:");
        sb.AppendLine($"    run: {run}");
        sb.AppendLine($"    report: {report}");
        if (reportFile is not null) sb.AppendLine($"    reportFile: {reportFile}");
        if (timeout is not null) sb.AppendLine($"    timeoutSeconds: {timeout}");
        sb.Append(tests);
        return sb.ToString();
    }

    public const string DefaultTests = """
          tests:
            - id: c1
              title: first
              match: "suite > first"
            - id: c2
              title: second
              match: "second*"
              hidden: true
              hint: think

        """;

    public static void Install(Fixture f, string? yaml = null)
    {
        var path = Path.Combine(f.Root, Lesson);
        var text = File.ReadAllText(path);
        var from = text.IndexOf("exercise:\n", StringComparison.Ordinal);
        var to = text.IndexOf("challenge:\n", StringComparison.Ordinal);
        File.WriteAllText(path, text[..from] + (yaml ?? Yaml()) + text[to..]);
        var dir = Path.Combine(f.LessonDir("02-vars"), "cmd");
        Write(dir, "starter/src/a.mjs", "export const a = 0;\n");
        Write(dir, "solution/src/a.mjs", "export const a = 1;\n");
        Write(dir, "tests/a.test.mjs", "import test from 'node:test';\n");
    }

    public static void Write(string dir, string rel, string content)
    {
        var full = Path.Combine(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}

public class CommandExerciseLoadingTests
{
    private static bool HasError(ContentLoadResult r, string part) =>
        r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(part));

    private static ContentLoadResult Load(string? yaml = null)
    {
        using var f = new Fixture();
        CommandLesson.Install(f, yaml);
        return f.Load();
    }

    [Fact]
    public void ValidCommandExerciseLoadsCleanWithAllFiles()
    {
        using var f = new Fixture();
        CommandLesson.Install(f);
        var r = f.Load();
        Assert.Empty(r.Diagnostics);
        var ex = r.Content.FindLesson("py.vars")!.Exercise!;
        Assert.Equal(EvaluationKind.Command, ex.Evaluation);
        Assert.Equal("javascript", ex.LanguageId);
        var cmd = ex.Command!;
        Assert.Equal("node", cmd.Run[0]);
        Assert.Equal(CommandReportFormat.Junit, cmd.Report);
        Assert.Equal("report.xml", cmd.ReportFile);
        Assert.Equal(30, cmd.TimeoutSeconds);
        Assert.Equal("tests", cmd.TestsTarget);
        Assert.Equal("a.test.mjs", Assert.Single(cmd.TestFiles).Path);
        Assert.Equal("src/a.mjs", Assert.Single(ex.Starter).Path);
        Assert.Equal("suite > first", ex.Tests[0].Match);
        Assert.Equal("second*", ex.Tests[1].Match);
        Assert.True(ex.Tests[1].Hidden);
    }

    [Fact]
    public void DisallowedExecutableIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(run: "[\"bash\", \"--test\"]")), "'bash' is not allowed"));

    [Fact]
    public void DangerousNodeFlagIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(run: "[\"node\", \"--test\", \"--require=x.js\", \"tests/a.test.mjs\"]")), "not allowed"));

    [Fact]
    public void ParentDirectoryArgumentIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(run: "[\"node\", \"--test\", \"../x.test.mjs\"]")), "without '..'"));

    [Fact]
    public void JunitWithoutReportFileIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(run: "[\"node\", \"--test\", \"tests/*.test.mjs\"]", reportFile: null)), "junit report needs command.reportFile"));

    [Fact]
    public void MissingMatchIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(tests: """
              tests:
                - id: c1
                  title: first
                - id: c2
                  title: second
                  match: "second"
                  hidden: true

            """)), "command test 'c1' needs 'match'"));

    [Fact]
    public void EmptyMatchIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(tests: """
              tests:
                - id: c1
                  title: first
                  match: "   "
                - id: c2
                  title: second
                  match: "second"
                  hidden: true

            """)), "needs 'match'"));

    [Fact]
    public void MatchThatSelectsEverythingIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(tests: """
              tests:
                - id: c1
                  title: first
                  match: "*"
                - id: c2
                  title: second
                  match: "second"
                  hidden: true

            """)), "selects everything"));

    [Fact]
    public void MissingHiddenTestIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml(tests: """
              tests:
                - id: c1
                  title: first
                  match: "first"
                - id: c2
                  title: second
                  match: "second"

            """)), "at least one hidden test"));

    [Theory]
    [InlineData("61")]
    [InlineData("0")]
    public void TimeoutOutsideRangeIsRejected(string timeout) =>
        Assert.True(HasError(Load(CommandLesson.Yaml(timeout: timeout)), "timeoutSeconds must be 1-60"));

    [Fact]
    public void MissingTestsDirectoryIsRejected()
    {
        using var f = new Fixture();
        CommandLesson.Install(f);
        Directory.Delete(Path.Combine(f.LessonDir("02-vars"), "cmd", "tests"), true);
        Assert.True(HasError(f.Load(), "hidden test files are required"));
    }

    [Fact]
    public void TestsDirEscapingTheLessonIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml().Replace("tests_dir: cmd/tests", "tests_dir: ../../../..")), "must stay inside the lesson directory"));

    [Fact]
    public void LearnerFileCollidingWithTestFileIsRejected()
    {
        using var f = new Fixture();
        CommandLesson.Install(f);
        CommandLesson.Write(Path.Combine(f.LessonDir("02-vars"), "cmd"), "starter/tests/a.test.mjs", "x");
        Assert.True(HasError(f.Load(), "collides with a starter/solution file"));
    }

    [Fact]
    public void StdinOnACommandTestIsRejected() =>
        Assert.True(HasError(Load(CommandLesson.Yaml().Replace("match: \"suite > first\"", "match: \"suite > first\"\n      stdin: \"x\"")), "must not set stdin/expected"));
}

public class TestDataFileTests
{
    private static string IoYaml(string extra1, string extra2) => $"""
        exercise:
          id: py.vars.ex1
          title: Echo
          prompt: Echo the input.
          entryFile: main.py
          evaluation: io
          hints: [direction, problem]
          tests:
            - id: t1
              title: simple
              {extra1}
            - id: t2
              title: hidden
              hidden: true
              hint: handle any text
              {extra2}

        """;

    private static (Fixture Fixture, string Dir) Setup(string extra1, string extra2 = "stdin: \"yo\\n\"\n      expected: \"yo\\n\"")
    {
        var f = new Fixture();
        var path = Path.Combine(f.Root, CommandLesson.Lesson);
        var text = File.ReadAllText(path);
        var from = text.IndexOf("exercise:\n", StringComparison.Ordinal);
        var to = text.IndexOf("challenge:\n", StringComparison.Ordinal);
        File.WriteAllText(path, text[..from] + IoYaml(extra1, extra2) + text[to..]);
        return (f, f.LessonDir("02-vars"));
    }

    private static TestCase First(ContentLoadResult r) => r.Content.FindLesson("py.vars")!.Exercise!.Tests[0];

    private static bool HasError(ContentLoadResult r, string part) =>
        r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains(part));

    [Fact]
    public void StdinAndExpectedFilesAreReadRelativeToTheLessonDirectory()
    {
        var (f, dir) = Setup("stdinFile: data/in.txt\n      expectedFile: data/out.txt");
        using var _ = f;
        Directory.CreateDirectory(Path.Combine(dir, "data"));
        File.WriteAllText(Path.Combine(dir, "data", "in.txt"), "line1\nline2\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "data", "out.txt"), "Привет\n");
        var r = f.Load();
        Assert.Empty(r.Diagnostics);
        Assert.Equal("line1\nline2\n", First(r).Stdin);
        Assert.Equal("Привет\n", First(r).Expected);
    }

    [Fact]
    public void Utf8BomIsStripped()
    {
        var (f, dir) = Setup("stdinFile: in.txt\n      expected: \"x\"");
        using var _ = f;
        File.WriteAllText(Path.Combine(dir, "in.txt"), "abc", new UTF8Encoding(true));
        Assert.Equal(0xEF, File.ReadAllBytes(Path.Combine(dir, "in.txt"))[0]);
        var r = f.Load();
        Assert.Empty(r.Diagnostics);
        Assert.Equal("abc", First(r).Stdin);
    }

    [Fact]
    public void InlineAndFileAreMutuallyExclusive()
    {
        var (f, dir) = Setup("stdin: \"a\"\n      stdinFile: in.txt\n      expected: \"x\"");
        using var _ = f;
        File.WriteAllText(Path.Combine(dir, "in.txt"), "abc");
        Assert.True(HasError(f.Load(), "mutually exclusive"));
    }

    [Fact]
    public void ExpectedInlineAndFileAreMutuallyExclusive()
    {
        var (f, dir) = Setup("stdin: \"a\"\n      expected: \"x\"\n      expectedFile: out.txt");
        using var _ = f;
        File.WriteAllText(Path.Combine(dir, "out.txt"), "abc");
        Assert.True(HasError(f.Load(), "'expected' and 'expectedFile' are mutually exclusive"));
    }

    [Fact]
    public void MissingFileIsAnError()
    {
        var (f, _) = Setup("stdinFile: nope.txt\n      expected: \"x\"");
        using var _f = f;
        Assert.True(HasError(f.Load(), "file 'nope.txt' not found"));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("../../../../../../../../etc/hosts")]
    public void PathEscapeIsRejected(string file)
    {
        var (f, _) = Setup($"stdinFile: {file}\n      expected: \"x\"");
        using var _f = f;
        File.WriteAllText(Path.Combine(f.LessonDir("02-vars"), "..", "outside.txt"), "secret");
        Assert.True(HasError(f.Load(), "must be a relative path inside the lesson directory"));
    }

    [Fact]
    public void AbsolutePathIsRejected()
    {
        var abs = Path.Combine(Path.GetTempPath(), "da-abs-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(abs, "x");
        try
        {
            // Single-quoted YAML has no escapes, so a Windows path (C:\\Users\\...) reaches the loader unchanged.
            var (f, _) = Setup($"stdinFile: '{abs}'\n      expected: \"x\"");
            using var _f = f;
            Assert.True(HasError(f.Load(), "must be a relative path inside the lesson directory"));
        }
        finally { File.Delete(abs); }
    }

    [Fact]
    public void FileOverTheLimitIsRejectedButExactlyAtTheLimitLoads()
    {
        var (f, dir) = Setup("stdinFile: big.txt\n      expected: \"x\"");
        using var _ = f;
        var big = Path.Combine(dir, "big.txt");
        using (var fs = new FileStream(big, FileMode.Create)) fs.SetLength(16L * 1024 * 1024 + 1); // sparse
        var r = f.Load();
        Assert.True(HasError(r, "the limit is"));
        Assert.True(HasError(r, "16 MB"));

        using (var fs = new FileStream(big, FileMode.Create)) fs.SetLength(16L * 1024 * 1024);
        Assert.DoesNotContain(f.Load().Diagnostics, d => d.Message.Contains("the limit is"));
    }

    [Fact]
    public void InvalidUtf8IsRejected()
    {
        var (f, dir) = Setup("stdinFile: bad.bin\n      expected: \"x\"");
        using var _ = f;
        File.WriteAllBytes(Path.Combine(dir, "bad.bin"), [0x61, 0xFF, 0xFE, 0x62]);
        Assert.True(HasError(f.Load(), "not valid UTF-8"));
    }
}
