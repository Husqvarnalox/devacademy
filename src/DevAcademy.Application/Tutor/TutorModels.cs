using DevAcademy.AI;
using DevAcademy.Application.Evaluation;
using DevAcademy.Domain;

namespace DevAcademy.Application.Tutor;

public enum TutorScenario
{
    Explain, Debug, Hint, CodeReview, AlgorithmExplanation, LanguageComparison, ProjectMentor, InterviewSimulator, LearningAdaptation,
}

/// <summary>Which pieces of context may leave the machine. Shown to the learner before sending.</summary>
public sealed record TutorContextOptions(bool IncludeLesson = true, bool IncludeCode = true, bool IncludeResults = true, bool IncludePreviousAttempts = false);

/// <param name="HintLevel">1..4, used by <see cref="TutorScenario.Hint"/>: 1 direction, 2 locate the problem, 3 concrete, 4 partial explanation.</param>
/// <param name="CurrentFiles">The learner's current editor files (sent only if code is included).</param>
/// <param name="LastReport">Latest evaluation of the learner's code; hidden tests are reduced to pass/fail.</param>
/// <param name="RunOutput">Output/compile errors of a plain Run (no judging).</param>
/// <param name="History">Earlier turns of this conversation (user/assistant only).</param>
/// <param name="Options">Null = defaults (everything on except previous attempts); code is additionally subject to the global switch.</param>
public sealed record TutorRequest(
    TutorScenario Scenario,
    string UserMessage,
    string? LessonId = null,
    string? ExerciseId = null,
    IReadOnlyList<ExerciseFile>? CurrentFiles = null,
    EvaluationReport? LastReport = null,
    string? RunOutput = null,
    int HintLevel = 1,
    IReadOnlyList<ChatMessage>? History = null,
    TutorContextOptions? Options = null);

/// <summary>Exactly what will be sent to the provider (Messages), for the preview dialog.</summary>
public sealed record TutorPayload(
    TutorScenario Scenario,
    string ProviderId,
    string ModelId,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<string> IncludedSections,
    int MaxOutputTokens)
{
    public int ApproxInputChars => Messages.Sum(m => m.Content.Length);
}

public sealed record TutorResponse(
    string Text,
    TutorScenario Scenario,
    string ProviderId,
    string ModelId,
    TokenUsage? Usage,
    decimal? ApproxCostUsd,
    string? FinishReason);

public sealed record ModelAssignment(string ProviderId, string ModelId);

public sealed record TutorSettings(
    bool AiEnabled,
    ModelAssignment? Default,
    IReadOnlyDictionary<TutorScenario, ModelAssignment> PerScenario,
    bool AllowCode,
    decimal? MonthlyLimitUsd,
    decimal? SessionLimitUsd,
    int? MaxOutputTokens)
{
    public ModelAssignment? For(TutorScenario s) => PerScenario.TryGetValue(s, out var a) ? a : Default;
}

public static class TutorSettingsKeys
{
    public const string AiEnabled = "ai.enabled";
    public const string DefaultProvider = "ai.default.provider";
    public const string DefaultModel = "ai.default.model";
    public const string AllowCode = "ai.context.allow_code";
    public const string MonthlyLimitUsd = "ai.limit.monthly_usd";
    public const string SessionLimitUsd = "ai.limit.session_usd";
    public const string MaxOutputTokens = "ai.max_output_tokens";
    /// <summary>Base URL of the user's OpenAI-compatible endpoint (no secrets). Read by the composition root.</summary>
    public const string OpenAiCompatibleBaseUrl = "ai.openai_compatible.base_url";
    public const string ModelCachePrefix = "ai.models.";

    public static string ScenarioProvider(TutorScenario s) => $"ai.scenario.{s}.provider";
    public static string ScenarioModel(TutorScenario s) => $"ai.scenario.{s}.model";
    public static string ModelCache(string providerId) => ModelCachePrefix + providerId;
}

public enum TutorRefusalReason { AiDisabled, NoModelConfigured, UnknownProvider, SecretStoreUnavailable, NoApiKey, SpendLimit, EmptyMessage }

/// <summary>Raised before anything is sent. Nothing was transmitted and nothing is logged.</summary>
public sealed class TutorRefusedException(TutorRefusalReason reason, string message) : Exception(message)
{
    public TutorRefusalReason Reason { get; } = reason;
}
