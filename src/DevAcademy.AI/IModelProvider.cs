namespace DevAcademy.AI;

public interface IModelProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    /// <summary>False for local OpenAI-compatible servers that may run without a key.</summary>
    bool RequiresApiKey { get; }

    /// <summary>Lists all models, following pagination to the end.</summary>
    Task<IReadOnlyList<ModelInfo>> GetAvailableModelsAsync(string apiKey, CancellationToken ct = default);

    /// <summary>Cheap authenticated call (no model tokens). Invalid key => Valid=false; other failures throw.</summary>
    Task<CredentialCheck> ValidateCredentialsAsync(string apiKey, CancellationToken ct = default);

    /// <summary>Tiny real request (max ~16 output tokens) proving the model id works. Throws AiProviderException on failure.</summary>
    Task<ModelTestResult> TestModelAsync(string apiKey, string modelId, CancellationToken ct = default);

    Task<CompletionResult> CreateCompletionAsync(CompletionRequest request, CancellationToken ct = default);

    IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(CompletionRequest request, CancellationToken ct = default);
}

public interface ISecretStore
{
    bool IsAvailable { get; }
    string? UnavailableReason { get; }
    /// <summary>Null when no secret is stored for the provider.</summary>
    string? Get(string providerId);
    void Set(string providerId, string secret);
    void Delete(string providerId);
}

public sealed class SecretStoreException(string message, Exception? inner = null) : Exception(message, inner);
