using System.Net;
using DevAcademy.AI;
using DevAcademy.AI.Providers;

namespace DevAcademy.AI.Tests;

public class AnthropicTests
{
    private const string Key = "sk-ant-secret-key";

    private static string ModelPage(string id, bool hasMore, string last) => """
        {"data":[{"id":"@ID@","type":"model","display_name":"Name @ID@","created_at":"2026-01-01T00:00:00Z",
          "max_input_tokens":200000,"max_tokens":64000,
          "capabilities":{"batch":{"supported":true},"image_input":{"supported":true},"structured_outputs":{"supported":false},
            "effort":{"supported":true,"high":{"supported":true},"low":{"supported":true},"max":{"supported":false},"medium":{"supported":true}},
            "thinking":{"supported":true,"types":{"adaptive":{"supported":true},"disabled":{"supported":true},"enabled":{"supported":false}}}}}],
         "first_id":"@ID@","has_more":@MORE@,"last_id":"@LAST@"}
        """.Replace("@ID@", id).Replace("@MORE@", hasMore ? "true" : "false").Replace("@LAST@", last);

    [Fact]
    public async Task FollowsPaginationAndMapsCapabilities()
    {
        var h = new FakeHandler(r => r.Uri.Query.Contains("after_id=m1") ? FakeHandler.Json(ModelPage("m2", false, "m2")) : FakeHandler.Json(ModelPage("m1", true, "m1")));
        var models = await new AnthropicProvider(h.Client()).GetAvailableModelsAsync(Key);

        Assert.Equal(["m1", "m2"], models.Select(m => m.Id));
        Assert.Equal(2, h.Requests.Count);
        Assert.Contains("after_id=m1", h.Requests[1].Uri.Query);
        var m = models[0];
        Assert.Equal(200000, m.ContextWindow);
        Assert.Equal(64000, m.MaxOutputTokens);
        Assert.Equal(CapabilitySupport.Supported, m.Capabilities.MultimodalInput);
        Assert.Equal(CapabilitySupport.NotSupported, m.Capabilities.StructuredOutput);
        Assert.Equal(CapabilitySupport.Supported, m.Capabilities.ReasoningControls);
        Assert.Equal(CapabilitySupport.Unknown, m.Capabilities.ToolCalling); // not reported by the API
        Assert.Equal(CapabilitySupport.Unknown, m.Capabilities.TextGeneration);
        Assert.Null(m.Pricing);
    }

