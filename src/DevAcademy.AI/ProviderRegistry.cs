using System.Text.Json;
using DevAcademy.AI.Http;
using DevAcademy.AI.Providers;

namespace DevAcademy.AI;

/// <summary>Providers by id. Custom OpenAI-compatible endpoints are registered with their own id and base URL.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IModelProvider> _providers = new(StringComparer.Ordinal);

    public IReadOnlyList<IModelProvider> Providers => _providers.Values.ToList();

    public void Register(IModelProvider provider) => _providers[provider.ProviderId] = provider;

    public IModelProvider? Find(string providerId) => _providers.GetValueOrDefault(providerId);

    public IModelProvider Get(string providerId) =>
        Find(providerId) ?? throw new KeyNotFoundException($"Unknown AI provider '{providerId}'");

    /// <summary>OpenAI, Anthropic, Gemini, OpenRouter on their official base URLs. Pass an HttpClient for tests.</summary>
    public static ProviderRegistry CreateDefault(HttpClient? http = null)
    {
        http ??= HttpModelProvider.CreateDefaultHttpClient();
        var r = new ProviderRegistry();
        r.Register(new OpenAiProvider(http));
        r.Register(new AnthropicProvider(http));
        r.Register(new GeminiProvider(http));
        r.Register(new OpenRouterProvider(http));
        return r;
    }

    /// <summary>Registers a local/third-party OpenAI-compatible endpoint (e.g. Ollama http://localhost:11434/v1).</summary>
    public IModelProvider AddOpenAiCompatible(HttpClient http, Uri baseUri, string providerId = ProviderIds.OpenAICompatible,
        string displayName = "OpenAI-compatible", bool requiresApiKey = false)
    {
        var p = new OpenAiCompatibleProvider(http, baseUri, providerId, displayName, requiresApiKey);
        Register(p);
        return p;
    }
}

public sealed record CachedModelList(string ProviderId, DateTimeOffset FetchedAt, IReadOnlyList<ModelInfo> Models);

/// <summary>Optional persistence of the model list (no secrets in it).</summary>
public interface IModelCacheStore
{
    Task<CachedModelList?> LoadAsync(string providerId, CancellationToken ct = default);
    Task SaveAsync(CachedModelList list, CancellationToken ct = default);
}

public static class ModelCacheJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Web);
    public static string Serialize(CachedModelList list) => JsonSerializer.Serialize(list, Options);
    public static CachedModelList? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<CachedModelList>(json, Options); }
        catch (JsonException) { return null; }
    }
}

/// <summary>Model list cache: in memory plus optional store. Refreshed ONLY by an explicit RefreshAsync call.</summary>
public sealed class ModelCatalog(ProviderRegistry registry, IModelCacheStore? store = null, TimeProvider? time = null)
{
    private readonly Dictionary<string, CachedModelList> _memory = new(StringComparer.Ordinal);
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<CachedModelList?> GetCachedAsync(string providerId, CancellationToken ct = default)
    {
        if (_memory.TryGetValue(providerId, out var hit)) return hit;
        if (store is null) return null;
        var loaded = await store.LoadAsync(providerId, ct).ConfigureAwait(false);
        if (loaded is not null) _memory[providerId] = loaded;
        return loaded;
    }

    public async Task<CachedModelList> RefreshAsync(string providerId, string apiKey, CancellationToken ct = default)
    {
        var models = await registry.Get(providerId).GetAvailableModelsAsync(apiKey, ct).ConfigureAwait(false);
        var list = new CachedModelList(providerId, _time.GetUtcNow(), models);
        _memory[providerId] = list;
        if (store is not null) await store.SaveAsync(list, ct).ConfigureAwait(false);
        return list;
    }

    public async Task<ModelInfo?> FindAsync(string providerId, string modelId, CancellationToken ct = default) =>
        (await GetCachedAsync(providerId, ct).ConfigureAwait(false))?.Models.FirstOrDefault(m => m.Id == modelId);
}
