using DevAcademy.Application.Python;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Evaluation;

/// <summary>Picks the evaluator for the exercise's <see cref="EvaluationKind"/>. Default isolation: OsSandbox.</summary>
public sealed class ExerciseEvaluationService
{
    private readonly Dictionary<EvaluationKind, IExerciseEvaluator> _evaluators;
    private readonly IExerciseEnvironmentResolver? _environment;

    public ExerciseEvaluationService(ICodeRunner runner, IExerciseEnvironmentResolver? environment = null)
        : this([new IoEvaluator(runner), new HarnessEvaluator(runner), new CommandEvaluator(runner)], environment) { }

    public ExerciseEvaluationService(IEnumerable<IExerciseEvaluator> evaluators, IExerciseEnvironmentResolver? environment = null)
    {
        _evaluators = evaluators.ToDictionary(e => e.Kind);
        _environment = environment;
    }

    /// <param name="languageId">Variant to evaluate (null = the exercise's default variant). Tests are shared by all variants.</param>
    public async Task<EvaluationReport> EvaluateAsync(Exercise exercise, IReadOnlyList<ExerciseFile> learnerFiles,
        CancellationToken ct = default, IsolationLevel isolation = IsolationLevel.OsSandbox, string? languageId = null)
    {
        exercise = exercise.ForVariant(languageId);
        if (!_evaluators.TryGetValue(exercise.Evaluation, out var evaluator))
            throw new NotSupportedException($"No evaluator registered for {exercise.Evaluation}");

        PythonRunSelection? python = null;
        if (_environment is not null && exercise.LanguageId == "python")
        {
            // Requirements and accelerator are checked before anything of the learner's runs. A missing package is a setup problem, not a wrong answer.
            ExerciseRunPlan plan = await _environment.ResolveAsync(exercise, ct).ConfigureAwait(false);
            if (plan.IsBlocked)
                return EvaluationReport.Failure(Verdict.RuntimeMissing, plan.BlockedMessage!, isolation) with { LanguageId = exercise.LanguageId };
            python = plan.Python;
        }
        return await evaluator.EvaluateAsync(exercise, learnerFiles, isolation, ct, python).ConfigureAwait(false);
    }
}
