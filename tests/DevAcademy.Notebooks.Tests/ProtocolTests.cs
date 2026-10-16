using System.Text;
using System.Text.Json.Nodes;
using DevAcademy.Notebooks.Protocol;
using DevAcademy.Notebooks.Transport;

namespace DevAcademy.Notebooks.Tests;

public class ProtocolTests
{
    [Fact]
    public void Hmac_matches_the_reference_value_computed_with_python()
    {
        // python: hmac.new(b'secret', b'{"a":1}{}{}{"b":2}', hashlib.sha256).hexdigest()
        var signer = new MessageSigner("secret");
        var sig = signer.Sign("{\"a\":1}"u8, "{}"u8, "{}"u8, "{\"b\":2}"u8);
        Assert.Equal("9b14d94617bffb278285cea390d9dc17e01ec3188dba313f9548021906557a7b", sig);
        Assert.Equal(64, sig.Length);
        Assert.True(signer.Verify(sig, "{\"a\":1}"u8, "{}"u8, "{}"u8, "{\"b\":2}"u8));
        Assert.False(signer.Verify(sig, "{\"a\":2}"u8, "{}"u8, "{}"u8, "{\"b\":2}"u8));
    }

    [Fact]
    public void Encode_produces_the_documented_frame_layout()
    {
        var signer = new MessageSigner("k");
        var msg = JupyterMessage.ExecuteRequest("sess", "1+1");
        var frames = WireCodec.Encode(msg, signer);
        Assert.Equal(6, frames.Count);
        Assert.Equal("<IDS|MSG>", Encoding.ASCII.GetString(frames[0]));
        Assert.Equal(signer.Sign(frames[2], frames[3], frames[4], frames[5]), Encoding.ASCII.GetString(frames[1]));
        var header = JsonNode.Parse(frames[2])!.AsObject();
        Assert.Equal("execute_request", header["msg_type"]!.GetValue<string>());
        Assert.Equal("sess", header["session"]!.GetValue<string>());
        Assert.Equal("5.3", header["version"]!.GetValue<string>());
        var content = JsonNode.Parse(frames[5])!.AsObject();
        Assert.Equal("1+1", content["code"]!.GetValue<string>());
        Assert.False(content["silent"]!.GetValue<bool>());
        Assert.True(content["store_history"]!.GetValue<bool>());
        Assert.False(content["allow_stdin"]!.GetValue<bool>());
        Assert.Equal("{}", Encoding.ASCII.GetString(frames[3]));
    }

    [Fact]
    public void Decode_round_trips_identities_and_buffers()
    {
        var signer = new MessageSigner("k");
        var msg = new JupyterMessage
        {
            Identities = [[1, 2, 3], [9]],
            Header = JupyterMessage.NewHeader("stream", "s"),
            ParentHeader = JupyterMessage.NewHeader("execute_request", "s"),
            Content = new JsonObject { ["name"] = "stdout", ["text"] = "привет \"q\"\n" },
            Buffers = [[0xDE, 0xAD]],
        };
        var back = WireCodec.Decode(WireCodec.Encode(msg, signer), signer);
        Assert.Equal(2, back.Identities.Count);
        Assert.Equal(new byte[] { 1, 2, 3 }, back.Identities[0]);
        Assert.Equal("stream", back.MsgType);
        Assert.Equal(msg.ParentMsgId, back.ParentMsgId);
        Assert.Equal("привет \"q\"\n", back.Content["text"]!.GetValue<string>());
        Assert.Equal(new byte[] { 0xDE, 0xAD }, back.Buffers[0]);
    }

    [Fact]
    public void Tampered_content_is_rejected()
    {
        var signer = new MessageSigner("k");
        var frames = WireCodec.Encode(JupyterMessage.ExecuteRequest("s", "1"), signer).ToList();
        frames[5] = Encoding.UTF8.GetBytes("{\"code\":\"import os\"}");
        Assert.Throws<InvalidSignatureException>(() => WireCodec.Decode(frames, signer));
    }

