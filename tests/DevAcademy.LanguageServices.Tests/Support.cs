using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.JsonRpc;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.LanguageServices.Tests;

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

/// <summary>Raw message helpers for tests that play the server by hand.</summary>
public static class Raw
{
    public static byte[] Frame(string json) => MessageWriterFor(Encoding.UTF8.GetBytes(json));
    private static byte[] MessageWriterFor(byte[] body) => MessageWriter.Frame(body);

    public static async Task<JsonDocument> ReadAsync(MessageReader reader)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var body = await reader.ReadAsync(cts.Token) ?? throw new EndOfStreamException();
        return JsonDocument.Parse(body);
    }
}

/// <summary>A scriptable in-process language server (the "other side" of a client connection).</summary>
public sealed class FakeServer : IServerProcess
{
    private readonly PipeEnd _toServer = new(), _toClient = new();
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public JsonRpcConnection Rpc { get; }
    public List<string> Methods { get; } = [];
    public List<JsonElement> Params { get; } = [];
    public bool ShutdownSeen, ExitSeen;
    public object Capabilities { get; set; } = new
    {
        textDocumentSync = 2,
        completionProvider = new { triggerCharacters = new[] { "." }, resolveProvider = true },
        hoverProvider = true,
        definitionProvider = true,
        documentFormattingProvider = true,
        signatureHelpProvider = new { triggerCharacters = new[] { "(", "," } },
    };

    public FakeServer()
    {
        Rpc = new JsonRpcConnection(_toServer.Reader, _toClient.Writer);
        Rpc.OnRequest("initialize", (_, _) => Task.FromResult<object?>(new { capabilities = Capabilities, serverInfo = new { name = "fake", version = "9.9" } }));
        Rpc.OnRequest("shutdown", (_, _) => { ShutdownSeen = true; return Task.FromResult<object?>(null); });
        Rpc.OnNotification("exit", _ => { ExitSeen = true; _exited.TrySetResult(); });
    }

    public void Record(params string[] methods)
    {
        foreach (string m in methods)
        {
            Rpc.OnNotification(m, p => { lock (Methods) { Methods.Add(m); Params.Add(p ?? default); } });
        }
    }

    public Stream Output => _toClient.Reader;   // client reads what the server writes
    public Stream Input => _toServer.Writer;    // client writes what the server reads
    public Task Exited => _exited.Task;
    public int? ProcessId => null;
    public string StderrTail => "";
    public void Kill() { _exited.TrySetResult(); _toClient.Complete(); _toServer.Complete(); }
    public ValueTask DisposeAsync() { Kill(); return ValueTask.CompletedTask; }

    public async Task WaitForAsync(Func<bool> condition, int ms = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("condition not reached");
            await Task.Delay(10);
        }
    }
}

public sealed class FakeLauncher(Func<FakeServer>? factory = null) : IServerLauncher
{
    public List<FakeServer> Launched { get; } = [];
    public Func<FakeServer> Factory { get; set; } = factory ?? (() => new FakeServer());

    public Task<IServerProcess> LaunchAsync(ResolvedServer server, string workingDirectory, string searchPath, CancellationToken ct)
    {
        var s = Factory();
        lock (Launched) Launched.Add(s);
        return Task.FromResult<IServerProcess>(s);
    }
}

public static class TestEnv
{
    public static string TempDir(string name)
    {
        string d = Path.Combine(Path.GetTempPath(), "da-lsp-tests", name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    /// <summary>A manager whose servers are fakes: the "fake" definition resolves to /bin/sh (exists everywhere we run).</summary>
    public static LanguageServerManager FakeManager(FakeLauncher launcher, Action<string>? log = null, TimeSpan? idle = null, int maxRestarts = 3)
    {
        string exe = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var def = new LanguageServerDefinition("fake", "Fake LS", ["fakelang", "go"], [new ServerCommand(exe, [])], [], null, [], "install fake");
        return new LanguageServerManager(new LanguageServerManagerOptions
        {
            Definitions = [def],
            Locator = new ServerLocator(null, [Path.GetDirectoryName(Environment.ProcessPath)!]),
            Launcher = launcher,
            IdleTimeout = idle ?? TimeSpan.FromMinutes(5),
            KillTimeout = TimeSpan.FromMilliseconds(500),
            RestartDelays = [TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20)],
            MaxRestarts = maxRestarts,
            Log = log,
        });
    }
}
