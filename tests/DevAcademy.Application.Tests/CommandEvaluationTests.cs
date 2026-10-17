using DevAcademy.Application.Evaluation;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

internal static class Fx
{
    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}

public class JUnitReportParserTests
{
    private static ReportedTest Find(ParsedReport r, string fullName) => r.Tests.Single(t => t.FullName == fullName);

    [Fact]
    public void RealNodeReportYieldsFlatAndNestedTestsWithOutcomes()
    {
        var report = JUnitReportParser.Parse(Fx.Read("node-mixed.xml"))!;
        Assert.False(report.BuildFailed);
        Assert.Equal(7, report.Tests.Count);

        Assert.Equal(ReportedOutcome.Passed, Find(report, "mean of list").Outcome);
        Assert.Equal(ReportedOutcome.Passed, Find(report, "mean of empty list").Outcome);
        Assert.Equal(ReportedOutcome.Passed, Find(report, "async ok").Outcome);

        var nestedOk = Find(report, "group > nested ok");
        Assert.Equal("nested ok", nestedOk.Name);
        Assert.Equal(ReportedOutcome.Passed, nestedOk.Outcome);
        Assert.Equal("test", nestedOk.ClassName);

        var fail = Find(report, "group > nested fail");
        Assert.Equal(ReportedOutcome.Failed, fail.Outcome);
        Assert.Contains("wrong mean2 !== 3", fail.Message);

        Assert.Equal(ReportedOutcome.Skipped, Find(report, "group > skipped one").Outcome);
        Assert.Equal(ReportedOutcome.Skipped, Find(report, "group > todo one").Outcome);
        Assert.Null(Find(report, "group > skipped one").Message);
    }

    [Fact]
    public void NodeSyntaxErrorReportsAFailedFileLevelTestAndNothingElse()
    {
        var report = JUnitReportParser.Parse(Fx.Read("node-syntax-error.xml"))!;
        var only = Assert.Single(report.Tests);
        Assert.Equal("tests/stats.test.js", only.Name);
        Assert.Equal(ReportedOutcome.Failed, only.Outcome);
        Assert.Contains("test failed", only.Message);
    }

    [Fact]
    public void BomAndNonJUnitInputAreHandled()
    {
        Assert.NotNull(JUnitReportParser.Parse("﻿" + Fx.Read("node-mixed.xml")));
        Assert.Null(JUnitReportParser.Parse("not xml at all"));
        Assert.Null(JUnitReportParser.Parse("<html><body/></html>"));
        Assert.Null(JUnitReportParser.Parse(""));
    }

    [Fact]
    public void DoctypeIsProhibitedInsteadOfExpanded()
    {
        var xml = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e \"boom\">]><testsuites><testcase name=\"&e;\"/></testsuites>";
        Assert.Null(JUnitReportParser.Parse(xml));
    }

    [Fact]
    public void ErrorChildFailsAndTopLevelTestcaseIsAccepted()
    {
        var r = JUnitReportParser.Parse("<testsuite name=\"s\"><testcase classname=\"C\" name=\"n\"><error message=\"m\">body</error></testcase></testsuite>")!;
        var t = Assert.Single(r.Tests);
        Assert.Equal(ReportedOutcome.Failed, t.Outcome);
        Assert.Equal("s > n", t.FullName);
        Assert.Equal("C", t.ClassName);
        Assert.Equal("m\nbody", t.Message);
    }
}

public class GoTestJsonParserTests
{
    private static ReportedTest Find(ParsedReport r, string name) => r.Tests.Single(t => t.Name == name);

