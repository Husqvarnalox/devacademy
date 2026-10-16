using DevAcademy.Application.Python;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Services;

/// <summary>The editor's "Run" button: one run with the user's stdin, no judging.</summary>
public sealed class RunService(ICodeRunner runner, IExerciseEnvironmentResolver? environment = null)
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
        return await runner.RunOnceAsync(
            new ExecutionRequest(languageId, files.Select(f => new SourceFile(f.Path, f.Content)).ToList(), entryFile, IsolationLevel.OsSandbox, python),
            stdin, ResourceLimits.DefaultRun, ct).ConfigureAwait(false);
    }
}
