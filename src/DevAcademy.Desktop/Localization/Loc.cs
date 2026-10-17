using System.Globalization;
using System.Resources;
using Avalonia.Markup.Xaml;

namespace DevAcademy.Desktop.Localization;

/// <summary>Static accessor over Resources/Strings.resx (neutral language = Russian).</summary>
public static class Loc
{
    private static readonly ResourceManager Manager = new("DevAcademy.Desktop.Resources.Strings", typeof(Loc).Assembly);

    public static string T(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public static string F(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

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
