using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DevAcademy.Notebooks.Transport;

public sealed class ZmtpException(string message, Exception? inner = null) : IOException(message, inner);

/// <summary>
/// One ZMTP 3.0 connection with the NULL security mechanism over TCP, which is the whole of ZeroMQ that Jupyter needs:
/// the client side of DEALER (shell, control, stdin), SUB (iopub) and REQ (heartbeat) sockets, plus the server side used by tests.
/// Multipart messages in and out; no reconnection, no queues (the caller owns a socket per channel).
/// </summary>
/// <remarks>
/// Spec: https://rfc.zeromq.org/spec/23/ (ZMTP 3.0) and https://rfc.zeromq.org/spec/37/. Version 3.0 is announced on purpose: in 3.0
/// subscriptions are ordinary message frames (0x01 + prefix), which every libzmq 4.x peer accepts, and there is no PING/PONG to answer.
/// </remarks>
internal sealed class ZmtpSocket : IAsyncDisposable
{
    public const int MaxFrameBytes = 256 * 1024 * 1024;
    private const byte More = 1, Long = 2, Command = 4;

    private readonly TcpClient _tcp;
    private readonly Stream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private ZmtpSocket(TcpClient tcp)
    {
        _tcp = tcp;
        _stream = tcp.GetStream();
    }

    public string SocketType { get; private set; } = "";
    public string PeerSocketType { get; private set; } = "";

