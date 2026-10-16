using System.Runtime.CompilerServices;
using DevAcademy.AI;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DevAcademy.Desktop.Tests.Support;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new();
    public bool Available { get; set; } = true;
    public string Reason { get; set; } = "хранилище недоступно в тесте";
    public int SetCalls { get; private set; }
    public bool IsAvailable => Available;
    public string? UnavailableReason => Available ? null : Reason;
    public string? Get(string providerId) => Available ? _values.GetValueOrDefault(providerId) : throw new SecretStoreException(Reason);
    public void Set(string providerId, string secret) { if (!Available) throw new SecretStoreException(Reason); SetCalls++; _values[providerId] = secret; }
    public void Delete(string providerId) { if (!Available) throw new SecretStoreException(Reason); _values.Remove(providerId); }
}

/// <summary>Scriptable provider: no network. Records every call so tests can prove what was (not) sent.</summary>
public sealed class FakeModelProvider : IModelProvider
{
    public string ProviderId => "fake";
    public string DisplayName => "Fake AI";
    public bool RequiresApiKey => true;

    public List<ModelInfo> Models { get; set; } =
    [
        new("model-unknown", null, "fake", null, null, ModelCapabilities.AllUnknown),
        new("model-known", "Known Model", "fake", 128_000, 4096,
            new ModelCapabilities(Streaming: CapabilitySupport.Supported, ToolCalling: CapabilitySupport.NotSupported),
            new ModelPricing(0.000003m, 0.000015m)),
    ];
    public CredentialCheck Check { get; set; } = new(true);
    public Exception? FailWith { get; set; }
    public List<string> Chunks { get; set; } = ["Привет", ", ", "**мир**"];
    public TokenUsage Usage { get; set; } = new(12, 7);
    /// <summary>When set, streaming pauses after the first chunk until the gate opens or the token is cancelled.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public List<string> TestedModels { get; } = [];
    public List<string> KeysSeen { get; } = [];
    public List<CompletionRequest> Streamed { get; } = [];

    public Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(string apiKey, CancellationToken ct = default)
    {
        KeysSeen.Add(apiKey);
        if (FailWith is not null) throw FailWith;
        return Task.FromResult<IReadOnlyList<ModelInfo>>(Models);
    }

    public Task<CredentialCheck> ValidateCredentialsAsync(string apiKey, CancellationToken ct = default)
    {
        KeysSeen.Add(apiKey);
        if (FailWith is not null) throw FailWith;
        return Task.FromResult(Check);
    }

    public Task<ModelTestResult> TestModelAsync(string apiKey, string modelId, CancellationToken ct = default)
    {
        KeysSeen.Add(apiKey);
        TestedModels.Add(modelId);
        if (FailWith is not null) throw FailWith;
        return Task.FromResult(new ModelTestResult(true, new TokenUsage(5, 2), TimeSpan.FromMilliseconds(42)));
    }

    public Task<CompletionResult> CreateCompletionAsync(CompletionRequest request, CancellationToken ct = default) =>
        Task.FromResult(new CompletionResult(string.Concat(Chunks), Usage, "stop"));

    public async IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(CompletionRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Streamed.Add(request);
        if (FailWith is not null) throw FailWith;
        var first = true;
        foreach (var c in Chunks)
        {
            yield return new CompletionChunk(c);
            if (first && Gate is not null) await Gate.Task.WaitAsync(ct);
            first = false;
        }
        yield return new CompletionChunk(Usage: Usage, FinishReason: "stop");
    }
}

public static class AiKit
{
    public const string ModelId = "model-known";

    public static TestHost CreateHost(FakeModelProvider fake) => new(configure: s =>
        s.AddSingleton(_ =>
        {
            var registry = ProviderRegistry.CreateDefault();
            registry.Register(fake);
            return registry;
        }));

    /// <summary>Turns AI on with a saved key and the fake model as the default (what a configured learner has).</summary>
    public static async Task EnableAsync(TestHost host)
    {
        host.Secrets.Set("fake", "test-key-123");
        await host.Services.TutorSettings.SetAiEnabledAsync(true);
        await host.Services.TutorSettings.SetDefaultAsync(new ModelAssignment("fake", ModelId));
    }

    public static async Task<AiSettingsViewModel> LoadSettingsAsync(TestHost host)
    {
        var settings = new SettingsViewModel(host.Services, host.Main, host.Main.InitializeAsync);
        await settings.LoadAsync();
        return settings.Ai;
    }

    /// <summary>Opens the first lesson that has an exercise, so the tutor has code to (not) send.</summary>
    public static async Task<LessonWorkspaceViewModel> OpenExerciseLessonAsync(TestHost host)
    {
        await Helpers.CompleteOnboardingAsync(host);
        var lesson = host.Services.Catalog.GetLessons(Helpers.CourseId).First(l => l.Exercise is not null);
        await host.Main.OpenLessonAsync(lesson.Id);
        return (LessonWorkspaceViewModel)host.Main.Current!;
    }
}
