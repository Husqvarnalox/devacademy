using System.Text;
using DevAcademy.AI;
using DevAcademy.AI.Http;

namespace DevAcademy.AI.Tests;

public class SseTests
{
    private static async Task<List<SseEvent>> Parse(string s)
    {
        var l = new List<SseEvent>();
        await foreach (var e in SseReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(s)))) l.Add(e);
        return l;
    }

    [Fact]
    public async Task MultilineDataCommentsEventNamesAndEofFlush()
    {
        var ev = await Parse(": hi\n\nevent: a\ndata: line1\ndata: line2\n\ndata:nospace\r\n\r\ndata: last-without-blank");
        Assert.Equal(3, ev.Count);
        Assert.Equal("a", ev[0].Event);
        Assert.Equal("line1\nline2", ev[0].Data);
        Assert.Equal("nospace", ev[1].Data);
        Assert.Equal("last-without-blank", ev[2].Data);
    }

    [Fact]
    public async Task CommentOnlyBlocksProduceNoEvents() => Assert.Empty(await Parse(": OPENROUTER PROCESSING\n\n: OPENROUTER PROCESSING\n\n"));
}

public class CatalogTests
{
    private sealed class MemStore : IModelCacheStore
    {
        public Dictionary<string, CachedModelList> Items { get; } = [];
        public Task<CachedModelList?> LoadAsync(string p, CancellationToken ct = default) => Task.FromResult(Items.GetValueOrDefault(p));
        public Task SaveAsync(CachedModelList l, CancellationToken ct = default) { Items[l.ProviderId] = l; return Task.CompletedTask; }
    }

    [Fact]
    public async Task RefreshesOnlyOnExplicitRequestAndPersists()
    {
        var h = new FakeHandler(_ => FakeHandler.Json("""{"object":"list","data":[{"id":"gpt-a","object":"model"}]}"""));
        var reg = ProviderRegistry.CreateDefault(h.Client());
        var store = new MemStore();
        var catalog = new ModelCatalog(reg, store);
        Assert.Null(await catalog.GetCachedAsync("openai"));
        Assert.Empty(h.Requests);

        var list = await catalog.RefreshAsync("openai", "k");
        Assert.Single(h.Requests);
        Assert.Equal("gpt-a", (await catalog.GetCachedAsync("openai"))!.Models.Single().Id);
        Assert.Single(h.Requests); // reading the cache never hits the network
        Assert.Same(list, store.Items["openai"]);

        var fresh = new ModelCatalog(reg, store); // new process: loads persisted list
        Assert.Equal(list.FetchedAt, (await fresh.GetCachedAsync("openai"))!.FetchedAt);
    }

    [Fact]
    public void ModelCacheJsonRoundTrips()
    {
        var l = new CachedModelList("openrouter", DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            [new ModelInfo("a/b", "AB", "openrouter", 1000, 100, new ModelCapabilities(ToolCalling: CapabilitySupport.Supported), new ModelPricing(0.1m, 0.2m), ["temperature"])]);
        var back = ModelCacheJson.Deserialize(ModelCacheJson.Serialize(l))!;
        Assert.Equal(l.Models[0].Pricing, back.Models[0].Pricing);
        Assert.Equal(CapabilitySupport.Supported, back.Models[0].Capabilities.ToolCalling);
        Assert.Null(ModelCacheJson.Deserialize("garbage"));
    }

    [Fact]
    public void RegistryHasFourDefaultProviders() =>
        Assert.Equal(["anthropic", "gemini", "openai", "openrouter"], ProviderRegistry.CreateDefault().Providers.Select(p => p.ProviderId).Order());
}
