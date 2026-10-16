using System.Text;
using System.Text.RegularExpressions;

namespace DevAcademy.Execution;

/// <summary>A local web server found in a terminal's output.</summary>
public sealed record DevServerEndpoint(string Url, int Port);

/// <summary>
/// Finds the local URL a dev server prints (Vite "Local: http://localhost:5173/", Express "listening on http://localhost:3000",
/// Next/Nuxt "- Local: http://localhost:3000", "http://127.0.0.1:8080", "http://0.0.0.0:4000", "http://[::1]:5173"). Terminal output is
/// colored and arrives in arbitrary chunks, so the detector strips ANSI sequences and only looks at complete lines. It never connects anywhere.
/// </summary>
public sealed partial class DevServerUrlDetector
{
    private const int MaxPendingLine = 4096;

    private readonly StringBuilder _pending = new();

    // CSI (ESC [ ... final), OSC (ESC ] ... BEL or ESC \), and two-character escapes.
    [GeneratedRegex(@"\u001B\[[0-?]*[ -/]*[@-~]|\u001B\][^\u0007\u001B]*(?:\u0007|\u001B\\)|\u001B[@-Z\\-_]")]
    private static partial Regex Ansi();

    [GeneratedRegex(@"https?://(?:localhost|127\.0\.0\.1|0\.0\.0\.0|\[::1?\]|\[::\]):(\d{2,5})(?:/[^\s""'<>)\]]*)?", RegexOptions.IgnoreCase)]
    private static partial Regex LocalUrl();

    public static string StripAnsi(string text) => Ansi().Replace(text, "");

    /// <summary>First local URL in <paramref name="text"/> (already complete lines), or null.</summary>
    public static DevServerEndpoint? Find(string text)
    {
        foreach (Match m in LocalUrl().Matches(StripAnsi(text)))
        {
            if (!int.TryParse(m.Groups[1].Value, out var port) || port is < 1 or > 65535) continue;
            var url = m.Value.TrimEnd('.', ',', ';');
            // 0.0.0.0 / :: mean "all interfaces"; the browser needs a name it can connect to.
            url = Regex.Replace(url, @"^(https?://)(?:0\.0\.0\.0|\[::\])", "${1}localhost", RegexOptions.IgnoreCase);
            return new DevServerEndpoint(url, port);
        }
        return null;
    }

    /// <summary>Feeds a chunk of terminal output; returns the endpoint when a complete line just revealed one.</summary>
    public DevServerEndpoint? Feed(string chunk)
    {
        lock (_pending)
        {
            _pending.Append(chunk);
            var all = _pending.ToString();
            var cut = all.LastIndexOfAny(['\n', '\r']);
            if (cut < 0)
            {
                if (_pending.Length > MaxPendingLine) _pending.Clear();
                return null;
            }
            var complete = all[..(cut + 1)];
            _pending.Clear().Append(all[(cut + 1)..]);
            if (_pending.Length > MaxPendingLine) _pending.Clear();
            return Find(complete);
        }
    }
}
