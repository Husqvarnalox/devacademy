using System.Runtime.CompilerServices;
using System.Text;

namespace DevAcademy.AI.Http;

internal sealed record SseEvent(string? Event, string Data);

/// <summary>Minimal Server-Sent Events reader: comment lines (": ...") ignored, multi-line data joined with '\n', blank line dispatches.</summary>
internal static class SseReader
{
    public static async IAsyncEnumerable<SseEvent> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        string? eventName = null;
        var data = new StringBuilder();
        var hasData = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) break;

            if (line.Length == 0)
            {
                if (hasData) yield return new SseEvent(eventName, data.ToString());
                eventName = null; data.Clear(); hasData = false;
                continue;
            }
            if (line[0] == ':') continue;

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? "" : line[(colon + 1)..];
            if (value.StartsWith(' ')) value = value[1..];

            switch (field)
            {
                case "event": eventName = value; break;
                case "data":
                    if (hasData) data.Append('\n');
                    data.Append(value);
                    hasData = true;
                    break;
            }
        }
        if (hasData) yield return new SseEvent(eventName, data.ToString());
    }
}
