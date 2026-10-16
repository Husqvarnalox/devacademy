using System.Runtime.CompilerServices;
using DevAcademy.AI;
using DevAcademy.Application.Ports;

namespace DevAcademy.Application.Tutor;

/// <summary>
/// Sends tutor requests. Text in, text out: it never marks anything solved, never runs commands and never writes files.
/// No fallback to another model/provider ever happens. Only metadata is logged (no prompt or response text).
/// </summary>
public sealed class TutorService
{
    private static readonly IReadOnlyDictionary<TutorScenario, int> DefaultMaxTokens = new Dictionary<TutorScenario, int>
    {
        [TutorScenario.Hint] = 600,
        [TutorScenario.Debug] = 1000,
        [TutorScenario.Explain] = 1200,
        [TutorScenario.CodeReview] = 1500,
        [TutorScenario.AlgorithmExplanation] = 1500,
        [TutorScenario.LanguageComparison] = 1200,
        [TutorScenario.ProjectMentor] = 1200,
        [TutorScenario.InterviewSimulator] = 800,
        [TutorScenario.LearningAdaptation] = 1000,
    };

    private readonly ProviderRegistry _registry;
    private readonly ISecretStore _secrets;
    private readonly TutorSettingsService _settings;
    private readonly IAiRequestRepository _log;
    private readonly ModelCatalog _catalog;
    private readonly TutorContextBuilder _builder;
    private readonly IClock _clock;

    public TutorService(ProviderRegistry registry, ISecretStore secrets, TutorSettingsService settings, IAiRequestRepository log,
        ModelCatalog catalog, TutorContextBuilder builder, IClock clock)
    {
        (_registry, _secrets, _settings, _log, _catalog, _builder, _clock) = (registry, secrets, settings, log, catalog, builder, clock);
        SessionStart = clock.UtcNow;
    }

    public DateTimeOffset SessionStart { get; }

    /// <summary>Builds the exact messages that would be sent (preview). Works even when AI is disabled, so the learner can inspect first.</summary>
    public async Task<TutorPayload> PrepareAsync(TutorRequest request, CancellationToken ct = default)
    {
        var s = await _settings.LoadAsync(ct);
        var assignment = s.For(request.Scenario)
            ?? throw new TutorRefusedException(TutorRefusalReason.NoModelConfigured, "No AI model is selected. Choose a provider and model in the AI settings.");
        var requested = request.Options ?? new TutorContextOptions();
        var effective = requested with { IncludeCode = requested.IncludeCode && s.AllowCode };
        var ctx = await _builder.BuildAsync(request, effective, ct);
        var max = s.MaxOutputTokens ?? DefaultMaxTokens.GetValueOrDefault(request.Scenario, 1000);
        return new TutorPayload(request.Scenario, assignment.ProviderId, assignment.ModelId, ctx.Messages, ctx.Sections, max);
    }

    public async Task<TutorResponse> AskAsync(TutorRequest request, CancellationToken ct = default) =>
        await AskAsync(await PrepareAsync(request, ct), ct);

    public async Task<TutorResponse> AskAsync(TutorPayload payload, CancellationToken ct = default)
    {
        var (provider, completion) = await PreflightAsync(payload, ct);
        var started = _clock.UtcNow;
        try
        {
            var result = await provider.CreateCompletionAsync(completion, ct);
            var cost = await CostAsync(payload, result.Usage, ct);
            await LogAsync(payload, started, result.Usage, cost, "ok", null);
            return new TutorResponse(result.Text, payload.Scenario, payload.ProviderId, payload.ModelId, result.Usage, cost, result.FinishReason);
        }
        catch (Exception e)
        {
            await LogFailureAsync(payload, started, e);
            throw;
        }
    }