    [Fact]
    public void RealMixedRunHasSubtestsWithPassFailAndSkip()
    {
        var report = GoTestJsonParser.Parse(Fx.Read("go-mixed.json"));
        Assert.False(report.BuildFailed);
        Assert.Equal(ReportedOutcome.Failed, Find(report, "TestMean").Outcome);
        Assert.Equal(ReportedOutcome.Passed, Find(report, "TestMean/simple").Outcome);
        Assert.Equal(ReportedOutcome.Failed, Find(report, "TestMean/wrong").Outcome);
        Assert.Equal(ReportedOutcome.Passed, Find(report, "TestMean/single").Outcome);
        Assert.Equal(ReportedOutcome.Skipped, Find(report, "TestSkip").Outcome);
        Assert.Equal(ReportedOutcome.Passed, Find(report, "TestEmpty").Outcome);

        var wrong = Find(report, "TestMean/wrong");
        Assert.Equal("example.com/stats", wrong.ClassName);
        Assert.Contains("want 5", wrong.Message);
        Assert.DoesNotContain("=== RUN", wrong.Message);
        Assert.DoesNotContain("--- FAIL", wrong.Message);
        Assert.Null(Find(report, "TestMean/simple").Message);
    }

    [Fact]
    public void PanicFailsTheTestAndKeepsEarlierResults()
    {
        var report = GoTestJsonParser.Parse(Fx.Read("go-panic.json"));
        var panic = Find(report, "TestPanic");
        Assert.Equal(ReportedOutcome.Failed, panic.Outcome);
        Assert.Contains("assignment to entry in nil map", panic.Message);
        Assert.True(panic.Message!.Length <= 4100);
        Assert.Equal(ReportedOutcome.Passed, Find(report, "TestEmpty").Outcome);
    }

    [Theory]
    [InlineData("go-build-fail.json", "undefined: undefined1")]
    [InlineData("go-syntax-fail.json", "syntax error")]
    public void BuildFailureIsFlaggedWithCompilerOutputAndNoTests(string fixture, string text)
    {
        var report = GoTestJsonParser.Parse(Fx.Read(fixture));
        Assert.True(report.BuildFailed);
        Assert.Empty(report.Tests);
        Assert.Contains(text, report.BuildOutput);
    }

    [Fact]
    public void GarbageLinesAreIgnoredAndUnfinishedTestsAreNotReported()
    {
        var text = "hello\n{broken\n{\"Action\":\"run\",\"Package\":\"p\",\"Test\":\"TestA\"}\n[1,2]\n{\"Action\":\"run\",\"Package\":\"p\",\"Test\":\"TestB\"}\n{\"Action\":\"pass\",\"Package\":\"p\",\"Test\":\"TestB\"}\n";
        var report = GoTestJsonParser.Parse(text);
        var only = Assert.Single(report.Tests);
        Assert.Equal("TestB", only.Name);
    }
}

public class CommandCaseMatcherTests
{
    private static readonly IReadOnlyList<ReportedTest> Reported =
    [
        new("nested ok", "group > nested ok", "test", ReportedOutcome.Passed, null),
        new("mean", "mean", "", ReportedOutcome.Passed, null),
        new("TestMean/simple", "TestMean/simple", "example.com/stats", ReportedOutcome.Passed, null),
        new("TestMean/wrong", "TestMean/wrong", "example.com/stats", ReportedOutcome.Failed, "x"),
        new("TestMeanOther", "TestMeanOther", "example.com/stats", ReportedOutcome.Passed, null),
    ];

    [Theory]
    [InlineData("mean", 1)]
    [InlineData("nested ok", 1)]
    [InlineData("group > nested ok", 1)]
    [InlineData("test.nested ok", 1)]
    [InlineData("example.com/stats.TestMean/simple", 1)]
    [InlineData("TestMean/*", 2)]
    [InlineData("TestMean*", 3)]
    [InlineData("group >*", 1)]
    [InlineData("MEAN", 0)]
    [InlineData("mea", 0)]
    [InlineData("nope", 0)]
    [InlineData("", 0)]
    [InlineData("  ", 0)]
    public void SelectsByNameFullNameClassQualifiedNameOrPrefix(string pattern, int expected) =>
        Assert.Equal(expected, CommandCaseMatcher.Select(Reported, pattern).Count);

    [Fact]
    public void NullPatternMatchesNothing() => Assert.Empty(CommandCaseMatcher.Select(Reported, null));
}

public class CommandEvaluatorTests
{
    private const string Report = "report.xml";

