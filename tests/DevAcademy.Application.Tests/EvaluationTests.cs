using DevAcademy.Application.Evaluation;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

public class IoEvaluatorTests
{
    private static IoEvaluator Evaluator(FakeRunner r) => new(r);

    [Fact]
    public async Task PassesWhenAllOutputsMatchAndPreparesOnce()
    {
        var runner = new FakeRunner((stdin, _) => FakeRunner.Ok(stdin!.ToUpperInvariant()));
        var report = await Evaluator(runner).EvaluateAsync(Make.IoExercise(), [new("main.py", "x")], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Passed, report.Status);
        Assert.Equal(2, report.PassedCount);
        Assert.Equal(1, runner.Prepares);
        Assert.Equal(2, runner.Program!.Runs);
        Assert.Equal(IsolationLevel.OsSandbox, runner.LastRequest!.Isolation);
    }

    [Fact]
    public async Task WrongOutputFailsAndHiddenCaseLeaksNothing()
    {
        var runner = new FakeRunner((_, _) => FakeRunner.Ok("nope\n"));
        var report = await Evaluator(runner).EvaluateAsync(Make.IoExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Failed, report.Status);

        var pub = report.Cases[0];
        Assert.Equal("A\n", pub.Detail!.Expected);
        Assert.Equal("nope\n", pub.Detail.Actual);

        var hidden = report.Cases[1];
        Assert.True(hidden.Hidden);
        Assert.Equal("Hidden test 1", hidden.Title);
        Assert.Null(hidden.Detail);
        Assert.Contains("think about case", hidden.Message);
        var dump = System.Text.Json.JsonSerializer.Serialize(hidden);
        Assert.DoesNotContain("secret-in", dump);
        Assert.DoesNotContain("SECRET-IN", dump);
    }

