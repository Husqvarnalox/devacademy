using System.Text;
using DevAcademy.LanguageServices.JsonRpc;

namespace DevAcademy.LanguageServices.Tests;

public class FramingTests
{
    private static MessageReader ReaderOf(PipeEnd pipe) => new(pipe.Reader);

    [Fact]
    public async Task Reads_a_frame_delivered_one_byte_at_a_time()
    {
        var pipe = new PipeEnd();
        var reader = ReaderOf(pipe);
        byte[] frame = Raw.Frame("{\"jsonrpc\":\"2.0\",\"method\":\"x\"}");
        var read = reader.ReadAsync(CancellationToken.None);
        foreach (byte b in frame) pipe.Writer.Write([b]);
        Assert.Equal("{\"jsonrpc\":\"2.0\",\"method\":\"x\"}", Encoding.UTF8.GetString((await read)!));
    }

    [Fact]
    public async Task Reads_several_frames_arriving_in_one_chunk()
    {
        var pipe = new PipeEnd();
        var chunk = Raw.Frame("{\"a\":1}").Concat(Raw.Frame("{\"b\":2}")).Concat(Raw.Frame("{\"c\":3}")).ToArray();
        pipe.Writer.Write(chunk);
        var reader = ReaderOf(pipe);
        Assert.Equal("{\"a\":1}", Encoding.UTF8.GetString((await reader.ReadAsync(default))!));
        Assert.Equal("{\"b\":2}", Encoding.UTF8.GetString((await reader.ReadAsync(default))!));
        Assert.Equal("{\"c\":3}", Encoding.UTF8.GetString((await reader.ReadAsync(default))!));
    }

    [Fact]
    public async Task Frame_split_inside_header_and_inside_body()
    {
        var pipe = new PipeEnd();
        byte[] frame = Raw.Frame("{\"k\":\"value\"}");
        var reader = ReaderOf(pipe);
        var task = reader.ReadAsync(default);
        pipe.Writer.Write(frame.AsSpan(0, 10));          // inside "Content-Length"
        pipe.Writer.Write(frame.AsSpan(10, 12));         // through the blank line
        pipe.Writer.Write(frame.AsSpan(22, 3));          // part of body
        pipe.Writer.Write(frame.AsSpan(25));
        Assert.Equal("{\"k\":\"value\"}", Encoding.UTF8.GetString((await task)!));
    }

    [Fact]
    public async Task Length_counts_utf8_bytes_not_characters()
    {
        string json = "{\"text\":\"Привет, мир 😀 ж\"}";
        Assert.NotEqual(json.Length, Encoding.UTF8.GetByteCount(json));
        var pipe = new PipeEnd();
        pipe.Writer.Write(Raw.Frame(json));
        pipe.Writer.Write(Raw.Frame("{\"next\":true}"));
        var reader = ReaderOf(pipe);
        Assert.Equal(json, Encoding.UTF8.GetString((await reader.ReadAsync(default))!));
        Assert.Equal("{\"next\":true}", Encoding.UTF8.GetString((await reader.ReadAsync(default))!));
    }

    [Fact]
    public async Task Multibyte_character_split_across_reads()
    {
        string json = "{\"t\":\"😀\"}";
        byte[] frame = Raw.Frame(json);
        int emoji = Array.IndexOf(frame, (byte)0xF0);
        var pipe = new PipeEnd();
        var task = ReaderOf(pipe).ReadAsync(default);
        pipe.Writer.Write(frame.AsSpan(0, emoji + 2));
        pipe.Writer.Write(frame.AsSpan(emoji + 2));
        Assert.Equal(json, Encoding.UTF8.GetString((await task)!));
    }

    [Fact]
    public async Task Header_name_is_case_insensitive_and_extra_headers_are_ignored()
    {
        var pipe = new PipeEnd();
        pipe.Writer.Write(Encoding.ASCII.GetBytes("content-type: application/vscode-jsonrpc; charset=utf-8\r\ncontent-length: 2\r\n\r\n{}"));
        Assert.Equal("{}", Encoding.UTF8.GetString((await ReaderOf(pipe).ReadAsync(default))!));
    }

    [Fact]
    public async Task End_of_stream_returns_null_and_truncated_body_throws()
    {
        var pipe = new PipeEnd();
        pipe.Complete();
        Assert.Null(await ReaderOf(pipe).ReadAsync(default));

        var cut = new PipeEnd();
        cut.Writer.Write(Encoding.ASCII.GetBytes("Content-Length: 10\r\n\r\n{\"a\""));
        cut.Complete();
        await Assert.ThrowsAsync<EndOfStreamException>(() => ReaderOf(cut).ReadAsync(default));
    }

    [Fact]
    public async Task Missing_content_length_is_a_protocol_error()
    {
        var pipe = new PipeEnd();
        pipe.Writer.Write(Encoding.ASCII.GetBytes("X-Other: 1\r\n\r\n{}"));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReaderOf(pipe).ReadAsync(default));
    }

    [Fact]
    public async Task Large_message_larger_than_the_internal_buffer()
    {
        string json = "{\"s\":\"" + new string('x', 100_000) + "\"}";
        var pipe = new PipeEnd();
        byte[] f = Raw.Frame(json);
        for (int i = 0; i < f.Length; i += 7000) pipe.Writer.Write(f.AsSpan(i, Math.Min(7000, f.Length - i)));
        Assert.Equal(json, Encoding.UTF8.GetString((await ReaderOf(pipe).ReadAsync(default))!));
    }
}
