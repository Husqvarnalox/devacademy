using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Python;

/// <summary>One requirement that the interpreter does not satisfy.</summary>
public sealed record MissingRequirement(string Requirement, string Reason);

public sealed record RequirementCheckResult(IReadOnlyList<MissingRequirement> Missing, string? InterpreterVersion, string? ProbeError)
{
    public bool Satisfied => Missing.Count == 0 && ProbeError is null;
    public IReadOnlyList<string> MissingPackages => Missing.Where(m => !m.Reason.StartsWith(PythonVersionReason, StringComparison.Ordinal)).Select(m => m.Requirement).ToList();
    public const string PythonVersionReason = "python";
}

/// <summary>
/// Compares an exercise's <c>requires</c> with what an interpreter really has. Pure: the version of Python and the installed
/// distributions come from <see cref="PythonProbeResult"/> (an importlib.metadata probe, so nothing is imported).
/// </summary>
public static class RequirementChecker
{
    public static IReadOnlyList<PythonPackageSpec> ParsePackages(ExerciseRequirements requires)
    {
        var list = new List<PythonPackageSpec>();
        foreach (string text in requires.Packages)
            if (PythonPackageSpec.TryParse(text, out PythonPackageSpec spec, out _)) list.Add(spec);
        return list;
    }

    public static RequirementCheckResult Check(ExerciseRequirements requires, PythonProbeResult probe)
    {
        if (!probe.Ok) return new RequirementCheckResult([], null, probe.Error ?? "интерпретатор не отвечает");
        var missing = new List<MissingRequirement>();

        if (!string.IsNullOrWhiteSpace(requires.Python))
        {
            if (!PythonSpecifierSet.TryParse(requires.Python, out PythonSpecifierSet set, out string? err))
                missing.Add(new($"python{requires.Python}", $"недопустимое условие версии Python: {err}"));
            else if (!PythonVersion.TryParse(probe.Version, out PythonVersion v) || !set.Satisfies(v))
                missing.Add(new($"python{requires.Python}", $"{RequirementCheckResult.PythonVersionReason}: установлен Python {probe.Version}"));
        }

        foreach (PythonPackageSpec spec in ParsePackages(requires))
        {
            if (!probe.Installed.TryGetValue(spec.Name, out string? installed) || installed.Length == 0)
            {
                missing.Add(new(spec.Text, "не установлен"));
                continue;
            }
            if (PythonVersion.TryParse(installed, out PythonVersion iv) && !spec.Versions.Satisfies(iv))
                missing.Add(new(spec.Text, $"установлена версия {installed}"));
        }
        return new RequirementCheckResult(missing, probe.Version, null);
    }
}

/// <summary>What the machine and the chosen environment can do for an exercise that needs an accelerator.</summary>
public sealed record AcceleratorAssessment(bool Usable, string Explanation)
{
    /// <summary>
    /// Frameworks decide when any is installed in the environment (hardware alone cannot run a model); otherwise the hardware report does.
    /// </summary>
    public static AcceleratorAssessment Assess(HardwareReport hardware, IReadOnlyList<AcceleratorCapability>? frameworks)
    {
        var installed = frameworks?.Where(f => f.Status != CapabilityStatus.NotInstalled).ToList() ?? [];
        if (installed.Count > 0)
        {
            AcceleratorCapability? ok = installed.FirstOrDefault(f => f.IsAvailable);
            if (ok is not null) return new(true, ok.Message);
            return new(false, string.Join(" ", installed.Select(f => f.Message)));
        }
        AcceleratorCapability? hw = hardware.Capabilities.FirstOrDefault(c => c.IsAvailable);
        if (hw is not null)
            return new(true, hw.Message + (frameworks is null ? "" : " В окружении нет ML-фреймворков — установите нужные пакеты."));
        return new(false, "Ускорители не обнаружены: " + string.Join(" ", hardware.Capabilities.Select(c => c.Message)));
    }
}
