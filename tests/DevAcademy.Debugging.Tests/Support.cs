using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using DevAcademy.Debugging.Adapters;
using DevAcademy.LanguageServices.JsonRpc;

namespace DevAcademy.Debugging.Tests;

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


public static class Util
{
    public static string TempDir(string name)
    {
        string tmp = Path.GetTempPath();
        // Debuggers match breakpoints against the real path recorded in debug info; /var is a symlink to /private/var on macOS.
        if (OperatingSystem.IsMacOS() && tmp.StartsWith("/var/", StringComparison.Ordinal)) tmp = "/private" + tmp;
        string d = Path.Combine(tmp, "da-dbg-tests", name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    public static async Task<T> UntilAsync<T>(Func<T?> probe, string what, int ms = 15000) where T : class
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (probe() is { } v) return v;
            await Task.Delay(10);
        }
        throw new TimeoutException("timed out waiting for " + what);
    }

    public static async Task UntilAsync(Func<bool> cond, string what, int ms = 15000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond())
        {
            if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("timed out waiting for " + what);
            await Task.Delay(10);
        }
    }
}
