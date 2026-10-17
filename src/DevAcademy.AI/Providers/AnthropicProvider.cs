using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevAcademy.AI.Http;

namespace DevAcademy.AI.Providers;

/// <summary>
/// Anthropic Messages API. Docs: platform.claude.com/docs/en/api/models-list, /api/messages-streaming (build-with-claude/streaming), /api/errors.
/// Auth: x-api-key + anthropic-version. Model list pagination: has_more / last_id -> after_id.
/// </summary>
public sealed class AnthropicProvider : HttpModelProvider
{
    public const string ApiVersion = "2023-06-01";

    public AnthropicProvider(HttpClient http, Uri? baseUri = null)
        : base(http, ProviderIds.Anthropic, "Anthropic", baseUri ?? new Uri("https://api.anthropic.com/v1/"), true) { }

    protected override void Authorize(HttpRequestMessage request, string apiKey)
    {
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
    }

    internal override ErrorInfo ParseError(JsonElement root)
    {
        var err = Obj(root, "error");
        return err is null ? new ErrorInfo(null, null, null) : new ErrorInfo(Str(err.Value, "message"), Str(err.Value, "type"), null);
    }

    internal override AiErrorKind Classify(int status, ErrorInfo info) => info.Type switch
    {
        "authentication_error" => AiErrorKind.InvalidKey,
        "permission_error" => AiErrorKind.PermissionDenied,
        "not_found_error" => AiErrorKind.ModelNotFound,
        "billing_error" => AiErrorKind.QuotaExceeded,
        "rate_limit_error" => AiErrorKind.RateLimited,
        "overloaded_error" or "api_error" => AiErrorKind.ProviderUnavailable,
        "timeout_error" => AiErrorKind.Timeout,
        "invalid_request_error" or "request_too_large" => AiErrorKind.BadRequest,
        _ => status == 529 ? AiErrorKind.ProviderUnavailable : base.Classify(status, info),
    };

    public override async Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(string apiKey, CancellationToken ct = default)
    {
        var result = new List<ModelInfo>();
        string? after = null;
        for (var page = 0; page < 100; page++)
        {
            var url = Url("models?limit=1000" + (after is null ? "" : "&after_id=" + Uri.EscapeDataString(after)));
            using var req = CreateRequest(HttpMethod.Get, url, apiKey);
            using var doc = await SendJsonAsync(req, apiKey, ct).ConfigureAwait(false);
            var data = Arr(doc.RootElement, "data") ?? throw BadShape(ProviderId, "missing 'data' array");
            foreach (var m in data.EnumerateArray())
                if (ParseModel(m) is { } model) result.Add(model);

            var more = Bool(doc.RootElement, "has_more") == true;
            var last = Str(doc.RootElement, "last_id");
            if (!more || last is null || last == after) break;
            after = last;
        }
        return result;
    }

    private ModelInfo? ParseModel(JsonElement m)
    {
        var id = Str(m, "id");
        if (id is null) return null;

        var caps = ModelCapabilities.AllUnknown;
        if (Obj(m, "capabilities") is { } c)
        {
            bool? Sup(JsonElement parent, string name) => Obj(parent, name) is { } o ? Bool(o, "supported") : null;
            var effort = Sup(c, "effort");
            var thinking = Sup(c, "thinking");
            bool? reasoning = effort == true || thinking == true ? true : effort is null && thinking is null ? null : false;
            // text generation / streaming / tool calling are not reported by the Models API => Unknown.
            caps = new ModelCapabilities(
                StructuredOutput: ModelCapabilities.From(Sup(c, "structured_outputs")),
                MultimodalInput: ModelCapabilities.From(Sup(c, "image_input")),
                ReasoningControls: ModelCapabilities.From(reasoning));
        }
        return new ModelInfo(id, Str(m, "display_name"), ProviderId, Int(m, "max_input_tokens"), Int(m, "max_tokens"), caps);
    }

    // ---- messages -------------------------------------------------------------------------------------------------

    private JsonObject BuildBody(CompletionRequest r, bool stream)
    {
        var messages = new JsonArray();
        foreach (var m in r.Messages.Where(m => m.Role != ChatRole.System))
            messages.Add(new JsonObject { ["role"] = m.Role == ChatRole.Assistant ? "assistant" : "user", ["content"] = m.Content });

        var body = new JsonObject { ["model"] = r.ModelId, ["max_tokens"] = r.MaxOutputTokens, ["messages"] = messages };
        var system = BuildSystemText(r.Messages);
        if (system.Length > 0) body["system"] = system;
        if (r.Temperature is { } t && r.KnownModel?.UnsupportedParameters?.Contains("temperature") != true) body["temperature"] = t;
        if (stream) body["stream"] = true;
        return body;
    }

    public override async Task<CompletionResult> CreateCompletionAsync(CompletionRequest request, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, Url("messages"), request.ApiKey, BuildBody(request, false));
        using var doc = await SendJsonAsync(req, request.ApiKey, ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        if (Arr(doc.RootElement, "content") is { } content)
            foreach (var block in content.EnumerateArray())
                if (Str(block, "type") == "text") sb.Append(Str(block, "text"));
        TokenUsage? usage = null;
        if (Obj(doc.RootElement, "usage") is { } u) usage = new TokenUsage(Int(u, "input_tokens"), Int(u, "output_tokens"));
        return new CompletionResult(sb.ToString(), usage, Str(doc.RootElement, "stop_reason"));
    }

    public override async IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, Url("messages"), request.ApiKey, BuildBody(request, true));
        req.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
        using var resp = await SendAsync(req, request.ApiKey, ct).ConfigureAwait(false);

        int? inputTokens = null;
        var finished = false;
        await foreach (var ev in ReadSseAsync(resp, request.ApiKey, ProviderId, ct).ConfigureAwait(false))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(ev.Data); }
            catch (JsonException) { continue; }
            using (doc)
            {
                var root = doc.RootElement;
                switch (Str(root, "type") ?? ev.Event)
                {
                    case "message_start":
                        if (Obj(root, "message") is { } msg && Obj(msg, "usage") is { } mu) inputTokens = Int(mu, "input_tokens");
                        break;
                    case "content_block_delta":
                        if (Obj(root, "delta") is { } d && Str(d, "type") == "text_delta" && Str(d, "text") is { Length: > 0 } text)
                            yield return new CompletionChunk(text);
                        break; // thinking_delta / input_json_delta / signature_delta are not shown to the learner
                    case "message_delta":
                        {
                            var stop = Obj(root, "delta") is { } md ? Str(md, "stop_reason") : null;
                            TokenUsage? usage = null;
                            if (Obj(root, "usage") is { } u) usage = new TokenUsage(Int(u, "input_tokens") ?? inputTokens, Int(u, "output_tokens"));
                            yield return new CompletionChunk(null, usage, stop);
                            break;
                        }
                    case "message_stop":
                        finished = true;
                        yield break;
                    case "error":
                        throw MapStreamError(ParseError(root), null, request.ApiKey);
                        // ping, content_block_start/stop and unknown future events are ignored by design
                }
            }
        }
        if (!finished)
            throw new AiProviderException(AiErrorKind.Network, ProviderId, EngineMessages.StreamEnded());
    }
}
