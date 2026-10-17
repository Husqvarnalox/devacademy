using DevAcademy.Application.Go;
using DevAcademy.Application.Python;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Services;

/// <summary>The editor's "Run" button: one run with the user's stdin, no judging.</summary>
public sealed class RunService(ICodeRunner runner, IExerciseEnvironmentResolver? environment = null, IGoEnvironmentResolver? go = null)
{
    /// <param name="exercise">When given, its <c>requires</c>/<c>accelerator</c> are checked first; an unmet one returns RuntimeMissing instead of running.</param>
    public async Task<ExecutionResult> RunAsync(string languageId, IReadOnlyList<ExerciseFile> files, string entryFile,
        string? stdin, CancellationToken ct = default, Exercise? exercise = null)
    {
        PythonRunSelection? python = null;
        if (environment is not null && languageId == "python")
        {
            if (exercise is not null)
            {
                ExerciseRunPlan plan = await environment.ResolveAsync(exercise.ForVariant(languageId), ct).ConfigureAwait(false);
                if (plan.IsBlocked)
                    return new ExecutionResult(ExecutionStatus.RuntimeMissing, null, "", "", "", TimeSpan.Zero, null, IsolationLevel.OsSandbox, plan.BlockedMessage);
                python = plan.Python;
            }
            else
            {
                python = await environment.GetActiveSelectionAsync(ct).ConfigureAwait(false);
            }
        }
        GoRunSelection? goEnv = null;
        if (go is not null && languageId == "go" && exercise?.ForVariant(languageId) is { HasGoRequirements: true } goExercise)
        {
            GoRunPlan plan = await go.ResolveAsync(goExercise, ct).ConfigureAwait(false);
            if (plan.IsBlocked)
                return new ExecutionResult(ExecutionStatus.RuntimeMissing, null, "", "", "", TimeSpan.Zero, null, IsolationLevel.OsSandbox, plan.BlockedMessage);
            goEnv = plan.Go;
        }
        return await runner.RunOnceAsync(
            new ExecutionRequest(languageId, files.Select(f => new SourceFile(f.Path, f.Content)).ToList(), entryFile, IsolationLevel.OsSandbox, python, Go: goEnv),
            stdin, ExerciseLimits.For(exercise?.Requires), ct).ConfigureAwait(false);
    }
}
