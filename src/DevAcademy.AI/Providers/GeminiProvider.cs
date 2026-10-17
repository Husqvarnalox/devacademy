using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevAcademy.AI.Http;

namespace DevAcademy.AI.Providers;

/// <summary>
/// Google Gemini API (v1beta). Docs: ai.google.dev/api/models (models.list), ai.google.dev/api/generate-content.
/// Key goes in the x-goog-api-key header (never in the query string).
/// </summary>
public sealed class GeminiProvider : HttpModelProvider
{
    public GeminiProvider(HttpClient http, Uri? baseUri = null)
        : base(http, ProviderIds.Gemini, "Google Gemini", baseUri ?? new Uri("https://generativelanguage.googleapis.com/v1beta/"), true) { }

    protected override void Authorize(HttpRequestMessage request, string apiKey) =>
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

    internal override ErrorInfo ParseError(JsonElement root)
    {
        var err = Obj(root, "error");
        if (err is null) return new ErrorInfo(null, null, null);
        string? reason = null;
        if (Arr(err.Value, "details") is { } details)
            foreach (var d in details.EnumerateArray())
                reason ??= Str(d, "reason");
        return new ErrorInfo(Str(err.Value, "message"), Str(err.Value, "status"), reason ?? (Int(err.Value, "code")?.ToString()));
    }

    internal override AiErrorKind Classify(int status, ErrorInfo info)
    {
        if (info.Code == "API_KEY_INVALID" || (info.Message?.Contains("API key not valid", StringComparison.OrdinalIgnoreCase) ?? false))
            return AiErrorKind.InvalidKey;
        return info.Type switch
        {
            "UNAUTHENTICATED" => AiErrorKind.InvalidKey,
            "PERMISSION_DENIED" => AiErrorKind.PermissionDenied,
            "NOT_FOUND" => AiErrorKind.ModelNotFound,
            // RESOURCE_EXHAUSTED covers both per-minute limits and exhausted quota; the API does not distinguish them reliably.
            "RESOURCE_EXHAUSTED" => AiErrorKind.RateLimited,
            "UNAVAILABLE" or "INTERNAL" => AiErrorKind.ProviderUnavailable,
            "DEADLINE_EXCEEDED" => AiErrorKind.Timeout,
            "INVALID_ARGUMENT" or "FAILED_PRECONDITION" => AiErrorKind.BadRequest,
            _ => base.Classify(status, info),
        };
    }

    private static string ModelPath(string modelId)
    {
        var id = modelId.Trim();
        var full = id.StartsWith("models/", StringComparison.Ordinal) || id.StartsWith("tunedModels/", StringComparison.Ordinal) ? id : "models/" + id;
        return string.Join('/', full.Split('/').Select(Uri.EscapeDataString));
    }

