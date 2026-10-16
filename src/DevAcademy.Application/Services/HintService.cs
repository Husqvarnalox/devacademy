using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

public sealed record Hint(int Level, int Total, string Text);

/// <summary>Progressive content hints (level 1..n). The reference solution is a separate, explicit request.</summary>
public sealed class HintService(IContentRepository content, ISettingsRepository settings)
{
    public async Task<IReadOnlyList<Hint>> GetRevealedAsync(string exerciseId, CancellationToken ct = default)
    {
        var exercise = Require(exerciseId);
        var level = Math.Min(await settings.GetRevealedHintLevelAsync(exerciseId, ct), exercise.Hints.Count);
        return exercise.Hints.Take(level).Select((t, i) => new Hint(i + 1, exercise.Hints.Count, t)).ToList();
    }

    /// <summary>Reveals the next hint, or returns null when all hints are already revealed.</summary>
    public async Task<Hint?> RevealNextAsync(string exerciseId, CancellationToken ct = default)
    {
        var exercise = Require(exerciseId);
        var level = await settings.GetRevealedHintLevelAsync(exerciseId, ct);
        if (level >= exercise.Hints.Count) return null;
        await settings.SetRevealedHintLevelAsync(exerciseId, level + 1, ct);
        return new Hint(level + 1, exercise.Hints.Count, exercise.Hints[level]);
    }

    /// <summary>Reference solution of the given variant (null = default variant).</summary>
    public IReadOnlyList<ExerciseFile> GetSolution(string exerciseId, string? languageId = null) =>
        Require(exerciseId).ForVariant(languageId).Solution;

    private Exercise Require(string exerciseId) =>
        content.Current.FindExercise(exerciseId)?.Exercise
        ?? throw new ArgumentException($"Unknown exercise '{exerciseId}'", nameof(exerciseId));
}
