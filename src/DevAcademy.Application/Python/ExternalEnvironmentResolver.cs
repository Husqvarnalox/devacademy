using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Python;

/// <summary>
/// Runs every Python exercise that declares <c>requires</c> with one fixed environment (the CLI's <c>--python-env</c>), using the same
/// importlib.metadata probe and <see cref="RequirementChecker"/> as the desktop. Unlike the desktop it never falls back to another
/// interpreter: the Python version is checked against the interpreter that will actually run the code.
/// </summary>
public sealed class ExternalEnvironmentResolver(PythonEnvironmentManager environments, PythonRunSelection selection) : IExerciseEnvironmentResolver
{
    public Task<PythonRunSelection?> GetActiveSelectionAsync(CancellationToken ct) => Task.FromResult<PythonRunSelection?>(selection);

    public async Task<ExerciseRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct)
    {
        if (exercise.LanguageId != "python" || !exercise.HasRequirements) return ExerciseRunPlan.Default;
        string? problem = await CheckAsync(exercise.Requires!, ct).ConfigureAwait(false);
        return problem is null ? new ExerciseRunPlan(selection) : new ExerciseRunPlan(selection, problem);
    }

    /// <summary>Null when the environment satisfies <paramref name="requires"/>, otherwise a one-line explanation.</summary>
    public async Task<string?> CheckAsync(ExerciseRequirements requires, CancellationToken ct)
    {
        PythonProbeResult probe = await environments.ProbeAsync(selection.InterpreterPath,
            RequirementChecker.ParsePackages(requires).Select(p => p.Name).ToList(), ct).ConfigureAwait(false);
        RequirementCheckResult check = RequirementChecker.Check(requires, probe);
        if (check.Satisfied) return null;
        return check.ProbeError is not null
            ? $"cannot probe the Python environment '{selection.EnvironmentPath}': {check.ProbeError}"
            : $"environment '{selection.EnvironmentPath}' does not satisfy: " + string.Join("; ", check.Missing.Select(m => $"{m.Requirement} ({m.Reason})"));
    }
}
