using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevAcademy.LanguageServices.Protocol;

// Only the messages DevAcademy uses. Names follow the specification (camelCase on the wire).

public readonly record struct Position(int Line, int Character);
public readonly record struct LspRange(Position Start, Position End);
public sealed record Location(string Uri, LspRange Range);
public sealed record TextEdit(LspRange Range, string NewText);
public sealed record TextDocumentContentChange(LspRange? Range, string Text);

public enum DiagnosticSeverity { Error = 1, Warning = 2, Information = 3, Hint = 4 }

public sealed record Diagnostic(LspRange Range, DiagnosticSeverity? Severity, JsonElement? Code, string? Source, string Message)
{
    public DiagnosticSeverity EffectiveSeverity => Severity ?? DiagnosticSeverity.Error;
    public string? CodeText => Code is { ValueKind: JsonValueKind.String or JsonValueKind.Number } c ? c.ToString() : null;
}

public sealed record PublishDiagnosticsParams(string Uri, int? Version, List<Diagnostic> Diagnostics);

/// <summary>Completion item. documentation/textEdit are polymorphic on the wire and kept raw; use the helpers.</summary>
public sealed record CompletionItem
{
    public string Label { get; init; } = "";
    public int? Kind { get; init; }
    public string? Detail { get; init; }
    public JsonElement? Documentation { get; init; }
    public string? SortText { get; init; }
    public string? FilterText { get; init; }
    public string? InsertText { get; init; }
    public int? InsertTextFormat { get; init; }
    public JsonElement? TextEdit { get; init; }
    public List<TextEdit>? AdditionalTextEdits { get; init; }
    public JsonElement? Data { get; init; }
    public bool? Deprecated { get; init; }

    public string TextToInsert => InsertText ?? Label;
    public bool IsSnippet => InsertTextFormat == 2;

    /// <summary>The edit range from textEdit (TextEdit or InsertReplaceEdit; for the latter the insert range) and its new text.</summary>
    public (LspRange Range, string NewText)? PrimaryEdit()
    {
        if (TextEdit is not { ValueKind: JsonValueKind.Object } e || !e.TryGetProperty("newText", out var nt)) return null;
        JsonElement r;
        if (e.TryGetProperty("range", out var rr)) r = rr;
        else if (e.TryGetProperty("insert", out var ins)) r = ins;
        else return null;
        var range = r.Deserialize<LspRange>(JsonRpc.JsonRpcConnection.Json);
        return (range, nt.GetString() ?? "");
    }
}

public sealed record CompletionList(bool IsIncomplete, List<CompletionItem> Items);

public sealed record Hover(JsonElement Contents, LspRange? Range);

public sealed record ParameterInformation(JsonElement Label, JsonElement? Documentation);
public sealed record SignatureInformation(string Label, JsonElement? Documentation, List<ParameterInformation>? Parameters, int? ActiveParameter);
public sealed record SignatureHelp(List<SignatureInformation> Signatures, int? ActiveSignature, int? ActiveParameter);

/// <summary>What the editor needs to know about the server after initialize.</summary>
public sealed record ServerCapabilitiesInfo(
    string? ServerName,
    string? ServerVersion,
    PositionEncoding Encoding,
    int TextDocumentSyncKind,
    bool SupportsSave,
    bool Completion,
    bool CompletionResolve,
    IReadOnlyList<string> CompletionTriggerCharacters,
    bool Hover,
    bool Definition,
    bool Formatting,
    bool SignatureHelp,
    IReadOnlyList<string> SignatureTriggerCharacters)
{
    public static ServerCapabilitiesInfo None { get; } = new(null, null, PositionEncoding.Utf16, 1, false, false, false, [], false, false, false, false, []);
}

/// <summary>Flattening of LSP "MarkupContent | MarkedString | MarkedString[] | string".</summary>
public static class LspText
{
    public static (string Text, bool IsMarkdown) Flatten(JsonElement? value)
    {
        if (value is not { } v) return ("", false);
        switch (v.ValueKind)
        {
            case JsonValueKind.String: return (v.GetString() ?? "", false);
            case JsonValueKind.Object:
                if (v.TryGetProperty("kind", out var kind) && v.TryGetProperty("value", out var val))
                    return (val.GetString() ?? "", kind.GetString() == "markdown");
                if (v.TryGetProperty("language", out var lang) && v.TryGetProperty("value", out var code)) // MarkedString
                    return ($"```{lang.GetString()}\n{code.GetString()}\n```", true);
                return ("", false);
            case JsonValueKind.Array:
                var parts = new List<string>();
                bool md = false;
                foreach (var item in v.EnumerateArray())
                {
                    var (t, m) = Flatten(item);
                    if (t.Length == 0) continue;
                    parts.Add(t);
                    md |= m || item.ValueKind == JsonValueKind.Object;
                }
                return (string.Join("\n\n", parts), md);
            default: return ("", false);
        }
    }
}
