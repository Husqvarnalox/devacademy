using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DevAcademy.AI.Http;

namespace DevAcademy.AI.Providers;

public sealed record OpenAiProviderOptions
{
    public string ProviderId { get; init; } = ProviderIds.OpenAI;
    public string DisplayName { get; init; } = "OpenAI";
    public Uri BaseUri { get; init; } = new("https://api.openai.com/v1/");
    public bool RequiresApiKey { get; init; } = true;
    /// <summary>"max_completion_tokens" for api.openai.com (max_tokens is deprecated and rejected by reasoning models); "max_tokens" for most compatible servers.</summary>
    public string MaxTokensParameter { get; init; } = "max_completion_tokens";
    /// <summary>Sends stream_options.include_usage so the last chunk carries token usage.</summary>
    public bool RequestStreamUsage { get; init; } = true;
}

/// <summary>
/// OpenAI Chat Completions (/v1/chat/completions). Chosen over the Responses API because the same wire format is
/// spoken by OpenRouter, Ollama, LM Studio and most "OpenAI-compatible" servers, so one client serves all of them;
/// the Responses API adds nothing this app needs (single-turn text tutoring, no built-in tools).
/// Docs: OpenAI API reference (openapi spec, github.com/openai/openai-openapi).
/// </summary>
public class OpenAiProvider : HttpModelProvider
{
    private static readonly Regex TemperatureLockedModels = new(@"^(o\d|gpt-5)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly OpenAiProviderOptions _options;

    public OpenAiProvider(HttpClient http, OpenAiProviderOptions? options = null)
        : this(http, options ?? new OpenAiProviderOptions(), true) { }

    private OpenAiProvider(HttpClient http, OpenAiProviderOptions options, bool resolved)
        : base(http, options.ProviderId, options.DisplayName, options.BaseUri, options.RequiresApiKey)
    {
        _options = options;
    }

    protected override void Authorize(HttpRequestMessage request, string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
    }

    internal override ErrorInfo ParseError(JsonElement root)
    {
        var err = Obj(root, "error");
        if (err is null) return new ErrorInfo(null, null, null);
        var e = err.Value;
        string? code = Str(e, "code");
        if (code is null && e.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number) code = c.GetRawText();
        return new ErrorInfo(Str(e, "message"), Str(e, "type"), code);
    }

    internal override AiErrorKind Classify(int status, ErrorInfo info)
    {
        if (info.Type == "insufficient_quota" || info.Code == "insufficient_quota") return AiErrorKind.QuotaExceeded;
        if (info.Code is "invalid_api_key") return AiErrorKind.InvalidKey;
        if (info.Code is "model_not_found") return AiErrorKind.ModelNotFound;
        return base.Classify(status, info);
    }

    // ---- models ---------------------------------------------------------------------------------------------------

    public override async Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(string apiKey, CancellationToken ct = default)
    {
        var result = new List<ModelInfo>();
        var next = Url("models");
        for (var page = 0; page < 100 && next is not null; page++)
        {
            using var req = CreateRequest(HttpMethod.Get, next, apiKey);
            using var doc = await SendJsonAsync(req, apiKey, ct).ConfigureAwait(false);
            var data = Arr(doc.RootElement, "data") ?? throw BadShape(ProviderId, "missing 'data' array");
            foreach (var m in data.EnumerateArray())
            {
                var model = ParseModel(m);
                if (model is not null) result.Add(model);
            }
            next = NextPage(doc.RootElement);
        }
        return result;
    }

    /// <summary>Follows an absolute next-page link only if it stays on the provider's own host (the key is attached to it).</summary>
    protected virtual Uri? NextPage(JsonElement root) => null;

    protected Uri? SameHostLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        if (!Uri.TryCreate(BaseUri, link, out var uri)) return null;
        return uri.Scheme == BaseUri.Scheme && uri.Host == BaseUri.Host && uri.Port == BaseUri.Port ? uri : null;
    }

    /// <summary>OpenAI's model object has only id/created/owned_by: no context window, no capabilities, no prices => all Unknown.</summary>
    protected virtual ModelInfo? ParseModel(JsonElement m)
    {
        var id = Str(m, "id");
        return id is null ? null : new ModelInfo(id, null, ProviderId, null, null, ModelCapabilities.AllUnknown);
    }

    // ---- chat -----------------------------------------------------------------------------------------------------

