using System.Text.Json.Nodes;

namespace DevAcademy.Notebooks.Nbformat;

public enum CellType { Code, Markdown, Raw }

/// <summary>
/// One output of a code cell, in nbformat v4 shape. <see cref="Extra"/> holds fields this model does not know (kept on save).
/// <see cref="Data"/> maps a MIME type to a string, or to a JSON value for <c>application/json</c>-like types.
/// </summary>
public sealed class NotebookOutput
{
    public const string Stream = "stream", DisplayData = "display_data", ExecuteResult = "execute_result", Error = "error";

    public required string OutputType { get; set; }
    /// <summary>stream: "stdout" | "stderr".</summary>
    public string? Name { get; set; }
    public string? Text { get; set; }
    public JsonObject? Data { get; set; }
    public JsonObject? Metadata { get; set; }
    public int? ExecutionCount { get; set; }
    public string? ErrorName { get; set; }
    public string? ErrorValue { get; set; }
    public List<string> Traceback { get; set; } = [];
    public JsonObject Extra { get; } = new();

    public static NotebookOutput StreamOutput(string name, string text) => new() { OutputType = Stream, Name = name, Text = text };

    /// <summary>The string stored under a MIME type, or null.</summary>
    public string? GetText(string mime) => Data?[mime] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

public sealed class NotebookCell
{
    public CellType Type { get; set; }
    /// <summary>nbformat 4.5 cell id (kept when present; generated on save for notebooks of minor version 5 or newer).</summary>
    public string? Id { get; set; }
    public string Source { get; set; } = "";
    public JsonObject Metadata { get; set; } = new();
    public int? ExecutionCount { get; set; }
    public List<NotebookOutput> Outputs { get; set; } = [];
    /// <summary>Markdown/raw cells may carry attachments; kept verbatim.</summary>
    public JsonObject? Attachments { get; set; }
    public JsonObject Extra { get; } = new();

    public static NotebookCell NewCode(string source = "") => new() { Type = CellType.Code, Source = source, Id = NewId() };
    public static NotebookCell NewMarkdown(string source = "") => new() { Type = CellType.Markdown, Source = source, Id = NewId() };

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];
}

public sealed class NotebookDocument
{
    public int NbFormat { get; set; } = 4;
    public int NbFormatMinor { get; set; } = 5;
    public JsonObject Metadata { get; set; } = new();
    public List<NotebookCell> Cells { get; set; } = [];
    public JsonObject Extra { get; } = new();

    /// <summary>An empty Python 3 notebook (nbformat 4.5).</summary>
    public static NotebookDocument CreateNew(bool withEmptyCodeCell = true)
    {
        var doc = new NotebookDocument
        {
            Metadata = new JsonObject
            {
                ["kernelspec"] = new JsonObject { ["display_name"] = "Python 3", ["language"] = "python", ["name"] = "python3" },
                ["language_info"] = new JsonObject { ["name"] = "python" },
            },
        };
        if (withEmptyCodeCell) doc.Cells.Add(NotebookCell.NewCode());
        return doc;
    }
}
