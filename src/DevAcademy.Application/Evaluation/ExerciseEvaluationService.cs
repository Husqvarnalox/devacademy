using DevAcademy.Application.Go;
using DevAcademy.Application.Node;
using DevAcademy.Application.NuGet;
using DevAcademy.Application.Python;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Evaluation;

/// <summary>Picks the evaluator for the exercise's <see cref="EvaluationKind"/>. Default isolation: OsSandbox.</summary>
public sealed class ExerciseEvaluationService
{
    private readonly Dictionary<EvaluationKind, IExerciseEvaluator> _evaluators;
    private readonly IExerciseEnvironmentResolver? _environment;
    private readonly INodeEnvironmentResolver? _node;
    private readonly IGoEnvironmentResolver? _go;
    private readonly INuGetEnvironmentResolver? _nuget;

    public ExerciseEvaluationService(ICodeRunner runner, IExerciseEnvironmentResolver? environment = null, INodeEnvironmentResolver? node = null,
        IGoEnvironmentResolver? go = null, INuGetEnvironmentResolver? nuget = null)
        : this([new IoEvaluator(runner), new HarnessEvaluator(runner), new CommandEvaluator(runner)], environment, node, go, nuget) { }

    public ExerciseEvaluationService(IEnumerable<IExerciseEvaluator> evaluators, IExerciseEnvironmentResolver? environment = null, INodeEnvironmentResolver? node = null,
        IGoEnvironmentResolver? go = null, INuGetEnvironmentResolver? nuget = null)
    {
        _node = node;
        _go = go;
        _nuget = nuget;
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
        NodeRunSelection? nodeEnv = null;
        if (exercise.HasNodeRequirements && (_node is not null || exercise.NeedsNodeEnvironment))
        {
            // Without a node service (CLI without --node-env) a version-only requirement is not checked; the run itself shows an old Node.
            if (_node is null)
                return EvaluationReport.Failure(Verdict.RuntimeMissing, "Для упражнения нужны npm-пакеты курсов, но окружение не подключено (make node-env).", isolation) with { LanguageId = exercise.LanguageId };
            NodeRunPlan nodePlan = await _node.ResolveAsync(exercise, ct).ConfigureAwait(false);
            if (nodePlan.IsBlocked)
                return EvaluationReport.Failure(Verdict.RuntimeMissing, nodePlan.BlockedMessage!, isolation) with { LanguageId = exercise.LanguageId };
            nodeEnv = nodePlan.Node;
        }
        GoRunSelection? goEnv = null;
        if (exercise.HasGoRequirements && (_go is not null || exercise.NeedsGoEnvironment))
        {
            // Without a Go service (CLI without --go-env) a version-only requirement is not checked; the build itself shows an old Go.
            if (_go is null)
                return EvaluationReport.Failure(Verdict.RuntimeMissing, "Для упражнения нужны модули Go курса, но окружение не подключено (make go-env).", isolation) with { LanguageId = exercise.LanguageId };
            GoRunPlan goPlan = await _go.ResolveAsync(exercise, ct).ConfigureAwait(false);
            if (goPlan.IsBlocked)
                return EvaluationReport.Failure(Verdict.RuntimeMissing, goPlan.BlockedMessage!, isolation) with { LanguageId = exercise.LanguageId };
            goEnv = goPlan.Go;
        }
        NuGetRunSelection? nugetEnv = null;
        if (exercise.NeedsNuGetEnvironment)
        {
            if (_nuget is null)
                return EvaluationReport.Failure(Verdict.RuntimeMissing, "Для упражнения нужны пакеты NuGet курса, но окружение не подключено (make nuget-env).", isolation) with { LanguageId = exercise.LanguageId };
            NuGetRunPlan nugetPlan = await _nuget.ResolveAsync(exercise, ct).ConfigureAwait(false);
            if (nugetPlan.IsBlocked)
                return EvaluationReport.Failure(Verdict.RuntimeMissing, nugetPlan.BlockedMessage!, isolation) with { LanguageId = exercise.LanguageId };
            nugetEnv = nugetPlan.NuGet;
        }
        return await evaluator.EvaluateAsync(exercise, learnerFiles, isolation, ct, python, nodeEnv, goEnv, nugetEnv).ConfigureAwait(false);
    }
}