    [Fact]
    public async Task MissingCapabilitiesObjectIsAllUnknown()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"data":[{"id":"x","type":"model","display_name":"X","capabilities":null,"max_input_tokens":null,"max_tokens":null}],"has_more":false,"last_id":"x"}"""));
        var m = (await new AnthropicProvider(h.Client()).GetAvailableModelsAsync(Key)).Single();
        Assert.Equal(ModelCapabilities.AllUnknown, m.Capabilities);
        Assert.Null(m.ContextWindow);
    }

    [Fact]
    public async Task KeyOnlyInXApiKeyHeaderToAnthropicHost()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"data":[],"has_more":false,"last_id":null}"""));
        await new AnthropicProvider(h.Client()).GetAvailableModelsAsync(Key);
        var r = h.Requests.Single();
        Assert.Equal("api.anthropic.com", r.Uri.Host);
        Assert.Equal(Key, r.Headers["x-api-key"]);
        Assert.Equal("2023-06-01", r.Headers["anthropic-version"]);
        Assert.DoesNotContain("authorization", r.Headers.Keys);
        Assert.DoesNotContain(Key, r.Uri.ToString());
    }

    [Fact]
    public async Task CompletionBodyHasSystemTopLevelAndNoTemperatureUnlessSet()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"id":"msg_1","type":"message","role":"assistant","content":[{"type":"text","text":"Hello"}],"stop_reason":"end_turn","usage":{"input_tokens":12,"output_tokens":3}}"""));
        var p = new AnthropicProvider(h.Client());
        var res = await p.CreateCompletionAsync(new CompletionRequest
        {
            ApiKey = Key,
            ModelId = "my-manual-model-id",
            MaxOutputTokens = 99,
            Messages = [new(ChatRole.System, "sys"), new(ChatRole.User, "hi")],
        });
        Assert.Equal("Hello", res.Text);
        Assert.Equal(new TokenUsage(12, 3), res.Usage);
        var body = h.Requests[0].Json!;
        Assert.Equal("my-manual-model-id", (string?)body["model"]);
        Assert.Equal("sys", (string?)body["system"]);
        Assert.Equal(99, (int?)body["max_tokens"]);
        Assert.Single(body["messages"]!.AsArray());
        Assert.Null(body["temperature"]);
        Assert.Null(body["stream"]);

        await p.CreateCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "x", Temperature = 0.2, Messages = [new(ChatRole.User, "q")] });
        Assert.Equal(0.2, (double?)h.Requests[1].Json!["temperature"]);
    }

    private const string Stream = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"model":"m","stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":25,"output_tokens":1}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: ping
        data: {"type":"ping"}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"hidden"}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hel"}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"lo"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":15}}

        event: message_stop
        data: {"type":"message_stop"}


        """;

    [Fact]
    public async Task ParsesStreamAndCombinesUsage()
    {
        var h = new FakeHandler(_ => FakeHandler.Sse(Stream));
        var chunks = await Collect.All(new AnthropicProvider(h.Client()).StreamCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "hi")] }));
        Assert.Equal("Hello", Collect.Text(chunks));
        var last = chunks.Last();
        Assert.Equal(new TokenUsage(25, 15), last.Usage);
        Assert.Equal("end_turn", last.FinishReason);
        Assert.True((bool?)h.Requests[0].Json!["stream"]);
    }

    [Fact]
    public async Task MidStreamErrorEventThrowsTypedException()
    {
        const string s = "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}}\n\n" +
                         "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hi\"}}\n\n" +
                         "event: error\ndata: {\"type\": \"error\", \"error\": {\"type\": \"overloaded_error\", \"message\": \"Overloaded\"}}\n\n";
        var h = new FakeHandler(_ => FakeHandler.Sse(s));
        var seen = new List<string>();
        var ex = await Assert.ThrowsAsync<AiProviderException>(async () =>
        {
            await foreach (var c in new AnthropicProvider(h.Client()).StreamCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "x")] }))
                seen.Add(c.Text ?? "");
        });
        Assert.Equal(AiErrorKind.ProviderUnavailable, ex.Kind);
        Assert.Equal("Overloaded", ex.Message);
        Assert.Equal(["Hi"], seen);
    }

    [Fact]
    public async Task TruncatedStreamIsAnError()
    {
        var h = new FakeHandler(_ => FakeHandler.Sse("event: message_start\ndata: {\"type\":\"message_start\",\"message\":{}}\n\n"));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Collect.All(new AnthropicProvider(h.Client()).StreamCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "x")] })));
        Assert.Equal(AiErrorKind.Network, ex.Kind);
    }

    [Theory]
    [InlineData(401, "authentication_error", AiErrorKind.InvalidKey)]
    [InlineData(403, "permission_error", AiErrorKind.PermissionDenied)]
    [InlineData(404, "not_found_error", AiErrorKind.ModelNotFound)]
    [InlineData(429, "rate_limit_error", AiErrorKind.RateLimited)]
    [InlineData(402, "billing_error", AiErrorKind.QuotaExceeded)]
    [InlineData(500, "api_error", AiErrorKind.ProviderUnavailable)]
    [InlineData(529, "overloaded_error", AiErrorKind.ProviderUnavailable)]
    [InlineData(400, "invalid_request_error", AiErrorKind.BadRequest)]
    public async Task HttpErrorsMapToKinds(int status, string type, AiErrorKind expected)
    {
        var h = new FakeHandler(_ => FakeHandler.Json($$$"""{"type":"error","error":{"type":"{{{type}}}","message":"msg with {{{Key}}} inside"},"request_id":"req_1"}""", (HttpStatusCode)status));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => new AnthropicProvider(h.Client()).CreateCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "x")] }));
        Assert.Equal(expected, ex.Kind);
        Assert.Equal(status, ex.HttpStatus);
        Assert.DoesNotContain(Key, ex.Message); // keys are scrubbed from provider messages
    }

    [Fact]
    public async Task ValidateCredentialsReturnsFalseForInvalidKeyOnly()
    {
        var bad = new FakeHandler(_ => FakeHandler.Json("""{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""", HttpStatusCode.Unauthorized));
        Assert.False((await new AnthropicProvider(bad.Client()).ValidateCredentialsAsync(Key)).Valid);
        var down = new FakeHandler(_ => FakeHandler.Json("""{"type":"error","error":{"type":"api_error","message":"x"}}""", HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<AiProviderException>(() => new AnthropicProvider(down.Client()).ValidateCredentialsAsync(Key));
    }
}

public class GeminiTests
{
    private const string Key = "AIza-secret-gemini";

    [Fact]
    public async Task FollowsPageTokenAndMapsLimitsAndMethods()
    {
        var h = new FakeHandler(r => r.Uri.Query.Contains("pageToken=TOK2")
            ? FakeHandler.Json("""{"models":[{"name":"models/gemini-b","displayName":"B","inputTokenLimit":1000,"outputTokenLimit":100,"supportedGenerationMethods":["countTokens"]}]}""")
            : FakeHandler.Json("""{"models":[{"name":"models/gemini-a","displayName":"A","inputTokenLimit":1048576,"outputTokenLimit":65536,"supportedGenerationMethods":["generateContent","countTokens"],"thinking":true}],"nextPageToken":"TOK2"}"""));
        var models = await new GeminiProvider(h.Client()).GetAvailableModelsAsync(Key);
        Assert.Equal(["gemini-a", "gemini-b"], models.Select(m => m.Id));
        Assert.Equal(2, h.Requests.Count);
        Assert.All(h.Requests, r => Assert.Equal(Key, r.Headers["x-goog-api-key"]));
        Assert.All(h.Requests, r => Assert.DoesNotContain(Key, r.Uri.ToString()));
        Assert.All(h.Requests, r => Assert.Equal("generativelanguage.googleapis.com", r.Uri.Host));
        Assert.Equal(1048576, models[0].ContextWindow);
        Assert.Equal(65536, models[0].MaxOutputTokens);
        Assert.Equal(CapabilitySupport.Supported, models[0].Capabilities.TextGeneration);
        Assert.Equal(CapabilitySupport.Supported, models[0].Capabilities.ReasoningControls);
        Assert.Equal(CapabilitySupport.Unknown, models[0].Capabilities.Streaming);
        Assert.Equal(CapabilitySupport.NotSupported, models[1].Capabilities.TextGeneration);
        Assert.Equal(CapabilitySupport.Unknown, models[1].Capabilities.ReasoningControls);
    }

    [Theory]
    [InlineData("gemini-x", "/v1beta/models/gemini-x:generateContent")]
    [InlineData("models/gemini-x", "/v1beta/models/gemini-x:generateContent")]
    public async Task ManualModelIdPassesThrough(string id, string path)
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"candidates":[{"content":{"role":"model","parts":[{"text":"OK"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":2,"totalTokenCount":7}}"""));
        var res = await new GeminiProvider(h.Client()).CreateCompletionAsync(new CompletionRequest
        {
            ApiKey = Key,
            ModelId = id,
            MaxOutputTokens = 16,
            Messages = [new(ChatRole.System, "be brief"), new(ChatRole.User, "hi"), new(ChatRole.Assistant, "yo"), new(ChatRole.User, "again")],
        });
        Assert.Equal("OK", res.Text);
        Assert.Equal(new TokenUsage(5, 2), res.Usage);
        var r = h.Requests[0];
        Assert.Equal(path, r.Uri.AbsolutePath);
        var body = r.Json!;
        Assert.Equal("be brief", (string?)body["systemInstruction"]!["parts"]![0]!["text"]);
        Assert.Equal(["user", "model", "user"], body["contents"]!.AsArray().Select(c => (string?)c!["role"]));
        Assert.Equal(16, (int?)body["generationConfig"]!["maxOutputTokens"]);
        Assert.Null(body["generationConfig"]!["temperature"]);
    }

    [Fact]
    public async Task StreamsSseWithAltSseAndMultilineData()
    {
        var sse =
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Hel\"}]}}],\"usageMetadata\":{\"promptTokenCount\":4}}\r\n\r\n" +
            ": comment\r\n" +
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"lo\"}]},\r\n" +
            "data: \"finishReason\":\"STOP\"}],\"usageMetadata\":{\"promptTokenCount\":4,\"candidatesTokenCount\":6,\"thoughtsTokenCount\":3,\"totalTokenCount\":13}}\r\n\r\n";
        var h = new FakeHandler(_ => FakeHandler.Sse(sse));
        var chunks = await Collect.All(new GeminiProvider(h.Client()).StreamCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "gemini-x", Messages = [new(ChatRole.User, "q")] }));
        Assert.Equal("Hello", Collect.Text(chunks));
        Assert.Equal(new TokenUsage(4, 9), chunks.Last().Usage);
        Assert.Equal("STOP", chunks.Last().FinishReason);
        Assert.Equal("streamGenerateContent", h.Requests[0].Uri.AbsolutePath.Split(':')[1]);
        Assert.Contains("alt=sse", h.Requests[0].Uri.Query);
    }

    [Fact]
    public async Task MidStreamErrorThrows()
    {
        var sse = "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"a\"}]}}]}\n\n" +
                  "data: {\"error\":{\"code\":503,\"message\":\"The model is overloaded.\",\"status\":\"UNAVAILABLE\"}}\n\n";
        var h = new FakeHandler(_ => FakeHandler.Sse(sse));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Collect.All(new GeminiProvider(h.Client()).StreamCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "q")] })));
        Assert.Equal(AiErrorKind.ProviderUnavailable, ex.Kind);
    }

    [Theory]
    [InlineData(400, "INVALID_ARGUMENT", "API key not valid. Please pass a valid API key.", AiErrorKind.InvalidKey)]
    [InlineData(400, "INVALID_ARGUMENT", "Unknown field", AiErrorKind.BadRequest)]
    [InlineData(403, "PERMISSION_DENIED", "denied", AiErrorKind.PermissionDenied)]
    [InlineData(404, "NOT_FOUND", "models/zzz is not found", AiErrorKind.ModelNotFound)]
    [InlineData(429, "RESOURCE_EXHAUSTED", "quota", AiErrorKind.RateLimited)]
    [InlineData(503, "UNAVAILABLE", "overloaded", AiErrorKind.ProviderUnavailable)]
    [InlineData(500, "INTERNAL", "oops", AiErrorKind.ProviderUnavailable)]
    public async Task HttpErrorsMapToKinds(int status, string st, string message, AiErrorKind expected)
    {
        var h = new FakeHandler(_ => FakeHandler.Json($$$"""{"error":{"code":{{{status}}},"message":"{{{message}}}","status":"{{{st}}}"}}""", (HttpStatusCode)status));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => new GeminiProvider(h.Client()).CreateCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "x")] }));
        Assert.Equal(expected, ex.Kind);
    }
}