    [Fact]
    public void A_message_signed_with_another_key_is_rejected()
    {
        var frames = WireCodec.Encode(JupyterMessage.ExecuteRequest("s", "1"), new MessageSigner("a"));
        Assert.Throws<InvalidSignatureException>(() => WireCodec.Decode(frames, new MessageSigner("b")));
    }

    [Fact]
    public void Missing_delimiter_and_short_messages_are_malformed()
    {
        var signer = new MessageSigner("k");
        Assert.Throws<MalformedMessageException>(() => WireCodec.Decode([[1], [2]], signer));
        Assert.Throws<MalformedMessageException>(() => WireCodec.Decode([WireCodec.Delimiter, [], [], []], signer));
    }

    [Fact]
    public void Invalid_json_with_a_valid_signature_is_malformed()
    {
        var signer = new MessageSigner("k");
        byte[] bad = Encoding.UTF8.GetBytes("{not json");
        var sig = Encoding.ASCII.GetBytes(signer.Sign(bad, "{}"u8, "{}"u8, "{}"u8));
        Assert.Throws<MalformedMessageException>(() => WireCodec.Decode([WireCodec.Delimiter, sig, bad, "{}"u8.ToArray(), "{}"u8.ToArray(), "{}"u8.ToArray()], signer));
    }

    [Fact]
    public void An_empty_key_disables_signing()
    {
        var signer = new MessageSigner("");
        var frames = WireCodec.Encode(JupyterMessage.KernelInfoRequest("s"), signer);
        Assert.Empty(frames[1]);
        Assert.Equal("kernel_info_request", WireCodec.Decode(frames, signer).MsgType);
    }

    [Fact]
    public void Connection_info_uses_loopback_five_distinct_ports_and_a_fresh_key()
    {
        var a = ConnectionInfo.CreateRandom();
        var b = ConnectionInfo.CreateRandom();
        Assert.Equal("127.0.0.1", a.Ip);
        Assert.Equal("tcp", a.Transport);
        Assert.Equal("hmac-sha256", a.SignatureScheme);
        int[] ports = [a.ShellPort, a.IopubPort, a.StdinPort, a.ControlPort, a.HbPort];
        Assert.Equal(5, ports.Distinct().Count());
        Assert.All(ports, p => Assert.InRange(p, 1024, 65535));
        Assert.Equal(64, a.Key.Length);
        Assert.NotEqual(a.Key, b.Key);
        Assert.Equal($"tcp://127.0.0.1:{a.ShellPort}", a.Endpoint(a.ShellPort));
    }

    [Fact]
    public void Connection_file_matches_the_jupyter_schema_and_is_private()
    {
        var info = ConnectionInfo.CreateRandom();
        var path = Path.Combine(Path.GetTempPath(), "da-conn-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            info.WriteTo(path);
            var o = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            foreach (var key in new[] { "ip", "transport", "shell_port", "iopub_port", "stdin_port", "control_port", "hb_port", "signature_scheme", "key" })
                Assert.NotNull(o[key]);
            Assert.Equal(info, ConnectionInfo.FromJson(File.ReadAllText(path)));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally { File.Delete(path); }
    }
}

public class ZmtpTests
{
    [Fact]
    public async Task Multipart_messages_round_trip_including_long_frames()
    {
        using var listener = new ZmtpListener(0);
        var serverTask = listener.AcceptAsync("ROUTER", CancellationToken.None);
        await using var client = await ZmtpSocket.ConnectAsync("127.0.0.1", listener.Port, "DEALER", TimeSpan.FromSeconds(5), CancellationToken.None);
        await using var server = await serverTask;
        Assert.Equal("ROUTER", client.PeerSocketType);
        Assert.Equal("DEALER", server.PeerSocketType);

        var big = new byte[70_000];
        Random.Shared.NextBytes(big);
        await client.SendAsync([[], "a"u8.ToArray(), big, new byte[255], new byte[256]], CancellationToken.None);
        var got = await server.ReceiveAsync(CancellationToken.None);
        Assert.Equal(5, got.Count);
        Assert.Empty(got[0]);
        Assert.Equal(big, got[2]);
        Assert.Equal(255, got[3].Length);
        Assert.Equal(256, got[4].Length);

        await server.SendAsync([[7]], CancellationToken.None);
        Assert.Equal(new byte[] { 7 }, (await client.ReceiveAsync(CancellationToken.None))[0]);
    }

