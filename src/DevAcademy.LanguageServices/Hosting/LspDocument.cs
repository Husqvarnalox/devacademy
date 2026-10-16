using DevAcademy.LanguageServices.Protocol;

namespace DevAcademy.LanguageServices.Hosting;

/// <summary>
/// One open buffer on a server session. Survives server restarts (the session re-opens it with the current text). Edits are
/// reported through <see cref="ApplyEdit"/> after the editor changed; they are forwarded incrementally (or as full text for
/// servers that only support full sync). Requests are no-ops returning null/empty while the server is not ready.
/// </summary>
public sealed class LspDocument : IDisposable
{
    private readonly LanguageServerSession _session;
    private readonly Func<string> _getText;
    private readonly object _gate = new();
    private LspClient? _client;
    private int _version;
    private bool _disposed;
    private IReadOnlyList<Diagnostic> _diagnostics = [];

    internal LspDocument(LanguageServerSession session, string path, string lspLanguageId, Func<string> getText)
    {
        _session = session;
        Path = path;
        Uri = FileUri.FromPath(path);
        LanguageId = lspLanguageId;
        _getText = getText;
    }

    public string Path { get; }
    public string Uri { get; }
    public string LanguageId { get; }
    public LanguageServerSession Session => _session;
    public int Version { get { lock (_gate) return _version; } }
    public bool IsOpenOnServer { get { lock (_gate) return _client is not null; } }
    public PositionEncoding Encoding => _session.Capabilities.Encoding;
    public ServerCapabilitiesInfo Capabilities => _session.Capabilities;
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>Raised (on a thread-pool thread) whenever the server published new diagnostics for this file.</summary>
    public event Action<LspDocument>? DiagnosticsChanged;

    internal void SetDiagnostics(PublishDiagnosticsParams p)
    {
        lock (_gate)
        {
            // Results for an older version are superseded by the ones that follow; applying them would misplace squiggles.
            if (p.Version is { } v && _client is not null && v < _version) return;
        }
        _diagnostics = p.Diagnostics;
        DiagnosticsChanged?.Invoke(this);
    }

    internal void ClearDiagnostics()
    {
        if (_diagnostics.Count == 0) return;
        _diagnostics = [];
        DiagnosticsChanged?.Invoke(this);
    }

    /// <summary>Called by the session (on the thread owning the text) once the server is initialised.</summary>
    internal void OpenOn(LspClient client)
    {
        lock (_gate)
        {
            if (_disposed || ReferenceEquals(_client, client)) return;
            _client = client;
            _version = 1;
            _ = client.DidOpenAsync(Uri, LanguageId, _version, _getText());
        }
    }

    internal void Detach()
    {
        lock (_gate) _client = null;
    }

    /// <summary>
    /// The editor replaced <paramref name="removed"/> with <paramref name="inserted"/> at <paramref name="offset"/>;
    /// <paramref name="after"/> is the buffer state after that change.
    /// </summary>
    public void ApplyEdit(ITextBuffer after, int offset, string removed, string inserted)
    {
        lock (_gate)
        {
            if (_client is not { } c || _disposed) return;
            _version++;
            switch (c.Capabilities.TextDocumentSyncKind)
            {
                case 0: break;
                case 1:
                    _ = c.DidChangeAsync(Uri, _version, [new TextDocumentContentChange(null, _getText())]);
                    break;
                default:
                    _ = c.DidChangeAsync(Uri, _version, [ChangeConverter.FromReplacement(after, offset, removed, inserted, c.Encoding)]);
                    break;
            }
        }
    }

    public void NotifySaved()
    {
        lock (_gate)
            if (_client is { } c && !_disposed) _ = c.DidSaveAsync(Uri, _getText());
    }

    private LspClient? Client { get { lock (_gate) return _disposed ? null : _client; } }

    public Task<CompletionList?> CompletionAsync(Position pos, string? trigger, CancellationToken ct) =>
        Client is { } c ? c.CompletionAsync(Uri, pos, trigger, ct) : Task.FromResult<CompletionList?>(null);

    public Task<CompletionItem?> ResolveAsync(CompletionItem item, CancellationToken ct) =>
        Client is { } c ? c.ResolveCompletionAsync(item, ct) : Task.FromResult<CompletionItem?>(item);

    public Task<Hover?> HoverAsync(Position pos, CancellationToken ct) =>
        Client is { } c ? c.HoverAsync(Uri, pos, ct) : Task.FromResult<Hover?>(null);

    public Task<IReadOnlyList<Location>> DefinitionAsync(Position pos, CancellationToken ct) =>
        Client is { } c ? c.DefinitionAsync(Uri, pos, ct) : Task.FromResult<IReadOnlyList<Location>>([]);

    public Task<SignatureHelp?> SignatureHelpAsync(Position pos, CancellationToken ct) =>
        Client is { } c ? c.SignatureHelpAsync(Uri, pos, ct) : Task.FromResult<SignatureHelp?>(null);

    public Task<IReadOnlyList<TextEdit>> FormatAsync(int tabSize, bool insertSpaces, CancellationToken ct) =>
        Client is { } c ? c.FormattingAsync(Uri, tabSize, insertSpaces, ct) : Task.FromResult<IReadOnlyList<TextEdit>>([]);

    public void Dispose()
    {
        LspClient? c;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            c = _client;
            _client = null;
        }
        if (c is not null) _ = c.DidCloseAsync(Uri);
        _session.Release(this);
    }
}
