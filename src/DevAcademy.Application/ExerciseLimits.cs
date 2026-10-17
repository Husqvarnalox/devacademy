using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application;

/// <summary>Run limits of an exercise: the default run limits, with the memory limit raised only when the content declares <c>requires.memoryMb</c> (clamped to the allowed range).</summary>
public static class ExerciseLimits
{
    public static ResourceLimits For(ExerciseRequirements? requires) =>
        requires?.MemoryMb is { } mb
            ? ResourceLimits.DefaultRun with { MaxMemoryBytes = Math.Clamp(mb, ExerciseRequirements.MinMemoryMb, ExerciseRequirements.MaxMemoryMb) * 1024L * 1024 }
            : ResourceLimits.DefaultRun;
}
