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

    public static bool Has(string key) => Manager.GetString(key, CultureInfo.InvariantCulture) is not null;
}

/// <summary>XAML: <c>Text="{loc:T Catalog_Title}"</c>.</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(key);
}
