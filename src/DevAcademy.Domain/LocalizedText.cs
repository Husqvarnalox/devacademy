using System.Globalization;

namespace DevAcademy.Domain;

/// <summary>
/// A user-visible message produced by a language-neutral layer (execution, evaluation, debugging, language servers...).
/// <see cref="Fallback"/> is the finished English text: the CLI, logs and tests keep reading it. A UI looks <see cref="Key"/> up in its own
/// resources, formats the template with <see cref="Args"/> and uses <see cref="Fallback"/> when the key is missing.
/// An empty <see cref="Key"/> marks verbatim text that is never translated (compiler stderr, test output, author hints, paths).
/// </summary>
/// <param name="Key">Resource key, namespaced like <c>Engine_Run_TimeLimit</c>; empty for verbatim text.</param>
/// <param name="Fallback">English text with the arguments already substituted.</param>
/// <param name="Args">Dynamic parts (versions, paths, counts, tool names) in template order. A nested <see cref="LocalizedText"/> is localized on its own.</param>
public sealed record LocalizedText(string Key, string Fallback, IReadOnlyList<object>? Args = null)
{
    /// <summary>Key of a sequence: <c>Args[0]</c> is the separator string, the rest are the parts.</summary>
    public const string JoinKey = "@join";

    /// <summary>Builds a message from an English template (<c>{0}</c> placeholders, the same shape as the resource template) and its arguments.</summary>
    public static LocalizedText Create(string key, string englishTemplate, params object[] args) =>
        new(key, string.Format(CultureInfo.InvariantCulture, englishTemplate, args.Select(FallbackOf).ToArray()), args.Length == 0 ? null : args);

    /// <summary>Text that must reach the user unchanged (tool output, paths, content author's text).</summary>
    public static LocalizedText Literal(string text) => new("", text);

    /// <summary>Parts joined by <paramref name="separator"/>; empty parts are dropped. Zero parts give empty literal text, one part is returned as is.</summary>
    public static LocalizedText Join(string separator, IEnumerable<LocalizedText?> parts)
    {
        var kept = parts.Where(p => p is { Fallback.Length: > 0 }).Select(p => p!).ToList();
        if (kept.Count == 0) return Literal("");
        if (kept.Count == 1) return kept[0];
        return new LocalizedText(JoinKey, string.Join(separator, kept.Select(p => p.Fallback)), [separator, .. kept]);
    }

    public static LocalizedText Join(string separator, params LocalizedText?[] parts) => Join(separator, (IEnumerable<LocalizedText?>)parts);

    public override string ToString() => Fallback;

    private static object FallbackOf(object arg) => arg is LocalizedText t ? t.Fallback : arg;
}

/// <summary>An exception whose message the UI may show in the user's language; <see cref="Exception.Message"/> stays the English fallback.</summary>
public interface ILocalizedMessage
{
    LocalizedText Text { get; }
}

/// <summary>Operation failure with a localizable message (<see cref="Exception.Message"/> is the English fallback).</summary>
public sealed class LocalizedOperationException(LocalizedText text, Exception? inner = null)
    : InvalidOperationException(text.Fallback, inner), ILocalizedMessage
{
    public LocalizedText Text { get; } = text;
}
