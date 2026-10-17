using DevAcademy.Application.Go;
using DevAcademy.Application.Maven;
using DevAcademy.Application.NuGet;
using DevAcademy.Application.Python;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Services;

/// <summary>The editor's "Run" button: one run with the user's stdin, no judging.</summary>
public sealed class RunService(ICodeRunner runner, IExerciseEnvironmentResolver? environment = null, IGoEnvironmentResolver? go = null, INuGetEnvironmentResolver? nuget = null, IMavenEnvironmentResolver? maven = null)
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
        NuGetRunSelection? nugetEnv = null;
        if (nuget is not null && languageId == "csharp" && exercise?.ForVariant(languageId) is { NeedsNuGetEnvironment: true } nugetExercise)
        {
            NuGetRunPlan plan = await nuget.ResolveAsync(nugetExercise, ct).ConfigureAwait(false);
            if (plan.IsBlocked)
                return new ExecutionResult(ExecutionStatus.RuntimeMissing, null, "", "", "", TimeSpan.Zero, null, IsolationLevel.OsSandbox, plan.BlockedMessage);
            nugetEnv = plan.NuGet;
        }
        MavenRunSelection? mavenEnv = null;
        if (maven is not null && languageId == "java" && exercise?.ForVariant(languageId) is { NeedsMavenEnvironment: true } mavenExercise)
        {
            MavenRunPlan plan = await maven.ResolveAsync(mavenExercise, ct).ConfigureAwait(false);
            if (plan.IsBlocked)
                return new ExecutionResult(ExecutionStatus.RuntimeMissing, null, "", "", "", TimeSpan.Zero, null, IsolationLevel.OsSandbox, plan.BlockedMessage);
            mavenEnv = plan.Maven;
        }
        return await runner.RunOnceAsync(
            new ExecutionRequest(languageId, files.Select(f => new SourceFile(f.Path, f.Content)).ToList(), entryFile, IsolationLevel.OsSandbox, python, Go: goEnv, NuGet: nugetEnv, Maven: mavenEnv),
            stdin, ExerciseLimits.For(exercise?.Requires), ct).ConfigureAwait(false);
    }
}
