using DevAcademy.Application.Ports;
using DevAcademy.Domain;

namespace DevAcademy.Application.Services;

/// <summary>
/// Editor drafts per exercise + language + file, and the learner's last chosen language per exercise.
/// A classic single-language exercise stores drafts under its bare id (see <see cref="Exercise.DraftKey"/>),
/// so drafts written before variants existed stay valid. Progress is not touched: it is keyed by lesson.
/// </summary>
public sealed class DraftService(IDraftRepository drafts, ISettingsRepository settings)
{
    private static string LanguageKey(Exercise ex) => "exercise.language." + ex.Id;

    /// <summary>The variant's starter files with saved drafts laid over them.</summary>
    public async Task<IReadOnlyList<ExerciseFile>> LoadFilesAsync(Exercise exercise, string languageId, CancellationToken ct = default)
    {
        var variant = exercise.ForVariant(languageId);
        var saved = await drafts.GetAllAsync(exercise.DraftKey(variant.LanguageId), ct);
        return variant.Starter.Select(f => saved.TryGetValue(f.Path, out var d) ? f with { Content = d } : f).ToList();
    }

    public Task SaveAsync(Exercise exercise, string languageId, string path, string content, CancellationToken ct = default) =>
        drafts.SaveAsync(exercise.DraftKey(exercise.ForVariant(languageId).LanguageId), path, content, ct);

    /// <summary>Deletes the drafts of ONE language; other languages keep theirs.</summary>
    public Task ResetAsync(Exercise exercise, string languageId, CancellationToken ct = default) =>
        drafts.DeleteAsync(exercise.DraftKey(exercise.ForVariant(languageId).LanguageId), ct);

    /// <summary>Last language the learner worked in, if it is still a variant of the exercise; otherwise the default variant.</summary>
    public async Task<string> GetLanguageAsync(Exercise exercise, CancellationToken ct = default)
    {
        var saved = await settings.GetAsync(LanguageKey(exercise), ct);
        return saved is not null && exercise.FindVariant(saved) is { } v ? v.LanguageId : exercise.AllVariants[0].LanguageId;
    }

    public Task SetLanguageAsync(Exercise exercise, string languageId, CancellationToken ct = default) =>
        settings.SetAsync(LanguageKey(exercise), exercise.ForVariant(languageId).LanguageId, ct);
}
