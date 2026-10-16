using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using DevAcademy.AI;

namespace DevAcademy.AI.Tests;

public sealed record Captured(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, string Body)
{
    public JsonNode? Json => string.IsNullOrEmpty(Body) ? null : JsonNode.Parse(Body);
}

public sealed class FakeHandler(Func<Captured, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Captured> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var headers = request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(",", h.Value));
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var captured = new Captured(request.Method, request.RequestUri!, headers, body);
        Requests.Add(captured);
        return respond(captured);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Sse(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/event-stream") };

    public HttpClient Client() => new(this);
}

internal static class Collect
{
    public static async Task<List<CompletionChunk>> All(IAsyncEnumerable<CompletionChunk> s)
    {
        var l = new List<CompletionChunk>();
        await foreach (var c in s) l.Add(c);
        return l;
    }

    public static string Text(IEnumerable<CompletionChunk> chunks) => string.Concat(chunks.Select(c => c.Text));
}
