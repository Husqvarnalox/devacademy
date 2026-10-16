using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevAcademy.Notebooks.Protocol;

public sealed class InvalidSignatureException(string message) : Exception(message);

public sealed class MalformedMessageException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>HMAC-SHA256 over header, parent_header, metadata and content (the four JSON frames, in order). Empty key = unsigned (empty signature).</summary>
public sealed class MessageSigner(string key)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(key);

    public bool IsEnabled => _key.Length > 0;

    /// <summary>Lower-case hex digest, or "" when signing is disabled.</summary>
    public string Sign(ReadOnlySpan<byte> header, ReadOnlySpan<byte> parent, ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> content)
    {
        if (!IsEnabled) return "";
        using var h = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, _key);
        h.AppendData(header);
        h.AppendData(parent);
        h.AppendData(metadata);
        h.AppendData(content);
        return Convert.ToHexStringLower(h.GetHashAndReset());
    }

    public bool Verify(string signature, ReadOnlySpan<byte> header, ReadOnlySpan<byte> parent, ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> content)
    {
        var expected = Encoding.ASCII.GetBytes(Sign(header, parent, metadata, content));
        var actual = Encoding.ASCII.GetBytes(signature.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}

/// <summary>
/// Wire format: [identities...] "&lt;IDS|MSG&gt;" signature header parent_header metadata content [buffers...].
/// </summary>
public static class WireCodec
{
    public static readonly byte[] Delimiter = "<IDS|MSG>"u8.ToArray();

    public static IReadOnlyList<byte[]> Encode(JupyterMessage message, MessageSigner signer)
    {
        var header = Serialize(message.Header);
        var parent = Serialize(message.ParentHeader);
        var metadata = Serialize(message.Metadata);
        var content = Serialize(message.Content);
        var frames = new List<byte[]>(message.Identities.Count + 6 + message.Buffers.Count);
        frames.AddRange(message.Identities);
        frames.Add(Delimiter);
        frames.Add(Encoding.ASCII.GetBytes(signer.Sign(header, parent, metadata, content)));
        frames.Add(header);
        frames.Add(parent);
        frames.Add(metadata);
        frames.Add(content);
        frames.AddRange(message.Buffers);
        return frames;
    }

    /// <exception cref="InvalidSignatureException">The HMAC does not match.</exception>
    /// <exception cref="MalformedMessageException">Missing delimiter, too few frames or invalid JSON.</exception>
    public static JupyterMessage Decode(IReadOnlyList<byte[]> frames, MessageSigner signer)
    {
        var d = -1;
        for (var i = 0; i < frames.Count; i++)
            if (frames[i].AsSpan().SequenceEqual(Delimiter)) { d = i; break; }
        if (d < 0) throw new MalformedMessageException("No <IDS|MSG> delimiter.");
        if (frames.Count < d + 6) throw new MalformedMessageException("Fewer than five frames after the delimiter.");

        var signature = Encoding.ASCII.GetString(frames[d + 1]);
        byte[] header = frames[d + 2], parent = frames[d + 3], metadata = frames[d + 4], content = frames[d + 5];
        if (signer.IsEnabled && !signer.Verify(signature, header, parent, metadata, content))
            throw new InvalidSignatureException("Message signature mismatch.");

        try
        {
            return new JupyterMessage
            {
                Identities = frames.Take(d).ToList(),
                Header = ParseObject(header),
                ParentHeader = ParseObject(parent),
                Metadata = ParseObject(metadata),
                Content = ParseObject(content),
                Buffers = frames.Skip(d + 6).ToList(),
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new MalformedMessageException("Invalid JSON in a message frame.", ex);
        }
    }

    private static byte[] Serialize(JsonObject o) => Encoding.UTF8.GetBytes(o.ToJsonString());

    private static JsonObject ParseObject(byte[] bytes) =>
        bytes.Length == 0 ? new JsonObject() : JsonNode.Parse(bytes)?.AsObject() ?? new JsonObject();
}
