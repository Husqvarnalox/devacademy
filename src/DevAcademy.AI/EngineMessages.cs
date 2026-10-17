using DevAcademy.Domain;

namespace DevAcademy.AI;

/// <summary>
/// Catalog of the user-visible messages the AI layer itself produces (not those the provider returns, which stay verbatim). Each <c>const</c> is a
/// resource key a UI translates; the factory beside it holds the English template that stays the fallback. Details passed in have already had the
/// API key scrubbed out.
/// </summary>
public static class EngineMessages
{
    public const string StreamEndedKey = "Engine_Ai_StreamEnded";
    public const string PromptBlockedKey = "Engine_Ai_PromptBlocked";
    public const string EmptyKeyKey = "Engine_Ai_EmptyKey";
    public const string RequestTimedOutKey = "Engine_Ai_RequestTimedOut";
    public const string InvalidJsonKey = "Engine_Ai_InvalidJson";
    public const string NetworkErrorKey = "Engine_Ai_NetworkError";
    public const string ConnectionLostKey = "Engine_Ai_ConnectionLost";

    public static LocalizedText StreamEnded() => LocalizedText.Create(StreamEndedKey, "The stream ended before the response was complete.");
    public static LocalizedText PromptBlocked(string reason) => LocalizedText.Create(PromptBlockedKey, "The prompt was blocked by the provider ({0}).", LocalizedText.Literal(reason));
    public static LocalizedText EmptyKey() => LocalizedText.Create(EmptyKeyKey, "API key is empty.");
    public static LocalizedText RequestTimedOut() => LocalizedText.Create(RequestTimedOutKey, "The request to the provider timed out.");
    public static LocalizedText InvalidJson() => LocalizedText.Create(InvalidJsonKey, "The provider returned a response that is not valid JSON.");
    public static LocalizedText NetworkError(string detail) => LocalizedText.Create(NetworkErrorKey, "Network error: {0}", LocalizedText.Literal(detail));
    public static LocalizedText ConnectionLost(string detail) => LocalizedText.Create(ConnectionLostKey, "Connection lost while streaming: {0}", LocalizedText.Literal(detail));
}
