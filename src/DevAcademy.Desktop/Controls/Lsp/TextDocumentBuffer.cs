using AvaloniaEdit.Document;
using DevAcademy.LanguageServices.Protocol;

namespace DevAcademy.Desktop.Controls.Lsp;

/// <summary>Adapts an AvaloniaEdit document (read live, never copied) to the library's line/offset view.</summary>
public sealed class TextDocumentBuffer(TextDocument doc) : ITextBuffer
{
    public int Length => doc.TextLength;
    public int LineCount => doc.LineCount;
    public int GetLineIndex(int offset) => doc.GetLineByOffset(Math.Clamp(offset, 0, doc.TextLength)).LineNumber - 1;
    public int GetLineStart(int line) => doc.GetLineByNumber(line + 1).Offset;
    public int GetLineLength(int line) => doc.GetLineByNumber(line + 1).Length;
    public string GetText(int offset, int length) => doc.GetText(offset, length);
}
