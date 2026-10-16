using DevAcademy.AI;
using DevAcademy.Application.Ports;
using DevAcademy.Infrastructure;
using DevAcademy.Infrastructure.Secrets;

namespace DevAcademy.Infrastructure.Tests;

public class AiRequestRepositoryTests
{
    private static AiRequestRecord Rec(DateTimeOffset at, decimal? cost, string status = "ok") =>
        new(0, "openai", "gpt-x", "Hint", at, at.AddSeconds(2), 100, 50, cost, status, null);

    [Fact]
    public async Task StoresMetadataSumsCostAndDeletes()
    {
        using var t = new TempDb();
        var repo = new SqliteAiRequestRepository(t.Db);
        var now = DateTimeOffset.Parse("2026-10-16T10:00:00Z");
        await repo.AddAsync(Rec(now.AddDays(-40), 5m));
        await repo.AddAsync(Rec(now.AddDays(-1), 0.25m));
        var id = await repo.AddAsync(new AiRequestRecord(0, "anthropic", "m", "Debug", now, null, null, null, null, "error", "RateLimited"));
        Assert.True(id > 0);

        var recent = await repo.GetRecentAsync(10);
        Assert.Equal(3, recent.Count);
        Assert.Equal("Debug", recent[0].Scenario);
        Assert.Equal("RateLimited", recent[0].ErrorKind);
        Assert.Null(recent[0].ApproxCostUsd);
        Assert.Equal(0.25m, recent[1].ApproxCostUsd);

        Assert.Equal(0.25m, await repo.SumCostSinceAsync(now.AddDays(-30)));
        Assert.Equal(3, await repo.DeleteAllAsync());
        Assert.Empty(await repo.GetRecentAsync(10));
    }

    [Fact]
    public void TableHasNoColumnForPromptOrResponseText()
    {
        using var t = new TempDb();
        using var c = t.Db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('ai_requests')";
        using var r = cmd.ExecuteReader();
        var cols = new List<string>();
        while (r.Read()) cols.Add(r.GetString(0));
        Assert.DoesNotContain(cols, n => n.Contains("prompt") || n.Contains("response") || n.Contains("content") || n.Contains("text"));
        Assert.Contains("finished_at", cols);
    }
}

public class SecretStoreTests
{
    [Fact]
    public void MacKeychainStoreReadUpdateDelete()
    {
        if (!OperatingSystem.IsMacOS()) return; // verified on macOS only
        var service = "DevAcademy.Test." + Guid.NewGuid().ToString("N");
        var store = new MacKeychainSecretStore(service);
        const string provider = "test-provider";
        try
        {
            Assert.True(store.IsAvailable);
            Assert.Null(store.Get(provider));
            store.Set(provider, "dummy-secret-ключ-1");
            Assert.Equal("dummy-secret-ключ-1", store.Get(provider));
            store.Set(provider, "dummy-secret-2"); // update path (duplicate item)
            Assert.Equal("dummy-secret-2", store.Get(provider));
            store.Delete(provider);
            Assert.Null(store.Get(provider));
            store.Delete(provider); // deleting a missing item is not an error
        }
        finally { store.Delete(provider); }
    }

    [Fact]
    public void UnavailableStoreNeverStoresAnything()
    {
        var s = new UnavailableSecretStore("no keyring");
        Assert.False(s.IsAvailable);
        Assert.Equal("no keyring", s.UnavailableReason);
        Assert.Throws<SecretStoreException>(() => s.Set("p", "k"));
        Assert.Throws<SecretStoreException>(() => s.Get("p"));
    }

    [Fact]
    public void FactoryReturnsStoreForThisOs()
    {
        var s = SecretStoreFactory.CreateForCurrentOs();
        if (OperatingSystem.IsMacOS()) Assert.IsType<MacKeychainSecretStore>(s);
        Assert.NotNull(s);
        if (!s.IsAvailable) Assert.False(string.IsNullOrWhiteSpace(s.UnavailableReason));
    }
}
