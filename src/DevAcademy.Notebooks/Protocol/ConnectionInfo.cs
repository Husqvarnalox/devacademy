using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevAcademy.Notebooks.Protocol;

/// <summary>The content of a Jupyter connection file (<c>kernel-*.json</c>): where the kernel listens and how messages are signed.</summary>
public sealed record ConnectionInfo
{
    [JsonPropertyName("ip")] public string Ip { get; init; } = "127.0.0.1";
    [JsonPropertyName("transport")] public string Transport { get; init; } = "tcp";
    [JsonPropertyName("shell_port")] public int ShellPort { get; init; }
    [JsonPropertyName("iopub_port")] public int IopubPort { get; init; }
    [JsonPropertyName("stdin_port")] public int StdinPort { get; init; }
    [JsonPropertyName("control_port")] public int ControlPort { get; init; }
    [JsonPropertyName("hb_port")] public int HbPort { get; init; }
    [JsonPropertyName("signature_scheme")] public string SignatureScheme { get; init; } = "hmac-sha256";
    [JsonPropertyName("key")] public string Key { get; init; } = "";
    [JsonPropertyName("kernel_name")] public string KernelName { get; init; } = "python3";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Loopback only, five distinct free ports, a fresh random 256-bit HMAC key.</summary>
    public static ConnectionInfo CreateRandom()
    {
        var ports = FreePorts(5);
        return new ConnectionInfo
        {
            ShellPort = ports[0],
            IopubPort = ports[1],
            StdinPort = ports[2],
            ControlPort = ports[3],
            HbPort = ports[4],
            Key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
        };
    }

    /// <summary>Holds all listeners open at once so the ports are distinct, then releases them for the kernel to bind.</summary>
    public static int[] FreePorts(int count)
    {
        var listeners = new List<TcpListener>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                listeners.Add(l);
            }
            return listeners.Select(l => ((IPEndPoint)l.LocalEndpoint).Port).ToArray();
        }
        finally
        {
            foreach (var l in listeners) l.Stop();
        }
    }

    public string Endpoint(int port) => $"{Transport}://{Ip}:{port}";

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static ConnectionInfo FromJson(string json) =>
        JsonSerializer.Deserialize<ConnectionInfo>(json, Options) ?? throw new JsonException("Empty connection file.");

    /// <summary>Writes the connection file readable by the current user only (it contains the signing key).</summary>
    public void WriteTo(string path)
    {
        File.WriteAllText(path, ToJson());
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
