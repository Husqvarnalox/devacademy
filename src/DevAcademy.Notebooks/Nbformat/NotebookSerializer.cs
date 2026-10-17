using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevAcademy.Domain;

namespace DevAcademy.Notebooks.Nbformat;

public sealed class NotebookFormatException(LocalizedText text, Exception? inner = null) : Exception(text.Fallback, inner), ILocalizedMessage
{
    public LocalizedText Text { get; } = text;
}

/// <summary>
/// nbformat v4 reader/writer. Mirrors the reference implementation's on-disk conventions (sorted keys, indent 1, multiline strings as
/// line arrays, trailing newline) so saving an untouched notebook produces no diff. Fields this model does not know are kept.
/// </summary>
public static partial class NotebookSerializer
{
    private static readonly HashSet<string> NonTextSplitMimes = ["application/javascript", "image/svg+xml"];

    // ------------------------------------------------------------------ read

    public static NotebookDocument Parse(string json)
    {
        JsonObject root;
        try { root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true })?.AsObject() ?? throw new NotebookFormatException(EngineMessages.NotJsonObject()); }
        catch (JsonException ex) { throw new NotebookFormatException(EngineMessages.InvalidJson(ex.Message), ex); }
        catch (InvalidOperationException ex) { throw new NotebookFormatException(EngineMessages.NotNotebook(), ex); }

        var major = root["nbformat"]?.GetValue<int>() ?? 0;
        if (major != 4) throw new NotebookFormatException(major == 0 ? EngineMessages.NotIpynb() : EngineMessages.OnlyV4(major));

        var doc = new NotebookDocument
        {
            NbFormat = 4,
            NbFormatMinor = root["nbformat_minor"]?.GetValue<int>() ?? 0,
            Metadata = (root["metadata"] as JsonObject)?.DeepClone().AsObject() ?? new JsonObject(),
        };
        foreach (var kv in root)
            if (kv.Key is not ("nbformat" or "nbformat_minor" or "metadata" or "cells")) doc.Extra[kv.Key] = kv.Value?.DeepClone();
        try
        {
            foreach (var c in root["cells"]?.AsArray() ?? [])
                doc.Cells.Add(ReadCell(c!.AsObject()));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new NotebookFormatException(EngineMessages.CellStructure(ex.Message), ex);
        }
        return doc;
    }

    private static NotebookCell ReadCell(JsonObject o)
    {
        var type = o["cell_type"]?.GetValue<string>() switch
        {
            "code" => CellType.Code,
            "markdown" => CellType.Markdown,
            "raw" => CellType.Raw,
            var t => throw new NotebookFormatException(EngineMessages.UnknownCellType(t ?? "")),
        };
        var cell = new NotebookCell
        {
            Type = type,
            Id = o["id"]?.GetValue<string>(),
            Source = JoinLines(o["source"]),
            Metadata = (o["metadata"] as JsonObject)?.DeepClone().AsObject() ?? new JsonObject(),
            ExecutionCount = type == CellType.Code ? IntOrNull(o["execution_count"]) : null,
            Attachments = (o["attachments"] as JsonObject)?.DeepClone().AsObject(),
        };
        if (type == CellType.Code)
            foreach (var out_ in o["outputs"]?.AsArray() ?? [])
                cell.Outputs.Add(ReadOutput(out_!.AsObject()));
        foreach (var kv in o)
            if (kv.Key is not ("cell_type" or "id" or "source" or "metadata" or "execution_count" or "outputs" or "attachments"))
                cell.Extra[kv.Key] = kv.Value?.DeepClone();
        return cell;
    }

    private static NotebookOutput ReadOutput(JsonObject o)
    {
        var type = o["output_type"]?.GetValue<string>() ?? throw new NotebookFormatException(EngineMessages.NoOutputType());
        var output = new NotebookOutput { OutputType = type };
        switch (type)
        {
            case NotebookOutput.Stream:
                output.Name = o["name"]?.GetValue<string>() ?? "stdout";
                output.Text = JoinLines(o["text"]);
                break;
            case NotebookOutput.DisplayData:
            case NotebookOutput.ExecuteResult:
                output.Data = ReadMimeBundle(o["data"] as JsonObject);
                output.Metadata = (o["metadata"] as JsonObject)?.DeepClone().AsObject() ?? new JsonObject();
                if (type == NotebookOutput.ExecuteResult) output.ExecutionCount = IntOrNull(o["execution_count"]);
                break;
            case NotebookOutput.Error:
                output.ErrorName = o["ename"]?.GetValue<string>() ?? "";
                output.ErrorValue = o["evalue"]?.GetValue<string>() ?? "";
                output.Traceback = (o["traceback"] as JsonArray)?.Select(t => t?.GetValue<string>() ?? "").ToList() ?? [];
                break;
        }
        foreach (var kv in o)
            if (kv.Key is not ("output_type" or "name" or "text" or "data" or "metadata" or "execution_count" or "ename" or "evalue" or "traceback"))
                output.Extra[kv.Key] = kv.Value?.DeepClone();
        return output;
    }

    private static JsonObject ReadMimeBundle(JsonObject? data)
    {
        var result = new JsonObject();
        if (data is null) return result;
        foreach (var kv in data)
            result[kv.Key] = IsSplitMime(kv.Key) && kv.Value is JsonArray a ? JsonValue.Create(JoinLines(a)) : kv.Value?.DeepClone();
        return result;
    }

    private static bool IsSplitMime(string mime) => mime.StartsWith("text/", StringComparison.Ordinal) || NonTextSplitMimes.Contains(mime);

    private static int? IntOrNull(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    private static string JoinLines(JsonNode? n) => n switch
    {
        null => "",
        JsonArray a => string.Concat(a.Select(x => x?.GetValue<string>() ?? "")),
        _ => n.GetValue<string>(),
    };

    // ------------------------------------------------------------------ write

    public static string Serialize(NotebookDocument doc)
    {
        var root = new JsonObject();
        foreach (var kv in doc.Extra) root[kv.Key] = kv.Value?.DeepClone();
        root["cells"] = new JsonArray(doc.Cells.Select(c => (JsonNode?)WriteCell(c, doc.NbFormatMinor >= 5)).ToArray());
        root["metadata"] = doc.Metadata.DeepClone();
        root["nbformat"] = 4;
        root["nbformat_minor"] = doc.NbFormatMinor;
        return ToSortedJson(root) + "\n";
    }

    private static JsonObject WriteCell(NotebookCell c, bool needsId)
    {
        var o = new JsonObject();
        foreach (var kv in c.Extra) o[kv.Key] = kv.Value?.DeepClone();
        o["cell_type"] = c.Type switch { CellType.Code => "code", CellType.Markdown => "markdown", _ => "raw" };
        if (needsId) o["id"] = c.Id ??= NotebookCell.NewId();
        else if (c.Id is not null) o["id"] = c.Id;
        o["metadata"] = c.Metadata.DeepClone();
        o["source"] = SplitLines(c.Source);
        if (c.Attachments is not null && c.Type != CellType.Code) o["attachments"] = c.Attachments.DeepClone();
        if (c.Type == CellType.Code)
        {
            o["execution_count"] = c.ExecutionCount;
            o["outputs"] = new JsonArray(c.Outputs.Select(x => (JsonNode?)WriteOutput(x)).ToArray());
        }
        return o;
    }

    private static JsonObject WriteOutput(NotebookOutput out_)
    {
        var o = new JsonObject();
        foreach (var kv in out_.Extra) o[kv.Key] = kv.Value?.DeepClone();
        o["output_type"] = out_.OutputType;
        switch (out_.OutputType)
        {
            case NotebookOutput.Stream:
                o["name"] = out_.Name ?? "stdout";
                o["text"] = SplitLines(out_.Text ?? "");
                break;
            case NotebookOutput.DisplayData:
            case NotebookOutput.ExecuteResult:
                var data = new JsonObject();
                foreach (var kv in out_.Data ?? [])
                    data[kv.Key] = IsSplitMime(kv.Key) && kv.Value is JsonValue v && v.TryGetValue<string>(out var s) ? SplitLines(s) : kv.Value?.DeepClone();
                o["data"] = data;
                o["metadata"] = out_.Metadata?.DeepClone() ?? new JsonObject();
                if (out_.OutputType == NotebookOutput.ExecuteResult) o["execution_count"] = out_.ExecutionCount;
                break;
            case NotebookOutput.Error:
                o["ename"] = out_.ErrorName ?? "";
                o["evalue"] = out_.ErrorValue ?? "";
                o["traceback"] = new JsonArray(out_.Traceback.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
                break;
        }
        return o;
    }

    /// <summary>nbformat's multiline string: a list of lines, each keeping its newline; a single-line string stays a one-element list.</summary>
    private static JsonNode SplitLines(string s)
    {
        var arr = new JsonArray();
        if (s.Length == 0) return arr;
        var start = 0;
        for (var i = 0; i < s.Length; i++)
            if (s[i] == '\n') { arr.Add(s[start..(i + 1)]); start = i + 1; }
        if (start < s.Length) arr.Add(s[start..]);
        return arr;
    }

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 1,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = "\n",
    };

    /// <summary>JSON with sorted object keys, indent 1 and no ASCII escaping, like <c>json.dumps(indent=1, sort_keys=True, ensure_ascii=False)</c>.</summary>
    public static string ToSortedJson(JsonNode node)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, WriterOptions)) WriteSorted(w, node);
        // System.Text.Json writes \u001B; Python writes \u001b. Lower-case the hex (skipping escaped backslashes).
        return LowerHexEscape().Replace(Encoding.UTF8.GetString(ms.ToArray()), m => m.Value.Length == 2 ? m.Value : m.Value.ToLowerInvariant());
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\\\\|\\u[0-9A-Fa-f]{4}")]
    private static partial System.Text.RegularExpressions.Regex LowerHexEscape();

    private static void WriteSorted(Utf8JsonWriter w, JsonNode? node)
    {
        switch (node)
        {
            case null:
                w.WriteNullValue();
                break;
            case JsonObject o:
                w.WriteStartObject();
                foreach (var kv in o.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    w.WritePropertyName(kv.Key);
                    WriteSorted(w, kv.Value);
                }
                w.WriteEndObject();
                break;
            case JsonArray a:
                w.WriteStartArray();
                foreach (var item in a) WriteSorted(w, item);
                w.WriteEndArray();
                break;
            default:
                node.WriteTo(w);
                break;
        }
    }
}