public class OpenAiTests
{
    private const string Key = "sk-openai-secret";

    [Fact]
    public async Task ListsModelsWithAllCapabilitiesUnknown()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"object":"list","data":[{"id":"gpt-x","object":"model","created":1686935002,"owned_by":"openai"},{"id":"text-embedding-3-small","object":"model","created":1,"owned_by":"system"}]}"""));
        var models = await new OpenAiProvider(h.Client()).GetAvailableModelsAsync(Key);
        Assert.Equal(2, models.Count);
        Assert.All(models, m => { Assert.Equal(ModelCapabilities.AllUnknown, m.Capabilities); Assert.Null(m.ContextWindow); Assert.Null(m.Pricing); });
        var r = h.Requests[0];
        Assert.Equal("https://api.openai.com/v1/models", r.Uri.ToString());
        Assert.Equal("Bearer " + Key, r.Headers["authorization"]);
    }

    [Fact]
    public async Task BodyUsesMaxCompletionTokensAndOmitsTemperatureForReasoningModels()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"id":"c","object":"chat.completion","choices":[{"index":0,"message":{"role":"assistant","content":"OK"},"finish_reason":"stop"}],"usage":{"prompt_tokens":9,"completion_tokens":1,"total_tokens":10}}"""));
        var p = new OpenAiProvider(h.Client());
        var res = await p.CreateCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "o3-mini", Temperature = 0.3, MaxOutputTokens = 50, Messages = [new(ChatRole.System, "s"), new(ChatRole.User, "u")] });
        Assert.Equal("OK", res.Text);
        Assert.Equal(new TokenUsage(9, 1), res.Usage);
        var b = h.Requests[0].Json!;
        Assert.Equal(50, (int?)b["max_completion_tokens"]);
        Assert.Null(b["max_tokens"]);
        Assert.Null(b["temperature"]);
        Assert.Equal("system", (string?)b["messages"]![0]!["role"]);

        await p.CreateCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "gpt-4o-custom", Temperature = 0.3, Messages = [new(ChatRole.User, "u")] });
        Assert.Equal(0.3, (double?)h.Requests[1].Json!["temperature"]);
        Assert.Equal("gpt-4o-custom", (string?)h.Requests[1].Json!["model"]);
    }

    [Fact]
    public async Task KnownModelUnsupportedParameterIsNotSent()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"choices":[{"message":{"content":"x"},"finish_reason":"stop"}]}"""));
        var known = new ModelInfo("m", null, "openai", null, null, ModelCapabilities.AllUnknown, null, ["temperature"]);
        await new OpenAiProvider(h.Client()).CreateCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "m", Temperature = 0.5, KnownModel = known, Messages = [new(ChatRole.User, "u")] });
        Assert.Null(h.Requests[0].Json!["temperature"]);
    }

    private const string Sse =
        "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"\"},\"finish_reason\":null}],\"usage\":null}\n\n" +
        "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hel\"},\"finish_reason\":null}],\"usage\":null}\n\n" +
        "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"lo\"},\"finish_reason\":null}],\"usage\":null}\n\n" +
        "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}],\"usage\":null}\n\n" +
        "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[],\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":2,\"total_tokens\":13}}\n\n" +
        "data: [DONE]\n\n";

    [Fact]
    public async Task StreamsAndRequestsUsage()
    {
        var h = new FakeHandler(_ => FakeHandler.Sse(Sse));
        var chunks = await Collect.All(new OpenAiProvider(h.Client()).StreamCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "u")] }));
        Assert.Equal("Hello", Collect.Text(chunks));
        Assert.Equal(new TokenUsage(11, 2), chunks.Last().Usage);
        Assert.Contains(chunks, c => c.FinishReason == "stop");
        var b = h.Requests[0].Json!;
        Assert.True((bool?)b["stream"]);
        Assert.True((bool?)b["stream_options"]!["include_usage"]);
    }

    [Theory]
    [InlineData(401, "invalid_api_key", "invalid_request_error", AiErrorKind.InvalidKey)]
    [InlineData(403, "unsupported_country_region_territory", "invalid_request_error", AiErrorKind.PermissionDenied)]
    [InlineData(404, "model_not_found", "invalid_request_error", AiErrorKind.ModelNotFound)]
    [InlineData(429, "rate_limit_exceeded", "requests", AiErrorKind.RateLimited)]
    [InlineData(429, "insufficient_quota", "insufficient_quota", AiErrorKind.QuotaExceeded)]
    [InlineData(500, "server_error", "server_error", AiErrorKind.ProviderUnavailable)]
    [InlineData(503, "x", "server_error", AiErrorKind.ProviderUnavailable)]
    [InlineData(400, "x", "invalid_request_error", AiErrorKind.BadRequest)]
    public async Task HttpErrorsMapToKinds(int status, string code, string type, AiErrorKind expected)
    {
        var h = new FakeHandler(_ => FakeHandler.Json($$$"""{"error":{"message":"boom","type":"{{{type}}}","param":null,"code":"{{{code}}}"}}""", (HttpStatusCode)status));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => new OpenAiProvider(h.Client()).CreateCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "m", Messages = [new(ChatRole.User, "x")] }));
        Assert.Equal(expected, ex.Kind);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task NetworkAndTimeoutFailuresAreTyped()
    {
        var net = new HttpClient(new ThrowingHandler(new HttpRequestException("connection refused")));
        var e1 = await Assert.ThrowsAsync<AiProviderException>(() => new OpenAiProvider(net).GetAvailableModelsAsync(Key));
        Assert.Equal(AiErrorKind.Network, e1.Kind);
        var timeout = new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout")));
        var e2 = await Assert.ThrowsAsync<AiProviderException>(() => new OpenAiProvider(timeout).GetAvailableModelsAsync(Key));
        Assert.Equal(AiErrorKind.Timeout, e2.Kind);
    }

    [Fact]
    public async Task EmptyKeyFailsLocallyWithoutNetwork()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("{}"));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => new OpenAiProvider(h.Client()).GetAvailableModelsAsync(""));
        Assert.Equal(AiErrorKind.InvalidKey, ex.Kind);
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task RedirectIsNotFollowedAndNeverLeaksKey()
    {
        var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://evil.example/") } });
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => new OpenAiProvider(h.Client()).GetAvailableModelsAsync(Key));
        Assert.Equal(AiErrorKind.BadRequest, ex.Kind);
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task CompatibleProviderWorksWithoutKeyAndCustomBaseUrl()
    {
        var h = new FakeHandler(r => r.Uri.AbsolutePath.EndsWith("/models")
            ? FakeHandler.Json("""{"object":"list","data":[{"id":"llama3","object":"model","owned_by":"library"}]}""")
            : FakeHandler.Json("""{"choices":[{"message":{"content":"hi"},"finish_reason":"stop"}]}"""));
        var p = new OpenAiCompatibleProvider(h.Client(), new Uri("http://localhost:11434/v1"), "ollama", "Ollama");
        Assert.Equal("ollama", p.ProviderId);
        Assert.False(p.RequiresApiKey);
        Assert.Equal("llama3", (await p.GetAvailableModelsAsync("")).Single().Id);
        await p.CreateCompletionAsync(new CompletionRequest { ApiKey = "", ModelId = "llama3", MaxOutputTokens = 20, Messages = [new(ChatRole.User, "q")] });
        Assert.Equal("http://localhost:11434/v1/chat/completions", h.Requests[1].Uri.ToString());
        Assert.DoesNotContain("authorization", h.Requests[1].Headers.Keys);
        Assert.Equal(20, (int?)h.Requests[1].Json!["max_tokens"]);
    }
}

public class OpenRouterTests
{
    private const string Key = "sk-or-secret";

    private const string Models = """
        {"data":[
         {"id":"anthropic/claude-x","canonical_slug":"anthropic/claude-x","name":"Anthropic: Claude X","created":1,"context_length":200000,
          "pricing":{"prompt":"0.000003","completion":"0.000015","request":"0","image":"0.0048"},
          "architecture":{"modality":"text+image->text","input_modalities":["text","image"],"output_modalities":["text"],"tokenizer":"Claude"},
          "top_provider":{"context_length":200000,"max_completion_tokens":64000,"is_moderated":true},
          "supported_parameters":["max_tokens","temperature","tools","tool_choice","reasoning","structured_outputs"]},
         {"id":"openrouter/auto","name":"Auto Router","context_length":2000000,"pricing":{"prompt":"-1","completion":"-1"},
          "architecture":{"input_modalities":["text"],"output_modalities":["text"]},"top_provider":{"max_completion_tokens":null},
          "supported_parameters":["max_tokens"]},
         {"id":"mystery/model","name":"Mystery"}
        ]}
        """;

    [Fact]
    public async Task MapsContextPricingAndCapabilitiesFromApi()
    {
        var h = new FakeHandler(_ => FakeHandler.Json(Models));
        var list = await new OpenRouterProvider(h.Client()).GetAvailableModelsAsync(Key);
        Assert.Equal("https://openrouter.ai/api/v1/models", h.Requests[0].Uri.ToString());

        var a = list[0];
        Assert.Equal("Anthropic: Claude X", a.DisplayName);
        Assert.Equal(200000, a.ContextWindow);
        Assert.Equal(64000, a.MaxOutputTokens);
        Assert.Equal(0.000003m, a.Pricing!.PromptUsdPerToken);
        Assert.Equal(0.000015m, a.Pricing.CompletionUsdPerToken);
        Assert.Equal(CapabilitySupport.Supported, a.Capabilities.ToolCalling);
        Assert.Equal(CapabilitySupport.Supported, a.Capabilities.StructuredOutput);
        Assert.Equal(CapabilitySupport.Supported, a.Capabilities.ReasoningControls);
        Assert.Equal(CapabilitySupport.Supported, a.Capabilities.MultimodalInput);
        Assert.Equal(CapabilitySupport.Supported, a.Capabilities.TextGeneration);
        Assert.Equal(CapabilitySupport.Unknown, a.Capabilities.Streaming);
        Assert.Null(a.UnsupportedParameters);

        var auto = list[1];
        Assert.Null(auto.Pricing); // "-1" = dynamic pricing => unknown
        Assert.Equal(CapabilitySupport.NotSupported, auto.Capabilities.ToolCalling);
        Assert.Equal(CapabilitySupport.NotSupported, auto.Capabilities.MultimodalInput);
        Assert.Contains("temperature", auto.UnsupportedParameters!);

        var mystery = list[2];
        Assert.Equal(ModelCapabilities.AllUnknown, mystery.Capabilities);
        Assert.Null(mystery.Pricing);
        Assert.Null(mystery.UnsupportedParameters);
    }

    [Fact]
    public async Task FollowsSameHostNextLinkOnly()
    {
        var calls = 0;
        var h = new FakeHandler(r =>
        {
            calls++;
            return calls == 1
                ? FakeHandler.Json("""{"data":[{"id":"a/1"}],"links":{"next":"https://openrouter.ai/api/v1/models?page=2"}}""")
                : calls == 2 ? FakeHandler.Json("""{"data":[{"id":"a/2"}],"links":{"next":"https://evil.example/steal"}}""")
                : FakeHandler.Json("""{"data":[]}""");
        });
        var list = await new OpenRouterProvider(h.Client()).GetAvailableModelsAsync(Key);
        Assert.Equal(["a/1", "a/2"], list.Select(m => m.Id));
        Assert.All(h.Requests, r => Assert.Equal("openrouter.ai", r.Uri.Host));
    }

    [Fact]
    public async Task ValidatesViaKeyEndpoint()
    {
        var ok = new FakeHandler(_ => FakeHandler.Json("""{"data":{"label":"k","limit":null,"usage":1.5,"is_free_tier":false}}"""));
        Assert.True((await new OpenRouterProvider(ok.Client()).ValidateCredentialsAsync(Key)).Valid);
        Assert.Equal("/api/v1/key", ok.Requests[0].Uri.AbsolutePath);
        var bad = new FakeHandler(_ => FakeHandler.Json("""{"error":{"code":401,"message":"No auth credentials found"}}""", HttpStatusCode.Unauthorized));
        Assert.False((await new OpenRouterProvider(bad.Client()).ValidateCredentialsAsync(Key)).Valid);
    }

    [Fact]
    public async Task StreamIgnoresProcessingCommentsAndReadsUsageAndDone()
    {
        var sse = ": OPENROUTER PROCESSING\n\n: OPENROUTER PROCESSING\n\n" +
                  "data: {\"id\":\"g\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"},\"finish_reason\":null}]}\n\n" +
                  "data: {\"id\":\"g\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":1,\"total_tokens\":8,\"cost\":0.0001}}\n\n" +
                  "data: [DONE]\n\n";
        var h = new FakeHandler(_ => FakeHandler.Sse(sse));
        var chunks = await Collect.All(new OpenRouterProvider(h.Client()).StreamCompletionAsync(new CompletionRequest { ApiKey = Key, ModelId = "a/b", Messages = [new(ChatRole.User, "q")] }));
        Assert.Equal("Hi", Collect.Text(chunks));
        Assert.Equal(new TokenUsage(7, 1), chunks.Last().Usage);
        Assert.Equal("max_tokens", h.Requests[0].Json!.AsObject().Select(p => p.Key).Single(k => k.StartsWith("max")));
    }

    [Fact]
    public async Task MidStreamErrorChunkThrows()
    {
        var sse = "data: {\"id\":\"g\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"}}]}\n\n" +
                  "data: {\"error\":{\"code\":502,\"message\":\"Provider disconnected\"},\"choices\":[{\"index\":0,\"delta\":{\"content\":\"\"},\"finish_reason\":\"error\"}]}\n\n";
        var h = new FakeHandler(_ => FakeHandler.Sse(sse));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Collect.All(new OpenRouterProvider(h.Client()).StreamCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "a/b", Messages = [new(ChatRole.User, "q")] })));
        Assert.Equal(AiErrorKind.ProviderUnavailable, ex.Kind);
        Assert.Equal("Provider disconnected", ex.Message);
    }

    [Theory]
    [InlineData(401, AiErrorKind.InvalidKey)]
    [InlineData(402, AiErrorKind.QuotaExceeded)]
    [InlineData(403, AiErrorKind.PermissionDenied)]
    [InlineData(404, AiErrorKind.ModelNotFound)]
    [InlineData(408, AiErrorKind.Timeout)]
    [InlineData(429, AiErrorKind.RateLimited)]
    [InlineData(502, AiErrorKind.ProviderUnavailable)]
    [InlineData(503, AiErrorKind.ProviderUnavailable)]
    [InlineData(400, AiErrorKind.BadRequest)]
    public async Task HttpErrorsMapToKinds(int status, AiErrorKind expected)
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"error":{"code":@S@,"message":"nope","metadata":{}}}""".Replace("@S@", status.ToString()), (HttpStatusCode)status));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => new OpenRouterProvider(h.Client()).CreateCompletionAsync(
            new CompletionRequest { ApiKey = Key, ModelId = "a/b", Messages = [new(ChatRole.User, "x")] }));
        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public void CostEstimateUsesPublishedPricesAndIsNullWithout()
    {
        var pricing = new ModelPricing(0.000003m, 0.000015m);
        Assert.Equal(1000 * 0.000003m + 200 * 0.000015m, CostEstimator.Estimate(pricing, new TokenUsage(1000, 200)));
        Assert.Null(CostEstimator.Estimate(null, new TokenUsage(1000, 200)));
        Assert.Null(CostEstimator.Estimate(pricing, null));
        Assert.Null(CostEstimator.Estimate(new ModelPricing(0.1m, null), new TokenUsage(1, 1)));
    }
}

internal sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw ex;
}
