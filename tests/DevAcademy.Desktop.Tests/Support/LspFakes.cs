using System.Text.Json;
using System.Threading.Channels;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.JsonRpc;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.Desktop.Tests.Support;

/// <summary>In-memory one-way byte pipe: writes become chunks, reads return at most one chunk (like a real pipe, reads may be partial).</summary>
public sealed class PipeEnd
{
    private readonly Channel<byte[]> _ch = Channel.CreateUnbounded<byte[]>();
    public Stream Reader { get; }
    public Stream Writer { get; }

    public PipeEnd()
    {
        Reader = new ReadSide(_ch);
        Writer = new WriteSide(_ch);
    }

    public void Complete() => _ch.Writer.TryComplete();

    private sealed class WriteSide(Channel<byte[]> ch) : Stream
    {
        public override void Write(byte[] buffer, int offset, int count) => ch.Writer.TryWrite(buffer.AsSpan(offset, count).ToArray());
        public override void Write(ReadOnlySpan<byte> buffer) => ch.Writer.TryWrite(buffer.ToArray());
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ch.Writer.TryWrite(buffer.ToArray());
            return ValueTask.CompletedTask;
        }
        public override void Flush() { }
        protected override void Dispose(bool disposing) { if (disposing) ch.Writer.TryComplete(); base.Dispose(disposing); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class ReadSide(Channel<byte[]> ch) : Stream
    {
        private byte[] _cur = [];
        private int _pos;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (_pos >= _cur.Length)
            {
                if (!await ch.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)) return 0;
                if (ch.Reader.TryRead(out var next)) { _cur = next; _pos = 0; }
            }
            int n = Math.Min(buffer.Length, _cur.Length - _pos);
            _cur.AsSpan(_pos, n).CopyTo(buffer.Span);
            _pos += n;
            return n;
        }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Flush() { }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}


/// <summary>
/// A scripted in-process language server for editor tests: answers completion/hover/definition/formatting with fixed data,
/// publishes the diagnostics it is told to, and records document sync notifications.
/// </summary>
public sealed class ScriptedServer : IServerProcess
{
    private readonly PipeEnd _toServer = new(), _toClient = new();
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public JsonRpcConnection Rpc { get; }
    public List<string> Methods { get; } = [];
    public List<JsonElement> Params { get; } = [];
    public string? OpenedUri;

    public ScriptedServer()
    {
        Rpc = new JsonRpcConnection(_toServer.Reader, _toClient.Writer);
        Rpc.OnRequest("initialize", (_, _) => Task.FromResult<object?>(new
        {
            capabilities = new
            {
                textDocumentSync = 2,
                completionProvider = new { triggerCharacters = new[] { "." } },
                hoverProvider = true,
                definitionProvider = true,
                documentFormattingProvider = true,
                signatureHelpProvider = new { triggerCharacters = new[] { "(", "," } },
            },
            serverInfo = new { name = "scripted", version = "1.2.3" },
        }));
        Rpc.OnRequest("shutdown", (_, _) => Task.FromResult<object?>(null));
        Rpc.OnNotification("exit", _ => _exited.TrySetResult());
        foreach (var m in new[] { "initialized", "textDocument/didOpen", "textDocument/didChange", "textDocument/didSave", "textDocument/didClose" })
            Rpc.OnNotification(m, p =>
            {
                lock (Methods) { Methods.Add(m); Params.Add(p ?? default); }
                if (m == "textDocument/didOpen" && p is { } pe) OpenedUri = pe.GetProperty("textDocument").GetProperty("uri").GetString();
            });
        Rpc.OnRequest("textDocument/completion", (_, _) => Task.FromResult<object?>(new
        {
            isIncomplete = false,
            items = new object[]
            {
                new { label = "Println", kind = 3, detail = "func(a ...any)", sortText = "a" },
                new { label = "Printf", kind = 3, detail = "func(format string, a ...any)", sortText = "b" },
                new { label = "Sprintf", kind = 3, sortText = "c" },
            },
        }));
        Rpc.OnRequest("textDocument/hover", (_, _) => Task.FromResult<object?>(new
        {
            contents = new { kind = "markdown", value = "```go\nfunc main()\n```\nEntry point of the **program**." },
        }));
        Rpc.OnRequest("textDocument/definition", (_, _) => Task.FromResult<object?>(new[]
        {
            new { uri = OpenedUri ?? "", range = new { start = new { line = 0, character = 5 }, end = new { line = 0, character = 9 } } },
        }));
        Rpc.OnRequest("textDocument/signatureHelp", (_, _) => Task.FromResult<object?>(new
        {
            signatures = new[] { new { label = "Println(a ...any) (n int, err error)", parameters = new[] { new { label = "a ...any" } } } },
            activeSignature = 0,
            activeParameter = 0,
        }));
        Rpc.OnRequest("textDocument/formatting", (_, _) => Task.FromResult<object?>(Array.Empty<object>()));
    }

    public Task PublishDiagnosticsAsync(int line, int startChar, int endChar, string message, int severity = 1) =>
        Rpc.NotifyAsync("textDocument/publishDiagnostics", new
        {
            uri = OpenedUri,
            diagnostics = new[]
            {
                new
                {
                    range = new { start = new { line, character = startChar }, end = new { line, character = endChar } },
                    severity, source = "scripted", message,
                },
            },
        });

    public int Count(string method) { lock (Methods) return Methods.Count(m => m == method); }

    public Stream Output => _toClient.Reader;
    public Stream Input => _toServer.Writer;
    public Task Exited => _exited.Task;
    public int? ProcessId => null;
    public string StderrTail => "";
    public void Kill() { _exited.TrySetResult(); _toClient.Complete(); _toServer.Complete(); }
    public ValueTask DisposeAsync() { Kill(); return ValueTask.CompletedTask; }
}

public sealed class ScriptedLauncher : IServerLauncher
{
    public List<ScriptedServer> Launched { get; } = [];
    public ScriptedServer? Last { get { lock (Launched) return Launched.LastOrDefault(); } }

    public Task<IServerProcess> LaunchAsync(ResolvedServer server, string workingDirectory, string searchPath, CancellationToken ct)
    {
        var s = new ScriptedServer();
        lock (Launched) Launched.Add(s);
        return Task.FromResult<IServerProcess>(s);
    }

    /// <summary>"go" served by a definition whose executable (/bin/sh) exists everywhere the tests run.</summary>
    public static IReadOnlyList<LanguageServerDefinition> Definitions { get; } =
    [
        new LanguageServerDefinition("scripted", "scripted-ls", ["go"],
            [new ServerCommand(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh", [])], [], null, [], "install scripted-ls"),
    ];

    public static ServerLocator Locator => new(null, [Path.GetDirectoryName(Environment.ProcessPath)!]);
}