    public override async Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(string apiKey, CancellationToken ct = default)
    {
        var result = new List<ModelInfo>();
        string? token = null;
        for (var page = 0; page < 100; page++)
        {
            var url = Url("models?pageSize=1000" + (token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token)));
            using var req = CreateRequest(HttpMethod.Get, url, apiKey);
            using var doc = await SendJsonAsync(req, apiKey, ct).ConfigureAwait(false);
            if (Arr(doc.RootElement, "models") is { } models)
                foreach (var m in models.EnumerateArray())
                    if (ParseModel(m) is { } model) result.Add(model);
            token = Str(doc.RootElement, "nextPageToken");
            if (string.IsNullOrEmpty(token)) break;
        }
        return result;
    }

    private ModelInfo? ParseModel(JsonElement m)
    {
        var name = Str(m, "name");
        if (name is null) return null;
        var id = name.StartsWith("models/", StringComparison.Ordinal) ? name["models/".Length..] : name;

        var methods = Arr(m, "supportedGenerationMethods")?.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        var caps = new ModelCapabilities(
            TextGeneration: methods is null ? CapabilitySupport.Unknown : methods.Contains("generateContent") ? CapabilitySupport.Supported : CapabilitySupport.NotSupported,
            // streamGenerateContent is often not listed even though generateContent models stream: absence is "Unknown", not "NotSupported".
            Streaming: methods?.Contains("streamGenerateContent") == true ? CapabilitySupport.Supported : CapabilitySupport.Unknown,
            ReasoningControls: Bool(m, "thinking") == true ? CapabilitySupport.Supported : CapabilitySupport.Unknown);
        return new ModelInfo(id, Str(m, "displayName"), ProviderId, Int(m, "inputTokenLimit"), Int(m, "outputTokenLimit"), caps);
    }

    private static JsonObject BuildBody(CompletionRequest r)
    {
        var contents = new JsonArray();
        foreach (var m in r.Messages.Where(m => m.Role != ChatRole.System))
            contents.Add(new JsonObject
            {
                ["role"] = m.Role == ChatRole.Assistant ? "model" : "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = m.Content }),
            });
        var gen = new JsonObject { ["maxOutputTokens"] = r.MaxOutputTokens };
        if (r.Temperature is { } t && r.KnownModel?.UnsupportedParameters?.Contains("temperature") != true) gen["temperature"] = t;
        var body = new JsonObject { ["contents"] = contents, ["generationConfig"] = gen };
        var system = BuildSystemText(r.Messages);
        if (system.Length > 0) body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) };
        return body;
    }

    private static TokenUsage? ReadUsage(JsonElement root)
    {
        var u = Obj(root, "usageMetadata");
        if (u is null) return null;
        var prompt = Int(u.Value, "promptTokenCount");
        var cand = Int(u.Value, "candidatesTokenCount");
        var thoughts = Int(u.Value, "thoughtsTokenCount");
        int? output = cand is null && thoughts is null ? null : (cand ?? 0) + (thoughts ?? 0); // thinking tokens are billed as output
        return prompt is null && output is null ? null : new TokenUsage(prompt, output);
    }

    private (string Text, string? Finish) ReadCandidate(JsonElement root)
    {
        var sb = new StringBuilder();
        string? finish = null;
        if (Arr(root, "candidates") is { } cands)
            foreach (var cand in cands.EnumerateArray())
            {
                finish = Str(cand, "finishReason");
                if (Obj(cand, "content") is { } content && Arr(content, "parts") is { } parts)
                    foreach (var p in parts.EnumerateArray())
                        if (Bool(p, "thought") != true && Str(p, "text") is { } t) sb.Append(t);
                break;
            }
        return (sb.ToString(), finish);
    }

    private AiProviderException? BlockedPrompt(JsonElement root, string apiKey)
    {
        var reason = Obj(root, "promptFeedback") is { } pf ? Str(pf, "blockReason") : null;
        return reason is null ? null : new AiProviderException(AiErrorKind.BadRequest, ProviderId, EngineMessages.PromptBlocked(Scrub(reason, apiKey)));
    }

    public override async Task<CompletionResult> CreateCompletionAsync(CompletionRequest request, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, Url(ModelPath(request.ModelId) + ":generateContent"), request.ApiKey, BuildBody(request));
        using var doc = await SendJsonAsync(req, request.ApiKey, ct).ConfigureAwait(false);
        if (BlockedPrompt(doc.RootElement, request.ApiKey) is { } blocked) throw blocked;
        var (text, finish) = ReadCandidate(doc.RootElement);
        return new CompletionResult(text, ReadUsage(doc.RootElement), finish);
    }

    public override async IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, Url(ModelPath(request.ModelId) + ":streamGenerateContent?alt=sse"), request.ApiKey, BuildBody(request));
        req.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
        using var resp = await SendAsync(req, request.ApiKey, ct).ConfigureAwait(false);

        TokenUsage? usage = null;
        string? finish = null;
        await foreach (var ev in ReadSseAsync(resp, request.ApiKey, ProviderId, ct).ConfigureAwait(false))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(ev.Data); }
            catch (JsonException) { continue; }
            using (doc)
            {
                var root = doc.RootElement;
                if (Obj(root, "error") is not null)
                    throw MapStreamError(ParseError(root), Obj(root, "error") is { } e ? Int(e, "code") : null, request.ApiKey);
                if (BlockedPrompt(root, request.ApiKey) is { } blocked) throw blocked;
                var (text, f) = ReadCandidate(root);
                finish = f ?? finish;
                usage = ReadUsage(root) ?? usage; // usageMetadata is cumulative; the last one wins
                if (text.Length > 0) yield return new CompletionChunk(text);
            }
        }
        if (usage is not null || finish is not null) yield return new CompletionChunk(null, usage, finish);
    }
}
