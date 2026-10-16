using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevAcademy.AI.Http;

internal sealed record ErrorInfo(string? Message, string? Type, string? Code);

/// <summary>Shared HTTP plumbing: auth, error mapping, key scrubbing. Keys are attached per request, never stored on the HttpClient.</summary>
public abstract class HttpModelProvider : IModelProvider
{
    protected HttpModelProvider(HttpClient http, string providerId, string displayName, Uri baseUri, bool requiresApiKey)
    {
        Http = http;
        ProviderId = providerId;
        DisplayName = displayName;
        RequiresApiKey = requiresApiKey;
        var s = baseUri.AbsoluteUri;
        BaseUri = s.EndsWith('/') ? baseUri : new Uri(s + "/");
    }

    protected HttpClient Http { get; }
    public Uri BaseUri { get; }
    public string ProviderId { get; }
    public string DisplayName { get; }
    public bool RequiresApiKey { get; }

    /// <summary>HttpClient for production: no automatic redirects (so credentials can never follow a redirect to another host).</summary>
    public static HttpClient CreateDefaultHttpClient() =>
        new(new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        { Timeout = TimeSpan.FromSeconds(60) };

    public abstract Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(string apiKey, CancellationToken ct = default);
    public abstract Task<CompletionResult> CreateCompletionAsync(CompletionRequest request, CancellationToken ct = default);
    public abstract IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(CompletionRequest request, CancellationToken ct = default);

    protected abstract void Authorize(HttpRequestMessage request, string apiKey);
    internal abstract ErrorInfo ParseError(JsonElement root);

    public virtual async Task<CredentialCheck> ValidateCredentialsAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            await GetAvailableModelsAsync(apiKey, ct).ConfigureAwait(false);
            return new CredentialCheck(true);
        }
        catch (AiProviderException e) when (e.Kind == AiErrorKind.InvalidKey)
        {
            return new CredentialCheck(false, e.Message);
        }
    }

    public virtual async Task<ModelTestResult> TestModelAsync(string apiKey, string modelId, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var result = await CreateCompletionAsync(new CompletionRequest
        {
            ApiKey = apiKey,
            ModelId = modelId,
            MaxOutputTokens = 16,
            Messages = [new ChatMessage(ChatRole.User, "Reply with the single word: OK")],
        }, ct).ConfigureAwait(false);
        return new ModelTestResult(true, result.Usage, DateTime.UtcNow - started);
    }

    // ---- plumbing -------------------------------------------------------------------------------------------------

    protected HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string apiKey, JsonNode? body = null)
    {
        EnsureKey(apiKey);
        var req = new HttpRequestMessage(method, uri);
        Authorize(req, apiKey);
        if (body is not null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        return req;
    }

    protected Uri Url(string relative) => new(BaseUri, relative);

    protected void EnsureKey(string apiKey)
    {
        if (RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
            throw new AiProviderException(AiErrorKind.InvalidKey, ProviderId, "API key is empty.");
    }

    /// <summary>Sends and returns the response only if successful (headers read; body may still be streaming). Otherwise throws a typed exception.</summary>
    protected async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string apiKey, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException(AiErrorKind.Timeout, ProviderId, "The request to the provider timed out.");
        }
        catch (HttpRequestException e)
        {
            throw new AiProviderException(AiErrorKind.Network, ProviderId, Scrub($"Network error: {e.Message}", apiKey), inner: e);
        }

        if (resp.IsSuccessStatusCode) return resp;

        string body;
        try { body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
        catch (Exception e) when (e is HttpRequestException or IOException) { body = ""; }
        var status = (int)resp.StatusCode;
        resp.Dispose();
        throw MapHttpError(status, body, apiKey);
    }

    protected async Task<JsonDocument> SendJsonAsync(HttpRequestMessage req, string apiKey, CancellationToken ct)
    {
        using var resp = await SendAsync(req, apiKey, ct).ConfigureAwait(false);
        try
        {
            await using var s = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(s, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException e)
        {
            throw new AiProviderException(AiErrorKind.ProviderUnavailable, ProviderId, "The provider returned a response that is not valid JSON.", inner: e);
        }
        catch (Exception e) when (e is HttpRequestException or IOException)
        {
            throw new AiProviderException(AiErrorKind.Network, ProviderId, Scrub($"Network error: {e.Message}", apiKey), inner: e);
        }
    }

    protected AiProviderException MapHttpError(int status, string body, string apiKey)
    {
        ErrorInfo info = new(null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            info = ParseError(doc.RootElement);
        }
        catch (JsonException) { }

        var message = info.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            var snippet = body.Length > 300 ? body[..300] : body;
            message = string.IsNullOrWhiteSpace(snippet) ? $"HTTP {status}" : $"HTTP {status}: {snippet.Trim()}";
        }
        return new AiProviderException(Classify(status, info), ProviderId, Scrub(message, apiKey), status);
    }

    internal AiProviderException MapStreamError(ErrorInfo info, int? status, string apiKey) =>
        new(Classify(status ?? 0, info), ProviderId, Scrub(info.Message ?? "The provider reported an error during streaming.", apiKey), status);

    /// <summary>Default status-based classification; providers refine using the error type/code.</summary>
    internal virtual AiErrorKind Classify(int status, ErrorInfo info) => status switch
    {
        401 => AiErrorKind.InvalidKey,
        402 => AiErrorKind.QuotaExceeded,
        403 => AiErrorKind.PermissionDenied,
        404 => AiErrorKind.ModelNotFound,
        408 or 504 => AiErrorKind.Timeout,
        429 => AiErrorKind.RateLimited,
        >= 500 => AiErrorKind.ProviderUnavailable,
        _ => AiErrorKind.BadRequest,
    };

    protected static string Scrub(string text, string apiKey) =>
        string.IsNullOrEmpty(apiKey) ? text : text.Replace(apiKey, "***", StringComparison.Ordinal);

    internal static async IAsyncEnumerable<SseEvent> ReadSseAsync(HttpResponseMessage resp, string apiKey, string providerId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var e = SseReader.ReadAsync(stream, ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                try
                {
                    if (!await e.MoveNextAsync().ConfigureAwait(false)) yield break;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException)
                {
                    throw new AiProviderException(AiErrorKind.Network, providerId, Scrub($"Connection lost while streaming: {ex.Message}", apiKey), inner: ex);
                }
                yield return e.Current;
            }
        }
        finally { await e.DisposeAsync().ConfigureAwait(false); }
    }

    // ---- JSON helpers ---------------------------------------------------------------------------------------------

    protected static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    protected static int? Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    protected static JsonElement? Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    protected static JsonElement? Arr(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v : null;

    protected static bool? Bool(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    protected static AiProviderException BadShape(string providerId, string what) =>
        new(AiErrorKind.ProviderUnavailable, providerId, $"Unexpected response from provider: {what}.");

    protected static string BuildSystemText(IEnumerable<ChatMessage> messages) =>
        string.Join("\n\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Content));
}