    private static string Junit(params (string Name, string? Problem)[] tests) =>
        "<testsuites>" + string.Concat(tests.Select(t =>
            t.Problem is null ? $"<testcase name=\"{t.Name}\"/>"
            : t.Problem == "skip" ? $"<testcase name=\"{t.Name}\"><skipped/></testcase>"
            : $"<testcase name=\"{t.Name}\"><failure message=\"{t.Problem}\"/></testcase>")) + "</testsuites>";

    private static Exercise Exercise(CommandSpec? spec = null, params TestCase[] tests) =>
        new("cx", "Cmd", "", "javascript", "src/a.mjs", [new("src/a.mjs", "// todo")], [new("src/a.mjs", "// ok")], EvaluationKind.Command, [], null,
            tests.Length > 0 ? tests : [Case("c1", "alpha"), Case("c2", "beta", hidden: true, hint: "think")], ["h"],
            spec ?? Spec());

    private static TestCase Case(string id, string match, bool hidden = false, string? hint = null) =>
        new(id, "title " + id, null, null, OutputComparison.Trim, 0, hidden, hint, match);

    private static CommandSpec Spec(string target = "tests", CommandReportFormat format = CommandReportFormat.Junit, string? reportFile = Report) =>
        new(["node", "--test", "tests/*.test.mjs"], format, format == CommandReportFormat.Junit ? reportFile : null, null, target,
            [new ExerciseFile("a.test.mjs", "// hidden tests, SECRET-ASSERTION")]);

    private static ExecutionResult Done(int exit = 0, string stdout = "", string stderr = "", ExecutionStatus? status = null) =>
        new(status ?? (exit == 0 ? ExecutionStatus.Success : ExecutionStatus.RuntimeError), exit, stdout, stderr, "", TimeSpan.FromMilliseconds(5), null, IsolationLevel.OsSandbox);

    private static FakeRunner Runner(ExecutionResult result, string? report = null)
    {
        var runner = new FakeRunner((_, _) => result);
        if (report is not null) runner.OutputFiles[Report] = report;
        return runner;
    }

    private static Task<EvaluationReport> Run(FakeRunner runner, Exercise? ex = null, params ExerciseFile[] learner) =>
        new CommandEvaluator(runner).EvaluateAsync(ex ?? Exercise(), learner.Length > 0 ? learner : [new("src/a.mjs", "export {}")], IsolationLevel.OsSandbox, default);

    [Fact]
    public async Task AllDeclaredTestsPassed()
    {
        var runner = Runner(Done(), Junit(("alpha", null), ("beta", null)));
        var report = await Run(runner);
        Assert.Equal(Verdict.Passed, report.Status);
        Assert.Equal(2, report.PassedCount);
        Assert.Equal(1, runner.Prepares);
        Assert.Equal(1, runner.Program!.Runs);
        Assert.Equal("Hidden test 1", report.Cases[1].Title);
    }

    [Fact]
    public async Task RequestCarriesTheDeclaredArgvAndTestFilesUnderTheTarget()
    {
        var runner = Runner(Done(), Junit(("alpha", null), ("beta", null)));
        await Run(runner);
        var request = runner.LastRequest!;
        Assert.Equal(["node", "--test", "tests/*.test.mjs"], request.Command);
        Assert.Contains(request.Files, f => f.Path == "src/a.mjs" && f.Content == "export {}");
        Assert.Contains(request.Files, f => f.Path == "tests/a.test.mjs");
    }

    [Fact]
    public async Task MissingDeclaredTestFailsWithNotRunMessage()
    {
        var report = await Run(Runner(Done(), Junit(("alpha", null))));
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.True(report.Cases[0].Passed);
        Assert.False(report.Cases[1].Passed);
        Assert.Contains("the test was not run", report.Cases[1].Message);
    }

    [Fact]
    public async Task SkippedTestDoesNotCount()
    {
        var report = await Run(Runner(Done(), Junit(("alpha", null), ("beta", "skip"))));
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.Contains("skipped", report.Cases[1].Message);
    }

