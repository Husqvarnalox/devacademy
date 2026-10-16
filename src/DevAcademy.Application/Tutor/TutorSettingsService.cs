using System.Globalization;
using DevAcademy.AI;
using DevAcademy.Application.Ports;

namespace DevAcademy.Application.Tutor;

/// <summary>Reads/writes AI settings through ISettingsRepository. Contains no secrets: API keys live in the OS secret store only.</summary>
public sealed class TutorSettingsService(ISettingsRepository settings)
{
    public async Task<TutorSettings> LoadAsync(CancellationToken ct = default)
    {
        // Individual reads: GetAllAsync would also pull the (large) cached model lists stored in the same table.
        var all = new Dictionary<string, string?>();
        async Task Load(string k) => all[k] = await settings.GetAsync(k, ct);
        var keys = new List<string>
        {
            TutorSettingsKeys.AiEnabled, TutorSettingsKeys.DefaultProvider, TutorSettingsKeys.DefaultModel, TutorSettingsKeys.AllowCode,
            TutorSettingsKeys.MonthlyLimitUsd, TutorSettingsKeys.SessionLimitUsd, TutorSettingsKeys.MaxOutputTokens,
        };
        foreach (var sc in Enum.GetValues<TutorScenario>()) { keys.Add(TutorSettingsKeys.ScenarioProvider(sc)); keys.Add(TutorSettingsKeys.ScenarioModel(sc)); }
        foreach (var k in keys) await Load(k);
        string? Get(string k) => all.GetValueOrDefault(k);

        ModelAssignment? Pair(string? p, string? m) => !string.IsNullOrWhiteSpace(p) && !string.IsNullOrWhiteSpace(m) ? new ModelAssignment(p, m) : null;

        var per = new Dictionary<TutorScenario, ModelAssignment>();
        foreach (var s in Enum.GetValues<TutorScenario>())
            if (Pair(Get(TutorSettingsKeys.ScenarioProvider(s)), Get(TutorSettingsKeys.ScenarioModel(s))) is { } a) per[s] = a;

        return new TutorSettings(
            AiEnabled: Get(TutorSettingsKeys.AiEnabled) == "true",
            Default: Pair(Get(TutorSettingsKeys.DefaultProvider), Get(TutorSettingsKeys.DefaultModel)),
            PerScenario: per,
            AllowCode: Get(TutorSettingsKeys.AllowCode) != "false",
            MonthlyLimitUsd: Dec(Get(TutorSettingsKeys.MonthlyLimitUsd)),
            SessionLimitUsd: Dec(Get(TutorSettingsKeys.SessionLimitUsd)),
            MaxOutputTokens: int.TryParse(Get(TutorSettingsKeys.MaxOutputTokens), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null);
    }

    public Task SetAiEnabledAsync(bool enabled, CancellationToken ct = default) =>
        settings.SetAsync(TutorSettingsKeys.AiEnabled, enabled ? "true" : "false", ct);

    public Task SetAllowCodeAsync(bool allow, CancellationToken ct = default) =>
        settings.SetAsync(TutorSettingsKeys.AllowCode, allow ? "true" : "false", ct);

    public async Task SetDefaultAsync(ModelAssignment assignment, CancellationToken ct = default)
    {
        await settings.SetAsync(TutorSettingsKeys.DefaultProvider, assignment.ProviderId, ct);
        await settings.SetAsync(TutorSettingsKeys.DefaultModel, assignment.ModelId, ct);
    }

    /// <summary>Assigns provider+model to one scenario. Null clears the override (the global default applies).</summary>
    public async Task SetScenarioAsync(TutorScenario scenario, ModelAssignment? assignment, CancellationToken ct = default)
    {
        await settings.SetAsync(TutorSettingsKeys.ScenarioProvider(scenario), assignment?.ProviderId ?? "", ct);
        await settings.SetAsync(TutorSettingsKeys.ScenarioModel(scenario), assignment?.ModelId ?? "", ct);
    }

    /// <summary>Null clears the limit.</summary>
    public Task SetMonthlyLimitAsync(decimal? usd, CancellationToken ct = default) =>
        settings.SetAsync(TutorSettingsKeys.MonthlyLimitUsd, usd?.ToString(CultureInfo.InvariantCulture) ?? "", ct);

    public Task SetSessionLimitAsync(decimal? usd, CancellationToken ct = default) =>
        settings.SetAsync(TutorSettingsKeys.SessionLimitUsd, usd?.ToString(CultureInfo.InvariantCulture) ?? "", ct);

    private static decimal? Dec(string? s) =>
        decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : null;
}

/// <summary>Persists model lists in settings (no secrets in them).</summary>
public sealed class SettingsModelCacheStore(ISettingsRepository settings) : IModelCacheStore
{
    public async Task<CachedModelList?> LoadAsync(string providerId, CancellationToken ct = default)
    {
        var json = await settings.GetAsync(TutorSettingsKeys.ModelCache(providerId), ct);
        return string.IsNullOrEmpty(json) ? null : ModelCacheJson.Deserialize(json);
    }

    public Task SaveAsync(CachedModelList list, CancellationToken ct = default) =>
        settings.SetAsync(TutorSettingsKeys.ModelCache(list.ProviderId), ModelCacheJson.Serialize(list), ct);
}
