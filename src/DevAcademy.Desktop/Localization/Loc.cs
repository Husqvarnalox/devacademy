using System.Globalization;
using System.Resources;
using Avalonia.Markup.Xaml;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.Localization;

/// <summary>Static accessor over Resources/Strings.resx (neutral language = Russian).</summary>
public static class Loc
{
    private static readonly ResourceManager Manager = new("DevAcademy.Desktop.Resources.Strings", typeof(Loc).Assembly);

    public static string T(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public static string F(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>
    /// Renders an engine message: looks <see cref="LocalizedText.Key"/> up in the resources and formats its arguments (nested messages are
    /// rendered first), falling back to the engine's own English text when the key is missing or the template does not fit the arguments.
    /// An empty key is verbatim text.
    /// </summary>
    public static string Of(LocalizedText? text)
    {
        if (text is null) return "";
        if (text.Key.Length == 0) return text.Fallback;
        var args = text.Args ?? [];
        if (text.Key == LocalizedText.JoinKey)
        {
            var separator = args.Count > 0 ? args[0] as string ?? "" : "";
            return string.Join(separator, args.Skip(1).Select(OfArgument).Where(s => s.Length > 0));
        }
        var template = Manager.GetString(text.Key, CultureInfo.CurrentUICulture);
        if (template is null) return text.Fallback;
        try { return string.Format(CultureInfo.CurrentCulture, template, args.Select(a => a is LocalizedText n ? Of(n) : a).ToArray()); }
        catch (FormatException) { return text.Fallback; }
    }

    /// <summary>The translated message when the engine supplied one, otherwise its plain text (null gives an empty string).</summary>
    public static string Of(LocalizedText? text, string? plain) => text is not null ? Of(text) : plain ?? "";

    /// <summary>The user-facing text of an exception: translated when it carries an engine message, otherwise its own message.</summary>
    public static string Msg(Exception error) => error is ILocalizedMessage l ? Of(l.Text) : error.Message;

    private static string OfArgument(object part) => part is LocalizedText t ? Of(t) : part.ToString() ?? "";

    /// <summary>Russian plural form of a noun: <c>Plural(3, "курс", "курса", "курсов")</c> gives "3 курса".</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        var a = Math.Abs(n);
        var form = a % 100 is >= 11 and <= 14 ? many : (a % 10) switch { 1 => one, >= 2 and <= 4 => few, _ => many };
        return $"{n} {form}";
    }

    public static bool Has(string key) => Manager.GetString(key, CultureInfo.InvariantCulture) is not null;
}

/// <summary>XAML: <c>Text="{loc:T Catalog_Title}"</c>.</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(key);
}

/// <summary>Upper-cased variant for section labels (the caption-uppercase style): <c>Text="{loc:TU Nav_Group_Learning}"</c>.</summary>
public sealed class TUExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(key).ToUpper(CultureInfo.CurrentUICulture);
}
