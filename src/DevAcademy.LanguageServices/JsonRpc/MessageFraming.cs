using System.Text;

namespace DevAcademy.LanguageServices.JsonRpc;

/// <summary>
/// Reads Content-Length framed messages (the LSP base protocol). Tolerates partial reads, several messages in one read and
/// headers split at any byte; the length is in UTF-8 bytes. Returns null at end of stream.
/// </summary>
public sealed class MessageReader(Stream input)
{
    private const int MaxHeaderBytes = 8 * 1024;
    private const int MaxBodyBytes = 256 * 1024 * 1024;

    private byte[] _buffer = new byte[16 * 1024];
    private int _start, _end;

    public async Task<byte[]?> ReadAsync(CancellationToken ct)
    {
        int contentLength = -1;
        while (true)
        {
            // Header block: lines until an empty line.
            int headerEnd = FindHeaderEnd();
            if (headerEnd < 0)
            {
                if (_end - _start > MaxHeaderBytes) throw new InvalidDataException("LSP header block is too large.");
                if (!await FillAsync(ct).ConfigureAwait(false))
                    return _end == _start ? null : throw new EndOfStreamException("Stream ended inside an LSP header.");
                continue;
            }
            string header = Encoding.ASCII.GetString(_buffer, _start, headerEnd - _start);
            _start = headerEnd + 4;
            foreach (string line in header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                if (line.AsSpan(0, colon).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line.AsSpan(colon + 1).Trim(), out int n) && n >= 0)
                    contentLength = n;
            }
            if (contentLength < 0) throw new InvalidDataException("LSP message has no Content-Length header.");
            if (contentLength > MaxBodyBytes) throw new InvalidDataException("LSP message is too large.");
            break;
        }

        var body = new byte[contentLength];
        int have = Math.Min(contentLength, _end - _start);
        Buffer.BlockCopy(_buffer, _start, body, 0, have);
        _start += have;
        while (have < contentLength)
        {
            int n = await input.ReadAsync(body.AsMemory(have, contentLength - have), ct).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException("Stream ended inside an LSP message body.");
            have += n;
        }
        return body;
    }

    private int FindHeaderEnd()
    {
        for (int i = _start; i + 3 < _end; i++)
            if (_buffer[i] == '\r' && _buffer[i + 1] == '\n' && _buffer[i + 2] == '\r' && _buffer[i + 3] == '\n') return i;
        return -1;
    }

    private async Task<bool> FillAsync(CancellationToken ct)
    {
        if (_start == _end) _start = _end = 0;
        else if (_end == _buffer.Length)
        {
            if (_start > 0)
            {
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
                _end -= _start;
                _start = 0;
            }
            else Array.Resize(ref _buffer, _buffer.Length * 2);
        }
        int n = await input.ReadAsync(_buffer.AsMemory(_end), ct).ConfigureAwait(false);
        if (n == 0) return false;
        _end += n;
        return true;
    }
}

public static class MessageWriter
{
    /// <summary>Header + body as one buffer, so a message is a single write.</summary>
    public static byte[] Frame(ReadOnlySpan<byte> body)
    {
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        var all = new byte[header.Length + body.Length];
        header.CopyTo(all, 0);
        body.CopyTo(all.AsSpan(header.Length));
        return all;
    }
}