    public async IAsyncEnumerable<CompletionChunk> StreamAsync(TutorPayload payload, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (provider, completion) = await PreflightAsync(payload, ct);
        var started = _clock.UtcNow;
        TokenUsage? usage = null;
        var status = "cancelled"; // stays so if the consumer stops early
        string? errorKind = null;
        await using var enumerator = provider.StreamCompletionAsync(completion, ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                CompletionChunk chunk;
                try
                {
                    if (!await enumerator.MoveNextAsync()) { status = "ok"; break; }
                    chunk = enumerator.Current;
                }
                catch (OperationCanceledException) { status = "cancelled"; throw; }
                catch (AiProviderException e) { (status, errorKind) = ("error", e.Kind.ToString()); throw; }
                catch (Exception) { (status, errorKind) = ("error", "Unexpected"); throw; }
                if (chunk.Usage is not null) usage = chunk.Usage;
                yield return chunk;
            }
        }
        finally
        {
            var cost = await CostAsync(payload, usage, CancellationToken.None);
            await LogAsync(payload, started, usage, cost, status, errorKind);
        }
    }

    public Task<IReadOnlyList<AiRequestRecord>> GetHistoryAsync(int limit = 100, CancellationToken ct = default) => _log.GetRecentAsync(limit, ct);

    /// <summary>"Delete AI history": removes all request metadata rows.</summary>
    public Task<int> DeleteHistoryAsync(CancellationToken ct = default) => _log.DeleteAllAsync(ct);

    // ---- internals ------------------------------------------------------------------------------------------------

    private async Task<(IModelProvider Provider, CompletionRequest Request)> PreflightAsync(TutorPayload payload, CancellationToken ct)
    {
        var s = await _settings.LoadAsync(ct);
        if (!s.AiEnabled)
            throw new TutorRefusedException(TutorRefusalReason.AiDisabled, "AI features are turned off. Enable AI in the settings to use the tutor.");
        if (payload.Messages.Count == 0 || payload.Messages[^1].Content.Trim().Length == 0)
            throw new TutorRefusedException(TutorRefusalReason.EmptyMessage, "The message is empty.");
        var provider = _registry.Find(payload.ProviderId)
            ?? throw new TutorRefusedException(TutorRefusalReason.UnknownProvider, $"Unknown AI provider '{payload.ProviderId}'.");

        var key = "";
        if (provider.RequiresApiKey)
        {
            if (!_secrets.IsAvailable)
                throw new TutorRefusedException(TutorRefusalReason.SecretStoreUnavailable, $"The OS secret store is unavailable, so the API key cannot be used: {_secrets.UnavailableReason}");
            key = _secrets.Get(payload.ProviderId) ?? "";
            if (key.Length == 0)
                throw new TutorRefusedException(TutorRefusalReason.NoApiKey, $"No API key is saved for {provider.DisplayName}.");
        }
        else if (_secrets.IsAvailable)
            key = _secrets.Get(payload.ProviderId) ?? ""; // optional key for compatible endpoints

        var known = await _catalog.FindAsync(payload.ProviderId, payload.ModelId, ct);
        await CheckSpendLimitsAsync(s, payload, known, ct);

        return (provider, new CompletionRequest
        {
            ApiKey = key,
            ModelId = payload.ModelId,
            Messages = payload.Messages,
            MaxOutputTokens = payload.MaxOutputTokens,
            KnownModel = known,
        });
    }

    /// <summary>Worst-case estimate (input chars/4 + max output) against the limits; only when the provider published prices.</summary>
    private async Task CheckSpendLimitsAsync(TutorSettings s, TutorPayload payload, ModelInfo? model, CancellationToken ct)
    {
        if (s.MonthlyLimitUsd is null && s.SessionLimitUsd is null) return;
        var estimate = CostEstimator.Estimate(model?.Pricing, new TokenUsage(payload.ApproxInputChars / 4 + 1, payload.MaxOutputTokens));
        if (estimate is null) return; // no published prices => cannot estimate, the limit cannot be enforced

        var now = _clock.UtcNow;
        if (s.SessionLimitUsd is { } sessionLimit && await _log.SumCostSinceAsync(SessionStart, ct) + estimate > sessionLimit)
            throw new TutorRefusedException(TutorRefusalReason.SpendLimit, $"The approximate session spend limit (${sessionLimit:0.####}) would be exceeded.");
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        if (s.MonthlyLimitUsd is { } monthLimit && await _log.SumCostSinceAsync(monthStart, ct) + estimate > monthLimit)
            throw new TutorRefusedException(TutorRefusalReason.SpendLimit, $"The approximate monthly spend limit (${monthLimit:0.####}) would be exceeded.");
    }

    private async Task<decimal?> CostAsync(TutorPayload payload, TokenUsage? usage, CancellationToken ct) =>
        CostEstimator.Estimate((await _catalog.FindAsync(payload.ProviderId, payload.ModelId, ct))?.Pricing, usage);

    private Task LogAsync(TutorPayload p, DateTimeOffset started, TokenUsage? usage, decimal? cost, string status, string? errorKind) =>
        _log.AddAsync(new AiRequestRecord(0, p.ProviderId, p.ModelId, p.Scenario.ToString(), started, _clock.UtcNow,
            usage?.InputTokens, usage?.OutputTokens, cost, status, errorKind));

    private Task LogFailureAsync(TutorPayload p, DateTimeOffset started, Exception e) => e switch
    {
        OperationCanceledException => LogAsync(p, started, null, null, "cancelled", null),
        AiProviderException ap => LogAsync(p, started, null, null, "error", ap.Kind.ToString()),
        _ => LogAsync(p, started, null, null, "error", "Unexpected"),
    };
}