    protected virtual bool SupportsTemperature(CompletionRequest r)
    {
        if (r.KnownModel?.UnsupportedParameters?.Contains("temperature") == true) return false;
        // Documented behaviour of OpenAI reasoning models (o-series, gpt-5*): only the default temperature is accepted.
        if (ProviderId == ProviderIds.OpenAI && TemperatureLockedModels.IsMatch(r.ModelId)) return false;
        return true;
    }

    private JsonObject BuildBody(CompletionRequest r, bool stream)
    {
        var messages = new JsonArray();
        foreach (var m in r.Messages)
            messages.Add(new JsonObject { ["role"] = m.Role.ToString().ToLowerInvariant(), ["content"] = m.Content });

        var body = new JsonObject { ["model"] = r.ModelId, ["messages"] = messages, [_options.MaxTokensParameter] = r.MaxOutputTokens };
        if (r.Temperature is { } t && SupportsTemperature(r)) body["temperature"] = t;
        if (stream)
        {
            body["stream"] = true;
            if (_options.RequestStreamUsage) body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }
        return body;
    }

    private static TokenUsage? ReadUsage(JsonElement root)
    {
        var u = Obj(root, "usage");
        if (u is null) return null;
        var p = Int(u.Value, "prompt_tokens");
        var c = Int(u.Value, "completion_tokens");
        return p is null && c is null ? null : new TokenUsage(p, c);
    }

    public override async Task<CompletionResult> CreateCompletionAsync(CompletionRequest request, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, Url("chat/completions"), request.ApiKey, BuildBody(request, false));
        using var doc = await SendJsonAsync(req, request.ApiKey, ct).ConfigureAwait(false);
        ThrowIfErrorBody(doc.RootElement, request.ApiKey);
        var choices = Arr(doc.RootElement, "choices") ?? throw BadShape(ProviderId, "missing 'choices'");
        string text = "", finish = "";
        foreach (var choice in choices.EnumerateArray())
        {
            var message = Obj(choice, "message");
            text = message is null ? "" : Str(message.Value, "content") ?? "";
            finish = Str(choice, "finish_reason") ?? "";
            break;
        }
        return new CompletionResult(text, ReadUsage(doc.RootElement), string.IsNullOrEmpty(finish) ? null : finish);
    }

    private void ThrowIfErrorBody(JsonElement root, string apiKey)
    {
        if (Obj(root, "error") is null) return;
        var info = ParseError(root);
        int? status = int.TryParse(info.Code, out var s) ? s : null;
        throw MapStreamError(info, status, apiKey);
    }

    public override async IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(CompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, Url("chat/completions"), request.ApiKey, BuildBody(request, true));
        req.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
        using var resp = await SendAsync(req, request.ApiKey, ct).ConfigureAwait(false);

        await foreach (var ev in ReadSseAsync(resp, request.ApiKey, ProviderId, ct).ConfigureAwait(false))
        {
            if (ev.Data.Trim() == "[DONE]") yield break;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(ev.Data); }
            catch (JsonException) { continue; } // tolerate non-JSON keep-alive payloads
            using (doc)
            {
                ThrowIfErrorBody(doc.RootElement, request.ApiKey);
                string? text = null, finish = null;
                if (Arr(doc.RootElement, "choices") is { } choices)
                    foreach (var choice in choices.EnumerateArray())
                    {
                        if (Obj(choice, "delta") is { } delta) text = Str(delta, "content");
                        finish = Str(choice, "finish_reason");
                        break;
                    }
                var usage = ReadUsage(doc.RootElement);
                if (!string.IsNullOrEmpty(text) || usage is not null || finish is not null)
                    yield return new CompletionChunk(string.IsNullOrEmpty(text) ? null : text, usage, finish);
            }
        }
    }
}

/// <summary>Local or third-party OpenAI-compatible server (Ollama, LM Studio, vLLM, ...). Custom base URL; key optional.</summary>
public sealed class OpenAiCompatibleProvider(HttpClient http, Uri baseUri, string providerId = ProviderIds.OpenAICompatible,
    string displayName = "OpenAI-compatible", bool requiresApiKey = false, string maxTokensParameter = "max_tokens")
    : OpenAiProvider(http, new OpenAiProviderOptions
    {
        ProviderId = providerId,
        DisplayName = displayName,
        BaseUri = baseUri,
        RequiresApiKey = requiresApiKey,
        MaxTokensParameter = maxTokensParameter,
    });
