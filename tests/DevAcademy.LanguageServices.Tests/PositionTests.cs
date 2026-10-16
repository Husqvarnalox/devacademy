using System.Text;
using DevAcademy.LanguageServices.Protocol;

namespace DevAcademy.LanguageServices.Tests;

public class PositionTests
{
    // "a😀b" : UTF-16 units a=1, 😀=2, b=1. UTF-8 bytes 1,4,1. UTF-32 1,1,1.
    [Theory]
    [InlineData(PositionEncoding.Utf16, 3, 4)]
    [InlineData(PositionEncoding.Utf8, 5, 6)]
    [InlineData(PositionEncoding.Utf32, 2, 3)]
    public void Emoji_columns_convert_per_encoding(PositionEncoding enc, int afterEmoji, int end)
    {
        const string line = "a😀b";
        Assert.Equal(afterEmoji, PositionConverter.ToCharacter(line, 3, enc));
        Assert.Equal(end, PositionConverter.ToCharacter(line, 4, enc));
        Assert.Equal(3, PositionConverter.ToUtf16Column(line, afterEmoji, enc));
        Assert.Equal(4, PositionConverter.ToUtf16Column(line, end, enc));
    }

    [Fact]
    public void Cyrillic_is_two_bytes_but_one_utf16_unit()
    {
        const string line = "привет";
        Assert.Equal(6, PositionConverter.ToCharacter(line, 6, PositionEncoding.Utf16));
        Assert.Equal(12, PositionConverter.ToCharacter(line, 6, PositionEncoding.Utf8));
        Assert.Equal(3, PositionConverter.ToUtf16Column(line, 6, PositionEncoding.Utf8));
        Assert.Equal(3, PositionConverter.ToUtf16Column(line, 7, PositionEncoding.Utf8)); // inside a 2-byte char snaps back
    }

    [Fact]
    public void Position_inside_a_surrogate_pair_snaps_to_its_start_and_overshoot_clamps()
    {
        Assert.Equal(1, PositionConverter.ToUtf16Column("a😀b", 2, PositionEncoding.Utf8));
        Assert.Equal(4, PositionConverter.ToUtf16Column("a😀b", 100, PositionEncoding.Utf16));
        Assert.Equal(4, PositionConverter.ToUtf16Column("a😀b", 100, PositionEncoding.Utf8));
    }

    [Fact]
    public void Offset_position_roundtrip_over_mixed_line_endings()
    {
        var buf = new StringTextBuffer("one\r\nдва 😀\nthree\rfour");
        Assert.Equal(4, buf.LineCount);
        Assert.Equal(new Position(1, 4), PositionConverter.OffsetToPosition(buf, 5 + 4, PositionEncoding.Utf16));
        Assert.Equal(new Position(1, 6), PositionConverter.OffsetToPosition(buf, 5 + 6, PositionEncoding.Utf16)); // after the emoji: 4 units + 2
        for (int off = 0; off <= buf.Length; off++)
        {
            if (off > 0 && off < buf.Length && char.IsLowSurrogate(buf.GetText(off, 1)[0])) continue;
            foreach (var enc in Enum.GetValues<PositionEncoding>())
            {
                var p = PositionConverter.OffsetToPosition(buf, off, enc);
                // positions between \r and \n do not exist; they map to the line end
                int back = PositionConverter.PositionToOffset(buf, p, enc);
                if (off > 0 && off < buf.Length && buf.GetText(off - 1, 2) == "\r\n") continue;
                Assert.Equal(off, back);
            }
        }
    }

    // ---- incremental changes: the LSP server's view must equal the editor's text after every edit ---------------------

    private static string ApplyLsp(string text, TextDocumentContentChange c, PositionEncoding enc)
    {
        var buf = new StringTextBuffer(text);
        int s = PositionConverter.PositionToOffset(buf, c.Range!.Value.Start, enc), e = PositionConverter.PositionToOffset(buf, c.Range!.Value.End, enc);
        return text[..s] + c.Text + text[e..];
    }

