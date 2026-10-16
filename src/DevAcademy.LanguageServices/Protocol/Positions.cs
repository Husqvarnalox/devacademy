using System.Text;

namespace DevAcademy.LanguageServices.Protocol;

/// <summary>How the "character" of an LSP position counts: UTF-16 code units (default), UTF-8 bytes or Unicode code points.</summary>
public enum PositionEncoding { Utf16, Utf8, Utf32 }

public static class PositionEncodings
{
    public static string ToWire(PositionEncoding e) => e switch { PositionEncoding.Utf8 => "utf-8", PositionEncoding.Utf32 => "utf-32", _ => "utf-16" };

    public static PositionEncoding FromWire(string? s) => s switch
    {
        "utf-8" => PositionEncoding.Utf8,
        "utf-32" => PositionEncoding.Utf32,
        _ => PositionEncoding.Utf16, // absent or unknown means the protocol default
    };
}

/// <summary>Minimal view of an editor buffer: UTF-16 offsets and 0-based lines, as every .NET text control has.</summary>
public interface ITextBuffer
{
    int Length { get; }
    int LineCount { get; }
    int GetLineIndex(int offset);
    int GetLineStart(int line);
    /// <summary>Length without the line terminator.</summary>
    int GetLineLength(int line);
    string GetText(int offset, int length);
}

/// <summary>Plain string implementation (\n, \r\n and \r end a line), used by tests and the workspace mirror.</summary>
public sealed class StringTextBuffer : ITextBuffer
{
    private readonly string _text;
    private readonly List<int> _starts = [0];

    public StringTextBuffer(string text)
    {
        _text = text;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            else if (text[i] != '\r' && text[i] != '\n') continue;
            _starts.Add(i + 1);
        }
    }

    public int Length => _text.Length;
    public int LineCount => _starts.Count;

    public int GetLineIndex(int offset)
    {
        int i = _starts.BinarySearch(offset);
        return i >= 0 ? i : ~i - 1;
    }

    public int GetLineStart(int line) => _starts[line];

    public int GetLineLength(int line)
    {
        if (line + 1 >= _starts.Count) return _text.Length - _starts[line];
        int end = _starts[line + 1];
        if (end > _starts[line] && _text[end - 1] == '\n') end--;
        if (end > _starts[line] && _text[end - 1] == '\r') end--;
        return end - _starts[line];
    }

    public string GetText(int offset, int length) => _text.Substring(offset, length);
}

public static class PositionConverter
{
    /// <summary>Converts a UTF-16 column within a line into the negotiated unit.</summary>
    public static int ToCharacter(string lineText, int utf16Column, PositionEncoding enc)
    {
        utf16Column = Math.Clamp(utf16Column, 0, lineText.Length);
        if (enc == PositionEncoding.Utf16) return utf16Column;
        if (enc == PositionEncoding.Utf8) return Encoding.UTF8.GetByteCount(lineText.AsSpan(0, utf16Column));
        int count = 0;
        for (int i = 0; i < utf16Column; i++)
        {
            count++;
            if (char.IsHighSurrogate(lineText[i]) && i + 1 < utf16Column && char.IsLowSurrogate(lineText[i + 1])) i++;
        }
        return count;
    }

    /// <summary>Converts a character in the negotiated unit to a UTF-16 column. Out of range values clamp to the line end;
    /// a position inside a surrogate pair or multi-byte sequence snaps back to its start.</summary>
    public static int ToUtf16Column(string lineText, int character, PositionEncoding enc)
    {
        if (character <= 0) return 0;
        if (enc == PositionEncoding.Utf16) return Math.Min(character, lineText.Length);
        int units = 0, i = 0;
        while (i < lineText.Length)
        {
            int step = char.IsHighSurrogate(lineText[i]) && i + 1 < lineText.Length && char.IsLowSurrogate(lineText[i + 1]) ? 2 : 1;
            int w = enc == PositionEncoding.Utf32 ? 1 : Encoding.UTF8.GetByteCount(lineText.AsSpan(i, step));
            if (units + w > character) return i;
            units += w;
            i += step;
        }
        return lineText.Length;
    }

    public static Position OffsetToPosition(ITextBuffer buf, int offset, PositionEncoding enc)
    {
        offset = Math.Clamp(offset, 0, buf.Length);
        int line = buf.GetLineIndex(offset);
        int start = buf.GetLineStart(line);
        return new Position(line, ToCharacter(buf.GetText(start, offset - start), offset - start, enc));
    }

    public static int PositionToOffset(ITextBuffer buf, Position pos, PositionEncoding enc)
    {
        if (pos.Line >= buf.LineCount) return buf.Length;
        if (pos.Line < 0) return 0;
        int start = buf.GetLineStart(pos.Line);
        string text = buf.GetText(start, buf.GetLineLength(pos.Line));
        return start + ToUtf16Column(text, pos.Character, enc);
    }
}

/// <summary>Turns an editor change (UTF-16 offsets, evaluated against the text BEFORE the change) into an incremental LSP change.</summary>
public static class ChangeConverter
{
    /// <summary>
    /// Same conversion for editors that report a change AFTER applying it (AvaloniaEdit's Changed event): the text before
    /// <paramref name="offset"/> is untouched by the change, and the removed range is described by the removed text itself.
    /// </summary>
    public static TextDocumentContentChange FromReplacement(ITextBuffer after, int offset, string removedText, string insertedText, PositionEncoding enc)
    {
        var start = PositionConverter.OffsetToPosition(after, offset, enc);
        int lines = 0, lastBreakEnd = 0;
        for (int i = 0; i < removedText.Length; i++)
        {
            if (removedText[i] == '\r' && i + 1 < removedText.Length && removedText[i + 1] == '\n') i++;
            else if (removedText[i] != '\r' && removedText[i] != '\n') continue;
            lines++;
            lastBreakEnd = i + 1;
        }
        // A "\r" removed right before a "\n" that stays (or the reverse) is not representable by counting only the removed text;
        // editors keep \r\n atomic, so the simple count is exact.
        var end = lines == 0
            ? new Position(start.Line, start.Character + PositionConverter.ToCharacter(removedText, removedText.Length, enc))
            : new Position(start.Line + lines, PositionConverter.ToCharacter(removedText.Substring(lastBreakEnd), removedText.Length - lastBreakEnd, enc));
        return new TextDocumentContentChange(new LspRange(start, end), insertedText);
    }

    public static TextDocumentContentChange ToLsp(ITextBuffer before, int offset, int removalLength, string inserted, PositionEncoding enc)
    {
        var start = PositionConverter.OffsetToPosition(before, offset, enc);
        var end = PositionConverter.OffsetToPosition(before, offset + removalLength, enc);
        return new TextDocumentContentChange(new LspRange(start, end), inserted);
    }
}

public static class FileUri
{
    public static string FromPath(string path) => new Uri(Path.GetFullPath(path)).AbsoluteUri;

    public static string ToPath(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.IsFile ? u.LocalPath : uri;

    /// <summary>Stable dictionary key: servers may percent-encode differently from how we built the URI.</summary>
    public static string Key(string uri) => ToPath(uri);
}
