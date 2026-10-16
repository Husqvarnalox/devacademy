using System.Text.Json;
using DevAcademy.AI.Http;

namespace DevAcademy.AI.Providers;

/// <summary>
/// OpenRouter: OpenAI-compatible chat completions plus a richer /models list (context_length, pricing in USD per token as
/// strings, architecture modalities, supported_parameters) and GET /key for credential checks.
/// Docs: openrouter.ai/docs/api-reference (list models, streaming, errors, limits).
/// </summary>
public sealed class OpenRouterProvider(HttpClient http, Uri? baseUri = null) : OpenAiProvider(http, new OpenAiProviderOptions
{
    ProviderId = ProviderIds.OpenRouter,
    DisplayName = "OpenRouter",
    BaseUri = baseUri ?? new Uri("https://openrouter.ai/api/v1/"),
    MaxTokensParameter = "max_tokens",
})
{
    internal override AiErrorKind Classify(int status, ErrorInfo info) => status switch
    {
        402 => AiErrorKind.QuotaExceeded,
        502 or 503 => AiErrorKind.ProviderUnavailable,
        _ => base.Classify(status, info),
    };

    public override async Task<CredentialCheck> ValidateCredentialsAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, Url("key"), apiKey);
            using var doc = await SendJsonAsync(req, apiKey, ct).ConfigureAwait(false);
            return new CredentialCheck(true);
        }
        catch (AiProviderException e) when (e.Kind == AiErrorKind.InvalidKey)
        {
            return new CredentialCheck(false, e.Message);
        }
    }

    protected override Uri? NextPage(JsonElement root) =>
        Obj(root, "links") is { } links ? SameHostLink(Str(links, "next")) : null;

    protected override ModelInfo? ParseModel(JsonElement m)
    {
        var id = Str(m, "id");
        if (id is null) return null;

        var supported = Arr(m, "supported_parameters")?.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);

        CapabilitySupport Param(params string[] names) =>
            supported is null ? CapabilitySupport.Unknown
            : names.Any(supported.Contains) ? CapabilitySupport.Supported : CapabilitySupport.NotSupported;

        var arch = Obj(m, "architecture");
        var inMods = arch is null ? null : Mods(arch.Value, "input_modalities");
        var outMods = arch is null ? null : Mods(arch.Value, "output_modalities");

        var caps = new ModelCapabilities(
            TextGeneration: outMods is null ? CapabilitySupport.Unknown : outMods.Contains("text") ? CapabilitySupport.Supported : CapabilitySupport.NotSupported,
            Streaming: CapabilitySupport.Unknown,
            ToolCalling: Param("tools"),
            StructuredOutput: Param("structured_outputs", "response_format"),
            MultimodalInput: inMods is null ? CapabilitySupport.Unknown : inMods.Any(x => x != "text") ? CapabilitySupport.Supported : CapabilitySupport.NotSupported,
            ReasoningControls: Param("reasoning", "reasoning_effort", "include_reasoning"));

        ModelPricing? pricing = null;
        if (Obj(m, "pricing") is { } p)
        {
            // OpenRouter uses "-1" for dynamically priced routers: Parse() yields null for negatives => no estimate.
            var prompt = ModelPricing.Parse(Str(p, "prompt"));
            var completion = ModelPricing.Parse(Str(p, "completion"));
            if (prompt is not null || completion is not null) pricing = new ModelPricing(prompt, completion);
        }

        var top = Obj(m, "top_provider");
        return new ModelInfo(id, Str(m, "name"), ProviderId, Int(m, "context_length"),
            top is null ? null : Int(top.Value, "max_completion_tokens"), caps, pricing,
            UnsupportedParameters: supported is not null && !supported.Contains("temperature") ? ["temperature"] : null);
    }

    private static HashSet<string>? Mods(JsonElement arch, string name) =>
        Arr(arch, name)?.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
}