    [Theory]
    [InlineData(PositionEncoding.Utf16)]
    [InlineData(PositionEncoding.Utf8)]
    [InlineData(PositionEncoding.Utf32)]
    public void Random_edits_keep_server_copy_identical_to_editor_text(PositionEncoding enc)
    {
        var rng = new Random(42);
        string[] pieces = ["a", "ж", "😀", "\n", "\r\n", "  ", "fmt.Println(", "щ😀щ", "\n\n", ""];
        string editor = "package main\r\n\r\nfunc main() {\r\n\tfmt.Println(\"Привет 😀\")\r\n}\r\n", server = editor;
        for (int i = 0; i < 2000; i++)
        {
            // choose a removal that never splits a surrogate pair or a \r\n pair (editors keep those atomic)
            int off = rng.Next(editor.Length + 1);
            int len = rng.Next(0, Math.Min(6, editor.Length - off) + 1);
            while (off > 0 && off < editor.Length && (char.IsLowSurrogate(editor[off]) || (editor[off] == '\n' && editor[off - 1] == '\r'))) off--;
            while (off + len > 0 && off + len < editor.Length && (char.IsLowSurrogate(editor[off + len]) || (editor[off + len] == '\n' && editor[off + len - 1] == '\r'))) len++;
            string removed = editor.Substring(off, len);
            string inserted = pieces[rng.Next(pieces.Length)];
            // inserting a lone \r before an existing \n would glue into \r\n; skip that ambiguous case
            if (inserted.EndsWith('\r') || (inserted.Length > 0 && off + len < editor.Length && inserted[^1] == '\r')) continue;
            editor = editor[..off] + inserted + editor[(off + len)..];
            var change = ChangeConverter.FromReplacement(new StringTextBuffer(editor), off, removed, inserted, enc);
            server = ApplyLsp(server, change, enc);
            Assert.Equal(editor, server);
        }
    }

    [Fact]
    public void Before_state_conversion_matches_replacement_conversion()
    {
        string before = "x😀y\nline2\n";
        var a = ChangeConverter.ToLsp(new StringTextBuffer(before), 3, 5, "ZZ", PositionEncoding.Utf16);
        string after = before[..3] + "ZZ" + before[8..];
        var b = ChangeConverter.FromReplacement(new StringTextBuffer(after), 3, before.Substring(3, 5), "ZZ", PositionEncoding.Utf16);
        Assert.Equal(a, b);
        Assert.Equal(new Position(0, 3), a.Range!.Value.Start);
        Assert.Equal(new Position(1, 3), a.Range!.Value.End);
    }

    [Fact]
    public void Insert_at_end_of_document_and_delete_across_lines()
    {
        var ins = ChangeConverter.FromReplacement(new StringTextBuffer("ab\nXY"), 3, "", "XY", PositionEncoding.Utf16);
        Assert.Equal(new LspRange(new Position(1, 0), new Position(1, 0)), ins.Range);
        var del = ChangeConverter.FromReplacement(new StringTextBuffer("a"), 1, "b\ncd", "", PositionEncoding.Utf16);
        Assert.Equal(new LspRange(new Position(0, 1), new Position(1, 2)), del.Range);
    }

    [Fact]
    public void FileUri_roundtrip_with_spaces_and_unicode()
    {
        string p = Path.Combine(Path.GetTempPath(), "dir with space", "файл.go");
        string uri = FileUri.FromPath(p);
        Assert.StartsWith("file://", uri);
        Assert.Equal(Path.GetFullPath(p), FileUri.ToPath(uri));
    }

    [Fact]
    public void Wire_encoding_names()
    {
        Assert.Equal(PositionEncoding.Utf8, PositionEncodings.FromWire("utf-8"));
        Assert.Equal(PositionEncoding.Utf16, PositionEncodings.FromWire(null));
        Assert.Equal("utf-32", PositionEncodings.ToWire(PositionEncoding.Utf32));
        Assert.Equal(4, Encoding.UTF8.GetByteCount("😀"));
    }
}