    [Fact]
    public async Task FailedPublicTestShowsItsMessageAndHint()
    {
        var ex = Exercise(null, Case("c1", "alpha", hint: "check alpha"), Case("c2", "beta", hidden: true));
        var report = await Run(Runner(Done(1), Junit(("alpha", "expected 1 got 2"), ("beta", null))), ex);
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.Contains("expected 1 got 2", report.Cases[0].Message);
        Assert.Contains("check alpha", report.Cases[0].Message);
        Assert.True(report.Cases[1].Passed);
    }

    [Fact]
    public async Task HiddenFailureExposesNoAssertionText()
    {
        var report = await Run(Runner(Done(1), Junit(("alpha", null), ("beta", "SECRET-ASSERTION expected 42 got 7"))));
        var hidden = report.Cases[1];
        Assert.False(hidden.Passed);
        Assert.True(hidden.Hidden);
        Assert.Null(hidden.Detail);
        Assert.Equal("Hidden test failed. Hint: think", hidden.Message);
        var dump = System.Text.Json.JsonSerializer.Serialize(report);
        Assert.DoesNotContain("SECRET-ASSERTION", dump);
        Assert.DoesNotContain("expected 42", dump);
        Assert.DoesNotContain("beta", dump);
    }

    [Fact]
    public async Task HiddenFailureWithoutHintIsOnlyTheGenericMessage()
    {
        var ex = Exercise(null, Case("c1", "alpha"), Case("c2", "beta", hidden: true));
        var report = await Run(Runner(Done(1), Junit(("alpha", null), ("beta", "boom"))), ex);
        Assert.Equal("Hidden test failed", report.Cases[1].Message);
    }

    [Fact]
    public async Task NonZeroExitWithoutAnyReportIsCompileErrorWithOutput()
    {
        var report = await Run(Runner(Done(1, stderr: "SyntaxError: Unexpected token '}'\n    at src/a.mjs:3")));
        Assert.Equal(Verdict.CompileError, report.Status);
        Assert.Empty(report.Cases);
        Assert.Contains("SyntaxError", report.Message);
    }

    [Fact]
    public async Task NonZeroExitWhereNothingDeclaredRanIsCompileError()
    {
        // The learner's syntax error makes node report only the failing test file, not the declared tests.
        var xml = "<testsuites><testcase name=\"tests/a.test.mjs\"><failure message=\"test failed\"/></testcase></testsuites>";
        var report = await Run(Runner(Done(1, stderr: "SyntaxError in src/a.mjs"), xml));
        Assert.Equal(Verdict.CompileError, report.Status);
        Assert.Contains("SyntaxError", report.Message);
    }

    [Fact]
    public async Task ZeroExitWithoutReportIsAnErrorNotAPass()
    {
        var report = await Run(Runner(Done(0)));
        Assert.Equal(Verdict.Error, report.Status);
        Assert.Contains("no report", report.Message);
    }

    [Fact]
    public async Task GoBuildFailureIsCompileErrorWithCompilerOutput()
    {
        var spec = new CommandSpec(["go", "test", "-json", "./..."], CommandReportFormat.GoTestJson, null, null, ".", [new("a_test.go", "x")]);
        var ex = Exercise(spec) with { LanguageId = "go", EntryFile = "a.go" };
        var runner = Runner(Done(1, stdout: Fx.Read("go-build-fail.json")));
        var report = await Run(runner, ex);
        Assert.Equal(Verdict.CompileError, report.Status);
        Assert.Contains("undefined: undefined1", report.Message);
        Assert.Empty(report.Cases);
    }

    [Fact]
    public async Task GoReportIsReadFromStdoutAndPanicFailsOnlyThatCase()
    {
        var spec = new CommandSpec(["go", "test", "-json", "./..."], CommandReportFormat.GoTestJson, null, null, ".", [new("a_test.go", "x")]);
        var ex = Exercise(spec, Case("c1", "TestEmpty"), Case("c2", "TestPanic", hidden: true)) with { LanguageId = "go", EntryFile = "a.go" };
        var report = await Run(Runner(Done(1, stdout: Fx.Read("go-panic.json"))), ex);
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.True(report.Cases[0].Passed);
        Assert.False(report.Cases[1].Passed);
        Assert.DoesNotContain("nil map", report.Cases[1].Message);
    }

