using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DevAcademy.Notebooks.Nbformat;

namespace DevAcademy.Notebooks.Text;

public enum PresentationKind { Text, Error, Image, Markdown, Latex, Json }

/// <summary>How one output is shown natively: the richest MIME type we can render without a browser. HTML is never rendered.</summary>
public sealed record OutputPresentation(PresentationKind Kind, string Text, byte[]? ImageBytes = null, string? Note = null)
{
    private static readonly string[] ImageMimes = ["image/png", "image/jpeg"];

    public static OutputPresentation For(NotebookOutput o)
    {
        switch (o.OutputType)
        {
            case NotebookOutput.Stream:
                return new(PresentationKind.Text, AnsiText.ApplyCarriageReturns(o.Text ?? ""));
            case NotebookOutput.Error:
                var tb = o.Traceback.Count > 0 ? string.Join("\n", o.Traceback) : $"{o.ErrorName}: {o.ErrorValue}";
                return new(PresentationKind.Error, tb);
        }

        var data = o.Data ?? [];
        foreach (var mime in ImageMimes)
            if (o.GetText(mime) is { } b64 && TryDecode(b64) is { } bytes)
                return new(PresentationKind.Image, o.GetText("text/plain") ?? "", bytes);
        if (o.GetText("text/markdown") is { } md) return new(PresentationKind.Markdown, md);
        if (o.GetText("text/latex") is { } tex) return new(PresentationKind.Latex, tex);
        if (o.GetText("text/plain") is { } plain) return new(PresentationKind.Text, plain);
        if (data["application/json"] is { } json) return new(PresentationKind.Json, NotebookSerializer.ToSortedJson(json));
        if (o.GetText("text/html") is { } html)
            return new(PresentationKind.Text, HtmlToText(html), Note: "HTML не отображается; показан текст.");
        var first = data.FirstOrDefault().Key;
        return new(PresentationKind.Text, first is null ? "" : $"[{first}: формат не поддерживается]");
    }

    private static byte[]? TryDecode(string b64)
    {
        try { return Convert.FromBase64String(b64.Replace("\n", "").Replace("\r", "")); }
        catch (FormatException) { return null; }
    }

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Breaks = new(@"</(tr|p|div|h\d|li)>|<br\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Cells = new(@"</t[dh]>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Script = new(@"<(script|style)\b.*?</\1>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>Crude readable text from HTML (table cells separated by tabs, rows by newlines). Only used when a cell has no text/plain.</summary>
    public static string HtmlToText(string html)
    {
        var s = Script.Replace(html, "");
        s = Cells.Replace(s, "\t");
        s = Breaks.Replace(s, "\n");
        s = Tags.Replace(s, "");
        return System.Net.WebUtility.HtmlDecode(s).Trim();
    }
}