    /// <summary>Connects (retrying while nothing listens yet, the kernel binds after it starts) and performs the handshake.</summary>
    public static async Task<ZmtpSocket> ConnectAsync(string host, int port, string socketType, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var tcp = new TcpClient { NoDelay = true };
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attempt.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await tcp.ConnectAsync(host, port, attempt.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                tcp.Dispose();
                if (DateTime.UtcNow >= deadline) throw new ZmtpException($"Cannot connect to {host}:{port}: {ex.Message}", ex);
                await Task.Delay(50, ct).ConfigureAwait(false);
                continue;
            }

            // Once TCP is up, a failed handshake is not transient: report it instead of retrying forever.
            var socket = new ZmtpSocket(tcp);
            try
            {
                await socket.HandshakeAsync(socketType, asServer: false, attempt.Token).ConfigureAwait(false);
                return socket;
            }
            catch (ZmtpException)
            {
                tcp.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                tcp.Dispose();
                throw new ZmtpException($"ZMTP handshake with {host}:{port} failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>Server side of an accepted connection (used by the in-process fake kernel of the tests).</summary>
    public static async Task<ZmtpSocket> AcceptAsync(TcpClient accepted, string socketType, CancellationToken ct)
    {
        accepted.NoDelay = true;
        var socket = new ZmtpSocket(accepted);
        await socket.HandshakeAsync(socketType, asServer: true, ct).ConfigureAwait(false);
        return socket;
    }

    private async Task HandshakeAsync(string socketType, bool asServer, CancellationToken ct)
    {
        SocketType = socketType;
        var greeting = new byte[64];
        greeting[0] = 0xFF;
        greeting[9] = 0x7F;
        greeting[10] = 3; // major
        greeting[11] = 0; // minor
        Encoding.ASCII.GetBytes("NULL").CopyTo(greeting, 12);
        greeting[32] = asServer ? (byte)1 : (byte)0;
        await _stream.WriteAsync(greeting, ct).ConfigureAwait(false);

        var peer = new byte[64];
        await ReadExactlyAsync(peer, ct).ConfigureAwait(false);
        if (peer[0] != 0xFF || peer[9] != 0x7F) throw new ZmtpException("Peer is not speaking ZMTP.");
        if (peer[10] < 3) throw new ZmtpException($"Unsupported ZMTP version {peer[10]}.{peer[11]} (3.x required).");
        var mechanism = Encoding.ASCII.GetString(peer, 12, 20).TrimEnd('\0');
        if (mechanism != "NULL") throw new ZmtpException($"Unsupported security mechanism '{mechanism}'.");

        // READY command: name "READY" + properties (Socket-Type, Identity).
        var body = new List<byte> { 5 };
        body.AddRange(Encoding.ASCII.GetBytes("READY"));
        AddProperty(body, "Socket-Type", Encoding.ASCII.GetBytes(socketType));
        AddProperty(body, "Identity", []);
        await WriteFrameAsync(body.ToArray(), Command, ct).ConfigureAwait(false);

        var (flags, ready) = await ReadFrameAsync(ct).ConfigureAwait(false);
        if ((flags & Command) == 0) throw new ZmtpException("Expected a READY command.");
        var nameLen = ready[0];
        var name = Encoding.ASCII.GetString(ready, 1, nameLen);
        if (name == "ERROR") throw new ZmtpException("Peer rejected the connection: " + Encoding.UTF8.GetString(ready, 1 + nameLen, ready.Length - 1 - nameLen));
        if (name != "READY") throw new ZmtpException($"Expected READY, got {name}.");
        PeerSocketType = ParseSocketType(ready, 1 + nameLen);
    }

    private static void AddProperty(List<byte> body, string name, byte[] value)
    {
        body.Add((byte)name.Length);
        body.AddRange(Encoding.ASCII.GetBytes(name));
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)value.Length);
        body.AddRange(len.ToArray());
        body.AddRange(value);
    }

    private static string ParseSocketType(byte[] data, int offset)
    {
        var i = offset;
        while (i < data.Length)
        {
            int nl = data[i++];
            var name = Encoding.ASCII.GetString(data, i, nl);
            i += nl;
            var vl = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i, 4));
            i += 4;
            if (name.Equals("Socket-Type", StringComparison.OrdinalIgnoreCase)) return Encoding.ASCII.GetString(data, i, vl);
            i += vl;
        }
        return "";
    }

    /// <summary>Sends one multipart message atomically (frames of concurrent senders never interleave).</summary>
    public async Task SendAsync(IReadOnlyList<byte[]> frames, CancellationToken ct)
    {
        if (frames.Count == 0) throw new ArgumentException("A message needs at least one frame.", nameof(frames));
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var i = 0; i < frames.Count; i++)
                await WriteFrameAsync(frames[i], (byte)(i < frames.Count - 1 ? More : 0), ct).ConfigureAwait(false);
        }
        finally { _sendLock.Release(); }
    }

    /// <summary>SUB: subscribes to messages whose first frame starts with <paramref name="prefix"/> (empty = everything).</summary>
    public Task SubscribeAsync(byte[] prefix, CancellationToken ct) => SendAsync([[1, .. prefix]], ct);

    /// <summary>Receives the next complete message. Command frames (ERROR, PING...) are handled here and never returned.</summary>
    public async Task<IReadOnlyList<byte[]>> ReceiveAsync(CancellationToken ct)
    {
        var message = new List<byte[]>();
        while (true)
        {
            var (flags, body) = await ReadFrameAsync(ct).ConfigureAwait(false);
            if ((flags & Command) != 0)
            {
                if (body.Length > 0 && Encoding.ASCII.GetString(body, 1, Math.Min(body[0], body.Length - 1)) == "ERROR")
                    throw new ZmtpException("Peer sent ERROR.");
                continue;
            }
            message.Add(body);
            if ((flags & More) == 0) return message;
        }
    }

    private async Task WriteFrameAsync(byte[] body, byte flags, CancellationToken ct)
    {
        if (body.Length < 256)
        {
            var buf = new byte[2 + body.Length];
            buf[0] = flags;
            buf[1] = (byte)body.Length;
            body.CopyTo(buf, 2);
            await _stream.WriteAsync(buf, ct).ConfigureAwait(false);
        }
        else
        {
            var buf = new byte[9 + body.Length];
            buf[0] = (byte)(flags | Long);
            BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(1, 8), (ulong)body.Length);
            body.CopyTo(buf, 9);
            await _stream.WriteAsync(buf, ct).ConfigureAwait(false);
        }
    }

    private async Task<(byte Flags, byte[] Body)> ReadFrameAsync(CancellationToken ct)
    {
        var head = new byte[1];
        await ReadExactlyAsync(head, ct).ConfigureAwait(false);
        var flags = head[0];
        if ((flags & ~(More | Long | Command)) != 0) throw new ZmtpException("Invalid frame flags.");
        ulong size;
        if ((flags & Long) != 0)
        {
            var l = new byte[8];
            await ReadExactlyAsync(l, ct).ConfigureAwait(false);
            size = BinaryPrimitives.ReadUInt64BigEndian(l);
        }
        else
        {
            var s = new byte[1];
            await ReadExactlyAsync(s, ct).ConfigureAwait(false);
            size = s[0];
        }
        if (size > MaxFrameBytes) throw new ZmtpException($"Frame of {size} bytes exceeds the {MaxFrameBytes} byte limit.");
        var body = new byte[size];
        await ReadExactlyAsync(body, ct).ConfigureAwait(false);
        return (flags, body);
    }

    private async Task ReadExactlyAsync(byte[] buffer, CancellationToken ct)
    {
        try { await _stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false); }
        catch (EndOfStreamException ex) { throw new ZmtpException("Connection closed by peer.", ex); }
    }

    public ValueTask DisposeAsync()
    {
        _tcp.Dispose();
        _sendLock.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Listens on a loopback port and hands out server-side ZMTP sockets. Test infrastructure for the fake kernel.</summary>
internal sealed class ZmtpListener : IDisposable
{
    private readonly TcpListener _listener;

    public ZmtpListener(int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async Task<ZmtpSocket> AcceptAsync(string socketType, CancellationToken ct)
    {
        var tcp = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
        return await ZmtpSocket.AcceptAsync(tcp, socketType, ct).ConfigureAwait(false);
    }

    public void Dispose() => _listener.Stop();
}