    [Fact]
    public async Task LearnerCannotOverwriteHiddenTestFilesOrPreCreateTheReport()
    {
        var runner = Runner(Done(), Junit(("alpha", null), ("beta", null)));
        await Run(runner, null, new ExerciseFile("src/a.mjs", "ok"), new ExerciseFile("tests/a.test.mjs", "// learner: always pass"), new ExerciseFile(Report, "<testsuites/>"));
        var files = runner.LastRequest!.Files;
        Assert.Equal("// hidden tests, SECRET-ASSERTION", Assert.Single(files, f => f.Path == "tests/a.test.mjs").Content);
        Assert.DoesNotContain(files, f => f.Path == Report);
        Assert.Contains(files, f => f.Path == "src/a.mjs");
    }

    [Fact]
    public async Task TestsTargetDotPlacesTestFilesAtTheRootAndProtectsThem()
    {
        var spec = new CommandSpec(["go", "test", "-json", "./..."], CommandReportFormat.GoTestJson, null, null, ".", [new("a_test.go", "hidden")]);
        var ex = Exercise(spec) with { LanguageId = "go" };
        var runner = Runner(Done(), "");
        await Run(runner, ex, new ExerciseFile("a_test.go", "learner"), new ExerciseFile("a.go", "x"));
        Assert.Equal("hidden", Assert.Single(runner.LastRequest!.Files, f => f.Path == "a_test.go").Content);
    }

    [Fact]
    public async Task TimeoutFailsWithTimeLimitMessage()
    {
        var runner = Runner(Done(status: ExecutionStatus.Timeout));
        var report = await Run(runner);
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.Contains("Time limit exceeded (20 s)", report.Message);
        Assert.All(report.Cases, c => Assert.False(c.Passed));
    }

    [Fact]
    public async Task TimeoutIsClampedToThePolicyMaximum()
    {
        var ex = Exercise(Spec() with { TimeoutSeconds = 9999 });
        var report = await Run(Runner(Done(status: ExecutionStatus.Timeout)), ex);
        Assert.Contains($"({CommandPolicy.MaxTimeoutSeconds} s)", report.Message);
    }

    [Fact]
    public async Task ExitErrorAfterAllDeclaredPassedIsNotAPass()
    {
        var report = await Run(Runner(Done(1), Junit(("alpha", null), ("beta", null), ("other", "boom"))));
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.Contains("exited with an error", report.Message);
    }

    [Fact]
    public async Task RuntimeMissingMapsToRuntimeMissingVerdict()
    {
        var report = await Run(Runner(new ExecutionResult(ExecutionStatus.RuntimeMissing, null, "", "", "", TimeSpan.Zero, null, IsolationLevel.ProcessLimits, "node is not installed")));
        Assert.Equal(Verdict.RuntimeMissing, report.Status);
        Assert.Contains("node is not installed", report.Message);
    }

    [Fact]
    public async Task DisallowedExecutableIsAContentErrorAndNothingRuns()
    {
        var spec = Spec() with { Run = ["bash", "-c", "node --test"] };
        var runner = Runner(Done());
        var report = await Run(runner, Exercise(spec));
        Assert.Equal(Verdict.Error, report.Status);
        Assert.Contains("not allowed", report.Message);
        Assert.Equal(0, runner.Prepares);
    }

    [Fact]
    public async Task PreparationFailureIsReported()
    {
        var failure = new ExecutionResult(ExecutionStatus.CompileError, null, "", "", "bad", TimeSpan.Zero, null, IsolationLevel.ProcessLimits);
        var report = await Run(new FakeRunner((_, _) => Done(), failure));
        Assert.Equal(Verdict.CompileError, report.Status);
    }

    [Fact]
    public async Task WorkDirectoryIsHiddenInFailureMessages()
    {
        var runner = Runner(Done(1), Junit(("alpha", "bad at file:///tmp/fake/src/a.mjs:3"), ("beta", null)));
        var report = await Run(runner);
        Assert.DoesNotContain("/tmp/fake", report.Cases[0].Message);
        Assert.Contains("src/a.mjs:3", report.Cases[0].Message);
    }
}
