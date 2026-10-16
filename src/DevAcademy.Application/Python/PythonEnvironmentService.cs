using DevAcademy.Application.Ports;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Python;

/// <summary>
/// What may happen to an exercise run given its <c>requires</c>/<c>accelerator</c> and the active Python environment.
/// <see cref="BlockedMessage"/> non-null means "do not run"; <see cref="Python"/> is the environment to run with (null = the system interpreter).
/// </summary>
public sealed record ExerciseRunPlan(PythonRunSelection? Python, string? BlockedMessage = null, string? Notice = null, bool AcceleratorBlocked = false)
{
    public bool IsBlocked => BlockedMessage is not null;
    public static ExerciseRunPlan Default { get; } = new((PythonRunSelection?)null);
}

/// <summary>Decides how an exercise runs with respect to Python environments and accelerators. Reads only; never installs.</summary>
public interface IExerciseEnvironmentResolver
{
    Task<ExerciseRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct);

    /// <summary>The environment chosen in Settings, for plain "Run" of free code (null when none).</summary>
    Task<PythonRunSelection?> GetActiveSelectionAsync(CancellationToken ct);
}

/// <summary>
/// Application face of the Python Environment Manager and the ML Runtime Manager: environments, the active-environment setting,
/// requirement checks and accelerator gating. Installation is exposed as plan + execute so the UI can ask for consent in between.
/// </summary>
public sealed class PythonEnvironmentService(
    PythonEnvironmentManager environments,
    PythonInterpreterDetector detector,
    MlRuntimeManager ml,
    ISettingsRepository settings) : IExerciseEnvironmentResolver
{
    public const string ActiveEnvironmentKey = "python.activeEnvironment";

    private readonly object _gate = new();
    private Task<IReadOnlyList<PythonInterpreter>>? _interpreters;
    private Task<HardwareReport>? _hardware;
    private readonly Dictionary<string, Task<IReadOnlyList<AcceleratorCapability>>> _frameworkProbes = new(StringComparer.Ordinal);

    public PythonEnvironmentManager Environments => environments;
    public MlRuntimeManager Ml => ml;

    // ---------------------------------------------------------------- interpreters and environments

    public Task<IReadOnlyList<PythonInterpreter>> GetInterpretersAsync(bool refresh, CancellationToken ct)
    {
        Task<IReadOnlyList<PythonInterpreter>> task;
        lock (_gate)
        {
            if (refresh || _interpreters is null) _interpreters = Task.Run(() => detector.DetectAsync(CancellationToken.None));
            task = _interpreters;
        }
        return task.WaitAsync(ct);
    }

    public IReadOnlyList<ManagedPythonEnvironment> ListEnvironments() => environments.List();

    public async Task<PythonEnvResult> CreateEnvironmentAsync(string name, string interpreterPath, PythonEnvTool tool, CancellationToken ct) =>
        await environments.CreateAsync(name, interpreterPath, tool, ct).ConfigureAwait(false);

    public async Task<PythonEnvResult> DeleteEnvironmentAsync(string name, CancellationToken ct)
    {
        PythonEnvResult r = environments.Delete(name);
        if (r.Ok && string.Equals(await GetActiveNameAsync(ct).ConfigureAwait(false), name, StringComparison.Ordinal))
            await SetActiveAsync(null, ct).ConfigureAwait(false);
        lock (_gate) _frameworkProbes.Clear();
        return r;
    }

    public Task<(IReadOnlyList<PythonPackage> Packages, string? Error)> ListPackagesAsync(string envName, CancellationToken ct)
    {
        ManagedPythonEnvironment? env = environments.Get(envName);
        return env is null
            ? Task.FromResult<(IReadOnlyList<PythonPackage>, string?)>(([], $"Окружение «{envName}» не найдено."))
            : environments.ListPackagesAsync(env.Path, ct);
    }

    public Task<PythonEnvironmentValidation> ValidateAsync(string envName, CancellationToken ct)
    {
        ManagedPythonEnvironment? env = environments.Get(envName);
        return env is null
            ? Task.FromResult(new PythonEnvironmentValidation(false, null, null, $"Окружение «{envName}» не найдено."))
            : environments.ValidateAsync(env.Path, ct);
    }

    // ---------------------------------------------------------------- active environment

    public async Task<string?> GetActiveNameAsync(CancellationToken ct)
    {
        string? name = await settings.GetAsync(ActiveEnvironmentKey, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public async Task<ManagedPythonEnvironment?> GetActiveAsync(CancellationToken ct)
    {
        string? name = await GetActiveNameAsync(ct).ConfigureAwait(false);
        return name is null ? null : environments.Get(name);
    }

    /// <summary>Null clears the choice (runs use the system interpreter again).</summary>
    public async Task SetActiveAsync(string? name, CancellationToken ct)
    {
        if (name is not null && environments.Get(name) is null)
            throw new ArgumentException($"Окружение «{name}» не найдено.", nameof(name));
        await settings.SetAsync(ActiveEnvironmentKey, name ?? "", ct).ConfigureAwait(false);
    }

    public async Task<PythonRunSelection?> GetActiveSelectionAsync(CancellationToken ct)
    {
        ManagedPythonEnvironment? env = await GetActiveAsync(ct).ConfigureAwait(false);
        return env is not null && File.Exists(env.InterpreterPath) ? new PythonRunSelection(env.InterpreterPath, env.Path) : null;
    }

    // ---------------------------------------------------------------- install (plan, then execute)

    /// <summary>The command that would run plus accelerator notes. Nothing is executed and nothing is downloaded.</summary>
    public async Task<(PythonInstallPlan? Plan, IReadOnlyList<PackageAcceleratorNote> Notes, string? Error)> PlanInstallAsync(
        string envName, PythonInstallRequest request, CancellationToken ct)
    {
        (PythonInstallPlan? plan, string? error) = await environments.BuildInstallPlanAsync(envName, request, ct).ConfigureAwait(false);
        if (plan is null) return (null, [], error);
        HardwareReport hw = await GetHardwareAsync(false, ct).ConfigureAwait(false);
        return (plan, ml.DescribePackages(plan.PackageNames, hw), null);
    }

    public async Task<PythonInstallResult> InstallAsync(PythonInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        PythonInstallResult r = await environments.InstallAsync(plan, onLine, ct).ConfigureAwait(false);
        lock (_gate) _frameworkProbes.Clear(); // what is installed changed: probes are stale
        return r;
    }

    // ---------------------------------------------------------------- ML

    public Task<HardwareReport> GetHardwareAsync(bool refresh, CancellationToken ct)
    {
        Task<HardwareReport> task;
        lock (_gate)
        {
            if (refresh || _hardware is null) _hardware = Task.Run(() => ml.DetectHardwareAsync(CancellationToken.None));
            task = _hardware;
        }
        return task.WaitAsync(ct);
    }

    /// <summary>Framework probes inside the named environment (cached until packages change or <paramref name="refresh"/>).</summary>
    public async Task<IReadOnlyList<AcceleratorCapability>> ProbeFrameworksAsync(string envName, bool refresh, CancellationToken ct)
    {
        ManagedPythonEnvironment? env = environments.Get(envName);
        if (env is null) return [new AcceleratorCapability("env", "Окружение", CapabilityStatus.Unknown, $"Окружение «{envName}» не найдено.")];
        HardwareReport hw = await GetHardwareAsync(false, ct).ConfigureAwait(false);
        Task<IReadOnlyList<AcceleratorCapability>> task;
        lock (_gate)
        {
            if (refresh || !_frameworkProbes.TryGetValue(env.Path, out task!))
                _frameworkProbes[env.Path] = task = Task.Run(() => ml.ProbeFrameworksAsync(env.InterpreterPath, hw, CancellationToken.None));
        }
        return await task.WaitAsync(ct).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- exercise gating

    public async Task<ExerciseRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct)
    {
        if (exercise.LanguageId != "python") return ExerciseRunPlan.Default;

        ManagedPythonEnvironment? env = await GetActiveAsync(ct).ConfigureAwait(false);
        PythonRunSelection? selection = env is not null && File.Exists(env.InterpreterPath) ? new(env.InterpreterPath, env.Path) : null;
        string? notice = null;

        if (exercise.HasRequirements)
        {
            string? interpreter = selection?.InterpreterPath
                ?? (await GetInterpretersAsync(false, ct).ConfigureAwait(false)).FirstOrDefault()?.Path;
            if (interpreter is null)
                return new ExerciseRunPlan(selection, "Не найден ни один интерпретатор Python. Установите Python или создайте окружение (Настройки → Среды выполнения).");

            ExerciseRequirements req = exercise.Requires!;
            PythonProbeResult probe = await environments.ProbeAsync(interpreter,
                RequirementChecker.ParsePackages(req).Select(p => p.Name).ToList(), ct).ConfigureAwait(false);
            RequirementCheckResult check = RequirementChecker.Check(req, probe);
            if (!check.Satisfied)
                return new ExerciseRunPlan(selection, Explain(req, check, env));
        }

        if (exercise.Accelerator != AcceleratorNeed.None)
        {
            HardwareReport hw = await GetHardwareAsync(false, ct).ConfigureAwait(false);
            IReadOnlyList<AcceleratorCapability>? frameworks = env is null ? null : await ProbeFrameworksAsync(env.Name, false, ct).ConfigureAwait(false);
            AcceleratorAssessment a = AcceleratorAssessment.Assess(hw, frameworks);
            if (exercise.Accelerator == AcceleratorNeed.Required && !a.Usable)
                return new ExerciseRunPlan(selection, "Для этого упражнения нужен ускоритель (GPU), но он недоступен. " + a.Explanation, AcceleratorBlocked: true);
            notice = a.Usable
                ? "Ускоритель доступен: " + a.Explanation
                : "Упражнение выполнится на CPU. " + a.Explanation;
        }
        return new ExerciseRunPlan(selection, null, notice);
    }

    internal static string Explain(ExerciseRequirements req, RequirementCheckResult check, ManagedPythonEnvironment? env)
    {
        if (check.ProbeError is not null)
            return $"Не удалось проверить окружение Python: {check.ProbeError}";
        var lines = new List<string>
        {
            "Для этого упражнения не хватает: " + string.Join("; ", check.Missing.Select(m => $"{m.Requirement} ({m.Reason})")) + ".",
        };
        if (check.MissingPackages.Count > 0)
        {
            string target = env is null ? "создайте окружение и сделайте его активным" : $"окружение «{env.Name}»";
            lines.Add($"Установите пакеты: Настройки → Среды выполнения → Python-окружения → «Установить пакеты…» ({target}). Приложение ничего не устанавливает само.");
        }
        return string.Join(" ", lines);
    }
}