    [Fact]
    public async Task MatchingStdoutWithCrashStillFails()
    {
        var runner = new FakeRunner((stdin, _) => FakeRunner.Ok(stdin!.ToUpperInvariant(), ExecutionStatus.RuntimeError));
        var report = await Evaluator(runner).EvaluateAsync(Make.IoExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.All(report.Cases, c => Assert.False(c.Passed));
    }

    [Fact]
    public async Task TimeoutFails()
    {
        var runner = new FakeRunner((_, _) => FakeRunner.Ok("A\n", ExecutionStatus.Timeout));
        var report = await Evaluator(runner).EvaluateAsync(Make.IoExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.Contains("Time limit", report.Cases[0].Message);
    }

    [Fact]
    public async Task CompileErrorIsReportedWithOutput()
    {
        var failure = new ExecutionResult(ExecutionStatus.CompileError, 1, "", "", "syntax error here", TimeSpan.Zero, null, IsolationLevel.OsSandbox);
        var runner = new FakeRunner((_, _) => throw new InvalidOperationException("must not run"), failure);
        var report = await Evaluator(runner).EvaluateAsync(Make.IoExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.CompileError, report.Status);
        Assert.Contains("syntax error here", report.Message);
        Assert.Empty(report.Cases);
    }

    [Fact]
    public async Task MissingRuntimeIsNotAFailedAttempt()
    {
        var failure = new ExecutionResult(ExecutionStatus.RuntimeMissing, null, "", "", "", TimeSpan.Zero, null, IsolationLevel.ProcessLimits, "python not installed");
        var report = await Evaluator(new FakeRunner((_, _) => throw new InvalidOperationException(), failure))
            .EvaluateAsync(Make.IoExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.RuntimeMissing, report.Status);
        Assert.Equal(IsolationLevel.ProcessLimits, report.AppliedIsolation);
    }

    [Fact]
    public async Task ServiceDispatchesCommandKindToTheCommandEvaluator()
    {
        var runner = new FakeRunner((_, _) => FakeRunner.Ok(""));
        var service = new ExerciseEvaluationService(runner);
        var ex = Make.IoExercise() with { Evaluation = EvaluationKind.Command };
        // Without a 'command' spec the exercise is a content error, reported as Error (not an exception, nothing is run).
        var report = await service.EvaluateAsync(ex, []);
        Assert.Equal(Verdict.Error, report.Status);
        Assert.Contains("has no command", report.Message);
        Assert.Equal(0, runner.Prepares);
    }
}

public class HarnessTests
{
    private static string Line(string nonce, string id, bool passed, string? msg = null) =>
        $"{HarnessOutputParser.Prefix} {nonce} {{\"id\":\"{id}\",\"passed\":{(passed ? "true" : "false")}{(msg is null ? "" : $",\"message\":\"{msg}\"")}}}\n";

    [Fact]
    public void ParserIgnoresWrongNonceAndGarbage()
    {
        var stdout = Line("evil", "a", true) + "noise\n" + Line("good", "b", false, "boom") + $"{HarnessOutputParser.Prefix} good {{not json\n"
            + $"{HarnessOutputParser.Prefix} good {{\"id\":\"c\"}}\n";
        var parsed = HarnessOutputParser.Parse(stdout, "good");
        var only = Assert.Single(parsed);
        Assert.Equal("b", only.Key);
        Assert.False(only.Value.Passed);
        Assert.Equal("boom", only.Value.Message);
    }

    [Fact]
    public void ParserHandlesCrlfAndDuplicateFailureSticks()
    {
        var stdout = Line("n", "a", false).Replace("\n", "\r\n") + Line("n", "a", true);
        Assert.False(HarnessOutputParser.Parse(stdout, "n")["a"].Passed);
    }

    private static (HarnessEvaluator Eval, FakeRunner Runner) Setup(Func<string, string> stdoutFromNonce, ExecutionStatus status = ExecutionStatus.Success)
    {
        var runner = new FakeRunner((_, env) => FakeRunner.Ok(stdoutFromNonce(env![HarnessEvaluator.NonceVariable]), status));
        return (new HarnessEvaluator(runner), runner);
    }

    [Fact]
    public async Task PassesWhenAllDeclaredCasesReportPassed()
    {
        var (eval, runner) = Setup(n => Line(n, "a", true) + Line(n, "b", true));
        var report = await eval.EvaluateAsync(Make.HarnessExercise(), [new("main.py", "x")], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Passed, report.Status);
        Assert.Equal("test_main.py", runner.LastRequest!.EntryFile);
        Assert.Contains(runner.LastRequest.Files, f => f.Path == "test_main.py");
        var nonce = runner.Program!.Environments.Single()![HarnessEvaluator.NonceVariable];
        Assert.Equal(32, nonce.Length);
        Assert.Matches("^[0-9a-f]{32}$", nonce);
    }

    [Fact]
    public async Task NonceIsFreshPerRun()
    {
        var seen = new List<string>();
        var (eval, _) = Setup(n => { seen.Add(n); return ""; });
        await eval.EvaluateAsync(Make.HarnessExercise(), [], IsolationLevel.OsSandbox, default);
        await eval.EvaluateAsync(Make.HarnessExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.NotEqual(seen[0], seen[1]);
    }

    [Fact]
    public async Task SpoofedPassLinesWithWrongNonceDoNotCount()
    {
        var (eval, _) = Setup(n => Line("0000", "a", true) + Line("0000", "b", true) + Line(n, "a", true));
        var report = await eval.EvaluateAsync(Make.HarnessExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Failed, report.Status);
        Assert.True(report.Cases.Single(c => c.TestId == "a").Passed);
        var b = report.Cases.Single(c => c.TestId == "b");
        Assert.False(b.Passed);
        Assert.Contains("test did not report", b.Message);
    }

    [Fact]
    public async Task HiddenFailureMessageComesFromAuthorHintNotHarness()
    {
        var (eval, _) = Setup(n => Line(n, "a", true) + Line(n, "b", false, "expected 42 got 41"));
        var report = await eval.EvaluateAsync(Make.HarnessExercise(), [], IsolationLevel.OsSandbox, default);
        var b = report.Cases.Single(c => c.TestId == "b");
        Assert.Equal("Hidden test 1", b.Title);
        Assert.DoesNotContain("42", b.Message);
        Assert.Contains("hint b", b.Message);
    }

    [Fact]
    public async Task AllPassedButCrashedExitStillFails()
    {
        var (eval, _) = Setup(n => Line(n, "a", true) + Line(n, "b", true), ExecutionStatus.RuntimeError);
        var report = await eval.EvaluateAsync(Make.HarnessExercise(), [], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Failed, report.Status);
    }

    [Fact]
    public async Task HarnessFileCollisionIsContentError()
    {
        var (eval, runner) = Setup(_ => "");
        var report = await eval.EvaluateAsync(Make.HarnessExercise(), [new("test_main.py", "learner")], IsolationLevel.OsSandbox, default);
        Assert.Equal(Verdict.Error, report.Status);
        Assert.Contains("collides", report.Message);
        Assert.Equal(0, runner.Prepares);
    }
}
