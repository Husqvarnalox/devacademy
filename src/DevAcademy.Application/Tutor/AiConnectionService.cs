using DevAcademy.AI;
using DevAcademy.Application.Ports;

namespace DevAcademy.Application.Tutor;

public sealed record ProviderStatus(string ProviderId, string DisplayName, bool RequiresApiKey, bool HasKey);

/// <summary>Provider setup flow for the settings UI: save key (secret store only), validate, list models, test a model.</summary>
public sealed class AiConnectionService(ProviderRegistry registry, ISecretStore secrets, ModelCatalog catalog, IAiRequestRepository log, IClock clock)
{
    public bool SecretStoreAvailable => secrets.IsAvailable;
    public string? SecretStoreUnavailableReason => secrets.UnavailableReason;

    public IReadOnlyList<ProviderStatus> GetProviders() =>
        registry.Providers.Select(p => new ProviderStatus(p.ProviderId, p.DisplayName, p.RequiresApiKey, secrets.IsAvailable && HasKey(p.ProviderId))).ToList();

    public bool HasKey(string providerId) => secrets.IsAvailable && !string.IsNullOrEmpty(secrets.Get(providerId));

    /// <summary>Stores the key in the OS secret store. Throws SecretStoreException when unavailable: there is no plaintext fallback.</summary>
    public void SaveKey(string providerId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is empty.", nameof(apiKey));
        if (!secrets.IsAvailable) throw new SecretStoreException($"Cannot save the API key: {secrets.UnavailableReason}");
        _ = registry.Get(providerId);
        secrets.Set(providerId, apiKey.Trim());
    }

    public void RemoveKey(string providerId)
    {
        if (secrets.IsAvailable) secrets.Delete(providerId);
    }

    public Task<CredentialCheck> ValidateAsync(string providerId, CancellationToken ct = default) =>
        registry.Get(providerId).ValidateCredentialsAsync(KeyFor(providerId), ct);

    /// <summary>Explicit refresh (the only way the model list is re-fetched).</summary>
    public Task<CachedModelList> RefreshModelsAsync(string providerId, CancellationToken ct = default) =>
        catalog.RefreshAsync(providerId, KeyFor(providerId), ct);

    public Task<CachedModelList?> GetCachedModelsAsync(string providerId, CancellationToken ct = default) => catalog.GetCachedAsync(providerId, ct);

    /// <summary>Tiny real request (max 16 output tokens). Logged like any request, with scenario "ModelTest".</summary>
    public async Task<ModelTestResult> TestModelAsync(string providerId, string modelId, CancellationToken ct = default)
    {
        var started = clock.UtcNow;
        try
        {
            var r = await registry.Get(providerId).TestModelAsync(KeyFor(providerId), modelId, ct);
            var cost = CostEstimator.Estimate((await catalog.FindAsync(providerId, modelId, ct))?.Pricing, r.Usage);
            await log.AddAsync(new AiRequestRecord(0, providerId, modelId, "ModelTest", started, clock.UtcNow, r.Usage?.InputTokens, r.Usage?.OutputTokens, cost, "ok", null), CancellationToken.None);
            return r;
        }
        catch (AiProviderException e)
        {
            await log.AddAsync(new AiRequestRecord(0, providerId, modelId, "ModelTest", started, clock.UtcNow, null, null, null, "error", e.Kind.ToString()), CancellationToken.None);
            throw;
        }
    }

    private string KeyFor(string providerId)
    {
        var provider = registry.Get(providerId);
        if (!secrets.IsAvailable)
        {
            if (provider.RequiresApiKey) throw new SecretStoreException($"The OS secret store is unavailable: {secrets.UnavailableReason}");
            return "";
        }
        return secrets.Get(providerId) ?? "";
    }
}
