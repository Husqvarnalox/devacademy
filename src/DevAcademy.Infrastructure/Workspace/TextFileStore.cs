using System.Text;

namespace DevAcademy.Infrastructure.Workspace;

public enum LineEnding { None, Lf, Crlf, Cr }

/// <summary>A decoded text file plus the facts needed to write it back byte-for-byte compatible (BOM, line endings).</summary>
public sealed record TextFileContent(string Text, bool HasBom, LineEnding LineEnding)
{
    /// <summary>SHA-256 of the exact bytes on disk when this content was read or written; used to tell our own saves from external edits.</summary>
    public string Fingerprint { get; init; } = "";

    /// <summary>The line ending to use for a file that has none yet.</summary>
    public static LineEnding PlatformDefault => OperatingSystem.IsWindows() ? LineEnding.Crlf : LineEnding.Lf;
}

public enum TextFileProblem { None, NotFound, TooLarge, Binary, NotUtf8 }

public sealed record TextFileReadResult(TextFileContent? Content, TextFileProblem Problem)
{
    public bool Ok => Content is not null;
}

/// <summary>
/// UTF-8 text files with explicit BOM and line-ending preservation. The editor keeps the text exactly as read;
/// <see cref="SaveAsync"/> converts every line break back to the file's original convention.
/// </summary>
public static class TextFileStore
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    public static async Task<TextFileReadResult> ReadAsync(string path, CancellationToken ct = default)
    {
        FileInfo info;
        try { info = new FileInfo(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return new(null, TextFileProblem.NotFound); }
        if (!info.Exists) return new(null, TextFileProblem.NotFound);
        if (info.Length > MaxBytes) return new(null, TextFileProblem.TooLarge);

        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(path, ct); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(null, TextFileProblem.NotFound); }
        return Decode(bytes);
    }

    public static TextFileReadResult Decode(byte[] bytes)
    {
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var span = bom ? bytes.AsSpan(3) : bytes.AsSpan();
        if (span[..Math.Min(span.Length, 8192)].Contains((byte)0)) return new(null, TextFileProblem.Binary);
        string text;
        try { text = StrictUtf8.GetString(span); }
        catch (DecoderFallbackException) { return new(null, TextFileProblem.NotUtf8); }
        return new(new TextFileContent(text, bom, DetectLineEnding(text)) { Fingerprint = Fingerprint(bytes) }, TextFileProblem.None);
    }

    /// <summary>The dominant line ending (CRLF, LF or lone CR); None when the text has no line break.</summary>
    public static LineEnding DetectLineEnding(string text)
    {
        int crlf = 0, lf = 0, cr = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; }
                else cr++;
            }
            else if (text[i] == '\n') lf++;
        }
        if (crlf + lf + cr == 0) return LineEnding.None;
        if (crlf >= lf && crlf >= cr) return LineEnding.Crlf;
        return lf >= cr ? LineEnding.Lf : LineEnding.Cr;
    }

    /// <summary>Rewrites every line break (CRLF, LF, CR) to <paramref name="ending"/>.</summary>
    public static string Normalize(string text, LineEnding ending)
    {
        var eol = ending switch { LineEnding.Crlf => "\r\n", LineEnding.Cr => "\r", _ => "\n" };
        if (text.IndexOfAny(['\r', '\n']) < 0) return text;
        var sb = new StringBuilder(text.Length + 16);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                sb.Append(eol);
            }
            else if (ch == '\n') sb.Append(eol);
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    public static byte[] Encode(string text, bool bom, LineEnding ending)
    {
        var body = Encoding.UTF8.GetBytes(Normalize(text, ending == LineEnding.None ? TextFileContent.PlatformDefault : ending));
        if (!bom) return body;
        var result = new byte[body.Length + 3];
        result[0] = 0xEF; result[1] = 0xBB; result[2] = 0xBF;
        body.CopyTo(result, 3);
        return result;
    }

    public static string Fingerprint(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

    /// <summary>Fingerprint of the file as it is on disk now; null when it cannot be read.</summary>
    public static async Task<string?> FingerprintOfAsync(string path, CancellationToken ct = default)
    {
        try { return Fingerprint(await File.ReadAllBytesAsync(path, ct)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// Atomic save (temp file + rename) that keeps the BOM state and the line-ending convention of <paramref name="original"/>.
    /// Returns the state of the file as written (including the new fingerprint).
    /// </summary>
    public static async Task<TextFileContent> SaveAsync(string path, string text, TextFileContent? original, CancellationToken ct = default)
    {
        var bom = original?.HasBom ?? false;
        var ending = original?.LineEnding ?? LineEnding.None;
        var bytes = Encode(text, bom, ending);
        await AtomicFile.WriteAllBytesAsync(path, bytes, ct);
        var written = Decode(bytes).Content ?? new TextFileContent(text, bom, ending);
        return written with { Fingerprint = Fingerprint(bytes) };
    }
}