    [Fact]
    public async Task Subscribe_is_sent_as_a_0x01_message_frame()
    {
        using var listener = new ZmtpListener(0);
        var serverTask = listener.AcceptAsync("PUB", CancellationToken.None);
        await using var client = await ZmtpSocket.ConnectAsync("127.0.0.1", listener.Port, "SUB", TimeSpan.FromSeconds(5), CancellationToken.None);
        await using var server = await serverTask;
        await client.SubscribeAsync("topic"u8.ToArray(), CancellationToken.None);
        var got = await server.ReceiveAsync(CancellationToken.None);
        Assert.Equal(new byte[] { 1, (byte)'t', (byte)'o', (byte)'p', (byte)'i', (byte)'c' }, got[0]);
    }

    [Fact]
    public async Task The_greeting_is_64_bytes_with_the_zmtp_3_signature()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var connect = ZmtpSocket.ConnectAsync("127.0.0.1", port, "DEALER", TimeSpan.FromSeconds(2), CancellationToken.None);
        using var raw = await listener.AcceptTcpClientAsync();
        var stream = raw.GetStream();
        var greeting = new byte[64];
        await stream.ReadExactlyAsync(greeting);
        Assert.Equal(0xFF, greeting[0]);
        Assert.Equal(0x7F, greeting[9]);
        Assert.Equal(3, greeting[10]);
        Assert.Equal("NULL", Encoding.ASCII.GetString(greeting, 12, 4));
        Assert.Equal(0, greeting[32]); // client, not server
        var reply = new byte[64];
        reply[0] = 0xFF; reply[9] = 0x7F; reply[10] = 3;
        Encoding.ASCII.GetBytes("NULL").CopyTo(reply, 12);
        await stream.WriteAsync(reply);
        // The READY command follows: flags 0x04, size, 5 "READY" ... "Socket-Type" "DEALER".
        var head = new byte[2];
        await stream.ReadExactlyAsync(head);
        Assert.Equal(0x04, head[0]);
        var body = new byte[head[1]];
        await stream.ReadExactlyAsync(body);
        Assert.Equal("READY", Encoding.ASCII.GetString(body, 1, 5));
        Assert.Contains("Socket-Type", Encoding.ASCII.GetString(body));
        Assert.Contains("DEALER", Encoding.ASCII.GetString(body));
        raw.Close();
        await Assert.ThrowsAnyAsync<IOException>(() => connect);
    }

    [Fact]
    public async Task Connecting_to_a_closed_port_times_out_with_a_clear_error()
    {
        var port = ConnectionInfo.FreePorts(1)[0];
        var ex = await Assert.ThrowsAsync<ZmtpException>(() => ZmtpSocket.ConnectAsync("127.0.0.1", port, "DEALER", TimeSpan.FromMilliseconds(300), CancellationToken.None));
        Assert.Contains("Cannot connect", ex.Message);
    }

    [Fact]
    public async Task A_peer_that_is_not_zmtp_is_rejected()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var connect = ZmtpSocket.ConnectAsync("127.0.0.1", port, "DEALER", TimeSpan.FromMilliseconds(200), CancellationToken.None);
        using var raw = await listener.AcceptTcpClientAsync();
        await raw.GetStream().WriteAsync(new byte[64]); // all zeros: no 0xFF signature
        await Assert.ThrowsAnyAsync<IOException>(() => connect);
    }
}
