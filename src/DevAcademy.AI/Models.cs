using System.Globalization;

namespace DevAcademy.AI;

public static class ProviderIds
{
    public const string OpenAI = "openai";
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";
    public const string OpenRouter = "openrouter";
    public const string OpenAICompatible = "openai-compatible";
}

/// <summary>Whether a model capability is supported. Unknown = the provider's API did not say; never guessed.</summary>
public enum CapabilitySupport { Unknown = 0, Supported, NotSupported }

public sealed record ModelCapabilities(
    CapabilitySupport TextGeneration = CapabilitySupport.Unknown,
    CapabilitySupport Streaming = CapabilitySupport.Unknown,
    CapabilitySupport ToolCalling = CapabilitySupport.Unknown,
    CapabilitySupport StructuredOutput = CapabilitySupport.Unknown,
    CapabilitySupport MultimodalInput = CapabilitySupport.Unknown,
    CapabilitySupport ReasoningControls = CapabilitySupport.Unknown)
{
    public static ModelCapabilities AllUnknown { get; } = new();

    internal static CapabilitySupport From(bool? value) =>
        value is null ? CapabilitySupport.Unknown : value.Value ? CapabilitySupport.Supported : CapabilitySupport.NotSupported;
}

/// <summary>Published prices in USD per single token (as returned by the provider API). Null members = not published.</summary>
public sealed record ModelPricing(decimal? PromptUsdPerToken, decimal? CompletionUsdPerToken)
{
    public bool IsKnown => PromptUsdPerToken is not null && CompletionUsdPerToken is not null;

    internal static decimal? Parse(string? s) =>
        decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0 ? d : null;
}

/// <param name="UnsupportedParameters">Request parameters the provider's API explicitly says this model lacks (e.g. "temperature"). Empty when unknown.</param>
public sealed record ModelInfo(
    string Id,
    string? DisplayName,
    string ProviderId,
    int? ContextWindow,
    int? MaxOutputTokens,
    ModelCapabilities Capabilities,
    ModelPricing? Pricing = null,
    IReadOnlyList<string>? UnsupportedParameters = null);

public enum ChatRole { System, User, Assistant }

public sealed record ChatMessage(ChatRole Role, string Content);

/// <summary>Provider-agnostic completion request. ApiKey is excluded from ToString.</summary>
public sealed class CompletionRequest
{
    public required string ApiKey { get; init; }
    /// <summary>Any model id string (manual entry is first-class; there is no whitelist).</summary>
    public required string ModelId { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public int MaxOutputTokens { get; init; } = 1024;
    /// <summary>Sent only when set.</summary>
    public double? Temperature { get; init; }
    /// <summary>Optional discovered model metadata; used only to avoid sending parameters the model is known not to support.</summary>
    public ModelInfo? KnownModel { get; init; }

    public override string ToString() => $"CompletionRequest(model={ModelId}, messages={Messages.Count})";
}

public sealed record TokenUsage(int? InputTokens, int? OutputTokens);

public sealed record CompletionResult(string Text, TokenUsage? Usage, string? FinishReason);

/// <summary>One streamed piece. Text deltas have Text; the last chunks may carry Usage and/or FinishReason.</summary>
public sealed record CompletionChunk(string? Text = null, TokenUsage? Usage = null, string? FinishReason = null);

public sealed record CredentialCheck(bool Valid, string? Detail = null);

public sealed record ModelTestResult(bool Success, TokenUsage? Usage, TimeSpan Latency);

public enum AiErrorKind
{
    InvalidKey, PermissionDenied, ModelNotFound, RateLimited, QuotaExceeded,
    ProviderUnavailable, BadRequest, Network, Timeout,
}

public sealed class AiProviderException(AiErrorKind kind, string providerId, string message, int? httpStatus = null, Exception? inner = null)
    : Exception(message, inner)
{
    public AiErrorKind Kind { get; } = kind;
    public string ProviderId { get; } = providerId;
    public int? HttpStatus { get; } = httpStatus;
}

public static class CostEstimator
{
    /// <summary>Approximate USD = tokens x published price. Null when prices or token counts are unavailable.</summary>
    public static decimal? Estimate(ModelPricing? pricing, TokenUsage? usage)
    {
        if (pricing is null || usage is null) return null;
        if (usage.InputTokens is null && usage.OutputTokens is null) return null;
        if (usage.InputTokens is not null && pricing.PromptUsdPerToken is null) return null;
        if (usage.OutputTokens is not null && pricing.CompletionUsdPerToken is null) return null;
        return (usage.InputTokens ?? 0) * (pricing.PromptUsdPerToken ?? 0m) + (usage.OutputTokens ?? 0) * (pricing.CompletionUsdPerToken ?? 0m);
    }
}
