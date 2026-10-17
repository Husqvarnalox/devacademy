using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Tests;

public class ExerciseLimitsTests
{
    [Fact]
    public void Without_memory_requirement_uses_the_default_run_limits()
    {
        Assert.Equal(ResourceLimits.DefaultRun, ExerciseLimits.For(null));
        Assert.Equal(ResourceLimits.DefaultRun, ExerciseLimits.For(new ExerciseRequirements("3.12", ["torch"])));
    }

    [Fact]
    public void Memory_requirement_raises_only_the_memory_limit()
    {
        ResourceLimits limits = ExerciseLimits.For(new ExerciseRequirements("3.12", ["torch"], MemoryMb: 512));
        Assert.Equal(512L * 1024 * 1024, limits.MaxMemoryBytes);
        Assert.Equal(ResourceLimits.DefaultRun with { MaxMemoryBytes = limits.MaxMemoryBytes }, limits);
    }

    [Theory]
    [InlineData(64, ExerciseRequirements.MinMemoryMb)]
    [InlineData(100_000, ExerciseRequirements.MaxMemoryMb)]
    public void Memory_requirement_is_clamped_to_the_allowed_range(int requested, int expectedMb) =>
        Assert.Equal(expectedMb * 1024L * 1024, ExerciseLimits.For(new ExerciseRequirements("3.12", [], MemoryMb: requested)).MaxMemoryBytes);
}
