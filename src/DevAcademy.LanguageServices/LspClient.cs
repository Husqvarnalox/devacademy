using System.Text.Json;
using DevAcademy.LanguageServices.JsonRpc;
using DevAcademy.LanguageServices.Protocol;

namespace DevAcademy.LanguageServices;

/// <summary>
/// Typed LSP client over one connection: initialize handshake, document sync, the requests the editor uses, and the
/// diagnostics stream. Replies to the server requests every client must answer (configuration, progress creation, dynamic
/// registration, workspace folders).
/// </summary>
public sealed class LspClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions J = JsonRpcConnection.Json;
    private readonly JsonRpcConnection _rpc;
    private bool _exited;

    public LspClient(JsonRpcConnection rpc, string rootUri, string? rootName = null)
    {
        _rpc = rpc;
        RootUri = rootUri;
        RootName = rootName ?? "workspace";
        rpc.OnNotification("textDocument/publishDiagnostics", p =>
        {
            if (p is { } v && v.Deserialize<PublishDiagnosticsParams>(J) is { } d) Diagnostics?.Invoke(d);
        });
        rpc.OnNotification("window/logMessage", p => { if (p is { } v && v.TryGetProperty("message", out var m)) ServerLog?.Invoke(m.GetString() ?? ""); });
        rpc.OnNotification("window/showMessage", p => { if (p is { } v && v.TryGetProperty("message", out var m)) ServerLog?.Invoke(m.GetString() ?? ""); });
        rpc.OnRequest("workspace/configuration", (p, _) =>
        {
            int n = p is { } v && v.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array ? items.GetArrayLength() : 0;
            // "No settings": servers fall back to their defaults. An empty object suits servers that dereference the section.
            return Task.FromResult<object?>(Enumerable.Range(0, n).Select(_ => (object)new { }).ToArray());
        });
        rpc.OnRequest("window/workDoneProgress/create", (_, _) => Task.FromResult<object?>(null));
        rpc.OnRequest("client/registerCapability", (_, _) => Task.FromResult<object?>(null));
        rpc.OnRequest("client/unregisterCapability", (_, _) => Task.FromResult<object?>(null));
        rpc.OnRequest("window/showMessageRequest", (_, _) => Task.FromResult<object?>(null));
        rpc.OnRequest("workspace/workspaceFolders", (_, _) => Task.FromResult<object?>(new[] { new { uri = RootUri, name = RootName } }));
        rpc.OnRequest("workspace/applyEdit", (_, _) => Task.FromResult<object?>(new { applied = false }));
    }

    public string RootUri { get; }
    public string RootName { get; }
    public ServerCapabilitiesInfo Capabilities { get; private set; } = ServerCapabilitiesInfo.None;
    public PositionEncoding Encoding => Capabilities.Encoding;
    public Task Closed => _rpc.Closed;

    public event Action<PublishDiagnosticsParams>? Diagnostics;
    public event Action<string>? ServerLog;

    public async Task<ServerCapabilitiesInfo> InitializeAsync(object? initializationOptions, CancellationToken ct)
    {
        var parms = new
        {
            processId = Environment.ProcessId,
            clientInfo = new { name = "DevAcademy", version = "1.0" },
            rootUri = RootUri,
            workspaceFolders = new[] { new { uri = RootUri, name = RootName } },
            initializationOptions,
            trace = "off",
            capabilities = new
            {
                general = new { positionEncodings = new[] { "utf-16", "utf-8", "utf-32" } },
                workspace = new { configuration = true, workspaceFolders = true },
                window = new { workDoneProgress = true },
                textDocument = new
                {
                    synchronization = new { didSave = true, dynamicRegistration = false },
                    completion = new
                    {
                        contextSupport = true,
                        completionItem = new
                        {
                            snippetSupport = false,
                            documentationFormat = new[] { "markdown", "plaintext" },
                            resolveSupport = new { properties = new[] { "documentation", "detail", "additionalTextEdits" } },
                        },
                    },
                    hover = new { contentFormat = new[] { "markdown", "plaintext" } },
                    publishDiagnostics = new { versionSupport = true },
                    definition = new { linkSupport = false },
                    formatting = new { },
                    signatureHelp = new
                    {
                        contextSupport = true,
                        signatureInformation = new
                        {
                            documentationFormat = new[] { "markdown", "plaintext" },
                            parameterInformation = new { labelOffsetSupport = false },
                            activeParameterSupport = true,
                        },
                    },
                },
            },
        };
        JsonElement result = await _rpc.RequestAsync("initialize", parms, ct).ConfigureAwait(false);
        Capabilities = ParseCapabilities(result);
        await _rpc.NotifyAsync("initialized", new { }).ConfigureAwait(false);
        return Capabilities;
    }

    internal static ServerCapabilitiesInfo ParseCapabilities(JsonElement result)
    {
        string? name = null, version = null;
        if (result.TryGetProperty("serverInfo", out var si) && si.ValueKind == JsonValueKind.Object)
        {
            name = si.TryGetProperty("name", out var n) ? n.GetString() : null;
            version = si.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        if (!result.TryGetProperty("capabilities", out var caps) || caps.ValueKind != JsonValueKind.Object)
            return ServerCapabilitiesInfo.None with { ServerName = name, ServerVersion = version };

        var enc = PositionEncodings.FromWire(caps.TryGetProperty("positionEncoding", out var pe) ? pe.GetString() : null);
        int sync = 1;
        bool save = false;
        if (caps.TryGetProperty("textDocumentSync", out var ts))
        {
            if (ts.ValueKind == JsonValueKind.Number) sync = ts.GetInt32();
            else if (ts.ValueKind == JsonValueKind.Object)
            {
                sync = ts.TryGetProperty("change", out var ch) && ch.ValueKind == JsonValueKind.Number ? ch.GetInt32() : 1;
                save = ts.TryGetProperty("save", out var sv) && sv.ValueKind != JsonValueKind.False && sv.ValueKind != JsonValueKind.Null;
            }
        }

        static bool Has(JsonElement c, string p) => c.TryGetProperty(p, out var x) && x.ValueKind != JsonValueKind.False && x.ValueKind != JsonValueKind.Null;
        static List<string> Triggers(JsonElement c, string provider, string prop)
        {
            var l = new List<string>();
            if (c.TryGetProperty(provider, out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var e in arr.EnumerateArray()) if (e.GetString() is { Length: > 0 } s) l.Add(s);
            return l;
        }

        bool resolve = caps.TryGetProperty("completionProvider", out var cp) && cp.ValueKind == JsonValueKind.Object
                       && cp.TryGetProperty("resolveProvider", out var rp) && rp.ValueKind == JsonValueKind.True;
        var sigTriggers = Triggers(caps, "signatureHelpProvider", "triggerCharacters");
        sigTriggers.AddRange(Triggers(caps, "signatureHelpProvider", "retriggerCharacters"));
        return new ServerCapabilitiesInfo(name, version, enc, sync, save, Has(caps, "completionProvider"), resolve,
            Triggers(caps, "completionProvider", "triggerCharacters"), Has(caps, "hoverProvider"), Has(caps, "definitionProvider"),
            Has(caps, "documentFormattingProvider"), Has(caps, "signatureHelpProvider"), sigTriggers.Distinct().ToList());
    }

    // ---- document sync -------------------------------------------------------------------------------------------------

    public Task DidOpenAsync(string uri, string languageId, int version, string text) =>
        _rpc.NotifyAsync("textDocument/didOpen", new { textDocument = new { uri, languageId, version, text } });

    public Task DidChangeAsync(string uri, int version, IReadOnlyList<TextDocumentContentChange> changes) =>
        _rpc.NotifyAsync("textDocument/didChange", new { textDocument = new { uri, version }, contentChanges = changes });

    public Task DidSaveAsync(string uri, string? text) =>
        _rpc.NotifyAsync("textDocument/didSave", new { textDocument = new { uri }, text = Capabilities.SupportsSave ? text : null });

    public Task DidCloseAsync(string uri) => _rpc.NotifyAsync("textDocument/didClose", new { textDocument = new { uri } });

    // ---- requests ------------------------------------------------------------------------------------------------------

    private static object Doc(string uri, Position p) => new { textDocument = new { uri }, position = p };

    public async Task<CompletionList?> CompletionAsync(string uri, Position pos, string? triggerCharacter, CancellationToken ct)
    {
        if (!Capabilities.Completion) return null;
        object context = triggerCharacter is null ? new { triggerKind = 1 } : new { triggerKind = 2, triggerCharacter };
        JsonElement r = await _rpc.RequestAsync("textDocument/completion", new { textDocument = new { uri }, position = pos, context }, ct).ConfigureAwait(false);
        return r.ValueKind switch
        {
            JsonValueKind.Array => new CompletionList(false, r.Deserialize<List<CompletionItem>>(J) ?? []),
            JsonValueKind.Object => r.Deserialize<CompletionList>(J),
            _ => null,
        };
    }

    public async Task<CompletionItem?> ResolveCompletionAsync(CompletionItem item, CancellationToken ct)
    {
        if (!Capabilities.CompletionResolve) return item;
        return await _rpc.RequestAsync<CompletionItem>("completionItem/resolve", item, ct).ConfigureAwait(false) ?? item;
    }

    public async Task<Hover?> HoverAsync(string uri, Position pos, CancellationToken ct) =>
        Capabilities.Hover ? await _rpc.RequestAsync<Hover>("textDocument/hover", Doc(uri, pos), ct).ConfigureAwait(false) : null;

    public async Task<IReadOnlyList<Location>> DefinitionAsync(string uri, Position pos, CancellationToken ct)
    {
        if (!Capabilities.Definition) return [];
        JsonElement r = await _rpc.RequestAsync("textDocument/definition", Doc(uri, pos), ct).ConfigureAwait(false);
        return ParseLocations(r);
    }

    internal static IReadOnlyList<Location> ParseLocations(JsonElement r)
    {
        static Location? One(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            if (e.TryGetProperty("uri", out var u) && e.TryGetProperty("range", out var rg))
                return new Location(u.GetString()!, rg.Deserialize<LspRange>(J));
            if (e.TryGetProperty("targetUri", out var tu)) // LocationLink
            {
                var rg2 = e.TryGetProperty("targetSelectionRange", out var sr) ? sr : e.GetProperty("targetRange");
                return new Location(tu.GetString()!, rg2.Deserialize<LspRange>(J));
            }
            return null;
        }
        if (r.ValueKind == JsonValueKind.Object) return One(r) is { } l ? [l] : [];
        if (r.ValueKind != JsonValueKind.Array) return [];
        return r.EnumerateArray().Select(One).OfType<Location>().ToList();
    }

    /// <summary>Always sent as an explicit invocation: gopls answers null for a typed "," trigger but correctly for the same position invoked.</summary>
    public async Task<SignatureHelp?> SignatureHelpAsync(string uri, Position pos, CancellationToken ct)
    {
        if (!Capabilities.SignatureHelp) return null;
        return await _rpc.RequestAsync<SignatureHelp>("textDocument/signatureHelp",
            new { textDocument = new { uri }, position = pos, context = new { triggerKind = 1, isRetrigger = false } }, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TextEdit>> FormattingAsync(string uri, int tabSize, bool insertSpaces, CancellationToken ct)
    {
        if (!Capabilities.Formatting) return [];
        return await _rpc.RequestAsync<List<TextEdit>>("textDocument/formatting",
            new { textDocument = new { uri }, options = new { tabSize, insertSpaces } }, ct).ConfigureAwait(false) ?? [];
    }

    // ---- lifetime ------------------------------------------------------------------------------------------------------

    /// <summary>shutdown + exit. Returns false when the server did not answer within <paramref name="timeout"/> (the caller kills the process).</summary>
    public async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        if (_exited) return true;
        _exited = true;
        if (_rpc.IsClosed) return true;
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await _rpc.RequestAsync("shutdown", null, cts.Token).ConfigureAwait(false);
            await _rpc.NotifyAsync("exit", null).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or RpcException) { return false; }
    }

    public ValueTask DisposeAsync() => _rpc.DisposeAsync();
}
